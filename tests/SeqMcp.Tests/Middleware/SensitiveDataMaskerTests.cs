using FluentAssertions;
using SeqMcp.Middleware;

namespace SeqMcp.Tests.Middleware;

public class SensitiveDataMaskerTests
{
    [Theory]
    [InlineData("\"ApiKey\":\"secret123\"", "\"ApiKey\":\"***\"")]
    [InlineData("\"apiKey\":\"abc\"", "\"apiKey\":\"***\"")]
    [InlineData("\"api_key\":\"xyz\"", "\"api_key\":\"***\"")]
    [InlineData("\"api-key\":\"q\"", "\"api-key\":\"***\"")]
    [InlineData("\"Authorization\":\"Bearer token\"", "\"Authorization\":\"***\"")]
    [InlineData("\"token\":\"v\"", "\"token\":\"***\"")]
    [InlineData("\"password\":\"p\"", "\"password\":\"***\"")]
    [InlineData("\"secret\":\"s\"", "\"secret\":\"***\"")]
    public void Should_Mask_Sensitive_Field(string input, string expected)
    {
        // Act
        var result = SensitiveDataMasker.Mask(input);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void Should_Mask_Field_With_Whitespace_Around_Colon()
    {
        // Arrange
        var input = "\"ApiKey\" : \"secret\"";

        // Act
        var result = SensitiveDataMasker.Mask(input);

        // Assert
        result.Should().Be("\"ApiKey\" : \"***\"");
    }

    [Fact]
    public void Should_Preserve_Non_Sensitive_Fields()
    {
        // Arrange
        var input = "{\"username\":\"alice\",\"email\":\"a@b.c\"}";

        // Act
        var result = SensitiveDataMasker.Mask(input);

        // Assert
        result.Should().Be(input);
    }

    [Fact]
    public void Should_Mask_Within_Nested_Object()
    {
        // Arrange
        var input = "{\"user\":{\"name\":\"alice\",\"apiKey\":\"deadbeef\"}}";

        // Act
        var result = SensitiveDataMasker.Mask(input);

        // Assert
        result.Should().Be("{\"user\":{\"name\":\"alice\",\"apiKey\":\"***\"}}");
    }

    [Fact]
    public void Should_Mask_Value_With_Escaped_Quote()
    {
        // Arrange — внутри значения экранированная кавычка
        var input = "\"token\":\"abc\\\"def\"";

        // Act
        var result = SensitiveDataMasker.Mask(input);

        // Assert
        result.Should().Be("\"token\":\"***\"");
    }

    [Fact]
    public void Should_Mask_Multiple_Sensitive_Fields_In_One_String()
    {
        // Arrange
        var input = "{\"apiKey\":\"k1\",\"password\":\"p1\",\"name\":\"alice\"}";

        // Act
        var result = SensitiveDataMasker.Mask(input);

        // Assert
        result.Should().Be("{\"apiKey\":\"***\",\"password\":\"***\",\"name\":\"alice\"}");
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void Should_Return_Input_Unchanged_When_Null_Or_Empty(string? input)
    {
        // Act
        var result = SensitiveDataMasker.Mask(input!);

        // Assert
        result.Should().Be(input);
    }

    [Fact]
    public void Should_Mask_Header_Value_Authorization()
    {
        var result = SensitiveDataMasker.MaskHeaderValue("Authorization", "Bearer eyJabc.def");

        result.Should().Be("***");
    }

    [Fact]
    public void Should_Not_Mask_Non_Sensitive_Header()
    {
        var result = SensitiveDataMasker.MaskHeaderValue("Content-Type", "application/json");

        result.Should().Be("application/json");
    }

    [Theory]
    [InlineData("X-Seq-ApiKey")]
    [InlineData("x-seq-apikey")]
    [InlineData("Authorization")]
    [InlineData("authorization")]
    public void Should_Mask_Known_Sensitive_Headers_Case_Insensitive(string headerName)
    {
        var result = SensitiveDataMasker.MaskHeaderValue(headerName, "any-secret-value");

        result.Should().Be("***");
    }
}
