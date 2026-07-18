using Shouldly;
using UI.Spelling;
using UI.Tests.TestDoubles;
using Xunit;

namespace UI.Tests.Spelling;

/// <summary>
/// Tests for <see cref="UserAwareSpellDictionary"/> (INV-040): a word in the User Dictionary is never
/// a Misspelling, whatever the inner speller thinks; everything else is judged by the inner speller.
/// </summary>
public sealed class UserAwareSpellDictionaryTests
{
    [Fact]
    public void IsMisspelled_ForAnAcceptedWord_IsFalse_EvenThoughTheSpellerRejectsIt_INV040()
    {
        var dictionary = new UserAwareSpellDictionary(
            new StubSpellDictionary("Shouldly"),
            new FakeUserDictionary("Shouldly"));

        dictionary.IsMisspelled("Shouldly").ShouldBeFalse();
    }

    [Fact]
    public void IsMisspelled_ForAnUnacceptedWord_TheSpellerRejects_IsTrue_INV040()
    {
        var dictionary = new UserAwareSpellDictionary(
            new StubSpellDictionary("qwerty"),
            new FakeUserDictionary());

        dictionary.IsMisspelled("qwerty").ShouldBeTrue();
    }

    [Fact]
    public void IsMisspelled_ForAWordTheSpellerAccepts_IsFalse()
    {
        var dictionary = new UserAwareSpellDictionary(new StubSpellDictionary(), new FakeUserDictionary());

        dictionary.IsMisspelled("hello").ShouldBeFalse();
    }

    [Fact]
    public void Suggest_DelegatesToTheInnerSpeller()
    {
        var inner = new StubSpellDictionary("colour") { Suggestions = ["color"] };
        var dictionary = new UserAwareSpellDictionary(inner, new FakeUserDictionary());

        dictionary.Suggest("colour").ShouldBe(["color"]);
    }

    [Fact]
    public void Constructor_GivenNullInner_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new UserAwareSpellDictionary(null!, new FakeUserDictionary()));
    }

    [Fact]
    public void Constructor_GivenNullUserDictionary_Throws()
    {
        Should.Throw<ArgumentNullException>(() => new UserAwareSpellDictionary(new StubSpellDictionary(), null!));
    }
}
