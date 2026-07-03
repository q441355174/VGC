namespace VGC.Facts;

public sealed record FactValidationResult(bool IsValid, string? Error)
{
    public static FactValidationResult Valid { get; } = new(true, null);

    public static FactValidationResult Invalid(string error) => new(false, error);
}
