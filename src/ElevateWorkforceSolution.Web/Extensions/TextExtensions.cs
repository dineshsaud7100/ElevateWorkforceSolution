using System.Diagnostics.CodeAnalysis;

namespace ElevateWorkforceSolution.Web.Extensions;

/// <summary>Small text helpers used inside views.</summary>
public static class TextExtensions
{
    /// <summary>First letters of the first two words, used for the generated company monograms.</summary>
    public static string Initials(this string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "?";
        }

        var words = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length switch
        {
            0 => "?",
            1 => char.ToUpperInvariant(words[0][0]).ToString(),
            _ => string.Concat(char.ToUpperInvariant(words[0][0]), char.ToUpperInvariant(words[1][0]))
        };
    }

    [return: NotNullIfNotNull(nameof(value))]
    public static string? OrDash(this string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    public static string FormatBytes(this long bytes) => bytes switch
    {
        < 1024 => $"{bytes} B",
        < 1024 * 1024 => $"{bytes / 1024.0:0.#} KB",
        _ => $"{bytes / (1024.0 * 1024.0):0.#} MB"
    };
}
