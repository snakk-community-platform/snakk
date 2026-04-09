namespace Snakk.Shared.Models;

public record SupportedLanguage(string Code, string Name, string NativeName, string FlagCode);

public static class SupportedLanguages
{
    public static readonly IReadOnlyList<SupportedLanguage> All =
    [
        new("ar", "Arabic", "العربية", "sa"),
        new("zh", "Chinese", "中文", "cn"),
        new("cs", "Czech", "Čeština", "cz"),
        new("da", "Danish", "Dansk", "dk"),
        new("nl", "Dutch", "Nederlands", "nl"),
        new("en", "English", "English", "gb"),
        new("fi", "Finnish", "Suomi", "fi"),
        new("fr", "French", "Français", "fr"),
        new("de", "German", "Deutsch", "de"),
        new("el", "Greek", "Ελληνικά", "gr"),
        new("hi", "Hindi", "हिन्दी", "in"),
        new("hu", "Hungarian", "Magyar", "hu"),
        new("id", "Indonesian", "Bahasa Indonesia", "id"),
        new("it", "Italian", "Italiano", "it"),
        new("ja", "Japanese", "日本語", "jp"),
        new("ko", "Korean", "한국어", "kr"),
        new("no", "Norwegian", "Norsk", "no"),
        new("pl", "Polish", "Polski", "pl"),
        new("pt", "Portuguese", "Português", "br"),
        new("ro", "Romanian", "Română", "ro"),
        new("ru", "Russian", "Русский", "ru"),
        new("es", "Spanish", "Español", "es"),
        new("sv", "Swedish", "Svenska", "se"),
        new("th", "Thai", "ไทย", "th"),
        new("tr", "Turkish", "Türkçe", "tr"),
        new("uk", "Ukrainian", "Українська", "ua"),
        new("vi", "Vietnamese", "Tiếng Việt", "vn"),
    ];

    public static SupportedLanguage? GetByCode(string? code) =>
        code is null
            ? null
            : All.FirstOrDefault(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

    public static bool IsValidCode(string? code) =>
        code is null
        || All.Any(l => l.Code.Equals(code, StringComparison.OrdinalIgnoreCase));

    public static string GetEffectiveLanguageCode(
        string? ownCode,
        string? parentCode1 = null,
        string? parentCode2 = null
    ) => ownCode ?? parentCode1 ?? parentCode2 ?? "en";
}
