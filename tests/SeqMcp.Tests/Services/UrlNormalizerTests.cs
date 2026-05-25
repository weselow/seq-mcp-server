using FluentAssertions;
using SeqMcp.Core.Services;

namespace SeqMcp.Tests.Services;

/// <summary>
/// Verifies URL normalization rules (design decision 4):
/// the path is preserved (so two path-scoped Seqs stay distinct), but
/// scheme/host/trailing-slash/default-port/percent-encoding differences
/// collapse into a single canonical form.
/// </summary>
public class UrlNormalizerTests
{
    [Theory]
    [InlineData("https://Host/", "https://host")]
    [InlineData("https://host", "https://host")]
    [InlineData("https://HOST/", "https://host")]
    [InlineData("https://host:443/", "https://host")]
    [InlineData("http://host:80", "http://host")]
    [InlineData("https://host:5341", "https://host:5341")]
    public void Should_Collapse_Cosmetic_Differences(string input, string expected)
    {
        UrlNormalizer.Normalize(input).Should().Be(expected);
    }

    [Fact]
    public void Should_Preserve_Path()
    {
        UrlNormalizer.Normalize("https://host/seq-a").Should().Be("https://host/seq-a");
        UrlNormalizer.Normalize("https://host/seq-b").Should().Be("https://host/seq-b");
    }

    [Fact]
    public void Should_Distinguish_Path_Variants()
    {
        var a = UrlNormalizer.Normalize("https://host/a");
        var b = UrlNormalizer.Normalize("https://host/b");
        a.Should().NotBe(b);
    }

    [Fact]
    public void Should_Trim_Trailing_Slash_From_Path()
    {
        UrlNormalizer.Normalize("https://host/path/").Should().Be("https://host/path");
    }

    [Fact]
    public void Should_Strip_Query_And_Fragment()
    {
        UrlNormalizer.Normalize("https://host/path?x=1#section")
            .Should().Be("https://host/path");
    }

    [Fact]
    public void Should_Strip_Credentials()
    {
        UrlNormalizer.Normalize("https://user:pass@host/path")
            .Should().Be("https://host/path");
    }

    [Fact]
    public void Should_Lowercase_Percent_Encoded_Sequences()
    {
        UrlNormalizer.Normalize("https://host/path%2Fsub")
            .Should().Be("https://host/path%2fsub");
    }

    [Fact]
    public void Should_Throw_On_Invalid_Url()
    {
        var act = () => UrlNormalizer.Normalize("not a url");
        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Should_Throw_On_Null_Or_Empty()
    {
        ((Action)(() => UrlNormalizer.Normalize(null!))).Should().Throw<ArgumentException>();
        ((Action)(() => UrlNormalizer.Normalize(""))).Should().Throw<ArgumentException>();
    }
}
