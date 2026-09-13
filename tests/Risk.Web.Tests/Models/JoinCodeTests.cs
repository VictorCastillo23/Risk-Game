using Risk.Web.Models;

namespace Risk.Web.Tests.Models;

public class JoinCodeTests
{
    [Fact]
    public void Generate_ReturnsSixCharCodeFromUnambiguousAlphabet()
    {
        var code = JoinCode.Generate();

        Assert.Equal(6, code.Value.Length);
        Assert.Matches("^[A-HJ-NP-Z2-9]{6}$", code.Value);
    }

    [Fact]
    public void Generate_ProducesDistinctCodes()
    {
        var first = JoinCode.Generate();
        var second = JoinCode.Generate();

        Assert.NotEqual(first, second);
    }

    [Theory]
    [InlineData("R7K9X2", true)]
    [InlineData("r7k9x2", true)]
    [InlineData("ABCDEF", true)]
    [InlineData("", false)]
    [InlineData("AB12", false)]
    [InlineData("ABCDEFG", false)]
    [InlineData("AB1O34", false)]
    [InlineData("AB-123", false)]
    [InlineData(null, false)]
    public void TryParse_AcceptsOnlyWellFormedCodes(string? input, bool expected)
    {
        var ok = JoinCode.TryParse(input, out var code);

        Assert.Equal(expected, ok);
        Assert.Equal(expected, code is not null);
    }

    [Fact]
    public void TryParse_NormalizesToUppercase()
    {
        Assert.True(JoinCode.TryParse("r7k9x2", out var code));

        Assert.Equal("R7K9X2", code!.Value);
    }
}
