# Ý tưởng: Kiểm tra chương tác giả viết có giống chương AI sinh ra hay không

## AI Similarity Check – Mô tả và công nghệ sử dụng

**Mô tả:** The system compares the author's submitted chapter with AI-generated drafts created by the platform during the writing process. Semantic similarity is calculated using text embeddings and cosine similarity. If the similarity score exceeds a configurable threshold (e.g. 70%), the system marks the chapter as potentially AI-assisted (trường `isSimilar` trong response và có thể lưu phần trăm vào `chapters.ai_similarity_percent` khi chương đã PUBLISHED).

**Công nghệ sử dụng (Technology used):**

| Thành phần | Công nghệ / Chi tiết |
|------------|----------------------|
| **Text embeddings** | **Ollama** (mặc định): API `POST /api/embed`, model **nomic-embed-text** — hoặc **OpenAI-compatible** (vd. OpenAI, Azure): `POST /v1/embeddings`, model cấu hình (vd. text-embedding-3-small). Cấu hình: `AI:EmbeddingBaseUrl`, `AI:EmbeddingModel`, `AI:EmbeddingProvider`, `AI:EmbeddingApiKey` (nếu dùng OpenAI). |
| **Vector biểu diễn** | Mỗi đoạn văn → vector số thực (float[]) độ dài cố định (nomic-embed-text: 768 chiều; OpenAI tùy model). |
| **Độ tương đồng ngữ nghĩa** | **Cosine similarity** giữa hai vector embedding: cos(A, B) = (A·B) / (‖A‖ × ‖B‖), quy về thang 0–100%. |
| **Fallback khi không có embedding** | **Jaccard similarity** theo từ (word-level): tách từ, tính intersection/union của hai tập từ → %. Không cần API, chạy local. |
| **Ngưỡng “AI-assisted”** | Cấu hình `ChapterCompare:SimilarityThresholdPercent` trong appsettings (mặc định **85%**). Nếu similarity ≥ ngưỡng → `isSimilar = true` (đánh dấu có thể có đóng góp AI). Có thể đổi thành 70% hoặc giá trị khác. |
| **Lưu kết quả** | Khi chương đã **PUBLISHED** và gọi **POST /api/ai/compare-chapter**, điểm similarity được ghi vào cột **chapters.ai_similarity_percent** (decimal 0–100). |
| **Backend / API** | .NET (C#), service **ChapterCompareService**, helper **EmbeddingHelper**; endpoint **POST /api/ai/compare-chapter** (JWT, role AUTHOR hoặc ADMIN). |

---

## Mục đích

- Cho phép hệ thống (hoặc tác giả) biết **nội dung chương hiện tại** (tác giả viết/chỉnh sửa) **giống đến mức nào** so với **bản nháp do AI sinh ra** cho chương đó.
- Dùng để: minh bạch (hiển thị % đóng góp AI), kiểm tra đạo văn nội bộ, hoặc gợi ý chỉnh sửa khi tác giả lệch xa bản AI.

## Dữ liệu hiện có

| Nguồn | Bảng / Entity | Nội dung |
|-------|----------------|----------|
| Chương (tác giả) | `chapters` | `content` = nội dung chương hiện tại |
| Chương do AI sinh | `ai_generated_content` | `chapter_id`, `ai_output` = nội dung AI sinh cho chương đó |

Một chương có thể có nhiều bản `ai_generated_content` (nhiều lần gợi ý/đồng sáng tác). So sánh thường lấy **bản AI mới nhất** theo `created_at`.

---

## Khi dùng đồng sáng tác (co-create), nội dung AI lưu ở đâu?

**Luồng hiện tại (đã triển khai):**

1. **POST /api/ai/co-create** (đồng sáng tác): Backend chạy 3 agent (dàn ý → viết → kiểm duyệt), có thể sửa tối đa vài vòng theo feedback.
2. **Kết quả:** API trả về **CoCreationResponse**, trong đó có **FinalContent** = nội dung văn bản do AI sinh ra.
3. **Cách cũ — vừa chương vừa ai_generated_content:** Mỗi lần co-create **thành công** (có FinalContent), backend **tạo một chương nháp (DRAFT)** trong **chapters** (title "Bản nháp AI #n", content = FinalContent) **và** ghi một bản ghi vào **ai_generated_content** (story_id, chapter_id = chương vừa tạo, user_id, input_prompt, ai_output = FinalContent).
4. **Response:** **ChapterId** (chương nháp vừa tạo) và **AiGeneratedContentId** (bản ghi ai_generated_content). Compare-chapter dùng chapter_id để lấy nội dung tác giả (chapters.content) và nội dung AI (ai_generated_content.ai_output).

**Tóm lại:** Nội dung AI được lưu **cả vào chapters** (chương nháp DRAFT) **và ai_generated_content** (có story_id, chapter_id). Bảng đã thêm **story_id**; script: DataAccessObjects/Scripts/ai_generated_content_add_story_id.sql.

---

## Cách làm (so sánh)

1. **Vào API:** truyền `chapterId` (hoặc `storyId` + `orderIndex`).
2. **Lấy dữ liệu:**
   - Nội dung tác giả: `chapters.content` (chapter hiện tại).
   - Nội dung AI: **tất cả** bản ghi `ai_generated_content` của chương đó (giới hạn 50 bản mới nhất).
3. **So sánh với từng bản AI, lấy điểm cao nhất:** Tác giả có thể dùng bản AI sinh ra **đầu tiên**, **thứ hai**, hay **mới nhất** — hệ thống so sánh nội dung tác giả với **từng** bản AI rồi chọn **similarity cao nhất** làm kết quả. Như vậy dù tác giả chọn bản nào thì vẫn ra điểm đúng.
4. **Tính độ giống nhau (similarity)** cho mỗi cặp (tác giả vs từng bản AI):
   - **Option A – Embedding:** embed cả hai đoạn (dùng `EmbeddingHelper`), tính **cosine similarity** → điểm 0–1 (hoặc 0–100%). Ưu: hiểu nghĩa, paraphrase vẫn gần.
   - **Option B – Text:** chuẩn hóa (bỏ dấu, lowercase), so sánh theo từ (Jaccard, n-gram) hoặc LCS. Ưu: không cần embedding, nhanh.
5. **Kết quả trả về:** ví dụ `similarityScore` (%), `isSimilar` (true nếu > ngưỡng, e.g. 85%), và có thể cập nhật `ai_generated_content.similarity_score` khi chạy kiểm tra.

## API đã triển khai

- **Endpoint:** `POST /api/ai/compare-chapter` (Authorization: Bearer JWT, role AUTHOR hoặc ADMIN).
- **Request body:** `{ "chapterId": "guid" }`.
- **Response:**  
  `{ "similarityScore": 92.5, "isSimilar": true, "authorContentLength": 1200, "aiContentLength": 1150, "hasBothContents": true, "message": "Nội dung chương rất giống với bản AI." }`
- **Điều kiện:** Chỉ tác giả truyện (story.author_id = userId) được so sánh; chương phải có `chapters.content` và ít nhất một bản `ai_generated_content`. Hệ thống so sánh với **mọi** bản AI của chương (tối đa 50 bản) và trả về **điểm cao nhất** (tương ứng bản AI giống nội dung tác giả nhất).
- **Cách tính similarity:** (chi tiết ở mục dưới.)

---

## Các bước test trên Swagger

### Chuẩn bị

1. **Chạy API:** Mở terminal, chạy dự án (vd. `dotnet run` trong thư mục `AIStory.API`). API thường chạy tại `https://localhost:7xxx` hoặc `http://localhost:5xxx` (xem output khi chạy).
2. **Tài khoản:** Cần một user có **role AUTHOR** (hoặc ADMIN) và là **tác giả** của ít nhất một truyện có chương.
3. **Dữ liệu test:** Cần ít nhất một **chapter** có:
   - `chapters.content` không rỗng (nội dung chương hiện tại),
   - và ít nhất một bản ghi **ai_generated_content** cho chương đó (vd. đã dùng đồng sáng tác).  
   Nếu chưa có, tạo truyện → tạo chương → dùng API đồng sáng tác để sinh bản AI, rồi lưu nội dung vào chương.

### Bước 1: Mở Swagger

- Trình duyệt mở: **https://localhost:7xxx/swagger** hoặc **http://localhost:5xxx/swagger** (thay đúng cổng của bạn).

### Bước 2: Đăng nhập lấy JWT

1. Trong Swagger, tìm **POST /api/auth/login**.
2. Bấm **Try it out**.
3. Body mẫu:
   ```json
   {
     "email": "email_tac_gia@example.com",
     "password": "mat_khau"
   }
   ```
4. Bấm **Execute**.
5. Trong response (200), copy giá trị **`accessToken`** (chuỗi dài, không copy dấu ngoặc).

### Bước 3: Authorize trong Swagger

1. Bấm nút **Authorize** (ở góc trên Swagger UI).
2. Ở ô **Value** cho **Bearer**: dán **accessToken** vừa copy.  
   - Một số phiên bản Swagger tự thêm chữ `Bearer `; nếu không có thì nhập: `Bearer <dán_token_vào_đây>`.
3. Bấm **Authorize** rồi **Close**.

### Bước 4: Gọi API so sánh chương

1. Tìm **POST /api/ai/compare-chapter**.
2. Bấm **Try it out**.
3. **Request body** nhập đúng **chapterId** (Guid của chương cần so sánh), ví dụ:
   ```json
   {
     "chapterId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
   }
   ```
   Thay `3fa85f64-5717-4562-b3fc-2c963f66afa6` bằng **id thật** của một chương thuộc truyện mà user đăng nhập là tác giả (lấy từ GET truyện/chương hoặc database).
4. Bấm **Execute**.

### Bước 5: Đọc kết quả

- **200:** Response body có dạng:
  - `similarityScore`: độ giống 0–100.
  - `isSimilar`: true nếu ≥ ngưỡng (mặc định 85%).
  - `authorContentLength`, `aiContentLength`: độ dài hai nội dung.
  - `hasBothContents`: true nếu có đủ cả nội dung tác giả và bản AI.
  - `message`: thông báo ngắn.
- **401 Unauthorized:** Chưa đăng nhập hoặc token hết hạn → làm lại Bước 2 và 3.
- **403:** User không phải tác giả của truyện chứa chương đó → dùng tài khoản AUTHOR là tác giả của truyện đó.
- **200 nhưng hasBothContents = false:** Chương chưa có nội dung hoặc chưa có bản AI → cần tạo/chạy đồng sáng tác trước.

### Lấy chapterId để test

- Cách 1: **GET /api/stories/{storyId}** (hoặc danh sách truyện của tác giả) → vào từng truyện, gọi API lấy danh sách chương (nếu có) → lấy `id` của một chương.
- Cách 2: Trong database, truy vấn bảng `chapters` (cột `id`) với `story_id` thuộc truyện do user AUTHOR sở hữu.

---

## Cách tính similarity – giải thích đơn giản

Hệ thống cần **một con số từ 0% đến 100%** để biết hai đoạn văn “giống” đến mức nào. Có **hai cách** tính, tùy bạn có cấu hình embedding hay không.

### 1. Khi đã cấu hình embedding (AI:EmbeddingBaseUrl, AI:EmbeddingModel)

**Embedding là gì?**  
Mỗi đoạn văn được chuyển thành **một dãy số** (vector) thể hiện “nghĩa” của đoạn đó. Hai đoạn nghĩa càng gần thì dãy số càng giống nhau.

**Embedding biến đoạn văn thành vector kiểu gì?**  
- **Đầu vào:** Một đoạn văn (chuỗi ký tự), ví dụ *"Lan thức dậy sớm. Cô nhìn ra cửa sổ."*  
- **Đầu ra:** Một **vector** = mảng số thực (float) có **độ dài cố định** do model quy định (vd. model **nomic-embed-text** của Ollama thường ra **768** số; OpenAI text-embedding-3-small ra 1536 số).  
- **Cách làm:** Backend **gửi đoạn văn lên API embedding** (Ollama: `POST /api/embed`, hoặc OpenAI-compatible: `POST /embeddings`). Model embedding là **mạng neural** đã được huấn luyện sẵn: nó đọc cả đoạn văn, “nén” thông tin nghĩa thành một điểm trong không gian nhiều chiều (mỗi chiều là một số). Model được train sao cho hai đoạn **nghĩa gần nhau** thì vector **gần nhau** (góc nhỏ, cosine lớn), đoạn nghĩa khác thì vector xa nhau.  
- **Không có công thức tường minh** từ chữ → từng số: từng chiều trong vector không gắn nhãn “chiều 1 = chủ đề A, chiều 2 = chủ đề B” mà model tự học; ta chỉ dùng cả vector để so sánh (cosine) với vector đoạn khác.  
- **Trong dự án:** `EmbeddingHelper.GetEmbeddingAsync(text, ...)` gọi API (Ollama hoặc OpenAI), nhận về mảng `float[]` — đó chính là vector của đoạn văn.

**Cosine similarity là gì?**  
Từ hai dãy số (một của chương tác giả, một của chương AI), hệ thống tính góc giữa chúng:
- Góc = 0° → **100%** (hai đoạn rất giống nghĩa).
- Góc càng lớn → % càng thấp.
- Kết quả được quy về thang **0–100%**.

**Công thức này lấy ở đâu?**  
- Xuất phát từ **toán học**: trong không gian vector, cosin của góc giữa hai vector **A** và **B** được định nghĩa là **cos(A, B) = (A·B) / (‖A‖ × ‖B‖)**, trong đó A·B là tích vô hướng (dot product), ‖A‖ và ‖B‖ là độ dài (norm) của từng vector. Đây là công thức chuẩn trong đại số tuyến tính và hình học, không phải “tự nghĩ ra” cho dự án.
- Trong **NLP** và tìm kiếm theo nghĩa, cosine similarity được dùng từ lâu để so sánh vector văn bản (TF-IDF, embedding). Các model embedding (OpenAI, Ollama nomic-embed-text, v.v.) đều mặc định so vector bằng cosine.

**Tại sao lại dùng nó?**  
- **Chỉ so “hướng”, không phụ thuộc độ dài:** Hai đoạn dài/ngắn khác nhau nhưng cùng hướng nghĩa thì vector gần song song → cos gần 1. Độ dài vector bị triệt tiêu trong công thức nên không làm lệch kết quả.
- **Giá trị nằm trong khoảng cố định:** cos ∈ [-1, 1]; với embedding thường không âm thì cos ∈ [0, 1], dễ đổi sang phần trăm (0–100%).
- **Chuẩn trong ngành:** Tìm kiếm semantic, RAG, so sánh embedding trong các API/paper đều dùng cosine; dùng cùng cách giúp kết quả dễ so sánh và đúng với cách model được huấn luyện.

**Ưu điểm:** Hiểu được **nghĩa**. Ví dụ “Lan thức dậy sớm” và “Lan dậy sớm” vẫn được coi là gần nhau dù khác chữ.

**Ví dụ minh họa (số giả lập):**

Trong thực tế, mỗi đoạn văn được model embedding (vd. nomic-embed-text) chuyển thành một vector có **rất nhiều số** (vd. 768 chiều). Ở đây dùng vector **3 chiều** để dễ hình dung.

| Bước | Mô tả |
|------|--------|
| 1. Đoạn tác giả | *"Lan thức dậy sớm. Cô nhìn ra cửa sổ."* |
| 2. Đoạn AI | *"Lan dậy sớm. Cô nhìn ra cửa sổ."* |
| 3. Embedding (giả lập) | Đoạn tác giả → vector **A** = (0.8, 0.5, 0.3). Đoạn AI → vector **B** = (0.75, 0.55, 0.28). |
| 4. Cosine similarity | cos(A,B) = (A·B) / (‖A‖ × ‖B‖). A·B = 0.8×0.75 + 0.5×0.55 + 0.3×0.28 = 0.959. ‖A‖×‖B‖ ≈ 0.99 → cos ≈ 0.959/0.99 ≈ **0,97** → **97%**. |
| 5. Kết luận | Hai câu gần giống nghĩa (tác giả chỉ bỏ từ “thức”) → điểm cao dù không trùng từ 100%. |

Nếu tác giả viết lại hoàn toàn, ví dụ *"Lan ngủ đến trưa. Tỉnh dậy bên bờ suối."*, vector sẽ khác hướng → cos nhỏ (vd. 0.3 → 30%).

---

### 2. Khi chưa cấu hình embedding: Jaccard theo từ

Không gọi API embedding thì hệ thống so sánh **theo từ** (word), không hiểu nghĩa.

**Các bước:**

1. Tách mỗi đoạn thành **tập từ** (bỏ dấu câu, không phân biệt hoa thường).
2. Đếm:
   - **Intersection** = số từ **xuất hiện ở cả hai** đoạn.
   - **Union** = tổng số từ **khác nhau** khi gộp hai đoạn (mỗi từ chỉ đếm một lần).
3. **Công thức:** `similarity = (intersection / union) × 100%`.

**Ví dụ bằng số:**

- Đoạn tác giả: *"Lan thức dậy sớm. Cô nhìn ra cửa sổ."*  
  → Tập từ: `{ Lan, thức, dậy, sớm, Cô, nhìn, ra, cửa, sổ }` → **9 từ**.

- Đoạn AI: *"Lan dậy sớm. Cô nhìn ra cửa sổ."*  
  → Tập từ: `{ Lan, dậy, sớm, Cô, nhìn, ra, cửa, sổ }` → **8 từ**.

- **Từ có ở cả hai:** Lan, dậy, sớm, Cô, nhìn, ra, cửa, sổ → **intersection = 8**.
- **Gộp hai tập (bỏ trùng):** Lan, thức, dậy, sớm, Cô, nhìn, ra, cửa, sổ → **union = 9**.
- **Similarity = 8/9 ≈ 88,9%**.

Nếu tác giả viết lại hoàn toàn, ít từ trùng → intersection nhỏ, union lớn → % thấp (vd. 20–40%).

---

### 3. Ngưỡng “giống” (mặc định 85%)

Sau khi có con số 0–100%, hệ thống cần quyết định: **coi là “giống” hay “khác”?**

- **Quy tắc:** Nếu `similarityScore ≥ 85%` → `isSimilar = true` (coi là giống bản AI).
- **85%** là mặc định; bạn có thể đổi trong appsettings: `"ChapterCompare": { "SimilarityThresholdPercent": 85 }`.

Tóm lại:
- **Cách tính** = embedding (cosine) hoặc Jaccard theo từ → ra số %.
- **Ngưỡng** = so số % với 85% (hoặc giá trị cấu hình) → ra `isSimilar` true/false.

---

## Trong dự án này nên dùng cách nào?

Dự án của bạn đã có:

- **Embedding:** `AI:EmbeddingProvider = Ollama`, `AI:EmbeddingBaseUrl = http://localhost:11434`, `AI:EmbeddingModel = nomic-embed-text`
- **RAG / VectorStore:** FAISS (đã dùng embedding để index truyện)

**Khuyến nghị: dùng Embedding (cosine similarity).**

| Tiêu chí | Embedding (cosine) | Jaccard (từ) |
|----------|--------------------|--------------|
| **Đã cấu hình** | Có (Ollama + nomic-embed-text) | Không cần cấu hình |
| **Đồng bộ với RAG** | Cùng cách biểu diễn nghĩa với RAG | Không liên quan |
| **Chất lượng** | Hiểu nghĩa, paraphrase vẫn gần | Chỉ so trùng từ, không hiểu nghĩa |
| **Phụ thuộc** | Cần Ollama chạy (localhost:11434) | Không phụ thuộc gì thêm |
| **Tốc độ** | 2 lần gọi API embedding (có thể chậm nếu chương dài) | Rất nhanh, không gọi API |

**Cách dùng trong code (đã implement):**

- Nếu **có** `AI:EmbeddingBaseUrl` (và với Ollama không cần ApiKey) → service **tự dùng embedding + cosine**.
- Nếu **xóa/để trống** `EmbeddingBaseUrl` hoặc Ollama không chạy → service **tự fallback Jaccard** (không lỗi).

**Tóm tắt:**

- **Máy dev / server có chạy Ollama** → giữ cấu hình như hiện tại → dùng **embedding (cosine)**.
- **Deploy lên môi trường không có Ollama**, hoặc muốn so sánh nhanh, không phụ thuộc API → tắt embedding (xóa/comment `EmbeddingBaseUrl` hoặc để rỗng) → dùng **Jaccard**.

Không cần đổi code; chỉ cần bật/tắt cấu hình embedding là đủ.

---

## Ví dụ cụ thể (từng bước)

### Tình huống

- Bạn là **tác giả** truyện "Rừng xanh".
- Bạn dùng tính năng **đồng sáng tác** → AI sinh ra bản nháp cho **Chương 3**.
- Bạn chỉnh sửa bản nháp rồi lưu vào chương.
- Bạn muốn biết: **chương bạn vừa sửa có còn “giống” bản AI đã sinh ra hay đã khác hẳn?**

### Dữ liệu trong database (minh họa)

**Bảng `chapters`** (chương hiện tại – do tác giả lưu):

| id (chapterId) | story_id | title    | content |
|----------------|----------|----------|---------|
| abc-111        | story-99 | Chương 3 | *"Sáng hôm sau, Lan thức dậy sớm. Cô nhìn ra cửa sổ, rừng vẫn còn sương mù. Cô quyết định đi tìm con đường ra..."* |

**Bảng `ai_generated_content`** (bản AI đã sinh cho chương đó):

| id   | chapter_id | ai_output | created_at |
|------|------------|-----------|------------|
| xyz-1 | abc-111   | *"Sáng hôm sau, Lan thức dậy sớm. Cô nhìn ra cửa sổ, rừng vẫn còn sương mù. Cô quyết định đi tìm con đường ra..."* | 2025-03-01 10:00 |

→ Ở đây **nội dung gần như copy** từ AI → độ giống sẽ **rất cao** (vd. 95%+).

**Ví dụ khác:** Tác giả viết lại hoàn toàn:

- `chapters.content` = *"Lan ngủ say đến trưa. Tỉnh dậy cô thấy mình nằm bên bờ suối. Không còn nhớ chuyện đêm qua."*
- `ai_generated_content.ai_output` = *"Sáng hôm sau, Lan thức dậy sớm. Cô nhìn ra cửa sổ..."*

→ Hai đoạn **khác nhau** → điểm giống sẽ **thấp** (vd. 20–40%), `isSimilar` = false.

### Gọi API

**Request:**

```http
POST /api/ai/compare-chapter
Authorization: Bearer <JWT của tác giả>
Content-Type: application/json

{
  "chapterId": "abc-111"
}
```

**Response (trường hợp giống – tác giả gần như giữ nguyên bản AI):**

```json
{
  "similarityScore": 96.2,
  "isSimilar": true,
  "authorContentLength": 156,
  "aiContentLength": 158,
  "hasBothContents": true,
  "message": "Nội dung chương rất giống với bản AI."
}
```

**Response (trường hợp khác – tác giả viết lại nhiều):**

```json
{
  "similarityScore": 32.5,
  "isSimilar": false,
  "authorContentLength": 98,
  "aiContentLength": 158,
  "hasBothContents": true,
  "message": "Nội dung chương khác so với bản AI."
}
```

**Response (chưa có bản AI cho chương này):**

```json
{
  "similarityScore": 0,
  "isSimilar": false,
  "authorContentLength": 156,
  "aiContentLength": 0,
  "hasBothContents": false,
  "message": "Chưa có bản nội dung AI sinh ra cho chương này."
}
```

### Ý nghĩa trong ví dụ

| similarityScore | isSimilar | Ý nghĩa (trong ví dụ) |
|-----------------|-----------|-------------------------|
| ~95%            | true      | Chương tác giả **gần giống** bản AI (ít chỉnh sửa / gần như copy). |
| ~30%            | false     | Chương tác giả **khác nhiều** so với bản AI (viết lại, sáng tạo thêm). |
| hasBothContents = false | - | Chưa có bản AI để so (chưa dùng đồng sáng tác cho chương này, hoặc nội dung trống). |

---

### Ví dụ về `ai_similarity_percent` (cột trong bảng `chapters`)

`ai_similarity_percent` là **phần trăm giống nhau** (0–100) giữa nội dung chương hiện tại (`chapters.content`) và bản AI giống nhất trong số các bản `ai_generated_content` của chương đó. Chỉ được **cập nhật** khi chương đã **PUBLISHED** và có gọi API **compare-chapter**.

**Cách so sánh / lưu:**

1. Tác giả publish chương → `chapters.status = 'PUBLISHED'`.
2. Ai đó (tác giả hoặc hệ thống) gọi **POST /api/ai/compare-chapter** với `chapterId` của chương đó.
3. Backend so sánh `chapters.content` với **từng** bản `ai_generated_content` của chương, lấy **điểm cao nhất** (similarity %).
4. Backend ghi điểm đó vào **`chapters.ai_similarity_percent`** (chỉ khi chương đang PUBLISHED). Response API vẫn trả `similarityScore` giống giá trị đó.

**Ví dụ số liệu:**

| Tình huống | chapters.content (tóm tắt) | Bản AI so sánh | ai_similarity_percent (sau compare) | Ý nghĩa |
|------------|----------------------------|----------------|--------------------------------------|--------|
| Tác giả gần như giữ nguyên bản AI | *"Sáng hôm sau, Lan thức dậy sớm. Cô nhìn ra cửa sổ..."* | Giống gần như từng chữ | **96.2** | Nội dung chương **rất giống** một bản AI → đóng góp AI cao. |
| Tác giả sửa ít từ, thêm vài câu | *"Sáng hôm sau, Lan dậy sớm. Cô nhìn ra cửa sổ. Mặt trời vừa lên."* | Bản AI: *"Sáng hôm sau, Lan thức dậy sớm. Cô nhìn ra cửa sổ..."* | **88.0** | Vẫn **giống** (≥ 85%) → có thể hiển thị "Nội dung gần với bản AI". |
| Tác giả viết lại hoàn toàn | *"Lan ngủ đến trưa. Tỉnh dậy bên bờ suối."* | Bản AI: *"Sáng hôm sau, Lan thức dậy sớm..."* | **28.5** | **Khác** so với bản AI → đóng góp tác giả nhiều, AI ít. |
| Chương chưa gọi compare hoặc DRAFT | (bất kỳ) | - | **NULL** | Chưa có lần so sánh nào được lưu vào chương. |

**Dùng trong ứng dụng:**

- **Hiển thị cho tác giả:** Ví dụ "Chương này giống bản AI **92%**" hoặc "Độ tương đồng với bản nháp AI: **92%**".
- **Lọc / báo cáo:** Ví dụ chỉ lấy các chương có `ai_similarity_percent >= 80` để xem chương nào “gần bản AI”.
- **Ngưỡng:** Nếu `ai_similarity_percent >= 85` (hoặc ngưỡng cấu hình) → coi là **giống** bản AI (`isSimilar = true` khi gọi API).

**Lưu ý:** Giá trị chỉ thay đổi khi có **lần gọi compare-chapter mới** (và chương đang PUBLISHED). Nếu tác giả sửa nội dung chương sau đó mà không gọi lại compare-chapter thì `ai_similarity_percent` vẫn là giá trị cũ.

---

## Tóm tắt

- **Chương tác giả** = `chapters.content`.  
- **Chương AI** = `ai_generated_content.ai_output` (bản mới nhất theo `chapter_id`).  
- So sánh = tính **similarity** (embedding hoặc text) giữa hai chuỗi → trả về điểm và nhận định có “giống” hay không.
