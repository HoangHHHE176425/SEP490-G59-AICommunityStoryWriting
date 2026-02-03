using System;
using System.Collections.Generic;
using BusinessObjects.Entities;
using Microsoft.EntityFrameworkCore;

namespace BusinessObjects.Models;

public partial class StoryPlatformDbContext : DbContext
{
    public StoryPlatformDbContext()
    {
    }

    public StoryPlatformDbContext(DbContextOptions<StoryPlatformDbContext> options)
        : base(options)
    {
    }

    public virtual DbSet<AuthToken> AuthTokens { get; set; }

    public virtual DbSet<Category> Categories { get; set; }

    public virtual DbSet<Chapter> Chapters { get; set; }

    public virtual DbSet<Story> Stories { get; set; }

    public virtual DbSet<User> Users { get; set; }

    public virtual DbSet<UserProfile> UserProfiles { get; set; }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
#warning To protect potentially sensitive information in your connection string, you should move it out of source code. You can avoid scaffolding the connection string by using the Name= syntax to read it from configuration - see https://go.microsoft.com/fwlink/?linkid=2131148. For more guidance on storing connection strings, see https://go.microsoft.com/fwlink/?LinkId=723263.
        => optionsBuilder.UseSqlServer("Server= TRUONG\\HIHITRUONGNE;uid=sa;password=123;database=story_platform;Encrypt=True;TrustServerCertificate=True;");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AuthToken>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__auth_tok__3213E83FEE8E568B");

            entity.ToTable("auth_tokens");

            entity.HasIndex(e => e.RefreshToken, "UQ__auth_tok__7FB69BAD4D98DC7C").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.DeviceInfo).HasColumnName("device_info");
            entity.Property(e => e.ExpiresAt)
                .HasColumnType("datetime")
                .HasColumnName("expires_at");
            entity.Property(e => e.RefreshToken)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("refresh_token");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithMany(p => p.AuthTokens)
                .HasForeignKey(d => d.UserId)
                .HasConstraintName("fk_token_user");
        });

        modelBuilder.Entity<Category>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__categori__3213E83F149446BA");

            entity.ToTable("categories");

            entity.HasIndex(e => e.Slug, "UQ__categori__32DD1E4C4F0423BA").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.IconUrl).HasColumnName("icon_url");
            entity.Property(e => e.IsActive)
                .HasDefaultValue(true)
                .HasColumnName("is_active");
            entity.Property(e => e.Name)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("name");
            entity.Property(e => e.Slug)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("slug");
        });

        modelBuilder.Entity<Chapter>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__chapters__3213E83F92E875B7");

            entity.ToTable("chapters");

            entity.HasIndex(e => new { e.StoryId, e.OrderIndex }, "uq_story_chapter").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AccessType)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("FREE")
                .HasColumnName("access_type");
            entity.Property(e => e.AiContributionRatio)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(5, 2)")
                .HasColumnName("ai_contribution_ratio");
            entity.Property(e => e.CoinPrice)
                .HasDefaultValue(0)
                .HasColumnName("coin_price");
            entity.Property(e => e.Content).HasColumnName("content");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.IsAiClean)
                .HasDefaultValue(false)
                .HasColumnName("is_ai_clean");
            entity.Property(e => e.OrderIndex).HasColumnName("order_index");
            entity.Property(e => e.PublishedAt)
                .HasColumnType("datetime")
                .HasColumnName("published_at");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT")
                .HasColumnName("status");
            entity.Property(e => e.StoryId).HasColumnName("story_id");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("title");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
            entity.Property(e => e.WordCount)
                .HasDefaultValue(0)
                .HasColumnName("word_count");

            entity.HasOne(d => d.Story).WithMany(p => p.Chapters)
                .HasForeignKey(d => d.StoryId)
                .HasConstraintName("fk_chapter_story");
        });

        modelBuilder.Entity<Story>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__stories__3213E83F08E52C3A");

            entity.ToTable("stories");

            entity.HasIndex(e => e.Slug, "UQ__stories__32DD1E4C36990A7A").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AccessType)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("FREE")
                .HasColumnName("access_type");
            entity.Property(e => e.AgeRating)
                .HasMaxLength(10)
                .IsUnicode(false)
                .HasDefaultValue("ALL")
                .HasColumnName("age_rating");
            entity.Property(e => e.AuthorId).HasColumnName("author_id");
            entity.Property(e => e.AvgRating)
                .HasDefaultValue(0m)
                .HasColumnType("decimal(3, 2)")
                .HasColumnName("avg_rating");
            entity.Property(e => e.CategoryId).HasColumnName("category_id");
            entity.Property(e => e.CoverImage).HasColumnName("cover_image");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.ExpectedChapters)
                .HasDefaultValue(0)
                .HasColumnName("expected_chapters");
            entity.Property(e => e.FullStoryPrice)
                .HasDefaultValue(0)
                .HasColumnName("full_story_price");
            entity.Property(e => e.LastPublishedAt)
                .HasColumnType("datetime")
                .HasColumnName("last_published_at");
            entity.Property(e => e.PublishedAt)
                .HasColumnType("datetime")
                .HasColumnName("published_at");
            entity.Property(e => e.ReleaseFrequencyDays)
                .HasDefaultValue(7)
                .HasColumnName("release_frequency_days");
            entity.Property(e => e.SaleType)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("CHAPTER")
                .HasColumnName("sale_type");
            entity.Property(e => e.Slug)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("slug");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("DRAFT")
                .HasColumnName("status");
            entity.Property(e => e.Summary).HasColumnName("summary");
            entity.Property(e => e.Title)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("title");
            entity.Property(e => e.TotalChapters)
                .HasDefaultValue(0)
                .HasColumnName("total_chapters");
            entity.Property(e => e.TotalFavorites)
                .HasDefaultValue(0)
                .HasColumnName("total_favorites");
            entity.Property(e => e.TotalViews)
                .HasDefaultValue(0L)
                .HasColumnName("total_views");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
            entity.Property(e => e.WordCount)
                .HasDefaultValue(0)
                .HasColumnName("word_count");

            entity.HasOne(d => d.Author).WithMany(p => p.Stories)
                .HasForeignKey(d => d.AuthorId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_story_author");

            entity.HasOne(d => d.Category).WithMany(p => p.Stories)
                .HasForeignKey(d => d.CategoryId)
                .OnDelete(DeleteBehavior.SetNull)
                .HasConstraintName("fk_story_category");
        });

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__users__3213E83F9EF0AC26");

            entity.ToTable("users");

            entity.HasIndex(e => e.Email, "UQ__users__AB6E6164ACA5F49D").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("created_at");
            entity.Property(e => e.DeletionRequestedAt)
                .HasColumnType("datetime")
                .HasColumnName("deletion_requested_at");
            entity.Property(e => e.Email)
                .HasMaxLength(255)
                .IsUnicode(false)
                .HasColumnName("email");
            entity.Property(e => e.EmailVerifiedAt)
                .HasColumnType("datetime")
                .HasColumnName("email_verified_at");
            entity.Property(e => e.IsAuthor)
                .HasDefaultValue(false)
                .HasColumnName("is_author");
            entity.Property(e => e.MustResignPolicy)
                .HasDefaultValue(false)
                .HasColumnName("must_resign_policy");
            entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
            entity.Property(e => e.Role)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("USER")
                .HasColumnName("role");
            entity.Property(e => e.Status)
                .HasMaxLength(20)
                .IsUnicode(false)
                .HasDefaultValue("PENDING")
                .HasColumnName("status");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
        });

        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(e => e.Id).HasName("PK__user_pro__3213E83F953B7E54");

            entity.ToTable("user_profiles");

            entity.HasIndex(e => e.Nickname, "UQ__user_pro__5CF1C59BFFFA6742").IsUnique();

            entity.HasIndex(e => e.UserId, "UQ__user_pro__B9BE370E098867B6").IsUnique();

            entity.Property(e => e.Id).HasColumnName("id");
            entity.Property(e => e.AvatarUrl).HasColumnName("avatar_url");
            entity.Property(e => e.Bio).HasColumnName("bio");
            entity.Property(e => e.Description).HasColumnName("description");
            entity.Property(e => e.Nickname)
                .HasMaxLength(100)
                .IsUnicode(false)
                .HasColumnName("nickname");
            entity.Property(e => e.Settings)
                .HasDefaultValue("{\"allow_notif\":true,\"dark_mode\":false}")
                .HasColumnName("settings");
            entity.Property(e => e.SocialLinks)
                .HasDefaultValue("{}")
                .HasColumnName("social_links");
            entity.Property(e => e.UpdatedAt)
                .HasDefaultValueSql("(getdate())")
                .HasColumnType("datetime")
                .HasColumnName("updated_at");
            entity.Property(e => e.UserId).HasColumnName("user_id");

            entity.HasOne(d => d.User).WithOne(p => p.UserProfile)
                .HasForeignKey<UserProfile>(d => d.UserId)
                .HasConstraintName("fk_profile_user");
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
