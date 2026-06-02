using System.Text.RegularExpressions;

namespace Digest.Ingest.Processing;

/// <summary>
/// A single interest keyword with the right matching strategy:
/// alphanumeric terms match on word boundaries (so "ai" does not hit "training"),
/// while terms containing punctuation (".net", "c#", "llama.cpp") match as substrings.
/// All matching assumes the input text is already lower-cased.
/// </summary>
public sealed class Keyword
{
    private readonly string _text;
    private readonly Regex? _wordBoundary;

    public Keyword(string text)
    {
        _text = text.ToLowerInvariant();
        bool alphanumericOnly = _text.All(c => char.IsLetterOrDigit(c) || c == ' ');
        _wordBoundary = alphanumericOnly
            ? new Regex($@"\b{Regex.Escape(_text)}\b", RegexOptions.CultureInvariant)
            : null;
    }

    public string Text => _text;

    /// <summary><paramref name="lowerText"/> must already be lower-cased.</summary>
    public bool Matches(string lowerText) => _wordBoundary is not null
        ? _wordBoundary.IsMatch(lowerText)
        : lowerText.Contains(_text, StringComparison.Ordinal);

    public override string ToString() => _text;
}
