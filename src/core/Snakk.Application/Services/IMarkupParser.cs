namespace Snakk.Application.Services;

/// <summary>
/// Parses lightweight markup syntax and renders to safe HTML.
/// </summary>
public interface IMarkupParser
{
    /// <summary>
    /// Parses markup text and returns sanitized HTML.
    /// </summary>
    string ToHtml(string markup);

    /// <summary>
    /// Parses markup text and returns sanitized HTML, optionally running the
    /// auto-paragraph splitter on walls of text before markdown parsing.
    /// </summary>
    string ToHtml(string markup, bool autoParagraph);

    /// <summary>
    /// Extracts plain text from markup (for previews/snippets).
    /// </summary>
    string ToPlainText(string markup);
}
