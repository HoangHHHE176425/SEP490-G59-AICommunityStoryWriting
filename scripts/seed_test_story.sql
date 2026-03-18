/*
Seed dữ liệu test cho AI co-author / suggest-next-chapter / RAG.

Cách dùng (SQL Server):
1) Mở SSMS hoặc Azure Data Studio, chọn đúng database (vd. story_platform_v13)
2) Thay giá trị @AuthorId (bắt buộc) bằng userId của bạn (sub trong JWT)
3) (Tùy chọn) Thay @StoryId nếu muốn cố định; nếu không script tự tạo story mới.
4) Run toàn bộ script.

Kết quả:
- 1 story (PUBLISHED/ONGOING) + 4 chapters (PUBLISHED) có nội dung.
- In ra StoryId để bạn gọi API.
*/

SET NOCOUNT ON;

DECLARE @AuthorId UNIQUEIDENTIFIER = '00000000-0000-0000-0000-000000000000'; -- TODO: thay bằng userId (JWT sub)
DECLARE @StoryId UNIQUEIDENTIFIER = NULL; -- TODO: (optional) set GUID cố định

IF @AuthorId = '00000000-0000-0000-0000-000000000000'
BEGIN
    RAISERROR('Bạn phải set @AuthorId (userId của tác giả).', 16, 1);
    RETURN;
END

IF @StoryId IS NULL
    SET @StoryId = NEWID();

DECLARE @Now DATETIME2 = SYSDATETIME();
DECLARE @Slug NVARCHAR(200) = CONCAT('seed-test-story-', REPLACE(CONVERT(NVARCHAR(36), @StoryId), '-', ''));

IF NOT EXISTS (SELECT 1 FROM dbo.stories WHERE id = @StoryId)
BEGIN
    INSERT INTO dbo.stories
    (
        id, author_id, title, slug, cover_image, summary, status, story_progress_status,
        last_published_at, total_chapters, total_views, total_favorites, avg_rating, word_count,
        age_rating, created_at, updated_at, published_at
    )
    VALUES
    (
        @StoryId, @AuthorId,
        N'[SEED] Bí ẩn ở Phố Cũ',
        @Slug,
        NULL,
        N'Một câu chuyện trinh thám - tâm lý tại Hà Nội. Nhân vật chính lần theo những dấu vết rời rạc để tìm ra bí mật bị chôn vùi.',
        'PUBLISHED',
        'ONGOING',
        @Now,
        0, 0, 0, NULL, NULL,
        '16+',
        @Now, @Now, @Now
    );
END
ELSE
BEGIN
    -- đảm bảo đúng author để test co-author theo quyền tác giả
    UPDATE dbo.stories
    SET author_id = @AuthorId,
        updated_at = @Now
    WHERE id = @StoryId;
END

-- Upsert chapters (4 chương) - nếu đã tồn tại order_index thì update content
DECLARE @Ch1 UNIQUEIDENTIFIER = NEWID();
DECLARE @Ch2 UNIQUEIDENTIFIER = NEWID();
DECLARE @Ch3 UNIQUEIDENTIFIER = NEWID();
DECLARE @Ch4 UNIQUEIDENTIFIER = NEWID();

-- Chapter 1
IF EXISTS (SELECT 1 FROM dbo.chapters WHERE story_id = @StoryId AND order_index = 1)
BEGIN
    UPDATE dbo.chapters
    SET title = N'Chương 1: Lá thư không người nhận',
        content = N'Xuân trở về Phố Cũ vào một buổi chiều mưa lất phất. Trên bàn làm việc của ông Trọng, một phong thư đã ngả màu nằm im như chờ đợi.\n\nBức thư không ghi tên người nhận. Chỉ có một dấu sáp đỏ với biểu tượng bông cúc. Xuân mở ra, bên trong là vài dòng chữ rời rạc: “Đừng tin vào ký ức. Mọi thứ đã bị thay đổi.”\n\nTừ khoảnh khắc đó, Xuân hiểu: có ai đó đang kéo mình vào một trò chơi cũ mà anh từng cố quên.',
        status = 'PUBLISHED',
        access_type = 'FREE',
        coin_price = NULL,
        word_count = 0,
        published_at = @Now,
        updated_at = @Now
    WHERE story_id = @StoryId AND order_index = 1;
END
ELSE
BEGIN
    INSERT INTO dbo.chapters
    (
        id, story_id, title, order_index, content, status, access_type, coin_price, word_count,
        ai_contribution_ratio, is_ai_clean, published_at, created_at, updated_at, ai_similarity_percent
    )
    VALUES
    (
        @Ch1, @StoryId,
        N'Chương 1: Lá thư không người nhận',
        1,
        N'Xuân trở về Phố Cũ vào một buổi chiều mưa lất phất. Trên bàn làm việc của ông Trọng, một phong thư đã ngả màu nằm im như chờ đợi.\n\nBức thư không ghi tên người nhận. Chỉ có một dấu sáp đỏ với biểu tượng bông cúc. Xuân mở ra, bên trong là vài dòng chữ rời rạc: “Đừng tin vào ký ức. Mọi thứ đã bị thay đổi.”\n\nTừ khoảnh khắc đó, Xuân hiểu: có ai đó đang kéo mình vào một trò chơi cũ mà anh từng cố quên.',
        'PUBLISHED',
        'FREE',
        NULL,
        0,
        NULL, NULL,
        @Now, @Now, @Now,
        NULL
    );
END

-- Chapter 2
IF EXISTS (SELECT 1 FROM dbo.chapters WHERE story_id = @StoryId AND order_index = 2)
BEGIN
    UPDATE dbo.chapters
    SET title = N'Chương 2: Dấu sáp đỏ',
        content = N'Xuân tìm đến căn gác nhỏ của bà Phó Đoan, người từng quen ông Trọng từ thời bao cấp. Bà nhìn dấu sáp đỏ, mặt tái đi như gặp lại một bóng ma.\n\n“Dấu này…” bà thì thầm, “nó thuộc về Hội Cúc. Họ không bao giờ gửi thư nhầm.”\n\nTrong ngăn tủ gỗ, bà lôi ra một cuốn sổ tay cũ, trang giấy đã mủn. Trong đó có danh sách những cái tên bị gạch xóa. Tên ông Trọng ở trang cuối, ngay dưới một dòng chữ: ‘Kẻ giữ mảnh ghép.’\n\nBên ngoài, tiếng bước chân dừng lại trước cửa. Ai đó đang lắng nghe.',
        status = 'PUBLISHED',
        access_type = 'FREE',
        coin_price = NULL,
        word_count = 0,
        published_at = @Now,
        updated_at = @Now
    WHERE story_id = @StoryId AND order_index = 2;
END
ELSE
BEGIN
    INSERT INTO dbo.chapters
    (
        id, story_id, title, order_index, content, status, access_type, coin_price, word_count,
        ai_contribution_ratio, is_ai_clean, published_at, created_at, updated_at, ai_similarity_percent
    )
    VALUES
    (
        @Ch2, @StoryId,
        N'Chương 2: Dấu sáp đỏ',
        2,
        N'Xuân tìm đến căn gác nhỏ của bà Phó Đoan, người từng quen ông Trọng từ thời bao cấp. Bà nhìn dấu sáp đỏ, mặt tái đi như gặp lại một bóng ma.\n\n“Dấu này…” bà thì thầm, “nó thuộc về Hội Cúc. Họ không bao giờ gửi thư nhầm.”\n\nTrong ngăn tủ gỗ, bà lôi ra một cuốn sổ tay cũ, trang giấy đã mủn. Trong đó có danh sách những cái tên bị gạch xóa. Tên ông Trọng ở trang cuối, ngay dưới một dòng chữ: ‘Kẻ giữ mảnh ghép.’\n\nBên ngoài, tiếng bước chân dừng lại trước cửa. Ai đó đang lắng nghe.',
        'PUBLISHED',
        'FREE',
        NULL,
        0,
        NULL, NULL,
        @Now, @Now, @Now,
        NULL
    );
END

-- Chapter 3
IF EXISTS (SELECT 1 FROM dbo.chapters WHERE story_id = @StoryId AND order_index = 3)
BEGIN
    UPDATE dbo.chapters
    SET title = N'Chương 3: Ký ức bị đánh tráo',
        content = N'Đêm đó, Xuân trở về và phát hiện căn phòng đã bị lục lọi. Cuốn sổ tay biến mất. Trên gương, ai đó viết bằng son: “Nếu muốn sự thật, hãy trả lại mảnh ghép.”\n\nXuân lục lại ngăn kéo cũ của ông Trọng, tìm thấy một chiếc hộp thiếc. Bên trong là một mảnh ảnh rách: nửa khuôn mặt của một người đàn ông lạ.\n\nBất chợt, Xuân nhớ ra—một ký ức không thuộc về mình: tiếng còi tàu, mùi khói dầu, và một lời hứa bị nuốt chửng bởi đêm tối.\n\nAnh tự hỏi: ký ức ấy là của ai, và vì sao nó lại nằm trong đầu anh?',
        status = 'PUBLISHED',
        access_type = 'FREE',
        coin_price = NULL,
        word_count = 0,
        published_at = @Now,
        updated_at = @Now
    WHERE story_id = @StoryId AND order_index = 3;
END
ELSE
BEGIN
    INSERT INTO dbo.chapters
    (
        id, story_id, title, order_index, content, status, access_type, coin_price, word_count,
        ai_contribution_ratio, is_ai_clean, published_at, created_at, updated_at, ai_similarity_percent
    )
    VALUES
    (
        @Ch3, @StoryId,
        N'Chương 3: Ký ức bị đánh tráo',
        3,
        N'Đêm đó, Xuân trở về và phát hiện căn phòng đã bị lục lọi. Cuốn sổ tay biến mất. Trên gương, ai đó viết bằng son: “Nếu muốn sự thật, hãy trả lại mảnh ghép.”\n\nXuân lục lại ngăn kéo cũ của ông Trọng, tìm thấy một chiếc hộp thiếc. Bên trong là một mảnh ảnh rách: nửa khuôn mặt của một người đàn ông lạ.\n\nBất chợt, Xuân nhớ ra—một ký ức không thuộc về mình: tiếng còi tàu, mùi khói dầu, và một lời hứa bị nuốt chửng bởi đêm tối.\n\nAnh tự hỏi: ký ức ấy là của ai, và vì sao nó lại nằm trong đầu anh?',
        'PUBLISHED',
        'FREE',
        NULL,
        0,
        NULL, NULL,
        @Now, @Now, @Now,
        NULL
    );
END

-- Chapter 4
IF EXISTS (SELECT 1 FROM dbo.chapters WHERE story_id = @StoryId AND order_index = 4)
BEGIN
    UPDATE dbo.chapters
    SET title = N'Chương 4: Người gác ga',
        content = N'Xuân lần theo ký ức lạ đến ga Long Biên. Dưới mái hiên cũ kỹ, một người gác ga già ngồi châm thuốc, nhìn anh bằng ánh mắt như đã biết trước.\n\n“Cậu tìm mảnh ghép à?” ông ta hỏi. “Nó không chỉ là một bức ảnh. Nó là chìa khóa mở ra thứ người ta đã khóa lại trong đầu cậu.”\n\nÔng ta đưa cho Xuân một tờ vé tàu đã xé góc. Trên đó có một con số viết tay: 27.\n\nNgay lúc ấy, điện thoại của Xuân rung lên. Một tin nhắn lạ: “Đừng đến số 27. Nếu cậu đến, bà Phó Đoan sẽ chết.”',
        status = 'PUBLISHED',
        access_type = 'FREE',
        coin_price = NULL,
        word_count = 0,
        published_at = @Now,
        updated_at = @Now
    WHERE story_id = @StoryId AND order_index = 4;
END
ELSE
BEGIN
    INSERT INTO dbo.chapters
    (
        id, story_id, title, order_index, content, status, access_type, coin_price, word_count,
        ai_contribution_ratio, is_ai_clean, published_at, created_at, updated_at, ai_similarity_percent
    )
    VALUES
    (
        @Ch4, @StoryId,
        N'Chương 4: Người gác ga',
        4,
        N'Xuân lần theo ký ức lạ đến ga Long Biên. Dưới mái hiên cũ kỹ, một người gác ga già ngồi châm thuốc, nhìn anh bằng ánh mắt như đã biết trước.\n\n“Cậu tìm mảnh ghép à?” ông ta hỏi. “Nó không chỉ là một bức ảnh. Nó là chìa khóa mở ra thứ người ta đã khóa lại trong đầu cậu.”\n\nÔng ta đưa cho Xuân một tờ vé tàu đã xé góc. Trên đó có một con số viết tay: 27.\n\nNgay lúc ấy, điện thoại của Xuân rung lên. Một tin nhắn lạ: “Đừng đến số 27. Nếu cậu đến, bà Phó Đoan sẽ chết.”',
        'PUBLISHED',
        'FREE',
        NULL,
        0,
        NULL, NULL,
        @Now, @Now, @Now,
        NULL
    );
END

UPDATE dbo.stories
SET total_chapters = (SELECT COUNT(1) FROM dbo.chapters WHERE story_id = @StoryId),
    last_published_at = @Now,
    updated_at = @Now
WHERE id = @StoryId;

/* =========================
   Seed Memory Tables
   - story_story_state (snapshot)
   - story_character_memory (per character)
   - story_event_memory (timeline)
   ========================= */

-- Story State (1 snapshot per story)
IF EXISTS (SELECT 1 FROM dbo.story_story_state WHERE story_id = @StoryId)
BEGIN
    UPDATE dbo.story_story_state
    SET state_snapshot_json = N'{"location":"Hà Nội - Phố Cũ / Ga Long Biên","currentMystery":"Mảnh ghép bức ảnh và Hội Cúc","threatLevel":"Cao","lastKnownHook":"Tin nhắn cảnh báo về số 27 và bà Phó Đoan"}',
        updated_at = @Now
    WHERE story_id = @StoryId;
END
ELSE
BEGIN
    INSERT INTO dbo.story_story_state (id, story_id, state_snapshot_json, updated_at)
    VALUES
    (
        NEWID(),
        @StoryId,
        N'{"location":"Hà Nội - Phố Cũ / Ga Long Biên","currentMystery":"Mảnh ghép bức ảnh và Hội Cúc","threatLevel":"Cao","lastKnownHook":"Tin nhắn cảnh báo về số 27 và bà Phó Đoan"}',
        @Now
    );
END

-- Character Memory (upsert theo story_id + character_name)
IF EXISTS (SELECT 1 FROM dbo.story_character_memory WHERE story_id = @StoryId AND character_name = N'Xuân')
    UPDATE dbo.story_character_memory
    SET state_json = N'{"role":"Nhân vật chính","traits":["cẩn trọng","tò mò","kiên trì"],"status":"Đang điều tra","lastSeen":"Ga Long Biên","notes":"Bị ám ảnh bởi ký ức không thuộc về mình"}',
        updated_at = @Now
    WHERE story_id = @StoryId AND character_name = N'Xuân';
ELSE
    INSERT INTO dbo.story_character_memory (id, story_id, character_name, state_json, updated_at)
    VALUES (NEWID(), @StoryId, N'Xuân', N'{"role":"Nhân vật chính","traits":["cẩn trọng","tò mò","kiên trì"],"status":"Đang điều tra","lastSeen":"Ga Long Biên","notes":"Bị ám ảnh bởi ký ức không thuộc về mình"}', @Now);

IF EXISTS (SELECT 1 FROM dbo.story_character_memory WHERE story_id = @StoryId AND character_name = N'Ông Trọng')
    UPDATE dbo.story_character_memory
    SET state_json = N'{"role":"Người giữ mảnh ghép","status":"Mất tích","risk":"Bị Hội Cúc truy đuổi","notes":"Liên quan trực tiếp đến dấu sáp đỏ và bí mật Hội Cúc"}',
        updated_at = @Now
    WHERE story_id = @StoryId AND character_name = N'Ông Trọng';
ELSE
    INSERT INTO dbo.story_character_memory (id, story_id, character_name, state_json, updated_at)
    VALUES (NEWID(), @StoryId, N'Ông Trọng', N'{"role":"Người giữ mảnh ghép","status":"Mất tích","risk":"Bị Hội Cúc truy đuổi","notes":"Liên quan trực tiếp đến dấu sáp đỏ và bí mật Hội Cúc"}', @Now);

IF EXISTS (SELECT 1 FROM dbo.story_character_memory WHERE story_id = @StoryId AND character_name = N'Bà Phó Đoan')
    UPDATE dbo.story_character_memory
    SET state_json = N'{"role":"Nhân chứng","status":"Đang gặp nguy hiểm","notes":"Nhận ra dấu sáp đỏ của Hội Cúc; biết về danh sách tên bị gạch xóa"}',
        updated_at = @Now
    WHERE story_id = @StoryId AND character_name = N'Bà Phó Đoan';
ELSE
    INSERT INTO dbo.story_character_memory (id, story_id, character_name, state_json, updated_at)
    VALUES (NEWID(), @StoryId, N'Bà Phó Đoan', N'{"role":"Nhân chứng","status":"Đang gặp nguy hiểm","notes":"Nhận ra dấu sáp đỏ của Hội Cúc; biết về danh sách tên bị gạch xóa"}', @Now);

IF EXISTS (SELECT 1 FROM dbo.story_character_memory WHERE story_id = @StoryId AND character_name = N'Người gác ga')
    UPDATE dbo.story_character_memory
    SET state_json = N'{"role":"Người dẫn đường","status":"Còn sống","notes":"Đưa vé tàu có số 27; biết nhiều hơn những gì nói ra"}',
        updated_at = @Now
    WHERE story_id = @StoryId AND character_name = N'Người gác ga';
ELSE
    INSERT INTO dbo.story_character_memory (id, story_id, character_name, state_json, updated_at)
    VALUES (NEWID(), @StoryId, N'Người gác ga', N'{"role":"Người dẫn đường","status":"Còn sống","notes":"Đưa vé tàu có số 27; biết nhiều hơn những gì nói ra"}', @Now);

-- Event Memory: reset timeline cho story (để test dễ và tránh trùng khi chạy lại)
DELETE FROM dbo.story_event_memory WHERE story_id = @StoryId;

INSERT INTO dbo.story_event_memory (id, story_id, chapter_id, order_index, description, created_at)
VALUES
    (NEWID(), @StoryId, NULL, 1, N'Xuân nhận được phong thư không người nhận có dấu sáp đỏ biểu tượng bông cúc.', @Now),
    (NEWID(), @StoryId, NULL, 2, N'Bà Phó Đoan xác nhận dấu sáp thuộc Hội Cúc và đưa ra cuốn sổ tay danh sách tên bị gạch xóa.', @Now),
    (NEWID(), @StoryId, NULL, 3, N'Cuốn sổ tay bị mất; Xuân tìm thấy mảnh ảnh rách và xuất hiện ký ức lạ không thuộc về mình.', @Now),
    (NEWID(), @StoryId, NULL, 4, N'Xuân đến ga Long Biên; người gác ga đưa vé tàu có số 27 và có tin nhắn cảnh báo nếu đến số 27 thì bà Phó Đoan sẽ chết.', @Now);

SELECT @StoryId AS SeededStoryId;

