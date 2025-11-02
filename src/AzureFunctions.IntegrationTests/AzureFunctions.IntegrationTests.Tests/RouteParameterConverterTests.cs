using AzureFunctions.IntegrationTests.Extensions;

namespace AzureFunctions.IntegrationTests.Tests;

public class RouteParameterConverterTests
{
    [Theory]
    [InlineData("42", typeof(int), 42)]
    [InlineData("0", typeof(int), 0)]
    [InlineData("-100", typeof(int), -100)]
    public void ConvertParameter_Int_ShouldConvertCorrectly(string input, Type targetType, object expected)
    {
        // Act
        var result = RouteParameterConverter.ConvertParameter(input, targetType);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("550e8400-e29b-41d4-a716-446655440000")]
    [InlineData("00000000-0000-0000-0000-000000000000")]
    public void ConvertParameter_Guid_ShouldConvertCorrectly(string input)
    {
        // Arrange
        var expectedGuid = Guid.Parse(input);

        // Act
        var result = RouteParameterConverter.ConvertParameter(input, typeof(Guid));

        // Assert
        Assert.Equal(expectedGuid, result);
    }

    [Fact]
    public void ConvertParameter_String_ShouldReturnSameString()
    {
        // Arrange
        var input = "test-string";

        // Act
        var result = RouteParameterConverter.ConvertParameter(input, typeof(string));

        // Assert
        Assert.Equal(input, result);
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("false", false)]
    [InlineData("True", true)]
    [InlineData("False", false)]
    public void ConvertParameter_Bool_ShouldConvertCorrectly(string input, bool expected)
    {
        // Act
        var result = RouteParameterConverter.ConvertParameter(input, typeof(bool));

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("99.99", typeof(decimal))]
    [InlineData("0.01", typeof(decimal))]
    [InlineData("1234567890.123", typeof(decimal))]
    public void ConvertParameter_Decimal_ShouldConvertCorrectly(string input, Type targetType)
    {
        // Arrange
        var expected = decimal.Parse(input);

        // Act
        var result = RouteParameterConverter.ConvertParameter(input, targetType);

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("2025-01-15")]
    [InlineData("2025-01-15T10:30:00")]
    public void ConvertParameter_DateTime_ShouldConvertCorrectly(string input)
    {
        // Arrange
        var expected = DateTime.Parse(input);

        // Act
        var result = RouteParameterConverter.ConvertParameter(input, typeof(DateTime));

        // Assert
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("", typeof(int?))]
    [InlineData("", typeof(Guid?))]
    [InlineData("", typeof(bool?))]
    public void ConvertParameter_NullableTypes_WithEmptyString_ShouldReturnNull(string input, Type targetType)
    {
        // Act
        var result = RouteParameterConverter.ConvertParameter(input, targetType);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ConvertParameter_NullableInt_WithValue_ShouldConvertCorrectly()
    {
        // Act
        var result = RouteParameterConverter.ConvertParameter("42", typeof(int?));

        // Assert
        Assert.Equal(42, result);
    }

    [Fact]
    public void ConvertParameter_InvalidGuid_ShouldThrowArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => RouteParameterConverter.ConvertParameter("not-a-guid", typeof(Guid)));

        Assert.Contains("Cannot convert", exception.Message);
    }

    [Fact]
    public void ConvertParameter_InvalidInt_ShouldThrowArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => RouteParameterConverter.ConvertParameter("not-a-number", typeof(int)));

        Assert.Contains("Cannot convert", exception.Message);
    }

    [Theory]
    [InlineData("Active", typeof(TestEnum), TestEnum.Active)]
    [InlineData("Inactive", typeof(TestEnum), TestEnum.Inactive)]
    [InlineData("active", typeof(TestEnum), TestEnum.Active)] // Case insensitive
    public void ConvertParameter_Enum_ShouldConvertCorrectly(string input, Type targetType, object expected)
    {
        // Act
        var result = RouteParameterConverter.ConvertParameter(input, targetType);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ConvertParameter_InvalidEnum_ShouldThrowArgumentException()
    {
        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(
            () => RouteParameterConverter.ConvertParameter("InvalidValue", typeof(TestEnum)));

        Assert.Contains("Cannot convert", exception.Message);
    }

    public enum TestEnum
    {
        Active,
        Inactive,
        Pending
    }
}
