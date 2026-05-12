namespace Snakk.Application.Services;

using Snakk.Shared.Enums;
using System.Text.RegularExpressions;

public static class ScriptGroupRegistry
{
    // Maps each ScriptGroup to (regex character-class fragment, char-membership predicate)
    // Regex fragments use \uHHHH notation which .NET regex interprets as Unicode codepoints.
    private static readonly (string Fragment, Func<char, bool> Contains)[] Definitions =
    [
        /* Latin      */ (@"A-Za-zÀ-ɏ", c => c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= 'À' and <= 'ɏ')),
        /* Cyrillic   */ (@"Ѐ-ԯ",         c => c is >= 'Ѐ' and <= 'ԯ'),
        /* Greek      */ (@"Ͱ-Ͽἀ-῿", c => c is (>= 'Ͱ' and <= 'Ͽ') or (>= 'ἀ' and <= '῿')),
        /* Arabic     */ (@"؀-ۿ",         c => c is >= '؀' and <= 'ۿ'),
        /* Hebrew     */ (@"֐-׿",         c => c is >= '֐' and <= '׿'),
        /* Devanagari */ (@"ऀ-ॿ",         c => c is >= 'ऀ' and <= 'ॿ'),
        /* CjkUnified */ (@"一-鿿㐀-䶿", c => c is (>= '一' and <= '鿿') or (>= '㐀' and <= '䶿')),
        /* Hiragana   */ (@"ぁ-ゟ",         c => c is >= 'ぁ' and <= 'ゟ'),
        /* Katakana   */ (@"ァ-ヿ",         c => c is >= 'ァ' and <= 'ヿ'),
        /* Hangul     */ (@"가-힣ᄀ-ᇿ", c => c is (>= '가' and <= '힣') or (>= 'ᄀ' and <= 'ᇿ')),
        /* Thai       */ (@"ก-๛",         c => c is >= 'ก' and <= '๛'),
        /* Armenian   */ (@"Ա-և",         c => c is >= 'Ա' and <= 'և'),
        /* Georgian   */ (@"Ⴀ-ჿ",         c => c is >= 'Ⴀ' and <= 'ჿ'),
    ];

    // ScriptGroup ordinal values map to Definitions indices (must stay in sync with the enum).

    public static Regex BuildAllowedCharsRegex(IReadOnlyList<ScriptGroup> enabledScripts)
    {
        var fragment = string.Concat(enabledScripts.Select(sg => Definitions[(int)sg].Fragment));
        return new Regex($@"^[0-9_\-{fragment}]+$");
    }

    // Returns which script a character belongs to (null for neutral chars like 0-9, _, -)
    public static ScriptGroup? GetCharScript(char c)
    {
        for (var i = 0; i < Definitions.Length; i++)
            if (Definitions[i].Contains(c)) return (ScriptGroup)i;
        return null;
    }

    // For single-script enforcement: Hiragana and Katakana are grouped with CjkUnified
    // since mixing them is standard in Japanese and Chinese text.
    public static ScriptGroup? GetCharScriptFamily(char c)
    {
        var script = GetCharScript(c);
        return script is ScriptGroup.Hiragana or ScriptGroup.Katakana ? ScriptGroup.CjkUnified : script;
    }

    public static IReadOnlyList<ScriptGroupInfo> All { get; } = Enum.GetValues<ScriptGroup>()
        .Select(sg => new ScriptGroupInfo(sg, GetDisplayName(sg), GetExamples(sg)))
        .ToArray();

    public static string GetDisplayName(ScriptGroup sg) => sg switch
    {
        ScriptGroup.Latin => "Latin",
        ScriptGroup.Cyrillic => "Cyrillic",
        ScriptGroup.Greek => "Greek",
        ScriptGroup.Arabic => "Arabic",
        ScriptGroup.Hebrew => "Hebrew",
        ScriptGroup.Devanagari => "Devanagari",
        ScriptGroup.CjkUnified => "CJK (Chinese / Japanese kanji / Korean hanja)",
        ScriptGroup.Hiragana => "Hiragana",
        ScriptGroup.Katakana => "Katakana",
        ScriptGroup.Hangul => "Hangul (Korean)",
        ScriptGroup.Thai => "Thai",
        ScriptGroup.Armenian => "Armenian",
        ScriptGroup.Georgian => "Georgian",
        _ => sg.ToString()
    };

    public static string GetExamples(ScriptGroup sg) => sg switch
    {
        ScriptGroup.Latin => "A–Z, a–z, æ ø å é ü ñ",
        ScriptGroup.Cyrillic => "А Б В а б в (Ru/Ua/Bg)",
        ScriptGroup.Greek => "Α Β Γ α β γ",
        ScriptGroup.Arabic => "ا ب ت ث",
        ScriptGroup.Hebrew => "א ב ג ד",
        ScriptGroup.Devanagari => "क ख ग घ (Hi)",
        ScriptGroup.CjkUnified => "一 二 三 中",
        ScriptGroup.Hiragana => "あ い う え お",
        ScriptGroup.Katakana => "ア イ ウ エ オ",
        ScriptGroup.Hangul => "가 나 다 라",
        ScriptGroup.Thai => "ก ข ค ง",
        ScriptGroup.Armenian => "Ա Բ Գ Դ",
        ScriptGroup.Georgian => "ა ბ გ დ",
        _ => ""
    };
}

public sealed record ScriptGroupInfo(ScriptGroup Group, string DisplayName, string Examples);
