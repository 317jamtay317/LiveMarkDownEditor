using System.Globalization;
using Shouldly;
using UI.Controls;
using UI.Core;
using Xunit;

namespace UI.Tests.Controls;

/// <summary>
/// Tests for <see cref="EqualityConverter"/>: the one-way comparison a menu's check state binds
/// through — true exactly when the bound value names the converter parameter (INV-061's Page Setup
/// menu checks).
/// </summary>
public sealed class EqualityConverterTests
{
    private readonly EqualityConverter _converter = new();

    [Fact]
    public void Convert_GivenTheNamedValue_IsTrue()
    {
        _converter.Convert(PageOrientation.Landscape, typeof(bool), "Landscape", CultureInfo.InvariantCulture)
            .ShouldBe(true);
    }

    [Fact]
    public void Convert_GivenAnotherValue_IsFalse()
    {
        _converter.Convert(PageOrientation.Portrait, typeof(bool), "Landscape", CultureInfo.InvariantCulture)
            .ShouldBe(false);
    }

    [Fact]
    public void Convert_GivenNull_IsFalse()
    {
        _converter.Convert(null, typeof(bool), "Landscape", CultureInfo.InvariantCulture).ShouldBe(false);
    }
}
