-- Thêm cột story_id vào ai_generated_content (bản nháp AI thuộc truyện nào).
-- Chạy script này trên database trước khi dùng co-create chỉ lưu vào ai_generated_content.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('ai_generated_content') AND name = 'story_id'
)
BEGIN
    ALTER TABLE ai_generated_content ADD story_id UNIQUEIDENTIFIER NULL;
END
GO

-- Tùy chọn: xóa cột similarity_score nếu không dùng (tính on-the-fly trong API compare-chapter).
-- Nếu giữ lại cột thì bỏ comment 2 dòng dưới trong entity ai_generated_content và thêm lại property similarity_score.
-- IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('ai_generated_content') AND name = 'similarity_score')
--     ALTER TABLE ai_generated_content DROP COLUMN similarity_score;
-- GO
