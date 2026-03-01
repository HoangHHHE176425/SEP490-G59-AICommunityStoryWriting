# Demo data & JSON mẫu để test API

## 1. Chạy seed dữ liệu demo

Trong **SQL Server Management Studio** (hoặc sqlcmd), mở và chạy:

```
docs/database/seed_demo_data.sql
```

Sẽ tạo:

- **User (AUTHOR):** `author@demo.com` / mật khẩu `Author123!`
- **1 truyện:** "Đấu Phá Thương Khung – Bản Demo" với **3 chương** có nội dung
- **StoryId cố định:** `B3333333-3333-3333-3333-333333333301` (dùng cho API gợi ý chương tiếp)

---

## 2. JSON mẫu để test

### 2.1. Đăng nhập (lấy JWT)

**Request:** `POST /api/auth/login`  
**Body:** dùng file `login-request.json` hoặc:

```json
{
  "email": "author@demo.com",
  "password": "Author123!"
}
```

**Response mẫu:** `login-response-sample.json`

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9..."
}
```

Copy `accessToken` → trong Swagger bấm **Authorize**, dán token (có thể thêm prefix `Bearer ` nếu UI yêu cầu).

---

### 2.2. AI gợi ý chương tiếp theo

**Request:** `POST /api/stories/{id}/suggest-next-chapter`  
- **Method:** POST  
- **Path:** `id` = StoryId (Guid của truyện demo)  
- **Body:** không cần (không có request body)

**StoryId demo (sau khi chạy seed):**

```
B3333333-3333-3333-3333-333333333301
```

Ví dụ URL đầy đủ (thay `{baseUrl}` bằng URL API của bạn, ví dụ `https://localhost:7000`):

```
POST {baseUrl}/api/stories/B3333333-3333-3333-3333-333333333301/suggest-next-chapter
Authorization: Bearer <accessToken>
```

**Response mẫu:** xem `suggest-next-chapter-response-sample.json` (kết quả thực tế sẽ do Gemini API trả về, có thể khác một chút).

---

## 3. Thứ tự test nhanh (Swagger)

1. Chạy seed `seed_demo_data.sql`.
2. Cấu hình `Gemini:ApiKey` trong `appsettings.Development.json`.
3. Chạy API: `dotnet run --project AIStory.API`.
4. Mở Swagger UI.
5. **POST /api/auth/login** → Body: `login-request.json` → Execute → copy `accessToken`.
6. Authorize: dán token.
7. **POST /api/stories/B3333333-3333-3333-3333-333333333301/suggest-next-chapter** → Execute.

Nếu thành công, response sẽ có `suggestions` (3–5 gợi ý cho chương 4).
