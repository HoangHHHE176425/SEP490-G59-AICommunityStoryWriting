# Gợi ý tối ưu chi phí AI (gợi ý chương tiếp)

Phần “gợi ý chương tiếp” gọi LLM mỗi request; dưới đây là cách chọn model và giảm token để tối ưu chi phí.

---

## 0. Dùng tài khoản Gemini Pro (đăng ký thẻ sinh viên FPT)

Nếu bạn đã có **tài khoản Gemini Pro** (đăng ký bằng thẻ sinh viên FPT):

1. **Lấy API key**
   - Vào [Google AI Studio](https://aistudio.google.com/) (đăng nhập đúng tài khoản Gemini Pro).
   - Mục **Get API key** / **API keys** → tạo key mới (hoặc dùng key có sẵn).

2. **Cấu hình trong project**
   - Mở `appsettings.Development.json` (hoặc `appsettings.Local.json`).
   - Điền key vào:
     ```json
     "Gemini": {
       "ApiKey": "AIza...",
       "Model": "gemini-2.0-flash",
       "MaxRequestsPerUserPerMinute": 2,
       "MaxRequestsPerUserPerDay": 20,
       "DemoMode": false,
       "SuggestionCacheExpirationHours": 24
     }
     ```
     - `DemoMode: true` = không gọi Gemini, luôn trả gợi ý mẫu (an toàn khi bảo vệ đồ án).
     - `SuggestionCacheExpirationHours` = thời gian cache gợi ý (cùng truyện + số chương), mặc định 24.
   - Chạy lại API và gọi **POST …/suggest-next-chapter** (có JWT) để test.

3. **Model “Pro”**
   - Tài khoản Pro thường dùng được cả **gemini-2.0-flash** (rẻ, nhanh) và **gemini-2.0-pro** / **gemini-1.5-pro** (chất lượng cao hơn, tốn token hơn).
   - Nếu muốn gợi ý chi tiết hơn, trong config đổi `"Model": "gemini-2.0-pro"` (hoặc tên model Pro đang hiển thị trong AI Studio). Giữ **gemini-2.0-flash** để tiết kiệm token cho đồ án.

4. **Quota – Pro có tăng token được không?**
   - **Có.** Tài khoản **Gemini Pro** (ví dụ đăng ký thẻ sinh viên FPT) thường được **tăng hạn mức (quota)** so với free tier thường: nhiều request hơn mỗi phút/ngày, nhiều token hơn mỗi phút/ngày. Con số cụ thể xem tại [AI Studio](https://aistudio.google.com/) → Usage / quota.
   - Trong **app của bạn**: khi dùng Pro và ít bị 429, có thể **tăng** giới hạn trong appsettings để tận dụng quota:
     - `MaxRequestsPerUserPerMinute`: 2 → 5 (nếu Google cho phép).
     - `MaxRequestsPerUserPerDay`: 20 → 50 hoặc 100 (tùy quota Pro).
   - **Token mỗi request** (độ dài nội dung gửi đi / độ dài gợi ý trả về): giới hạn kỹ thuật do **model** (Gemini 2.0 Pro/Flash đều có context lớn). Trong code hiện tại đang giới hạn 5 chương × 1200 ký tự + max output 1024 token; nếu cần gửi dài hơn hoặc nhận gợi ý dài hơn có thể chỉnh constant trong `StoryAiService` sau.

5. **Tại sao mới test lần đầu đã bị quá quota (429)?**
   - **Retry trong code = nhiều request:** Mỗi lần bạn gọi API gợi ý chương, backend sẽ gọi Gemini. Nếu Gemini trả 429, app **tự retry tối đa 5 lần** (exponential backoff). Vậy **1 lần bấm** có thể = **tới 5 request** gửi tới Google trong vài chục giây. Free tier thường giới hạn **2–15 request/phút** → dễ ngay lần đầu đã đụng rate limit.
   - **API key đã dùng chỗ khác:** Cùng key dùng trong AI Studio, Postman, script khác, hoặc app khác đều **cộng chung** quota. Nếu hôm nay bạn đã thử nhiều lần trên AI Studio hoặc tool khác, quota/ngày có thể đã hết.
   - **Free tier rất chặt:** Một số gói free chỉ **15 RPM**, **25–60 request/ngày** tùy model. Tài khoản Gemini Pro (sinh viên) thường có quota cao hơn; nếu bạn dùng key **free** (chưa đăng ký Pro), giới hạn thấp hơn nhiều.
   - **Cách xử lý:** (1) Vào [AI Studio](https://aistudio.google.com/) → xem **Usage / quota** (đã dùng bao nhiêu trong ngày/phút). (2) Thử **tạo API key mới** (có khi key mới được reset quota). (3) Tạm **giảm retry** trong appsettings: `Gemini:MaxRetriesOn429`: **1** hoặc **2** để 1 lần gọi không tạo quá nhiều request. (4) Đảm bảo dùng **đúng tài khoản Pro** (thẻ SV FPT) và key lấy từ tài khoản đó.

6. **Khi gặp 429 (quota exceeded)**
   - **Nguyên nhân:** Google giới hạn số request/token theo phút và theo ngày. Khi vượt → 429.
   - **Cách xử lý:**
     1. **Đợi 1–2 phút** rồi thử lại (quota theo phút thường reset mỗi phút).
     2. **Giảm tần suất gọi:** trong appsettings hạ `MaxRequestsPerUserPerMinute` (ví dụ 1) và `MaxRequestsPerUserPerDay` (ví dụ 10) để tránh vượt quota.
     3. **Kiểm tra Usage:** [AI Studio](https://aistudio.google.com/) → xem usage/quota hiện tại; [Rate limits](https://ai.google.dev/gemini-api/docs/rate-limits).
     4. **Retry + throttle trong backend:** App đã thêm **global throttle** (mặc định 30 giây giữa mỗi lần gọi Gemini) để tránh vượt quota. Retry khi 429: mặc định 2 lần, delay 20s → 40s. Cấu hình: `Gemini:MinSecondsBetweenCalls` (giây tối thiểu giữa các lần gọi, mặc định 30), `Gemini:MaxRetriesOn429`, `Gemini:RetryDelayBaseMs`.
   - Nếu 429 **liên tục** dù đã đợi: kiểm tra billing/plan của API key, hoặc dùng API key khác (project khác / tài khoản Pro).

7. **An toàn khi demo đồ án (tránh trượt vì hết quota)**
   - **Cache gợi ý (in-memory):** Backend lưu kết quả theo **truyện + số chương**. Lần đầu gọi Gemini cho truyện A (đang 3 chương) → trả gợi ý và **cache**. Lần sau cùng truyện A vẫn 3 chương → **trả từ cache**, không gọi Gemini. Cấu hình: `Gemini:SuggestionCacheExpirationHours` (mặc định 24 giờ).
   - **Chế độ Demo:** Trong appsettings đặt `"Gemini": { "DemoMode": true }` → **không gọi Gemini**, luôn trả 5 gợi ý mẫu. Dùng **trước và trong ngày bảo vệ** để chắc chắn không phụ thuộc quota. Response có `isFallbackOrDemo: true`, `modelUsed: "demo"`.
   - **Fallback khi 429:** Khi Gemini trả 429/quota, backend **không trả lỗi** mà trả gợi ý mẫu (giống demo), response 200, `isFallbackOrDemo: true`. Demo không bị “đỏ” dù đúng lúc hết quota.
   - **Gợi ý thực tế:** (1) Trước ngày demo 1–2 ngày: tắt DemoMode, gọi gợi ý 1 lần cho **đúng truyện sẽ demo** → cache đã có. (2) Trong ngày bảo vệ: hoặc bật `DemoMode: true`, hoặc giữ cache (gọi lại cùng truyện = cache hit). (3) Nếu vẫn 429 khi gọi mới → fallback tự trả gợi ý mẫu, giám khảo vẫn thấy tính năng hoạt động.

8. **Cách khác (chấp nhận tốn phí – ổn định, không lo quota)**
   - **Bật billing cho Gemini (Google AI Studio / Google Cloud):** Khi bật thanh toán, quota tăng mạnh (hàng trăm request/phút, token/ngày cao). Bạn trả theo dùng (pay-as-you-go). Vào [Google AI Studio](https://aistudio.google.com/) hoặc [Google Cloud Console](https://console.cloud.google.com/) → Billing → gắn thẻ. API key dùng chung với project đã bật billing.
   - **Dùng OpenAI làm nguồn chính hoặc dự phòng:** OpenAI (GPT-4o mini, GPT-4o) **không** giới hạn free chặt như Gemini; có API key là gọi được, trả tiền theo token. Có thể: (1) **Chuyển hẳn sang OpenAI** – đổi code gọi `openai.chat.completions` thay Gemini; (2) **Fallback khi 429** – giữ Gemini, khi Gemini trả 429 thì gọi OpenAI thay vì trả gợi ý mẫu (cần thêm config `OpenAI:ApiKey`, `OpenAI:Model` và đoạn code gọi OpenAI). Chi phí: ~$0.15–0.40/1M token input, ~$0.60–1.60/1M output (gpt-4o-mini rẻ hơn gpt-4o).
   - **Google Cloud Vertex AI (Gemini):** Gọi Gemini qua [Vertex AI](https://cloud.google.com/vertex-ai) thay vì AI Studio. Cần project GCP + bật billing. Quota và SLA tốt hơn, phù hợp nếu bạn đã dùng GCP.
   - **Azure OpenAI:** Nếu trường/doanh nghiệp có Azure, có thể dùng Azure OpenAI (GPT-4o mini, etc.) với quota riêng, không phụ thuộc Google.

**Tóm tắt:** Không muốn phụ thuộc quota free → bật billing (Gemini hoặc GCP) hoặc thêm OpenAI (chính hoặc fallback). Đồ án có ngân sách nhỏ thì **OpenAI GPT-4o mini** làm fallback khi 429 là hợp lý: demo vẫn có gợi ý AI thật, chỉ tốn vài cent khi Gemini hết quota.

---

## 1. Chọn model theo mục tiêu

| Mục tiêu | Gợi ý model | Lý do |
|----------|-------------|--------|
| **Rẻ nhất, đủ dùng** | **Gemini 2.0 Flash** (đang dùng) hoặc **Gemini 2.5 Flash Lite** | Input ~$0.10/1M token, output ~$0.40/1M; phù hợp task gợi ý outline + JSON. |
| **Free tier, demo** | Giữ **Gemini** (AI Studio) | Free tier giới hạn chặt; nên bật **rate limit** (MaxRequestsPerUserPerDay ~20) để không vượt quota. |
| **Có ngân sách, cần ổn định** | **OpenAI GPT-4o mini** | Đắt hơn Gemini Flash nhưng ổn định, JSON tốt; dùng khi Gemini quota/429 nhiều. |
| **Chi phí = 0 (dev / thử)** | **Ollama + model local** (Llama 3, Mistral, Qwen) | Không tốn tiền API; cần tự host, chất lượng tiếng Việt/JSON có thể kém hơn. |

**Với đồ án tốt nghiệp:** Nên giữ **Gemini 2.0 Flash** hoặc chuyển sang **Gemini 2.5 Flash Lite** (nếu có trên AI Studio), kèm rate limit để vừa đủ demo vừa tránh hết hạn mức.

---

## 2. So sánh nhanh giá (text, ~2025)

| Provider | Model | Input (USD/1M token) | Output (USD/1M token) | Ghi chú |
|----------|--------|----------------------|------------------------|---------|
| Google AI | gemini-2.0-flash | ~0.10 | ~0.40 | Rẻ, free tier rất giới hạn |
| Google AI | gemini-2.5-flash-lite | ~0.10 | ~0.40 | Rẻ nhất dòng 2.5, có thể ít quota free hơn |
| OpenAI | gpt-4o-mini | ~0.15–0.40 | ~0.60–1.60 | Đắt hơn Gemini Flash, ổn định |
| Anthropic | claude-3-haiku | tương đương mini | | Lựa chọn thay thế |

Giá thay đổi theo thời gian; nên xem trang chính thức: [Gemini pricing](https://ai.google.dev/pricing), [OpenAI pricing](https://openai.com/pricing).

---

## 3. Tối ưu token (giảm chi phí mỗi request)

- **Đã làm trong code:**
  - Chỉ đưa tối đa **5 chương** gần nhất.
  - Mỗi chương cắt tối đa **1200 ký tự** (`MaxCharsPerChapter`).
- **Có thể làm thêm:**
  - **Tóm tắt chương (summary):** Lưu 100–200 từ/chương trong DB; gửi summary thay vì full content → giảm mạnh token.
  - **Giảm số chương / ký tự:** Trong config hoặc code giảm `MaxChaptersInContext` (vd 3), `MaxCharsPerChapter` (vd 800) khi cần tiết kiệm.
  - **Giới hạn output:** Đã set `maxOutputTokens: 1024`; có thể hạ xuống ~512 cho gợi ý ngắn.

---

## 4. Cấu hình gợi ý (appsettings)

```json
"Gemini": {
  "ApiKey": "...",
  "Model": "gemini-2.0-flash",
  "MaxRequestsPerUserPerMinute": 2,
  "MaxRequestsPerUserPerDay": 20
}
```

- **Chỉ dùng free tier:** Giữ `MaxRequestsPerUserPerDay` thấp (15–20), `MaxRequestsPerUserPerMinute` = 1–2.
- **Đã bật billing:** Có thể tăng dần (vd 5/phút, 100/ngày) và thử **gemini-2.5-flash-lite** nếu API hỗ trợ (tên model có thể khác trên AI Studio).

---

## 5. Kết luận nhanh

- **Tối ưu chi phí:** Giữ **Gemini (2.0 Flash hoặc 2.5 Flash Lite)**; bật **rate limit**; có thể thêm **tóm tắt chương** để giảm token.
- **Khi hay gặp 429 / hết quota:** Giảm `MaxRequestsPerUserPerDay` hoặc cân nhắc **OpenAI GPT-4o mini** (trả phí nhưng ổn định).
- **Dev / demo không tốn tiền:** Rate limit chặt + có thể tích hợp **Ollama** (model local) qua một endpoint/config riêng sau.

Nếu sau này bạn muốn hỗ trợ nhiều provider (Gemini + OpenAI hoặc Ollama), có thể tách interface `ILlmService` và implement từng provider; config chọn provider và model.
