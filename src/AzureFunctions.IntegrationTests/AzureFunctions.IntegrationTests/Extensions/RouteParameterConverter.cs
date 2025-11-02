namespace AzureFunctions.IntegrationTests.Extensions;

/// <summary>
/// Converts route parameter strings to their target types
/// </summary>
public static class RouteParameterConverter
{
    /// <summary>
    /// Converts a route parameter string to the specified type
    /// </summary>
    /// <param name="paramValue">The string value from the route</param>
    /// <param name="targetType">The target type to convert to</param>
    /// <returns>The converted value</returns>
    /// <exception cref="ArgumentException">Thrown when conversion fails</exception>
    public static object ConvertParameter(string paramValue, Type targetType)
    {
        try
        {
            // Handle Guid conversion
            if (targetType == typeof(Guid))
            {
                if (Guid.TryParse(paramValue, out var guidValue))
                {
                    return guidValue;
                }
                throw new ArgumentException($"Cannot convert '{paramValue}' to Guid");
            }

            // Handle nullable Guid conversion
            if (targetType == typeof(Guid?))
            {
                if (string.IsNullOrEmpty(paramValue))
                {
                    return null!;
                }
                if (Guid.TryParse(paramValue, out var nullableGuidValue))
                {
                    return nullableGuidValue;
                }
                throw new ArgumentException($"Cannot convert '{paramValue}' to nullable Guid");
            }

            // Handle int conversion
            if (targetType == typeof(int))
            {
                if (int.TryParse(paramValue, out var intValue))
                {
                    return intValue;
                }
                throw new ArgumentException($"Cannot convert '{paramValue}' to int");
            }

            // Handle nullable int conversion
            if (targetType == typeof(int?))
            {
                if (string.IsNullOrEmpty(paramValue))
                {
                    return null!;
                }
                if (int.TryParse(paramValue, out var nullableIntValue))
                {
                    return nullableIntValue;
                }
                throw new ArgumentException($"Cannot convert '{paramValue}' to nullable int");
            }

            // Handle decimal conversion
            if (targetType == typeof(decimal))
            {
                if (decimal.TryParse(paramValue, out var decimalValue))
                {
                    return decimalValue;
                }
                throw new ArgumentException($"Cannot convert '{paramValue}' to decimal");
            }

            // Handle nullable decimal conversion
            if (targetType == typeof(decimal?))
            {
                if (string.IsNullOrEmpty(paramValue))
                {
                    return null!;
                }
                if (decimal.TryParse(paramValue, out var nullableDecimalValue))
                {
                    return nullableDecimalValue;
                }
                throw new ArgumentException($"Cannot convert '{paramValue}' to nullable decimal");
            }

            // Handle string (no conversion needed)
            if (targetType == typeof(string))
            {
                return paramValue;
            }

            // Handle DateTime conversion
            if (targetType == typeof(DateTime))
            {
                if (DateTime.TryParse(paramValue, out var dateTimeValue))
                {
                    return dateTimeValue;
                }
                throw new ArgumentException($"Cannot convert '{paramValue}' to DateTime");
            }

            // Handle nullable DateTime conversion
            if (targetType == typeof(DateTime?))
            {
                if (string.IsNullOrEmpty(paramValue))
                {
                    return null!;
                }
                if (DateTime.TryParse(paramValue, out var nullableDateTimeValue))
                {
                    return nullableDateTimeValue;
                }
                throw new ArgumentException($"Cannot convert '{paramValue}' to nullable DateTime");
            }

            // Handle bool conversion
            if (targetType == typeof(bool))
            {
                if (bool.TryParse(paramValue, out var boolValue))
                {
                    return boolValue;
                }
                throw new ArgumentException($"Cannot convert '{paramValue}' to bool");
            }

            // Handle nullable bool conversion
            if (targetType == typeof(bool?))
            {
                if (string.IsNullOrEmpty(paramValue))
                {
                    return null!;
                }
                if (bool.TryParse(paramValue, out var nullableBoolValue))
                {
                    return nullableBoolValue;
                }
                throw new ArgumentException($"Cannot convert '{paramValue}' to nullable bool");
            }

            // Handle enum conversion
            if (targetType.IsEnum)
            {
                if (Enum.TryParse(targetType, paramValue, true, out var enumValue))
                {
                    return enumValue;
                }
                throw new ArgumentException($"Cannot convert '{paramValue}' to enum {targetType.Name}");
            }

            // Handle nullable enum conversion
            var underlyingEnumType = Nullable.GetUnderlyingType(targetType);
            if (underlyingEnumType?.IsEnum == true)
            {
                if (string.IsNullOrEmpty(paramValue))
                {
                    return null!;
                }
                if (Enum.TryParse(underlyingEnumType, paramValue, true, out var nullableEnumValue))
                {
                    return nullableEnumValue;
                }
                throw new ArgumentException($"Cannot convert '{paramValue}' to nullable enum {underlyingEnumType.Name}");
            }

            // Default fallback: try to use Convert.ChangeType
            return Convert.ChangeType(paramValue, targetType);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Cannot convert route parameter '{paramValue}' to type {targetType.Name}: {ex.Message}", ex);
        }
    }
}
