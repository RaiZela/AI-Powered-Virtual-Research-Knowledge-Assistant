namespace BLL.Helper;

using System.Globalization;
using System.Text;

public static class TextNormalize
{
    public static string NormalizeCommon(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var s = input.Normalize(NormalizationForm.FormC);

        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (char.IsControl(ch) && ch != '\n' && ch != '\t') continue;
            sb.Append(ch);
        }

        var cleaned = sb.ToString()
            .Replace("\r\n", "\n")
            .Replace("\r", "\n");

        cleaned = string.Join("\n",
            cleaned.Split('\n')
                   .Select(line => string.Join(" ", line.Split(' ', StringSplitOptions.RemoveEmptyEntries)).Trim())
        );

        return cleaned.Trim();
    }

    public static string RemoveDiacritics(string input)
    {
        if (string.IsNullOrEmpty(input)) return input;

        var formD = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(formD.Length);

        foreach (var ch in formD)
        {
            var uc = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (uc != UnicodeCategory.NonSpacingMark &&
                uc != UnicodeCategory.SpacingCombiningMark &&
                uc != UnicodeCategory.EnclosingMark)
            {
                sb.Append(ch);
            }
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    public static string NormalizeArabicForSearch(string input)
    {
        var s = NormalizeCommon(input);

        // Remove tatweel
        s = s.Replace("ـ", "");

        // Remove Arabic diacritics (covers many harakat/cantillation)
        s = RemoveDiacritics(s);

        // Optional letter unification (enable if needed)
        s = s.Replace("أ", "ا").Replace("إ", "ا").Replace("آ", "ا");
        s = s.Replace("ى", "ي");

        return s;
    }

    public static string NormalizeHebrewForSearch(string input)
    {
        var s = NormalizeCommon(input);

        // Remove niqqud/cantillation by removing combining marks
        s = RemoveDiacritics(s);

        return s;
    }
}

