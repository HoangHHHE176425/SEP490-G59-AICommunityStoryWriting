# Bảng database cho module AI

Script: `migration_ai_tables.sql` (chạy trên SQL Server, database `story_platform_v13`).

## Bảng đã có sẵn (trong db.txt) – vẫn dùng

| Bảng | Mục đích |
|------|----------|
| `ai_configs` | Cấu hình key/value cho AI |
| `ai_usage_logs` | Log mỗi lần gọi AI (user, story, chapter, action_type, tokens, status) |
| `ai_generated_content` | Nội dung do AI sinh (chapter_id, user_id, input_prompt, ai_output) |
| `ai_sensitive_words` | Từ nhạy cảm |
| `ai_plagiarism_reports` | Báo cáo đạo văn theo chương |
| `ai_model_registry` | Đăng ký model (provider, model_name, default cho gen/mod) |

## Bảng mới thêm (migration_ai_tables.sql)

| Bảng | Mục đích | Ứng với tài liệu AI |
|------|----------|---------------------|
| **chapter_summaries** | Tóm tắt từng chương (1 row/chapter). Cập nhật khi nội dung chương đổi. | §1.1 Context summarization |
| **story_ai_summaries** | Tóm tắt tổng + nhân vật chính (1 row/story). Do AI tạo/cập nhật. | §1.1 Hierarchical summarization |
| **chapter_metadata** | Metadata có cấu trúc (nhân vật, địa điểm, sự kiện, mâu thuẫn) dạng JSON theo chương. | §1.3 Structured extraction |
| **story_chunks** | Chunk nội dung + embedding (JSON) cho RAG. | §1.2 RAG |
| **ai_chapter_suggestions** | Lưu gợi ý chương tiếp (suggestions_json) mỗi lần user gọi tính năng. | §1.4 Gợi ý chương tiếp |

## Cột mới

- **ai_generated_content.feature_type** – Phân loại: `CONTINUATION`, `REWRITE`, `INLINE_SUGGESTION`, `DIALOGUE`, `NEXT_CHAPTER_OUTLINE`, `OTHER`.

## FK bổ sung

- `ai_generated_content.user_id` → `users(id)`
- `ai_usage_logs.story_id` → `stories(id)` ON DELETE SET NULL
- `ai_usage_logs.chapter_id` → `chapters(id)` ON DELETE SET NULL

## Cách chạy migration

```sql
-- Trong SSMS hoặc sqlcmd, chạy file:
-- docs/database/migration_ai_tables.sql
-- (sau khi đã có database story_platform_v13 và các bảng gốc từ db.txt)
```

Script dùng `IF NOT EXISTS` nên chạy nhiều lần an toàn (idempotent).
