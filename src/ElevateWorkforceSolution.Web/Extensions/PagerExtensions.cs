using ElevateWorkforceSolution.Application.Common;
using ElevateWorkforceSolution.Web.Models;

namespace ElevateWorkforceSolution.Web.Extensions;

/// <summary>Turns any paged result into the primitive pager state the shared partial renders.</summary>
public static class PagerExtensions
{
    public static PagerViewModel ToPager<T>(this PagedResult<T> result, string baseUrl) => new()
    {
        PageNumber = result.PageNumber,
        TotalPages = result.TotalPages,
        TotalCount = result.TotalCount,
        FirstItemIndex = result.FirstItemIndex,
        LastItemIndex = result.LastItemIndex,
        BaseUrl = BuildBaseUrl(baseUrl)
    };

    /// <summary>
    /// Normalises a base URL so the page parameter can always be appended. A path keeps its own
    /// query string (other filters are preserved); a bare "?" stays a bare "?".
    /// </summary>
    private static string BuildBaseUrl(string baseUrl)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return "?";
        }

        var url = baseUrl;

        // Drop any existing page parameter so it cannot be duplicated.
        var questionMark = url.IndexOf('?');
        if (questionMark >= 0)
        {
            var path = url[..questionMark];
            var query = url[(questionMark + 1)..];
            var kept = query
                .Split('&', StringSplitOptions.RemoveEmptyEntries)
                .Where(pair => !pair.StartsWith("page=", StringComparison.OrdinalIgnoreCase))
                .ToList();

            url = kept.Count == 0
                ? $"{path}?"
                : $"{path}?{string.Join('&', kept)}&";
        }
        else
        {
            url += "?";
        }

        return url;
    }
}
