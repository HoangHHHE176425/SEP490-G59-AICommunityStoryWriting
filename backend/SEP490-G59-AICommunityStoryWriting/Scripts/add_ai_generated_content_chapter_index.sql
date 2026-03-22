-- Thêm chapter_index: thứ tự chương dự kiến (khớp chapters.order_index). Chạy trên SQL Server trước khi deploy code mới.
IF COL_LENGTH('dbo.ai_generated_content', 'chapter_index') IS NULL
BEGIN
    ALTER TABLE dbo.ai_generated_content ADD chapter_index INT NULL;
END
GO

-- (Tùy chọn) index cho truy vấn theo truyện + thứ tự chương
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ai_generated_content_story_chapter_index' AND object_id = OBJECT_ID('dbo.ai_generated_content'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_ai_generated_content_story_chapter_index
    ON dbo.ai_generated_content (story_id, chapter_index)
    WHERE chapter_index IS NOT NULL;
END
GO
