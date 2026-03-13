using BusinessObjects;
using BusinessObjects.Entities;

namespace DataAccessObjects.DAOs;

/// <summary>Cấu hình AI (key-value). Bảng ai_configs.</summary>
public static class AiConfigsDAO
{
    public static string? GetValue(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        using var context = new StoryPlatformDbContext();
        var row = context.ai_configs.FirstOrDefault(c => c.key == key);
        return row?.value;
    }

    public static void Upsert(string key, string value, string? description = null)
    {
        if (string.IsNullOrWhiteSpace(key)) return;
        if (key.Length > 50) key = key[..50];
        using var context = new StoryPlatformDbContext();
        var row = context.ai_configs.FirstOrDefault(c => c.key == key);
        var now = DateTime.UtcNow;
        if (row != null)
        {
            row.value = value ?? "";
            row.updated_at = now;
            if (description != null) row.description = description;
            context.SaveChanges();
        }
        else
        {
            context.ai_configs.Add(new ai_configs
            {
                key = key.Trim(),
                value = value ?? "",
                description = description,
                updated_at = now
            });
            context.SaveChanges();
        }
    }
}
