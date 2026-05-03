namespace Services;

public sealed class AuthorAiEstimatedTokensInsufficientException : Exception
{
    public long? TokensRemaining { get; }
    public int MinRequiredTokens { get; }

    public AuthorAiEstimatedTokensInsufficientException(long? tokensRemaining, int minRequiredTokens)
        : base(
            $"Số token AI còn lại không đủ để thực hiện yêu cầu này (cần tối thiểu {minRequiredTokens:N0} token). Vui lòng đợi đến kỳ cấp token tiếp theo.")
    {
        TokensRemaining = tokensRemaining;
        MinRequiredTokens = minRequiredTokens;
    }
}
