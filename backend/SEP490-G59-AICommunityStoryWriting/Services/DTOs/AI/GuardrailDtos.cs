namespace Services.DTOs.AI;

/// <summary>Kết quả guardrail: đạt hay có vi phạm (từ cấm).</summary>
public class GuardrailResult
{
    public bool Passed { get; set; }
    public List<GuardrailViolation> Violations { get; set; } = new();
}

/// <summary>Một vi phạm: loại + mô tả + (tùy chọn) đoạn trích.</summary>
public class GuardrailViolation
{
    public string Type { get; set; } = null!; // BannedWord
    public string Message { get; set; } = null!;
    public string? Quote { get; set; }
}
