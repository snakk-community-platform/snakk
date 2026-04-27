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
    /// Parses markup text and returns sanitized HTML enriched with srcset, sizes, and
    /// data-blur attributes sourced from the provided image render data.
    /// </summary>
    string ToHtml(string markup, bool autoParagraph, IReadOnlyDictionary<string, ImageRenderData>? imageData)
        => ToHtml(markup, autoParagraph);

    /// <summary>
    /// Extracts plain text from markup (for previews/snippets).
    /// </summary>
    string ToPlainText(string markup);
}
