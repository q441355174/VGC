using System.Globalization;

namespace VGC.Facts;

public enum ParameterWriteStatus
{
    None,
    Pending,
    Succeeded,
    Failed
}

public sealed record ParameterWriteState(
    ParameterWriteStatus Status,
    int RetryCount = 0,
    string? LastError = null)
{
    public static ParameterWriteState None { get; } = new(ParameterWriteStatus.None);
}

public sealed record ParameterEditCommitResult(
    bool Accepted,
    string StatusText,
    Fact? Fact = null,
    object? ParsedValue = null)
{
    public static ParameterEditCommitResult Rejected(string statusText) => new(false, statusText);

    public static ParameterEditCommitResult AcceptedCommit(Fact fact, object? parsedValue)
    {
        return new(true, $"Pending write {fact.Name}", fact, parsedValue);
    }
}

public sealed class ParameterEditService
{
    public ParameterEditCommitResult Commit(
        ParameterManager manager,
        int componentId,
        string name,
        string text,
        IParameterMetadataCatalog? metadataCatalog = null)
    {
        if (!manager.TryGetParameter(componentId, name, out var fact) || fact is null)
        {
            return ParameterEditCommitResult.Rejected($"Parameter {name} is not loaded.");
        }

        var metadata = metadataCatalog?.Find(componentId, name);
        if (!TryParseValue(text, fact, out var parsedValue, out var error))
        {
            manager.FailParameterWrite(componentId, name, error);
            return ParameterEditCommitResult.Rejected(error);
        }

        var validation = ValidateMetadata(metadata, parsedValue);
        if (!validation.IsValid)
        {
            var validationError = validation.Error ?? $"Invalid value for {name}.";
            manager.FailParameterWrite(componentId, name, validationError);
            return ParameterEditCommitResult.Rejected(validationError);
        }

        manager.BeginParameterWrite(componentId, name);
        return ParameterEditCommitResult.AcceptedCommit(fact, parsedValue);
    }

    private static bool TryParseValue(string text, Fact fact, out object? parsedValue, out string error)
    {
        parsedValue = null;
        error = string.Empty;
        var trimmed = text.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            error = $"{fact.Name} requires a value.";
            return false;
        }

        switch (fact.MetaData.ValueType)
        {
            case FactValueType.Int32:
                if (int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var intValue))
                {
                    parsedValue = intValue;
                    return true;
                }

                break;
            case FactValueType.UInt32:
                if (uint.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var uintValue))
                {
                    parsedValue = uintValue;
                    return true;
                }

                break;
            case FactValueType.Float:
            case FactValueType.Double:
                if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var doubleValue))
                {
                    parsedValue = doubleValue;
                    return true;
                }

                break;
            case FactValueType.Boolean:
                if (bool.TryParse(trimmed, out var boolValue))
                {
                    parsedValue = boolValue;
                    return true;
                }

                if (trimmed == "0" || trimmed == "1")
                {
                    parsedValue = trimmed == "1";
                    return true;
                }

                break;
            case FactValueType.String:
                parsedValue = trimmed;
                return true;
            default:
                parsedValue = trimmed;
                return true;
        }

        error = $"{fact.Name} value '{text}' is not a valid {fact.MetaData.ValueType}.";
        return false;
    }

    private static FactValidationResult ValidateMetadata(ParameterMetadata? metadata, object? parsedValue)
    {
        if (metadata is null)
        {
            return FactValidationResult.Valid;
        }

        if (metadata.EnumValues is { Count: > 0 })
        {
            var raw = Convert.ToString(parsedValue, CultureInfo.InvariantCulture) ?? string.Empty;
            if (!metadata.EnumValues.Any(item => string.Equals(item.Value, raw, StringComparison.OrdinalIgnoreCase)))
            {
                return FactValidationResult.Invalid($"{metadata.Name} must be one of {string.Join(", ", metadata.EnumValues.Select(static item => item.Value))}.");
            }
        }

        if (parsedValue is null || (metadata.Min is null && metadata.Max is null))
        {
            return FactValidationResult.Valid;
        }

        if (!double.TryParse(Convert.ToString(parsedValue, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var numeric))
        {
            return FactValidationResult.Valid;
        }

        if (metadata.Min is { } min && numeric < min)
        {
            return FactValidationResult.Invalid($"{metadata.Name} must be >= {min.ToString(CultureInfo.InvariantCulture)}.");
        }

        if (metadata.Max is { } max && numeric > max)
        {
            return FactValidationResult.Invalid($"{metadata.Name} must be <= {max.ToString(CultureInfo.InvariantCulture)}.");
        }

        return FactValidationResult.Valid;
    }
}
