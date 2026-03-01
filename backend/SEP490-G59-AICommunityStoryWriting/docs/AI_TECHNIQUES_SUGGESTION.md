# Gợi ý kỹ thuật AI cho đồ án: Trang web viết truyện cộng đồng có AI đồng sáng tác

Tài liệu này gợi ý các kỹ thuật để triển khai hai vai trò chính của AI:
1. **Đọc toàn bộ chương đã viết → Gợi ý chương tiếp theo**
2. **Đồng sáng tác** (co-author) cho bộ truyện

---

## Phần 1: AI đọc truyện và gợi ý chương tiếp theo

### 1.1. Tóm tắt ngữ cảnh (Context summarization)

**Vấn đề:** Truyện dài → không thể gửi toàn bộ nội dung vào prompt LLM (giới hạn token).

**Kỹ thuật:**
- **Tóm tắt theo cấp (hierarchical summarization):**
  - Mỗi chương → tóm tắt ngắn (ví dụ 100–200 từ).
  - N chương gần nhất → tóm tắt “đoạn truyện” (arc).
  - Toàn bộ truyện → tóm tắt “tóm tắt tổng” (overall summary) + nhân vật chính, bối cảnh, cốt truyện.
- **Lưu trữ:** Có thể lưu summary mỗi chương trong DB (bảng mới hoặc trường trong `chapters`), cập nhật khi chương thay đổi.
- **Khi gợi ý chương tiếp:** Đưa vào prompt: overall summary + summary vài chương gần nhất + (tuỳ chọn) full text 1–2 chương cuối.

**Ưu điểm:** Giảm token, chi phí thấp, vẫn giữ mạch truyện. Phù hợp đồ án.

---

### 1.2. RAG (Retrieval-Augmented Generation)

**Ý tưởng:** Không đưa cả truyện vào prompt, mà “lấy đúng đoạn liên quan” rồi mới cho LLM đọc.

**Cách làm:**
- **Chunking:** Chia nội dung từng chương thành các đoạn (chunk) theo câu/đoạn văn (khoảng 200–500 từ/chunk).
- **Embedding:** Dùng model embedding (OpenAI `text-embedding-3-small`, hoặc open-source: sentence-transformers, Cohere, v.v.) để vector hoá từng chunk.
- **Lưu vector:** Lưu embedding vào vector store (Pinecone, Weaviate, Qdrant, hoặc pgvector trong PostgreSQL).
- **Truy vấn:** Khi cần gợi ý chương tiếp theo:
  - Lấy “ngữ cảnh hiện tại” (ví dụ: vài chương cuối hoặc outline chương tiếp) → embed.
  - Query vector store tìm top-k chunk “giống” nhất trong cùng story_id.
  - Đưa các chunk đó + overall summary vào prompt cho LLM.

**Kết quả:** LLM chỉ đọc các đoạn liên quan → gợi ý nhất quán với nội dung đã viết, đồng thời tiết kiệm token.

---

### 1.3. Trích xuất thông tin có cấu trúc (Structured extraction)

**Mục tiêu:** AI không chỉ “đọc” dạng văn bản, mà rút ra **nhân vật, sự kiện, địa điểm, mâu thuẫn** để gợi ý chính xác hơn.

**Cách làm:**
- Sau mỗi chương (hoặc khi user bấm “Gợi ý chương tiếp”), gọi LLM với prompt có cấu trúc:
  - Input: nội dung chương (hoặc summary).
  - Output: JSON theo schema cố định, ví dụ:
    - `characters`: tên, vai trò, trạng thái hiện tại.
    - `locations`: địa điểm xuất hiện.
    - `events`: sự kiện chính.
    - `conflicts`: mâu thuẫn chưa giải quyết.
    - `foreshadowing`: chi tiết có thể khai thác sau.
- Lưu kết quả (bảng `chapter_metadata` hoặc JSON trong DB).
- **Gợi ý chương tiếp:** Prompt gồm: metadata N chương gần nhất + overall summary → LLM đưa ra outline/ideas cho chương tiếp (bullet points, plot beats).

**Lợi ích:** Gợi ý bám sát nhân vật và cốt truyện, tránh “quên” chi tiết quan trọng.

---

### 1.4. Prompt engineering cho “gợi ý chương tiếp”

**Cấu trúc prompt gợi ý (ví dụ):**
```
Bạn là trợ lý sáng tác cho bộ truyện [thể loại]. Nhiệm vụ: đọc ngữ cảnh sau và đưa ra GỢI Ý cho chương tiếp theo (không viết full, chỉ outline).

## Thông tin truyện
- Tựa đề: ...
- Tóm tắt tổng: ...
- Nhân vật chính: ...

## Nội dung / tóm tắt các chương gần nhất
[chương N-2]: ...
[chương N-1]: ...
[chương N]: ...

## Yêu cầu
- Gợi ý 3–5 hướng phát triển (bullet) cho chương tiếp.
- Mỗi gợi ý: mục đích (tình tiết, cảm xúc), gợi ý tình tiết ngắn.
- Giữ nhất quán phong cách và nhân vật.
```

Có thể thêm **few-shot examples** (1–2 ví dụ gợi ý mẫu) để LLM bắt tone tốt hơn.

---

## Phần 2: AI đồng sáng tác (Co-author)

### 2.1. Continuation / completion (viết tiếp)

**Ý tưởng:** User viết một đoạn (hoặc vài câu cuối) → AI viết tiếp 1–2 đoạn theo đúng phong cách và nội dung.

**Kỹ thuật:**
- **Prompt:** Đưa vào: (1) overall story summary, (2) vài chương gần nhất (hoặc summary), (3) đoạn user vừa viết.
- **Instruction:** “Viết tiếp 1–2 đoạn, giữ cùng phong cách, ngôi kể và nhịp truyện. Không kết thúc câu chuyện.”
- **Giới hạn:** Giới hạn độ dài output (max tokens) để dễ chỉnh sửa và kiểm soát chất lượng.

Có thể lưu output vào `ai_generated_content` (input_prompt = đoạn user, ai_output = đoạn AI viết tiếp), và dùng `ai_contribution_ratio` khi publish chương.

---

### 2.2. Inline suggestion (gợi ý tại chỗ)

**Ý tưởng:** Trong lúc user gõ, AI gợi ý “câu tiếp theo” hoặc “đoạn tiếp theo” (kiểu autocomplete nâng cao).

**Kỹ thuật:**
- **Input:** Toàn bộ văn bản hiện tại (hoặc N ký tự/câu cuối) + story context (summary).
- **Output:** 2–3 phương án câu/đoạn tiếp (ngắn).
- **Cách triển khai:** API endpoint nhận `storyId`, `chapterId`, `currentText` → trả về danh sách gợi ý. Frontend hiển thị (dropdown hoặc ghost text).
- **Tối ưu:** Chỉ gọi API khi user dừng gõ (debounce 500–800ms), và có thể cache theo hash của `currentText` để tránh gọi trùng.

---

### 2.3. Role-playing nhân vật / đối thoại

**Ý tưởng:** AI đóng vai một nhân vật trong truyện để “đối thoại” với tác giả, giúp nghĩ ra lời thoại hoặc phản ứng nhân vật.

**Kỹ thuật:**
- Trích xuất mô tả nhân vật (từ summary hoặc structured extraction ở 1.3).
- Prompt dạng: “Bạn đang đóng vai [nhân vật]. Tính cách: ... Tình huống hiện tại: ... Tác giả cần lời thoại/ phản ứng cho: [tình huống]. Đưa ra 2–3 phương án ngắn.”
- Có thể làm API riêng: `POST /api/stories/{id}/characters/{name}/suggest-dialogue` với body là tình huống.

---

### 2.4. Rewrite / cải thiện văn (style transfer)

**Ý tưởng:** User viết bản thô → AI đề xuất bản “chỉnh sửa” (formal hơn, kịch tính hơn, hoặc đổi ngôi kể).

**Kỹ thuật:**
- Prompt: “Đoạn sau là bản nháp. Nhiệm vụ: viết lại với [phong cách: trang trọng / kịch tính / giản dị], giữ nguyên ý và sự kiện.”
- Có thể cho user chọn “tone” (dropdown) rồi map sang instruction cụ thể.
- Lưu cả bản gốc và bản AI trong `chapter_versions` hoặc tạm trong `ai_generated_content` để so sánh.

---

### 2.5. Kiểm soát mức độ AI (ai_contribution_ratio)

Backend đã có `ai_contribution_ratio` và `is_ai_clean`. Có thể:
- **ai_contribution_ratio:** Tính bằng % số từ do AI sinh / tổng số từ chương (ước lượng khi user chèn đoạn AI vào).
- **is_ai_clean:** Check list đơn giản (ví dụ: đã qua rewrite bởi user, hoặc user xác nhận “đã chỉnh sửa”) — có thể để sau.

---

## Phần 3: Công nghệ và stack gợi ý

| Thành phần        | Gợi ý cho đồ án |
|-------------------|------------------|
| **LLM**           | OpenAI API (GPT-4o mini / GPT-4o) hoặc Azure OpenAI — dễ tích hợp, ổn định. Nếu cần open-source: Llama 3, Mistral qua Ollama hoặc API. |
| **Embedding**     | OpenAI `text-embedding-3-small` hoặc `text-embedding-3-large`; open-source: sentence-transformers (chạy local). |
| **Vector DB**     | pgvector (PostgreSQL extension) — không cần service riêng; hoặc Qdrant / Chroma nếu muốn tách riêng. |
| **Backend**       | Giữ .NET: thêm service (ví dụ `IStoryAiService`), gọi HTTP client tới OpenAI/Azure. Có thể tách module AI sang Python microservice nếu cần RAG/ML phức tạp. |
| **Prompt**        | Giữ prompt trong config hoặc code (constants), sau có thể chuyển sang template trong DB. |
| **Rate limit / cost** | Giới hạn số lần gọi AI/user/ngày; ước lượng token để cảnh báo chi phí (trong admin). |

---

## Phần 4: Thứ tự triển khai gợi ý (cho đồ án)

1. **Phase 1 – Gợi ý chương tiếp**
   - Tóm tắt ngữ cảnh (1.1): summary từng chương + overall.
   - Prompt gợi ý (1.4): API nhận `storyId` → trả về 3–5 gợi ý (bullet).
   - Endpoint ví dụ: `GET/POST /api/stories/{id}/suggest-next-chapter`.

2. **Phase 2 – Đồng sáng tác cơ bản**
   - Continuation (2.1): API nhận `storyId`, `chapterId`, `currentContent` → trả về đoạn viết tiếp.
   - Lưu vào `ai_generated_content`, cập nhật `ai_contribution_ratio` khi user chèn vào chương.

3. **Phase 3 – Nâng cao (tuỳ thời gian)**
   - RAG (1.2) + embedding cho truyện → gợi ý chính xác hơn.
   - Structured extraction (1.3) → gợi ý bám nhân vật/sự kiện.
   - Inline suggestion (2.2) hoặc rewrite (2.4).

---

## Tài liệu tham khảo nhanh

- **Prompt engineering:** OpenAI Cookbook, Azure OpenAI Best Practices.
- **RAG:** LangChain RAG docs, LlamaIndex.
- **Embedding:** OpenAI Embeddings guide; sentence-transformers (Hugging Face).
- **Vector trong .NET:** Có thư viện gọi pgvector hoặc HTTP client tới Qdrant/Pinecone.

Nếu bạn muốn, bước tiếp theo có thể là: (1) thiết kế API chi tiết cho “gợi ý chương tiếp” và “viết tiếp”, hoặc (2) tạo interface `IStoryAiService` và stub implementation trong backend hiện tại.
