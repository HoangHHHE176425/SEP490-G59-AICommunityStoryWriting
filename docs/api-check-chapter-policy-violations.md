# POST /api/ai/check-chapter – Chỉ kiểm tra chính tả và từ cấm

API **POST /api/ai/check-chapter** chỉ thực hiện **hai loại kiểm tra**:

1. **Chính tả** (AI)
2. **Từ cấm** (BannedWords – config)

Không kiểm tra vi phạm chính sách hay nội dung không phù hợp; `hasInappropriateContent` luôn trả về `false`.

---

## 1. Chính tả (AI)

- **Nguồn:** Model AI (Ollama/Groq/OpenAI theo **AI:Provider**, **AI:Model**) đọc nội dung chương và trả về JSON: `spellingErrors` (từ/cụm sai + gợi ý sửa) và `summary`.
- **Cách biết:** AI so sánh với chuẩn chính tả tiếng Việt/Anh và liệt kê lỗi trong `spellingIssues`.

---

## 2. Từ cấm (BannedWords) – `policyViolations`

- **Nguồn:** Cấu hình **ContentGuardrail:BannedWords** hoặc **AI:CoCreateBannedWords** trong `appsettings.json` / `appsettings.Development.json`.
- **Cách biết vi phạm:** Nếu nội dung chương **chứa** bất kỳ từ nào trong danh sách (không phân biệt hoa thường), hệ thống thêm vào `policyViolations` với type **BannedWord**.
- **Ví dụ config:**
  ```json
  "ContentGuardrail": {
    "BannedWords": "từ_cấm_1, từ_cấm_2, từ_cấm_3"
  }
  ```

---

## Tóm tắt

| Kiểm tra   | Nguồn        | Kết quả trả về        |
|------------|--------------|------------------------|
| Chính tả   | AI (LLM)     | `spellingIssues`, `summary` |
| Từ cấm     | Config       | `policyViolations` (type BannedWord) |

**Passed = true** khi không có lỗi chính tả và không có từ cấm.
