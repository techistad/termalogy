namespace TermalogySkin.Services;

public static class FuzzyMatcher
{
    public static IReadOnlyList<string> Rank(string query, IEnumerable<string> candidates, int maxResults = 5)
    {
        var normalized = query.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(normalized))
        {
            return candidates
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(item => item, StringComparer.OrdinalIgnoreCase)
                .Take(maxResults)
                .ToList();
        }

        return candidates
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(candidate => new
            {
                Candidate = candidate,
                Score = Score(normalized, candidate.ToLowerInvariant())
            })
            .Where(entry => entry.Score > 0)
            .OrderByDescending(entry => entry.Score)
            .ThenBy(entry => entry.Candidate, StringComparer.OrdinalIgnoreCase)
            .Take(maxResults)
            .Select(entry => entry.Candidate)
            .ToList();
    }

    private static int Score(string query, string candidate)
    {
        if (query == candidate)
        {
            return 1000;
        }

        if (candidate.StartsWith(query, StringComparison.Ordinal))
        {
            return 850 - Math.Abs(candidate.Length - query.Length);
        }

        var containsIndex = candidate.IndexOf(query, StringComparison.Ordinal);
        if (containsIndex >= 0)
        {
            return 700 - (containsIndex * 10) - Math.Abs(candidate.Length - query.Length);
        }

        if (IsSubsequence(query, candidate, out var gapPenalty))
        {
            return 500 - gapPenalty;
        }

        return 0;
    }

    private static bool IsSubsequence(string query, string candidate, out int gapPenalty)
    {
        var queryIndex = 0;
        var lastMatch = -1;
        gapPenalty = 0;

        for (var i = 0; i < candidate.Length && queryIndex < query.Length; i++)
        {
            if (candidate[i] != query[queryIndex])
            {
                continue;
            }

            if (lastMatch >= 0)
            {
                gapPenalty += i - lastMatch - 1;
            }

            lastMatch = i;
            queryIndex++;
        }

        return queryIndex == query.Length;
    }
}
