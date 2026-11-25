using System.Text.RegularExpressions;

namespace Fundoo.Observability;

/// <summary>
/// Decides what may leave the process in span attributes and log attributes.
/// Rules (see docs/OBSERVABILITY.md):
///  * keys that name a secret (authorization, cookie, password, token, otp, secret, connection string, ...) are dropped;
///  * request/response bodies and SQL parameter values are dropped;
///  * query strings are cut from URLs;
///  * string literals in SQL text are replaced by '?';
///  * e-mail addresses and JWT-looking strings are masked in any value that survives.
/// </summary>
public static partial class SensitiveDataRedactor
{
    public const string Redacted = "[REDACTED]";

    // Substrings of an attribute key that mark its value as secret. Matched case-insensitively.
    private static readonly string[] SensitiveKeyFragments =
    {
        "authorization", "cookie", "password", "passwd", "pwd", "secret", "token", "jwt",
        "otp", "api-key", "api_key", "apikey", "credential", "connection_string", "connectionstring",
        "session",
    };

    // Exact keys that carry payloads or parameter values; never exported.
    private static readonly HashSet<string> DroppedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "db.connection_string",
        "db.statement.parameters",
        "http.request.body",
        "http.response.body",
        "messaging.message.body",
        "rpc.message.body",
    };

    private static readonly string[] DroppedKeyPrefixes =
    {
        "db.query.parameter.",
        "db.operation.parameter.",
    };

    // Keys whose value is a URL (or a path that may carry a query string).
    private static readonly HashSet<string> UrlKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "url.full", "url.original", "http.url", "http.target", "http.route.full",
    };

    private static readonly HashSet<string> QueryKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "url.query",
        "QueryString", // ASP.NET Core hosting log attribute
    };

    private static readonly HashSet<string> SqlKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "db.statement", "db.query.text",
    };

    [GeneratedRegex(@"[A-Za-z0-9._%+\-]+@[A-Za-z0-9.\-]+\.[A-Za-z]{2,}", RegexOptions.CultureInvariant)]
    private static partial Regex EmailRegex();

    [GeneratedRegex(@"eyJ[A-Za-z0-9_\-]{5,}\.[A-Za-z0-9_\-]{5,}\.[A-Za-z0-9_\-]*", RegexOptions.CultureInvariant)]
    private static partial Regex JwtRegex();

    [GeneratedRegex(@"(?i)\bbearer\s+[A-Za-z0-9._\-~+/]+=*", RegexOptions.CultureInvariant)]
    private static partial Regex BearerRegex();

    // A URL or absolute path followed by a query string, e.g. "GET http://gw/api/notes/1?debug=x".
    [GeneratedRegex(@"((?:https?://|/)[^\s?#""'<>]*)\?[^\s""'<>]+", RegexOptions.CultureInvariant)]
    private static partial Regex UrlWithQueryRegex();

    // Single-quoted SQL string literal, with '' escapes.
    [GeneratedRegex(@"'(?:[^']|'')*'", RegexOptions.CultureInvariant)]
    private static partial Regex SqlStringLiteralRegex();

    /// <summary>True when the attribute must not be exported at all.</summary>
    public static bool ShouldDrop(string key)
    {
        if (DroppedKeys.Contains(key))
            return true;

        foreach (var prefix in DroppedKeyPrefixes)
            if (key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                return true;

        foreach (var fragment in SensitiveKeyFragments)
            if (key.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }

    /// <summary>
    /// Returns the value to export for <paramref name="key"/>. Callers drop the attribute first when
    /// <see cref="ShouldDrop"/> is true.
    /// </summary>
    public static object? RedactValue(string key, object? value)
    {
        if (value is not string text || text.Length == 0)
            return value;

        if (QueryKeys.Contains(key))
            return Redacted;

        if (UrlKeys.Contains(key))
            text = StripQuery(text);
        else if (SqlKeys.Contains(key))
            text = SqlStringLiteralRegex().Replace(text, "?");

        return MaskFreeText(text);
    }

    /// <summary>
    /// Masks e-mail addresses, bearer tokens, JWTs and URL query strings inside arbitrary text
    /// (log messages such as "Request starting ... GET http://host/path?x=y", exception messages).
    /// </summary>
    public static string MaskFreeText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        if (text.Contains('?'))
            text = UrlWithQueryRegex().Replace(text, "$1?" + Redacted);
        if (text.Contains('@'))
            text = EmailRegex().Replace(text, "[EMAIL]");
        if (text.Contains("eyJ", StringComparison.Ordinal))
            text = JwtRegex().Replace(text, "[JWT]");
        if (text.Contains("earer", StringComparison.OrdinalIgnoreCase))
            text = BearerRegex().Replace(text, "Bearer " + Redacted);
        return text;
    }

    /// <summary>Removes the query string and fragment from a URL or path, keeping a marker that one existed.</summary>
    public static string StripQuery(string url)
    {
        var cut = url.IndexOfAny(new[] { '?', '#' });
        return cut < 0 ? url : url[..cut] + (url[cut] == '?' ? "?" + Redacted : string.Empty);
    }
}
