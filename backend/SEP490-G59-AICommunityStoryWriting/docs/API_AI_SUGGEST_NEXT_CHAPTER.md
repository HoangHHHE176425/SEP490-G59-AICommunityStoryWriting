# API: AI gợi ý chương tiếp theo

**Dữ liệu demo & JSON mẫu:** xem thư mục [docs/demo/](demo/README_DEMO.md) (seed SQL + `login-request.json`, response mẫu, StoryId demo).

**Giới hạn tránh hết hạn mức:** Mỗi user bị giới hạn số lần gọi/phút và/ngày (mặc định 2/phút, 20/ngày). Cấu hình `Gemini:MaxRequestsPerUserPerMinute` và `Gemini:MaxRequestsPerUserPerDay` trong appsettings. Vượt giới hạn → API trả **429** với message hướng dẫn.

**Gợi ý AI / tối ưu chi phí:** Xem [docs/AI_COST_OPTIMIZATION.md](AI_COST_OPTIMIZATION.md) (chọn model, so sánh giá, giảm token).

## Endpoint (Swagger)

- **Method:** `POST`
- **URL:** `/api/stories/{id}/suggest-next-chapter`
- **Authorization:** Bearer JWT (role **AUTHOR** hoặc **ADMIN**)
- **Path:** `id` = Guid của truyện (story)

## Cách demo bằng Swagger

1. **Chạy API**
   ```bash
   dotnet run --project AIStory.API
   ```
   Mở trình duyệt: `https://localhost:7xxx/swagger` hoặc `http://localhost:5xxx/swagger` (xem URL trong output hoặc `launchSettings.json`).

2. **Cấu hình Gemini API key**
   - Mở `appsettings.Development.json` hoặc `appsettings.Local.json`.
   - Đặt `Gemini:ApiKey` = API key của bạn (Google AI Studio: https://aistudio.google.com/apikey).
   - Ví dụ:
     ```json
     "Gemini": {
       "ApiKey": "AIza...",
       "Model": "gemini-2.0-flash"
     }
     ```
   - Không commit key lên git; dùng `appsettings.Local.json` (đã có trong optional load trong `Program.cs`).

3. **Đăng nhập lấy JWT**
   - Gọi API login (ví dụ `POST /api/auth/login`) để lấy token.
   - Trong Swagger: bấm **Authorize**, dán token (có thể bỏ chữ "Bearer" nếu UI tự thêm).

4. **Gọi gợi ý chương tiếp**
   - Chọn `POST /api/stories/{id}/suggest-next-chapter`.
   - Nhập **id** = Guid của một truyện **đã có ít nhất 1 chương** (và truyện đó thuộc author của token nếu bạn có check quyền theo story).
   - Execute.

## Response 200 (thành công)

```json
{
  "storyId": "guid",
  "storyTitle": "Tên truyện",
  "nextChapterNumber": 4,
  "suggestions": [
    { "title": "Tiêu đề gợi ý 1", "description": "Mô tả ngắn hướng phát triển" },
    { "title": "Tiêu đề gợi ý 2", "description": "Mô tả ngắn" }
  ],
  "modelUsed": "gemini-2.0-flash",
  "promptTokens": 1200,
  "completionTokens": 150,
  "isFallbackOrDemo": false
}
```

- **isFallbackOrDemo:** `true` khi backend trả **gợi ý mẫu** (chế độ demo `Gemini:DemoMode` hoặc fallback khi Gemini 429/quota). Dùng để hiển thị gợi ý vẫn đúng format nhưng biết là không từ AI thật.

## Response lỗi thường gặp

- **401:** Chưa đăng nhập hoặc token hết hạn.
- **403:** User không có role AUTHOR/ADMIN.
- **429:** Vượt giới hạn gọi (số lần/phút hoặc/ngày). Thử lại sau 1 phút hoặc ngày mai.
- **404:** Truyện không tồn tại, không có chương nào, hoặc chưa cấu hình `Gemini:ApiKey`.
- **502:** Lỗi kết nối tới Gemini (mạng, key sai).
- **503:** (Ít gặp nếu đã bật fallback.) Hết hạn mức Gemini – backend có thể **tự trả 200 + gợi ý mẫu** (`isFallbackOrDemo: true`) thay vì 503; xem [AI_COST_OPTIMIZATION.md](AI_COST_OPTIMIZATION.md). Nếu vẫn trả 503: thử lại sau vài phút hoặc bật `Gemini:DemoMode: true` cho ngày bảo vệ.

## Logic backend (tóm tắt)

- Lấy truyện theo `id`, lấy tối đa 5 chương gần nhất (theo `order_index`).
- Mỗi chương chỉ lấy tối đa 1200 ký tự nội dung để giới hạn token.
- Gửi prompt (systemInstruction + contents) tới **Google Gemini API** `generateContent`; yêu cầu trả về JSON `{"suggestions": [{ "title", "description" }, ...]}`.
- Parse JSON và trả về DTO; đồng thời ghi log vào `ai_usage_logs` (nếu có DbContext).
