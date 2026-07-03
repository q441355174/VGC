namespace VGC.Maps;

public sealed record AttributionRequirement(
    string ProviderName,
    string AttributionText,
    Uri? AttributionUrl,
    bool MustBeVisible);

public sealed class MapAttributionService
{
    public IReadOnlyList<AttributionRequirement> GetActiveAttributions(MapProviderDescriptor provider)
    {
        var attributions = new List<AttributionRequirement>();
        foreach (var layer in provider.TileLayers)
        {
            foreach (var attr in layer.Attributions)
            {
                if (attr.MustBeVisible)
                {
                    attributions.Add(new AttributionRequirement(
                        provider.DisplayName,
                        attr.Text,
                        attr.Url,
                        attr.MustBeVisible));
                }
            }
        }

        return attributions;
    }

    public string GetAttributionText(MapProviderDescriptor provider)
    {
        var attribs = GetActiveAttributions(provider);
        return attribs.Count == 0
            ? string.Empty
            : string.Join(" | ", attribs.Select(a => a.AttributionText));
    }
}
