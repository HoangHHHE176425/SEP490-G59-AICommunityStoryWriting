namespace Repositories;

/// <summary>Cấu hình AI (bảng ai_configs).</summary>
public interface IAiConfigsRepository
{
    string? GetValue(string key);
    void Upsert(string key, string value, string? description = null);
}
