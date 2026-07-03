using ReactiveUI;

namespace VGC.Facts;

public sealed class Fact : ReactiveObject
{
    private object? _rawValue;

    public Fact(int componentId, string name, FactMetaData metaData, object? rawValue = null)
    {
        ComponentId = componentId;
        Name = name;
        MetaData = metaData;
        _rawValue = rawValue;
    }

    public int ComponentId { get; }

    public string Name { get; }

    public FactMetaData MetaData { get; }

    public object? RawValue
    {
        get => _rawValue;
        private set => this.RaiseAndSetIfChanged(ref _rawValue, value);
    }

    public string DisplayValue => RawValue is null
        ? string.Empty
        : string.IsNullOrWhiteSpace(MetaData.Units)
            ? Convert.ToString(RawValue, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
            : $"{Convert.ToString(RawValue, System.Globalization.CultureInfo.InvariantCulture)} {MetaData.Units}";

    public FactValidationResult Validate(object? value)
    {
        return MetaData.Validate(value);
    }

    public void SetRawValue(object? value)
    {
        RawValue = value;
        this.RaisePropertyChanged(nameof(DisplayValue));
    }
}
