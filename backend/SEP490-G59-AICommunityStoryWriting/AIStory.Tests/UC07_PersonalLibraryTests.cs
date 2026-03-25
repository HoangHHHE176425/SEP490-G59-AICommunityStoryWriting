using AIStory.API.Controllers;
using BusinessObjects;
using BusinessObjects.Entities;
using DataAccessObjects.DAOs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Services.DTOs.Account;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Xunit;

namespace AIStory.Tests;

public class UC07_PersonalLibraryTests
{
    private static LibraryController CreateControllerWithUser(Guid userId)
    {
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, userId.ToString()) };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);
        return new LibraryController
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext { User = principal } }
        };
    }

    private static MyLibraryResponseDto AssertOkLibrary(IActionResult result)
    {
        var ok = Assert.IsType<OkObjectResult>(result);
        return Assert.IsType<MyLibraryResponseDto>(ok.Value);
    }

    [Fact]
    public void ReadPersonalLibrary_MissingSubClaim_Throws()
    {
        var controller = new LibraryController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
            }
        };

        Assert.Throws<InvalidOperationException>(() => controller.GetMyLibrary());
    }

    [Fact]
    public void ReadPersonalLibrary_UnparsableSubClaim_Throws()
    {
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, "not-a-guid") };
        var controller = new LibraryController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test")) }
            }
        };

        Assert.Throws<InvalidOperationException>(() => controller.GetMyLibrary());
    }

    [Fact]
    public void ReadPersonalLibrary_NoLibraryData_ReturnsOkWithEmptyLists()
    {
        var userId = Guid.NewGuid();
        var dto = AssertOkLibrary(CreateControllerWithUser(userId).GetMyLibrary());

        Assert.NotNull(dto.FollowedStories);
        Assert.NotNull(dto.FollowedAuthors);
        Assert.NotNull(dto.ReadingHistory);
        Assert.Empty(dto.FollowedStories);
        Assert.Empty(dto.FollowedAuthors);
        Assert.Empty(dto.ReadingHistory);
    }

    [Fact]
    public void ReadPersonalLibrary_FollowedPublishedStory_AppearsInFollowedStories()
    {
        var readerId = Guid.NewGuid();
        var storyId = Guid.NewGuid();

        try
        {
            InsertUser(readerId);
            InsertPublishedStory(storyId);
            InsertUserLibraryFollow(readerId, storyId);

            var dto = AssertOkLibrary(CreateControllerWithUser(readerId).GetMyLibrary());
            var item = Assert.Single(dto.FollowedStories);
            Assert.Equal(storyId, item.Id);
            Assert.Equal("PUBLISHED", item.Status);
        }
        finally
        {
            RemoveUserLibrary(readerId, storyId, UserLibraryDAO.RelationTypeFollow);
            DeleteStoryIfExists(storyId);
            DeleteUserIfExists(readerId);
        }
    }

    [Fact]
    public void ReadPersonalLibrary_FollowedDraftStory_ExcludedFromFollowedStories()
    {
        var readerId = Guid.NewGuid();
        var storyId = Guid.NewGuid();

        try
        {
            InsertUser(readerId);
            InsertDraftStory(storyId);
            InsertUserLibraryFollow(readerId, storyId);

            var dto = AssertOkLibrary(CreateControllerWithUser(readerId).GetMyLibrary());
            Assert.DoesNotContain(dto.FollowedStories, x => x.Id == storyId);
        }
        finally
        {
            RemoveUserLibrary(readerId, storyId, UserLibraryDAO.RelationTypeFollow);
            DeleteStoryIfExists(storyId);
            DeleteUserIfExists(readerId);
        }
    }

    [Fact]
    public void ReadPersonalLibrary_ReadingHistory_IncludesStoryAndChapter()
    {
        var readerId = Guid.NewGuid();
        var storyId = Guid.NewGuid();
        var chapterId = Guid.NewGuid();
        var lastRead = DateTime.UtcNow.AddMinutes(-5);

        try
        {
            InsertUser(readerId);
            InsertPublishedStory(storyId, title: "UT Lib Story");
            InsertChapter(chapterId, storyId, "UT Chapter One", orderIndex: 1);
            InsertUserLibraryReading(readerId, storyId, chapterId, lastRead);

            var dto = AssertOkLibrary(CreateControllerWithUser(readerId).GetMyLibrary());
            var row = Assert.Single(dto.ReadingHistory);
            Assert.Equal(storyId, row.StoryId);
            Assert.Equal("UT Lib Story", row.StoryTitle);
            Assert.Equal(chapterId, row.LastReadChapterId);
            Assert.Equal("UT Chapter One", row.LastReadChapterTitle);
            Assert.Equal(1, row.LastReadChapterOrder);
        }
        finally
        {
            RemoveUserLibrary(readerId, storyId, UserLibraryDAO.RelationTypeReading);
            DeleteChapterIfExists(chapterId);
            DeleteStoryIfExists(storyId);
            DeleteUserIfExists(readerId);
        }
    }

    [Fact]
    public void ReadPersonalLibrary_FollowedAuthor_AppearsInFollowedAuthors()
    {
        var readerId = Guid.NewGuid();
        var authorId = Guid.NewGuid();

        try
        {
            InsertUser(readerId);
            InsertUser(authorId, emailSuffix: "author");
            InsertFollow(readerId, authorId);

            var dto = AssertOkLibrary(CreateControllerWithUser(readerId).GetMyLibrary());
            var item = Assert.Single(dto.FollowedAuthors);
            Assert.Equal(authorId, item.AuthorId);
            Assert.False(string.IsNullOrWhiteSpace(item.AuthorName));
        }
        finally
        {
            RemoveFollow(readerId, authorId);
            DeleteUserIfExists(readerId);
            DeleteUserIfExists(authorId);
        }
    }

    private static void InsertUser(Guid id, string emailSuffix = "reader")
    {
        using var ctx = new StoryPlatformDbContext();
        ctx.users.Add(new users
        {
            id = id,
            email = $"ut-lib-{emailSuffix}-{id:N}@x.test",
            password_hash = "x",
            status = "ACTIVE",
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow
        });
        ctx.SaveChanges();
    }

    private static void InsertPublishedStory(Guid storyId, string? title = null)
    {
        var suffix = storyId.ToString("N")[..12];
        StoryDAO.Add(new stories
        {
            id = storyId,
            author_id = null,
            title = title ?? "UT Published",
            slug = "ut-lib-pub-" + suffix,
            status = "PUBLISHED",
            story_progress_status = "ONGOING",
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow
        });
    }

    private static void InsertDraftStory(Guid storyId)
    {
        var suffix = storyId.ToString("N")[..12];
        StoryDAO.Add(new stories
        {
            id = storyId,
            author_id = null,
            title = "UT Draft",
            slug = "ut-lib-dr-" + suffix,
            status = "DRAFT",
            story_progress_status = "ONGOING",
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow
        });
    }

    private static void InsertChapter(Guid chapterId, Guid storyId, string title, int orderIndex)
    {
        using var ctx = new StoryPlatformDbContext();
        ctx.chapters.Add(new chapters
        {
            id = chapterId,
            story_id = storyId,
            title = title,
            order_index = orderIndex,
            status = "PUBLISHED",
            access_type = "FREE",
            coin_price = 0,
            created_at = DateTime.UtcNow,
            updated_at = DateTime.UtcNow
        });
        ctx.SaveChanges();
    }

    private static void InsertUserLibraryFollow(Guid userId, Guid storyId)
    {
        using var ctx = new StoryPlatformDbContext();
        ctx.user_library.Add(new user_library
        {
            user_id = userId,
            story_id = storyId,
            relation_type = UserLibraryDAO.RelationTypeFollow
        });
        ctx.SaveChanges();
    }

    private static void InsertUserLibraryReading(Guid userId, Guid storyId, Guid chapterId, DateTime lastReadAt)
    {
        using var ctx = new StoryPlatformDbContext();
        ctx.user_library.Add(new user_library
        {
            user_id = userId,
            story_id = storyId,
            relation_type = UserLibraryDAO.RelationTypeReading,
            last_read_chapter_id = chapterId,
            last_read_at = lastReadAt
        });
        ctx.SaveChanges();
    }

    private static void InsertFollow(Guid readerId, Guid authorId)
    {
        using var ctx = new StoryPlatformDbContext();
        ctx.follows.Add(new follows
        {
            user_id = readerId,
            author_id = authorId,
            followed_at = DateTime.UtcNow
        });
        ctx.SaveChanges();
    }

    private static void RemoveUserLibrary(Guid userId, Guid storyId, string relationType)
    {
        using var ctx = new StoryPlatformDbContext();
        var row = ctx.user_library.FirstOrDefault(l =>
            l.user_id == userId && l.story_id == storyId && l.relation_type == relationType);
        if (row != null)
        {
            ctx.user_library.Remove(row);
            ctx.SaveChanges();
        }
    }

    private static void RemoveFollow(Guid readerId, Guid authorId)
    {
        using var ctx = new StoryPlatformDbContext();
        var row = ctx.follows.FirstOrDefault(f => f.user_id == readerId && f.author_id == authorId);
        if (row != null)
        {
            ctx.follows.Remove(row);
            ctx.SaveChanges();
        }
    }

    private static void DeleteChapterIfExists(Guid chapterId)
    {
        using var ctx = new StoryPlatformDbContext();
        var row = ctx.chapters.FirstOrDefault(c => c.id == chapterId);
        if (row == null)
            return;
        ctx.chapters.Remove(row);
        ctx.SaveChanges();
    }

    private static void DeleteStoryIfExists(Guid storyId)
    {
        using var ctx = new StoryPlatformDbContext();
        var row = ctx.stories.FirstOrDefault(s => s.id == storyId);
        if (row == null)
            return;
        ctx.stories.Remove(row);
        ctx.SaveChanges();
    }

    private static void DeleteUserIfExists(Guid userId)
    {
        using var ctx = new StoryPlatformDbContext();
        var row = ctx.users.FirstOrDefault(u => u.id == userId);
        if (row == null)
            return;
        ctx.users.Remove(row);
        ctx.SaveChanges();
    }
}
