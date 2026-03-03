using System.ClientModel;
using System.Text.Json;
using BusinessObjects.Entities;
using Microsoft.Extensions.Configuration;
using OpenAI.Chat;
using Repositories;
using Repositories.Interfaces;
using Services.DTOs.AI;
using Services.Helpers;
using Services.Interfaces;

namespace Services.Implementations
{
    public class AICoCreationService : IAICoCreationService
    {
        /// <summary>Tổng số ký tự tối đa dành cho nội dung tất cả chương (để vừa context window). AI được đọc toàn bộ chương, mỗi chương rút gọn nếu cần.</summary>
        private const int MaxTotalContextCharsForChapters = 26000;
        /// <summary>Số ký tự tối đa mỗi chương khi có ít chương.</summary>
        private const int MaxCharsPerChapter = 2500;
        private const int MaxRevisions = 2;
        private const string ActionOutline = "CO_CREATE_OUTLINE";
        private const string ActionWrite = "CO_CREATE_WRITE";
        private const string ActionReview = "CO_CREATE_REVIEW";

        private readonly IStoryRepository _storyRepository;
        private readonly IChapterRepository _chapterRepository;
        private readonly IAIUsageLogRepository _aiUsageLogRepository;
        private readonly IConfiguration _configuration;

        public AICoCreationService(
            IStoryRepository storyRepository,
            IChapterRepository chapterRepository,
            IAIUsageLogRepository aiUsageLogRepository,
            IConfiguration configuration)
        {
            _storyRepository = storyRepository;
            _chapterRepository = chapterRepository;
            _aiUsageLogRepository = aiUsageLogRepository;
            _configuration = configuration;
        }

        public async Task<CoCreationResponse> CoCreateAsync(
            CoCreationRequest request,
            Guid authorUserId,
            CancellationToken cancellationToken = default)
        {
            var story = _storyRepository.GetById(request.StoryId);
            if (story == null)
                throw new InvalidOperationException("Truyện không tồn tại.");

            if (story.author_id != authorUserId)
                throw new UnauthorizedAccessException("Chỉ tác giả của truyện mới được sử dụng tính năng đồng sáng tác.");

            var chapters = _chapterRepository.GetByStoryId(request.StoryId)
                .OrderBy(c => c.order_index)
                .ToList();

            IEnumerable<chapters> chaptersForContext = chapters;
            if (request.AfterChapterId.HasValue)
            {
                var afterIdx = chapters.FirstOrDefault(c => c.id == request.AfterChapterId.Value)?.order_index;
                if (afterIdx.HasValue)
                    chaptersForContext = chapters.Where(c => c.order_index <= afterIdx.Value);
            }

            var allChaptersForContext = chaptersForContext.ToList();
            int charsPerChapter = ComputeCharsPerChapter(allChaptersForContext.Count);
            var contextBlock = BuildContextBlock(story, allChaptersForContext, request.ContinuityNotes, charsPerChapter);

            var (provider, model, apiKey, baseUrl) = AIClientHelper.GetConfig(_configuration);
            var client = AIClientHelper.CreateChatClient(provider, model, apiKey, baseUrl);

            // --- Agent 1: Dàn ý ---
            var outline = await RunAgent1OutlineAsync(client, model, contextBlock, request.AuthorIdea, cancellationToken);
            LogUsage(authorUserId, request.StoryId, allChaptersForContext.LastOrDefault()?.id, ActionOutline, model, 0, 0);

            // --- Agent 2 + 3 với vòng sửa ---
            string draft = await RunAgent2WriteAsync(client, model, contextBlock, outline, feedback: null, cancellationToken);
            LogUsage(authorUserId, request.StoryId, allChaptersForContext.LastOrDefault()?.id, ActionWrite, model, 0, 0);

            var (approved, feedback) = await RunAgent3ReviewAsync(client, model, contextBlock, outline, draft, cancellationToken);
            LogUsage(authorUserId, request.StoryId, allChaptersForContext.LastOrDefault()?.id, ActionReview, model, 0, 0);

            int revisionCount = 0;
            string? lastFeedback = feedback;

            while (!approved && revisionCount < MaxRevisions)
            {
                revisionCount++;
                draft = await RunAgent2WriteAsync(client, model, contextBlock, outline, lastFeedback, cancellationToken);
                LogUsage(authorUserId, request.StoryId, allChaptersForContext.LastOrDefault()?.id, ActionWrite, model, 0, 0);

                var (approvedAgain, feedbackAgain) = await RunAgent3ReviewAsync(client, model, contextBlock, outline, draft, cancellationToken);
                LogUsage(authorUserId, request.StoryId, allChaptersForContext.LastOrDefault()?.id, ActionReview, model, 0, 0);

                if (approvedAgain)
                {
                    return new CoCreationResponse
                    {
                        Outline = outline,
                        FinalContent = draft,
                        Approved = true,
                        RevisionCount = revisionCount,
                        ReviewFeedback = null
                    };
                }

                lastFeedback = feedbackAgain;
            }

            return new CoCreationResponse
            {
                Outline = outline,
                FinalContent = draft,
                Approved = approved,
                RevisionCount = revisionCount,
                ReviewFeedback = lastFeedback
            };
        }

        /// <summary>Tính số ký tự cho mỗi chương: chia đều budget, không vượt trần. Ví dụ 100 chương → 260 ký tự/chương; 200 → 130; 500 → 52.</summary>
        private static int ComputeCharsPerChapter(int chapterCount)
        {
            if (chapterCount <= 0) return MaxCharsPerChapter;
            int perChapter = MaxTotalContextCharsForChapters / chapterCount;
            return Math.Min(MaxCharsPerChapter, Math.Max(1, perChapter));
        }

        private static string BuildContextBlock(stories story, List<chapters> allChapters, string? continuityNotes, int charsPerChapter)
        {
            var lines = new List<string>
            {
                $"## Truyện: {story.title}",
                string.IsNullOrWhiteSpace(story.summary) ? "" : $"Tóm tắt: {story.summary}"
            };

            if (!string.IsNullOrWhiteSpace(continuityNotes))
            {
                lines.Add("## Điểm cần nhất quán (tác giả cung cấp)");
                lines.Add(continuityNotes.Trim());
            }

            lines.Add("## Nội dung các chương (theo thứ tự, có thể rút gọn để vừa giới hạn ngữ cảnh)");
            foreach (var ch in allChapters)
            {
                var content = ch.content ?? "";
                if (content.Length > charsPerChapter)
                    content = content[..charsPerChapter] + "...";
                lines.Add($"### Chương {ch.order_index}: {ch.title}");
                lines.Add(content);
            }

            return string.Join("\n\n", lines.Where(s => !string.IsNullOrWhiteSpace(s)));
        }

        private static string GetAgent1SystemPrompt()
        {
            return """
Bạn là trợ lý viết dàn ý cho tác giả truyện. Nhiệm vụ: dựa trên thông tin truyện (bao gồm mục "Điểm cần nhất quán" nếu có), các chương gần nhất và ý tưởng của tác giả, viết một DÀN Ý (outline) rõ ràng cho nội dung cần viết.

Dàn ý gồm: các ý chính, trình tự sự kiện hoặc cảm xúc, bám sát ý tưởng tác giả và nhất quán với cốt truyện. Bắt buộc tôn trọng mọi chi tiết quan trọng đã nêu trong ngữ cảnh: trạng thái nhân vật, sự kiện đã xảy ra, quan hệ — không được tạo nội dung mâu thuẫn với các thông tin đó. Trả về DUY NHẤT phần dàn ý bằng văn bản, không thêm markdown hay giải thích khác. Ngôn ngữ trùng với ngôn ngữ của truyện (Việt hoặc Anh).
""";
        }

        private async Task<string> RunAgent1OutlineAsync(ChatClient client, string model, string contextBlock, string authorIdea, CancellationToken ct)
        {
            var userPrompt = $"Ngữ cảnh truyện:\n\n{contextBlock}\n\nÝ tưởng của tác giả:\n{authorIdea}\n\nHãy viết dàn ý theo ý tưởng trên.";
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(GetAgent1SystemPrompt()),
                new UserChatMessage(userPrompt)
            };

            var completion = await client.CompleteChatAsync(messages);
            var chat = completion.Value;
            var text = chat.Content?.Count > 0 ? chat.Content[0].Text : null;
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException("Agent dàn ý không trả về nội dung.");
            return text.Trim();
        }

        private static string GetAgent2SystemPrompt()
        {
            return """
Bạn là trợ lý viết nội dung truyện. Nhiệm vụ: viết nội dung văn bản (đoạn/chương nháp) theo ĐÚNG dàn ý được cung cấp, phong cách và giọng văn phù hợp với truyện. Bắt buộc tôn trọng mọi chi tiết quan trọng trong ngữ cảnh (kể cả mục "Điểm cần nhất quán" nếu có): trạng thái nhân vật, sự kiện đã xảy ra — không được mô tả ngược lại. Chỉ trả về phần nội dung văn bản, không thêm tiêu đề hay giải thích. Ngôn ngữ trùng với truyện.
""";
        }

        private async Task<string> RunAgent2WriteAsync(ChatClient client, string model, string contextBlock, string outline, string? feedback, CancellationToken ct)
        {
            var userPrompt = $"Ngữ cảnh truyện:\n\n{contextBlock}\n\nDàn ý cần viết:\n{outline}";
            if (!string.IsNullOrWhiteSpace(feedback))
                userPrompt += $"\n\nGóp ý từ kiểm duyệt (cần sửa theo đúng phần này):\n{feedback}";

            userPrompt += "\n\nHãy viết nội dung theo dàn ý (và góp ý nếu có).";

            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(GetAgent2SystemPrompt()),
                new UserChatMessage(userPrompt)
            };

            var completion = await client.CompleteChatAsync(messages);
            var chat = completion.Value;
            var text = chat.Content?.Count > 0 ? chat.Content[0].Text : null;
            if (string.IsNullOrWhiteSpace(text))
                throw new InvalidOperationException("Agent viết nội dung không trả về nội dung.");
            return text.Trim();
        }

        private static string GetAgent3SystemPrompt()
        {
            return """
Bạn là người kiểm duyệt nội dung truyện. Nhiệm vụ: đọc toàn bộ ngữ cảnh (kể cả mục "Điểm cần nhất quán" nếu có), dàn ý và bản nháp; kiểm tra (1) logic cốt truyện, (2) mâu thuẫn nội bộ, (3) tính nhất quán với mọi thông tin đã nêu trong ngữ cảnh — trạng thái nhân vật, sự kiện đã xảy ra, quan hệ. Bất kỳ mâu thuẫn nào giữa bản nháp và thông tin đã có đều phải bị đánh giá là chưa đạt.

Trả về DUY NHẤT một JSON hợp lệ, không kèm markdown hay giải thích:
{ "approved": true }  khi nội dung đạt.
{ "approved": false, "feedback": "Mô tả ngắn, rõ ràng vấn đề (sai logic, mâu thuẫn với ngữ cảnh, hoặc phần chưa hợp lý) để tác giả/AI viết lại" }  khi cần sửa.

Ngôn ngữ feedback: cùng ngôn ngữ truyện (Việt hoặc Anh).
""";
        }

        private async Task<(bool approved, string? feedback)> RunAgent3ReviewAsync(ChatClient client, string model, string contextBlock, string outline, string draft, CancellationToken ct)
        {
            var userPrompt = $"Ngữ cảnh truyện:\n\n{contextBlock}\n\nDàn ý:\n{outline}\n\nBản nháp cần kiểm duyệt:\n{draft}\n\nTrả về JSON theo đúng cấu trúc đã nêu.";
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage(GetAgent3SystemPrompt()),
                new UserChatMessage(userPrompt)
            };

            var completion = await client.CompleteChatAsync(messages);
            var chat = completion.Value;
            var text = chat.Content?.Count > 0 ? chat.Content[0].Text : null;
            if (string.IsNullOrWhiteSpace(text))
                return (false, "Không đọc được kết quả kiểm duyệt.");

            return ParseReviewResult(text);
        }

        private static (bool approved, string? feedback) ParseReviewResult(string text)
        {
            text = text.Trim();
            if (text.StartsWith("```"))
            {
                var start = text.IndexOf('\n') + 1;
                var end = text.IndexOf("```", start, StringComparison.Ordinal);
                if (end > start)
                    text = text[start..end];
            }

            try
            {
                var root = JsonDocument.Parse(text).RootElement;
                var approved = root.TryGetProperty("approved", out var a) && a.GetBoolean();
                var feedback = root.TryGetProperty("feedback", out var f) ? f.GetString() : null;
                return (approved, feedback);
            }
            catch
            {
                return (false, "Định dạng phản hồi kiểm duyệt không hợp lệ.");
            }
        }

        private void LogUsage(Guid userId, Guid storyId, Guid? chapterId, string actionType, string modelName, int promptTokens, int completionTokens)
        {
            _aiUsageLogRepository.Log(new ai_usage_logs
            {
                user_id = userId,
                story_id = storyId,
                chapter_id = chapterId,
                action_type = actionType,
                model_name = modelName,
                prompt_tokens = promptTokens,
                completion_tokens = completionTokens,
                total_tokens = promptTokens + completionTokens,
                status = "SUCCESS",
                created_at = DateTime.UtcNow
            });
        }
    }
}
