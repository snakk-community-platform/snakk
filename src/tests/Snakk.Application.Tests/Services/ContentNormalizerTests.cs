using Snakk.Application.Services;

namespace Snakk.Application.Tests.Services;

public class ContentNormalizerTests
{
    private readonly ContentNormalizer _normalizer = new();

    #region IsAllCaps detection

    [Test]
    public async Task NormalizeBody_WithAllCapsText_IsNormalized()
    {
        var result = _normalizer.NormalizeBody("THIS IS ALL CAPS TEXT");

        await Assert.That(result.WasNormalized).IsTrue();
        await Assert.That(result.Content).IsEqualTo("This is all caps text");
    }

    [Test]
    public async Task NormalizeBody_WithMixedCaseText_IsNotNormalized()
    {
        var result = _normalizer.NormalizeBody("This is Normal text");

        await Assert.That(result.WasNormalized).IsFalse();
        await Assert.That(result.Content).IsEqualTo("This is Normal text");
    }

    [Test]
    public async Task NormalizeBody_WithFewerThanFourWords_IsNotNormalized()
    {
        var result = _normalizer.NormalizeBody("ONLY THREE WORDS");

        await Assert.That(result.WasNormalized).IsFalse();
    }

    #endregion

    #region Sentence casing

    [Test]
    public async Task NormalizeBody_AllShortWords_FullyConverted()
    {
        var result = _normalizer.NormalizeBody("I AM NOT GOING TO DO THAT");

        await Assert.That(result.Content).IsEqualTo("I am not going to do that");
    }

    [Test]
    public async Task NormalizeBody_MultipleSentences_EachCapitalized()
    {
        var result = _normalizer.NormalizeBody("FIRST SENTENCE HERE. SECOND SENTENCE HERE");

        await Assert.That(result.Content).IsEqualTo("First sentence here. Second sentence here");
    }

    [Test]
    public async Task NormalizeBody_SingleLetterPronounI_Preserved()
    {
        var result = _normalizer.NormalizeBody("I SAID HELLO TO THEM TODAY");

        await Assert.That(result.Content).IsEqualTo("I said hello to them today");
    }

    [Test]
    public async Task NormalizeTitle_AllCaps_Normalized()
    {
        var result = _normalizer.NormalizeTitle("THIS IS AN ALL CAPS TITLE");

        await Assert.That(result.WasNormalized).IsTrue();
        await Assert.That(result.Content).IsEqualTo("This is an all caps title");
    }

    #endregion

    #region Protected blocks

    [Test]
    public async Task NormalizeBody_AllCapsInsideCodeBlock_Unchanged()
    {
        var input = "Normal line\n```\nTHIS IS CODE AND STAYS CAPS\n```\nNormal again";
        var result = _normalizer.NormalizeBody(input);

        await Assert.That(result.Content).Contains("THIS IS CODE AND STAYS CAPS");
    }

    [Test]
    public async Task NormalizeBody_AllCapsBlockquote_Unchanged()
    {
        var result = _normalizer.NormalizeBody("> THIS IS A QUOTED LINE HERE");

        await Assert.That(result.Content).IsEqualTo("> THIS IS A QUOTED LINE HERE");
        await Assert.That(result.WasNormalized).IsFalse();
    }

    #endregion
}
