using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace StudioCore.Services;

internal static partial class AlbumText
{
    [GeneratedRegex("[^a-z0-9]+", RegexOptions.Compiled)]
    private static partial Regex SlugSeparator();

    [GeneratedRegex(@"\s+", RegexOptions.Compiled)]
    private static partial Regex Whitespace();

    public static string Slugify(string? value, int maxLength = 44)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "album";
        }

        var normalized = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            var lower = char.ToLowerInvariant(character);
            builder.Append(lower == 'đ' ? 'd' : lower);
        }

        var slug = SlugSeparator().Replace(builder.ToString(), "-").Trim('-');
        if (slug.Length > maxLength)
        {
            slug = slug[..maxLength].Trim('-');
        }

        return string.IsNullOrWhiteSpace(slug) ? "album" : slug;
    }

    public static string NewShareToken(string title, Func<string, bool> alreadyExists)
    {
        var prefix = Slugify(title);
        for (var attempt = 0; attempt < 10; attempt++)
        {
            var suffix = Convert.ToHexString(RandomNumberGenerator.GetBytes(4)).ToLowerInvariant();
            var token = $"{prefix}-{suffix}";
            if (!alreadyExists(token))
            {
                return token;
            }
        }

        return $"{prefix}-{Guid.NewGuid():N}"[..Math.Min(60, prefix.Length + 33)];
    }

    public static string CleanCode(string fileName)
    {
        var name = Path.GetFileNameWithoutExtension(fileName).Trim();
        name = Whitespace().Replace(name, " ");
        if (name.Length > 42)
        {
            name = name[..42].Trim();
        }

        return string.IsNullOrWhiteSpace(name) ? "ANH" : name;
    }

    public static List<string> CreateUniqueCodes(IEnumerable<string> fileNames)
    {
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var codes = new List<string>();
        foreach (var fileName in fileNames)
        {
            var baseCode = CleanCode(fileName);
            var code = baseCode;
            var counter = 2;
            while (!used.Add(code))
            {
                code = $"{baseCode}-{counter:00}";
                counter++;
            }

            codes.Add(code);
        }

        return codes;
    }

    public static string NormalizeEventDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ||
            DateTime.TryParse(value, CultureInfo.GetCultureInfo("vi-VN"), DateTimeStyles.None, out parsed))
        {
            return parsed.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        return "";
    }

    public static string NormalizeOptionalDate(DateTimeOffset? date, string fallback = "") =>
        date?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? fallback;

    public static string TrimTo(string? value, int maxLength)
    {
        var trimmed = value?.Trim() ?? "";
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength].TrimEnd();
    }

    public static string CsvCell(string? value)
    {
        var cell = value ?? "";
        if (cell.Length > 0 && "=+-@\t\r".Contains(cell[0]))
        {
            cell = "'" + cell;
        }

        return "\"" + cell.Replace("\"", "\"\"") + "\"";
    }
}

public sealed class NaturalStringComparer : IComparer<string>
{
    public static readonly NaturalStringComparer Instance = new();

    public int Compare(string? x, string? y)
    {
        if (ReferenceEquals(x, y)) return 0;
        if (x is null) return -1;
        if (y is null) return 1;

        var ix = 0;
        var iy = 0;
        while (ix < x.Length && iy < y.Length)
        {
            var cx = x[ix];
            var cy = y[iy];

            if (char.IsDigit(cx) && char.IsDigit(cy))
            {
                var startX = ix;
                var startY = iy;
                while (ix < x.Length && char.IsDigit(x[ix])) ix++;
                while (iy < y.Length && char.IsDigit(y[iy])) iy++;

                var numberX = x[startX..ix].TrimStart('0');
                var numberY = y[startY..iy].TrimStart('0');
                if (numberX.Length == 0) numberX = "0";
                if (numberY.Length == 0) numberY = "0";

                if (numberX.Length != numberY.Length)
                {
                    return numberX.Length.CompareTo(numberY.Length);
                }

                var numberComparison = string.CompareOrdinal(numberX, numberY);
                if (numberComparison != 0)
                {
                    return numberComparison;
                }

                continue;
            }

            var comparison = char.ToUpperInvariant(cx).CompareTo(char.ToUpperInvariant(cy));
            if (comparison != 0)
            {
                return comparison;
            }

            ix++;
            iy++;
        }

        return (x.Length - ix).CompareTo(y.Length - iy);
    }
}
