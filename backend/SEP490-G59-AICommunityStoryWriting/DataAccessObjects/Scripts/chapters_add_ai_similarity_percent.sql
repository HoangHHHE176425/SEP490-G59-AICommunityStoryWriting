-- Thêm cột ai_similarity_percent vào bảng chapters.
-- Lưu phần trăm giống nhau (0–100) giữa nội dung chương và bản AI; cập nhật khi chương đã PUBLISHED và gọi API compare-chapter.

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('chapters') AND name = 'ai_similarity_percent'
)
BEGIN
    ALTER TABLE chapters ADD ai_similarity_percent DECIMAL(5, 2) NULL;
END
GO
