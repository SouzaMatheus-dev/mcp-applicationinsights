using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Azure.Core;

namespace McpApplicationInsights.Services;

public sealed record AssinaturaAzure(string Id, string Nome, string Estado);

public sealed record ComponenteAzure(string Nome, string ResourceGroup, string SubscriptionId, string SubscriptionNome, string Localizacao);

public sealed record AlvoConsulta(string Rotulo, string Componente, string ResourceGroup, string SubscriptionId, string SubscriptionNome);

public sealed class InventarioAzure
{
    private static readonly TokenRequestContext EscopoArm = new(["https://management.azure.com/.default"]);

    private readonly TokenCredential _credential;
    private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(40) };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private IReadOnlyList<AssinaturaAzure>? _assinaturas;
    private IReadOnlyList<ComponenteAzure>? _componentes;
    private DateTimeOffset _expira;

    public InventarioAzure(TokenCredential credential)
    {
        _credential = credential;
    }

    public async Task<IReadOnlyList<AssinaturaAzure>> AssinaturasAsync(CancellationToken cancellationToken)
    {
        var (assinaturas, _) = await CarregarAsync(cancellationToken).ConfigureAwait(false);
        return assinaturas;
    }

    public async Task<IReadOnlyList<ComponenteAzure>> ComponentesAsync(CancellationToken cancellationToken)
    {
        var (_, componentes) = await CarregarAsync(cancellationToken).ConfigureAwait(false);
        return componentes;
    }

    private async Task<(IReadOnlyList<AssinaturaAzure> Assinaturas, IReadOnlyList<ComponenteAzure> Componentes)> CarregarAsync(CancellationToken cancellationToken)
    {
        if (_assinaturas is not null && _componentes is not null && DateTimeOffset.UtcNow < _expira)
        {
            return (_assinaturas, _componentes);
        }

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_assinaturas is not null && _componentes is not null && DateTimeOffset.UtcNow < _expira)
            {
                return (_assinaturas, _componentes);
            }

            var assinaturas = await LerAssinaturasAsync(cancellationToken).ConfigureAwait(false);
            var nomes = assinaturas.ToDictionary(item => item.Id, item => item.Nome, StringComparer.OrdinalIgnoreCase);
            var componentes = await LerComponentesAsync(nomes, cancellationToken).ConfigureAwait(false);
            _assinaturas = assinaturas;
            _componentes = componentes;
            _expira = DateTimeOffset.UtcNow.AddMinutes(5);
            return (assinaturas, componentes);
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task<IReadOnlyList<AssinaturaAzure>> LerAssinaturasAsync(CancellationToken cancellationToken)
    {
        var url = "https://management.azure.com/subscriptions?api-version=2022-12-01";
        var lista = new List<AssinaturaAzure>();
        while (!string.IsNullOrWhiteSpace(url))
        {
            using var doc = await EnviarAsync(HttpMethod.Get, url, null, cancellationToken).ConfigureAwait(false);
            if (doc.RootElement.TryGetProperty("value", out var value))
            {
                foreach (var item in value.EnumerateArray())
                {
                    lista.Add(new AssinaturaAzure(
                        Texto(item, "subscriptionId"),
                        Texto(item, "displayName"),
                        Texto(item, "state")));
                }
            }

            url = doc.RootElement.TryGetProperty("nextLink", out var next) ? next.GetString() : null;
        }

        return lista
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .OrderBy(item => item.Nome, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<IReadOnlyList<ComponenteAzure>> LerComponentesAsync(
        IReadOnlyDictionary<string, string> nomes,
        CancellationToken cancellationToken)
    {
        const string consulta = "Resources | where type =~ 'microsoft.insights/components' | project name, resourceGroup, subscriptionId, location";
        var lista = new List<ComponenteAzure>();
        string? skip = null;
        for (var pagina = 0; pagina < 5; pagina++)
        {
            var corpo = skip is null
                ? JsonSerializer.Serialize(new { query = consulta })
                : JsonSerializer.Serialize(new { query = consulta, options = new Dictionary<string, string> { ["$skipToken"] = skip } });

            using var doc = await EnviarAsync(
                HttpMethod.Post,
                "https://management.azure.com/providers/Microsoft.ResourceGraph/resources?api-version=2021-03-01",
                corpo,
                cancellationToken).ConfigureAwait(false);

            if (doc.RootElement.TryGetProperty("data", out var data) && data.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in data.EnumerateArray())
                {
                    var subscriptionId = Texto(item, "subscriptionId");
                    nomes.TryGetValue(subscriptionId, out var nome);
                    lista.Add(new ComponenteAzure(
                        Texto(item, "name"),
                        Texto(item, "resourceGroup"),
                        subscriptionId,
                        string.IsNullOrWhiteSpace(nome) ? subscriptionId : nome,
                        Texto(item, "location")));
                }
            }

            skip = doc.RootElement.TryGetProperty("$skipToken", out var token) ? token.GetString() : null;
            if (string.IsNullOrWhiteSpace(skip))
            {
                break;
            }
        }

        return lista
            .OrderBy(item => item.SubscriptionNome, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Nome, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private async Task<JsonDocument> EnviarAsync(HttpMethod metodo, string url, string? corpo, CancellationToken cancellationToken)
    {
        var token = await _credential.GetTokenAsync(EscopoArm, cancellationToken).ConfigureAwait(false);
        using var request = new HttpRequestMessage(metodo, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
        if (corpo is not null)
        {
            request.Content = new StringContent(corpo, Encoding.UTF8, "application/json");
        }

        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var texto = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            if (texto.Length > 400)
            {
                texto = texto[..400];
            }

            throw new InvalidOperationException($"Inventário {(int)response.StatusCode}: {texto}");
        }

        return JsonDocument.Parse(texto);
    }

    private static string Texto(JsonElement item, string nome) =>
        item.TryGetProperty(nome, out var valor) ? valor.ToString() : "";
}
