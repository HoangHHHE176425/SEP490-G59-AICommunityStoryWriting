namespace Services.Helpers;

public static class AiMinRequiredTokensResolver
{
    public static int ResolveMinRequiredTokens(
        bool useHistory,
        int? maxTotalTokensFromHistory,
        int fallbackMinTokens,
        int historyBufferTokens)
    {
        if (!useHistory)
            return fallbackMinTokens;
        var buf = System.Math.Max(0, historyBufferTokens);
        if (maxTotalTokensFromHistory is { } hm && hm > 0)
        {
            var sum = (long)hm + buf;
            return (int)System.Math.Min(int.MaxValue, sum);
        }
        return fallbackMinTokens;
    }

    public static int ResolveCoCreateMinRequiredFromHistoryStepSum(
        bool useHistory,
        int historyStepSum,
        int fallbackMinTokens,
        int historyBufferTokens)
    {
        if (!useHistory)
            return fallbackMinTokens;
        var buf = System.Math.Max(0, historyBufferTokens);
        if (historyStepSum > 0)
        {
            var t = (long)historyStepSum + buf;
            return (int)System.Math.Min(t, int.MaxValue);
        }
        return fallbackMinTokens;
    }
}
