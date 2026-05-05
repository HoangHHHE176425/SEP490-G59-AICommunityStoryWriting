using System.Text;

namespace Services.Helpers;

public static class CoCreateAuthorIdeaLengthHelper
{
    public const int MaxDisplayLength = 6000;

    public static int GetDisplayLength(string? s)
    {
        if (string.IsNullOrEmpty(s)) return 0;
        return s.Normalize(NormalizationForm.FormD).Length;
    }
}
