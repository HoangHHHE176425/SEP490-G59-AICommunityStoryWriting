-- ==========================================================
-- SEED: Dữ liệu demo để test API (Login + AI gợi ý chương tiếp)
-- Database: SQL Server - story_platform_v13
-- Chạy sau khi đã có schema từ db.txt (và migration_ai_tables.sql nếu dùng)
-- ==========================================================

USE story_platform_v13;
GO

-- ==========================================================
-- 1. USER (AUTHOR) – dùng để đăng nhập và test suggest-next-chapter
-- Email: author@demo.com  |  Mật khẩu: Author123!
-- ==========================================================

DECLARE @AuthorId UNIQUEIDENTIFIER = 'A1111111-1111-1111-1111-111111111101';

IF NOT EXISTS (SELECT 1 FROM users WHERE id = @AuthorId)
BEGIN
    INSERT INTO users (id, email, password_hash, [role], [status], email_verified_at, created_at, updated_at)
    VALUES (
        @AuthorId,
        N'author@demo.com',
        N'$2a$11$E7QLWQS1mIjALFp6qpTdAeMYoD.gJw/aCfIKriTprLN8pMblSXQbW',  -- BCrypt hash của "Author123!"
        N'AUTHOR',
        N'ACTIVE',
        GETDATE(),
        GETDATE(),
        GETDATE()
    );
    PRINT 'Inserted demo user: author@demo.com';
END
ELSE
    PRINT 'Demo user already exists.';

-- Profile + Wallet cho author
IF NOT EXISTS (SELECT 1 FROM user_profiles WHERE user_id = @AuthorId)
    INSERT INTO user_profiles (user_id, nickname, updated_at)
    VALUES (@AuthorId, N'Demo Author', GETDATE());

IF NOT EXISTS (SELECT 1 FROM wallets WHERE user_id = @AuthorId)
    INSERT INTO wallets (user_id, balance_coin, income_balance, frozen_balance, currency, updated_at)
    VALUES (@AuthorId, 100, 0, 0, N'VND', GETDATE());

GO

-- ==========================================================
-- 2. CATEGORY – lấy hoặc tạo 1 thể loại (slug tien-hiep)
-- ==========================================================

DECLARE @CatId UNIQUEIDENTIFIER;

SELECT @CatId = id FROM categories WHERE slug = N'tien-hiep';

IF @CatId IS NULL
BEGIN
    SET @CatId = 'C2222222-2222-2222-2222-222222222201';
    INSERT INTO categories (id, [name], slug, description, is_active, created_at)
    VALUES (@CatId, N'Tiên hiệp', N'tien-hiep', N'Truyện tiên hiệp, tu tiên', 1, GETDATE());
    PRINT 'Inserted category: Tiên hiệp';
END
ELSE
    PRINT 'Using existing category: Tiên hiệp';

-- ==========================================================
-- 3. STORY – 1 truyện có tóm tắt và vài chương (cho AI đọc)
-- StoryId dùng ký tự hex (0-9, A-F) để SQL Server parse đúng UNIQUEIDENTIFIER
-- ==========================================================

DECLARE @AuthorId UNIQUEIDENTIFIER = 'A1111111-1111-1111-1111-111111111101';
DECLARE @StoryId UNIQUEIDENTIFIER = 'B3333333-3333-3333-3333-333333333301';

IF NOT EXISTS (SELECT 1 FROM stories WHERE id = @StoryId)
BEGIN
    INSERT INTO stories (id, author_id, title, slug, summary, [status], story_progress_status, total_chapters, word_count, age_rating, created_at, updated_at)
    VALUES (
        @StoryId,
        @AuthorId,
        N'Đấu Phá Thương Khung – Bản Demo',
        N'dau-pha-thuong-khung-demo',
        N'Tiểu thuyết kể về Tiêu Viêm, từ thiếu niên bị mất tu vi trở thành cao thủ. Trên con đường tìm lại sức mạnh, chàng gặp nhiều kỳ duyên và đối thủ. Demo này dùng để test API gợi ý chương tiếp.',
        N'PUBLISHED',
        N'ONGOING',
        3,
        0,
        N'ALL',
        GETDATE(),
        GETDATE()
    );

    INSERT INTO story_categories (story_id, category_id)
    VALUES (@StoryId, @CatId);

    PRINT 'Inserted demo story. StoryId (dùng trong API): ' + CAST(@StoryId AS NVARCHAR(50));
END

GO

-- ==========================================================
-- 4. CHAPTERS – 3 chương có nội dung (AI sẽ đọc để gợi ý chương 4)
-- ==========================================================

DECLARE @StoryId UNIQUEIDENTIFIER = 'B3333333-3333-3333-3333-333333333301';

IF NOT EXISTS (SELECT 1 FROM chapters WHERE story_id = @StoryId)
BEGIN
    INSERT INTO chapters (id, story_id, title, order_index, content, [status], access_type, word_count, created_at, updated_at)
    VALUES
    (
        NEWID(),
        @StoryId,
        N'Chương 1: Từ thiên tài rơi xuống',
        1,
        N'Tiêu Viêm từng là thiên tài trẻ của tộc Tiêu, nhưng ba năm qua tu vi của chàng không ngừng rớt xuống. Hiện tại chàng chỉ còn lại ba đạo Đấu Chi khí. Trên lễ thành niên, chàng bị chà đạp và hứa sẽ ly hôn với Nạp Lan Yên – người từng gắn bó với chàng từ nhỏ. Sau buổi lễ, một âm thanh bí ẩn vang lên trong đầu chàng: di vật của mẹ chàng – chiếc hắc giới – có thể chứa cơ duyên mới.',
        N'PUBLISHED',
        N'FREE',
        120,
        GETDATE(),
        GETDATE()
    ),
    (
        NEWID(),
        @StoryId,
        N'Chương 2: Dược lão trong hắc giới',
        2,
        N'Tiêu Viêm phát hiện trong hắc giới có một linh thể của Dược Trần – một luyện dược tôn giả. Dược lão đề nghị giúp chàng khôi phục tu vi với điều kiện chàng phải tìm đủ các linh dược và vật liệu để Dược lão tái tạo thân thể. Tiêu Viêm đồng ý. Từ đó chàng vừa tu luyện vừa săn tìm dược liệu, đồng thời phải đối mặt với sự khinh thường của tộc nhân và Nạp Lan tộc.',
        N'PUBLISHED',
        N'FREE',
        115,
        GETDATE(),
        GETDATE()
    ),
    (
        NEWID(),
        @StoryId,
        N'Chương 3: Thử thách ở Vân Lan tông',
        3,
        N'Tiêu Viêm quyết tâm lên đường tìm Nạp Lan Yên tại Vân Lan tông để chính thức ly hôn và lấy lại danh dự. Trên đường đi chàng gặp nhiều ma thú và đối thủ. Tu vi dần hồi phục nhờ sự trợ giúp của Dược lão. Chàng cũng biết được Nạp Lan Yên đã trở thành đồ đệ của Vân Chi Vân – tông chủ Vân Lan tông. Cuối chương, chàng đứng trước cổng Vân Lan tông, chuẩn bị bước vào thử thách lớn.',
        N'PUBLISHED',
        N'FREE',
        130,
        GETDATE(),
        GETDATE()
    );

    UPDATE stories SET total_chapters = 3, word_count = 365 WHERE id = @StoryId;
    PRINT 'Inserted 3 demo chapters for story.';
END

GO

-- In ra StoryId để copy vào request test
SELECT N'Demo StoryId (dùng cho POST /api/stories/{id}/suggest-next-chapter):' AS [Info], CAST('B3333333-3333-3333-3333-333333333301' AS NVARCHAR(50)) AS StoryId;
SELECT N'Demo login: author@demo.com / Author123!' AS [Info];
