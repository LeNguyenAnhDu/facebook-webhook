using System.Globalization;
using System.Text;

namespace FB.CoreService.Services;

public static class TextNormalization
{
    public static string Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input))
        {
            return string.Empty;
        }

        var decomposed = input.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var character in decomposed)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(character);
            }
        }

        return builder
            .ToString()
            .Replace('đ', 'd')
            .Replace('Đ', 'D')
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormC);
    }
}
