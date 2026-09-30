namespace McpApplicationInsights.Services;

public sealed class InsightsConfig
{
    public InsightsConfig()
    {
        SubscriptionId = EmptyToNull(Environment.GetEnvironmentVariable("AZURE_SUBSCRIPTION_ID"));
        TenantId = EmptyToNull(Environment.GetEnvironmentVariable("AZURE_TENANT_ID"));
        ResourceGroup = EmptyToNull(Environment.GetEnvironmentVariable("APPINSIGHTS_RESOURCE_GROUP"));
        MaxRows = ParseInt(Environment.GetEnvironmentVariable("APPINSIGHTS_MAX_ROWS"), 40, 1, 100);
    }

    public string? SubscriptionId { get; }

    public string? TenantId { get; }

    public string? ResourceGroup { get; }

    public int MaxRows { get; }

    public static int ClampHoras(int horas) => Math.Clamp(horas, 1, 168);

    private static int ParseInt(string? raw, int fallback, int min, int max)
    {
        if (!int.TryParse(raw, out var value))
        {
            return fallback;
        }

        return Math.Clamp(value, min, max);
    }

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
