namespace AIStory.API.Services;

/// <summary>Cấu hình giới hạn sử dụng AI (số lần/ngày). Đọc/ghi từ ai_configs hoặc config.</summary>
public interface IAIUsageLimitConfigService
{
    /// <summary>Số lần tối đa sử dụng AI trong 24h (rolling). Mặc định từ config hoặc 3.</summary>
    int GetMaxRequestsPerDay();

    /// <summary>Cập nhật giới hạn (1–100). Lưu vào DB.</summary>
    void SetMaxRequestsPerDay(int value, Guid? updatedBy = null);
}
