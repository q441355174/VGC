namespace VGC.Facts;

public sealed class FactMetaData
{
    public FactMetaData(string name, FactValueType valueType)
    {
        Name = name;
        ValueType = valueType;
    }

    public string Name { get; }

    public FactValueType ValueType { get; }

    public string? Units { get; init; }

    public double? Min { get; init; }

    public double? Max { get; init; }

    public string? ShortDescription { get; init; }

    public FactValidationResult Validate(object? value)
    {
        if (value is null)
        {
            return FactValidationResult.Invalid($"{Name} requires a value.");
        }

        if (value is IConvertible && (Min is not null || Max is not null))
        {
            var numericValue = Convert.ToDouble(value);
            if (Min is { } min && numericValue < min)
            {
                return FactValidationResult.Invalid($"{Name} must be >= {min}.");
            }

            if (Max is { } max && numericValue > max)
            {
                return FactValidationResult.Invalid($"{Name} must be <= {max}.");
            }
        }

        return FactValidationResult.Valid;
    }
}
