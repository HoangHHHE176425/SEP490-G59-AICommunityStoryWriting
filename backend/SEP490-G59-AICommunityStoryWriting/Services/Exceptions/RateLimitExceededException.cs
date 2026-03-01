namespace Services.Exceptions;

/// <summary>Ném khi user vượt quá số lần gọi AI cho phép (theo phút hoặc theo ngày).</summary>
public class RateLimitExceededException : InvalidOperationException
{
    public RateLimitExceededException(string message) : base(message) { }
}
