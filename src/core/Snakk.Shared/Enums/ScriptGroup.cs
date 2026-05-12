namespace Snakk.Shared.Enums;

public enum ScriptGroup
{
    Latin = 0,      // Basic Latin + Latin Extended (covers æøå, é, ü, ñ …)
    Cyrillic = 1,   // Russian, Ukrainian, Bulgarian, Serbian …
    Greek = 2,
    Arabic = 3,
    Hebrew = 4,
    Devanagari = 5, // Hindi, Nepali, Marathi …
    CjkUnified = 6, // Chinese, Japanese kanji, Korean hanja
    Hiragana = 7,   // Japanese hiragana (grouped with CjkUnified for single-script checks)
    Katakana = 8,   // Japanese katakana (grouped with CjkUnified for single-script checks)
    Hangul = 9,     // Korean syllable blocks
    Thai = 10,
    Armenian = 11,
    Georgian = 12,
}
