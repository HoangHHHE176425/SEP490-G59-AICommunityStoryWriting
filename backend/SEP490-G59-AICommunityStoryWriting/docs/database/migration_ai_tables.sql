-- ==========================================================
-- MIGRATION: Bổ sung bảng và cột cho module AI
-- (Gợi ý chương tiếp + Đồng sáng tác)
-- Database: SQL Server - story_platform_v13
-- ==========================================================

USE story_platform_v13;
GO

-- ==========================================================
-- 1. CHAPTER_SUMMARIES – Tóm tắt từng chương (Context summarization)
-- Dùng cho: đưa vào prompt gợi ý chương tiếp, không cần gửi full content
-- ==========================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'chapter_summaries')
BEGIN
    CREATE TABLE chapter_summaries (
        id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        chapter_id UNIQUEIDENTIFIER NOT NULL,
        story_id UNIQUEIDENTIFIER NOT NULL,
        summary_text NVARCHAR(MAX) NOT NULL,
        summary_word_count INT NULL,
        created_at DATETIME2 DEFAULT GETDATE(),
        updated_at DATETIME2 DEFAULT GETDATE(),

        CONSTRAINT uq_chapter_summaries_chapter UNIQUE (chapter_id),
        CONSTRAINT fk_chapter_summaries_chapter FOREIGN KEY (chapter_id) REFERENCES chapters(id) ON DELETE CASCADE,
        CONSTRAINT fk_chapter_summaries_story FOREIGN KEY (story_id) REFERENCES stories(id) ON DELETE CASCADE
    );

    CREATE INDEX IX_chapter_summaries_story ON chapter_summaries (story_id);
    PRINT 'Created table: chapter_summaries';
END
GO

-- ==========================================================
-- 2. STORY_AI_SUMMARIES – Tóm tắt tổng + nhân vật (Hierarchical summarization)
-- Một dòng per story: overall summary, main characters (do AI sinh hoặc cập nhật)
-- ==========================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'story_ai_summaries')
BEGIN
    CREATE TABLE story_ai_summaries (
        id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        story_id UNIQUEIDENTIFIER NOT NULL UNIQUE,
        overall_summary NVARCHAR(MAX) NULL,
        main_characters NVARCHAR(MAX) NULL,   -- JSON: [{ "name": "...", "role": "...", "description": "..." }]
        setting_or_world NVARCHAR(MAX) NULL,
        summary_updated_at DATETIME2 NULL,
        created_at DATETIME2 DEFAULT GETDATE(),
        updated_at DATETIME2 DEFAULT GETDATE(),

        CONSTRAINT fk_story_ai_summaries_story FOREIGN KEY (story_id) REFERENCES stories(id) ON DELETE CASCADE
    );

    PRINT 'Created table: story_ai_summaries';
END
GO

-- ==========================================================
-- 3. CHAPTER_METADATA – Structured extraction (nhân vật, sự kiện, địa điểm, mâu thuẫn)
-- LLM trích xuất metadata từ nội dung chương → lưu JSON
-- ==========================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'chapter_metadata')
BEGIN
    CREATE TABLE chapter_metadata (
        id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        chapter_id UNIQUEIDENTIFIER NOT NULL,
        metadata_json NVARCHAR(MAX) NOT NULL,  -- { "characters": [], "locations": [], "events": [], "conflicts": [], "foreshadowing": [] }
        extracted_at DATETIME2 DEFAULT GETDATE(),
        created_at DATETIME2 DEFAULT GETDATE(),

        CONSTRAINT uq_chapter_metadata_chapter UNIQUE (chapter_id),
        CONSTRAINT fk_chapter_metadata_chapter FOREIGN KEY (chapter_id) REFERENCES chapters(id) ON DELETE CASCADE
    );

    CREATE INDEX IX_chapter_metadata_chapter ON chapter_metadata (chapter_id);
    PRINT 'Created table: chapter_metadata';
END
GO

-- ==========================================================
-- 4. STORY_CHUNKS – Chunk + embedding cho RAG (Retrieval-Augmented Generation)
-- Mỗi chunk: một đoạn nội dung + vector embedding (JSON array of floats)
-- SQL Server: lưu embedding dạng NVARCHAR(MAX) JSON; nếu dùng Azure SQL có vector type thì có thể đổi sau
-- ==========================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'story_chunks')
BEGIN
    CREATE TABLE story_chunks (
        id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        story_id UNIQUEIDENTIFIER NOT NULL,
        chapter_id UNIQUEIDENTIFIER NULL,
        chunk_index INT NOT NULL,
        content_text NVARCHAR(MAX) NOT NULL,
        embedding_json NVARCHAR(MAX) NULL,     -- JSON array of floats, e.g. "[0.01, -0.02, ...]"
        token_count_approx INT NULL,
        created_at DATETIME2 DEFAULT GETDATE(),

        CONSTRAINT fk_story_chunks_story FOREIGN KEY (story_id) REFERENCES stories(id) ON DELETE CASCADE,
        CONSTRAINT fk_story_chunks_chapter FOREIGN KEY (chapter_id) REFERENCES chapters(id) ON DELETE SET NULL
    );

    CREATE INDEX IX_story_chunks_story ON story_chunks (story_id);
    CREATE INDEX IX_story_chunks_chapter ON story_chunks (chapter_id);
    PRINT 'Created table: story_chunks';
END
GO

-- ==========================================================
-- 5. AI_CHAPTER_SUGGESTIONS – Lưu gợi ý chương tiếp (để hiển thị lại / lịch sử)
-- Mỗi lần user gọi "gợi ý chương tiếp" → 1 bản ghi (suggestions_json = 3–5 bullet)
-- ==========================================================

IF NOT EXISTS (SELECT * FROM sys.tables WHERE name = 'ai_chapter_suggestions')
BEGIN
    CREATE TABLE ai_chapter_suggestions (
        id UNIQUEIDENTIFIER PRIMARY KEY DEFAULT NEWID(),
        story_id UNIQUEIDENTIFIER NOT NULL,
        user_id UNIQUEIDENTIFIER NOT NULL,
        suggestions_json NVARCHAR(MAX) NOT NULL,  -- [{ "title": "...", "description": "..." }, ...]
        next_chapter_number INT NULL,             -- chương tiếp theo là số mấy
        context_snapshot NVARCHAR(MAX) NULL,      -- tùy chọn: phần tóm tắt đã gửi cho model
        created_at DATETIME2 DEFAULT GETDATE(),

        CONSTRAINT fk_ai_sugg_story FOREIGN KEY (story_id) REFERENCES stories(id) ON DELETE CASCADE,
        CONSTRAINT fk_ai_sugg_user FOREIGN KEY (user_id) REFERENCES users(id)
    );

    CREATE INDEX IX_ai_chapter_suggestions_story ON ai_chapter_suggestions (story_id);
    CREATE INDEX IX_ai_chapter_suggestions_user ON ai_chapter_suggestions (user_id);
    CREATE INDEX IX_ai_chapter_suggestions_created ON ai_chapter_suggestions (created_at DESC);
    PRINT 'Created table: ai_chapter_suggestions';
END
GO

-- ==========================================================
-- 6. ALTER ai_generated_content – Phân loại tính năng AI (continuation, rewrite, dialogue...)
-- ==========================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.columns
    WHERE object_id = OBJECT_ID('ai_generated_content') AND name = 'feature_type'
)
BEGIN
    ALTER TABLE ai_generated_content
    ADD feature_type NVARCHAR(50) NULL;
    PRINT 'Added column ai_generated_content.feature_type';
END
GO

-- Gợi ý giá trị: CONTINUATION, REWRITE, INLINE_SUGGESTION, DIALOGUE, NEXT_CHAPTER_OUTLINE, OTHER

-- ==========================================================
-- 7. ALTER ai_generated_content – FK user_id (nếu chưa có)
-- ==========================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID('ai_generated_content')
      AND name LIKE '%user%'
)
BEGIN
    ALTER TABLE ai_generated_content
    ADD CONSTRAINT fk_aigen_user FOREIGN KEY (user_id) REFERENCES users(id);
    PRINT 'Added FK ai_generated_content -> users';
END
GO

-- ==========================================================
-- 8. ALTER ai_usage_logs – FK story_id, chapter_id (referential integrity)
-- ==========================================================

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID('ai_usage_logs')
      AND name = 'fk_ai_usage_story'
)
BEGIN
    ALTER TABLE ai_usage_logs
    ADD CONSTRAINT fk_ai_usage_story FOREIGN KEY (story_id) REFERENCES stories(id) ON DELETE SET NULL;
    PRINT 'Added FK ai_usage_logs -> stories';
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.foreign_keys
    WHERE parent_object_id = OBJECT_ID('ai_usage_logs')
      AND name = 'fk_ai_usage_chapter'
)
BEGIN
    ALTER TABLE ai_usage_logs
    ADD CONSTRAINT fk_ai_usage_chapter FOREIGN KEY (chapter_id) REFERENCES chapters(id) ON DELETE SET NULL;
    PRINT 'Added FK ai_usage_logs -> chapters';
END
GO

-- ==========================================================
-- 9. Index cho ai_usage_logs (query theo story/user)
-- ==========================================================

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ai_usage_logs_story' AND object_id = OBJECT_ID('ai_usage_logs'))
    CREATE INDEX IX_ai_usage_logs_story ON ai_usage_logs (story_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ai_usage_logs_chapter' AND object_id = OBJECT_ID('ai_usage_logs'))
    CREATE INDEX IX_ai_usage_logs_chapter ON ai_usage_logs (chapter_id);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ai_usage_logs_created' AND object_id = OBJECT_ID('ai_usage_logs'))
    CREATE INDEX IX_ai_usage_logs_created ON ai_usage_logs (created_at DESC);
GO

PRINT 'Migration AI tables completed.';
