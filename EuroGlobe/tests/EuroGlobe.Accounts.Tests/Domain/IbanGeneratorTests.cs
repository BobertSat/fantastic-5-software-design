using EuroGlobe.Accounts.Domain;
using Xunit;

namespace EuroGlobe.Accounts.Tests.Domain;

public class IbanGeneratorTests
{
    [Fact]
    public void Generate_ReturnsValidLithuanianIban()
    {
        var iban = IbanGenerator.Generate();

        Assert.StartsWith("LT", iban);
        Assert.Equal(20, iban.Length);
        Assert.True(IbanGenerator.IsValid(iban));
    }

    [Fact]
    public void Generate_UsesTheGivenBankCode()
    {
        var iban = IbanGenerator.Generate("12345");

        Assert.Equal("12345", iban.Substring(4, 5));
        Assert.True(IbanGenerator.IsValid(iban));
    }

    [Theory]
    [InlineData("")]
    [InlineData("1234")]
    [InlineData("123456")]
    [InlineData("ABCDE")]
    public void Generate_RejectsInvalidBankCode(string bankCode)
        => Assert.Throws<ArgumentException>(() => IbanGenerator.Generate(bankCode));

    [Fact]
    public void Generate_ProducesDistinctValues()
    {
        var ibans = Enumerable.Range(0, 200).Select(_ => IbanGenerator.Generate()).ToHashSet();

        Assert.Equal(200, ibans.Count);
    }

    [Theory]
    [InlineData("LT121000011101001000")]
    [InlineData("lt12 1000 0111 0100 1000")]
    [InlineData("DE89370400440532013000")]
    [InlineData("GB82WEST12345698765432")]
    public void IsValid_AcceptsCorrectIbans(string iban)
        => Assert.True(IbanGenerator.IsValid(iban));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("LT12")]
    [InlineData("LT121000011101001001")]
    [InlineData("LT12-1000011101001000")]
    [InlineData("1212 1000011101001000")]
    public void IsValid_RejectsBadIbans(string? iban)
        => Assert.False(IbanGenerator.IsValid(iban));

    [Fact]
    public void IsValid_RejectsIbansLongerThan34Characters()
        => Assert.False(IbanGenerator.IsValid("LT12" + new string('0', 31)));

    [Fact]
    public void IsValid_DetectsAnySingleDigitChange()
    {
        var iban = IbanGenerator.Generate();
        var last = iban[^1];
        var changed = iban[..^1] + (last == '0' ? '1' : '0');

        Assert.False(IbanGenerator.IsValid(changed));
    }
}
