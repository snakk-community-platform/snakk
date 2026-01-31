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
    /// Extracts plain text from markup (for previews/snippets).
    /// </summary>
    string ToPlainText(string markup);
}
