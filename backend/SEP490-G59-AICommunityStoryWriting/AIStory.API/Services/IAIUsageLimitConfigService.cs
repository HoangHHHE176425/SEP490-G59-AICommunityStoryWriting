namespace AIStory.API.Services;

/// <summary>Cấu hình giới hạn sử dụng AI (số lần/ngày). Đọc/ghi từ ai_configs hoặc config.</summary>
public interface IAIUsageLimitConfigService
{
    /// <summary>Số lần tối đa trong 24h (rolling) theo loại API (suggest vs co-create tách biệt).</summary>
    int GetMaxRequestsPerDay(AiRateLimitKind kind);

    /// <summary>Cập nhật giới hạn (1–100) theo loại. Lưu vào DB.</summary>
    void SetMaxRequestsPerDay(AiRateLimitKind kind, int value, Guid? updatedBy = null);
}
