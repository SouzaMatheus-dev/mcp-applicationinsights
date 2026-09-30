using System.Text.Encodings.Web;
using System.Text.Json;
using Azure.Core;
using Azure.Identity;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;

namespace McpApplicationInsights.Services;

public sealed class InsightsClient
{
    private static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private static readonly TokenRequestContext Escopo = new(["https://api.loganalytics.io/.default"]);

    private readonly InsightsConfig _config;
    private readonly LogsQueryClient _logs;
    private readonly TokenCredential _credential;
    private readonly InventarioAzure _inventario;

    public InsightsClient(InsightsConfig config)
    {
        _config = config;
        var opcoesCli = new AzureCliCredentialOptions { TenantId = config.TenantId };
        var opcoesVs = new VisualStudioCredentialOptions { TenantId = config.TenantId };
        var opcoesPs = new AzurePowerShellCredentialOptions { TenantId = config.TenantId };
        _credential = new ChainedTokenCredential(
            new AzureCliCredential(opcoesCli),
            new VisualStudioCredential(opcoesVs),
            new AzurePowerShellCredential(opcoesPs));
        _logs = new LogsQueryClient(_credential);
        _inventario = new InventarioAzure(_credential);
    }

    public async Task<string> ListarAssinaturasAsync(CancellationToken cancellationToken)
    {
        try
        {
            var assinaturas = await _inventario.AssinaturasAsync(cancellationToken).ConfigureAwait(false);
            var payload = new
            {
                tenantId = _config.TenantId,
                escolher = assinaturas.Count > 1,
                mensagem = assinaturas.Count switch
                {
                    0 => "Nenhuma assinatura visível para este login.",
                    1 => "Há uma assinatura. Pode seguir para listar_componentes.",
                    _ => "Escolha uma assinatura e chame listar_componentes."
                },
                assinaturas = assinaturas.Select(item => new
                {
                    id = item.Id,
                    nome = item.Nome,
                    estado = item.Estado
                })
            };
            return JsonSerializer.Serialize(payload, Json);
        }
        catch (Exception ex)
        {
            return Erro("Falha ao listar assinaturas.", ex);
        }
    }

    public async Task<string> ListarComponentesAsync(string? assinatura, CancellationToken cancellationToken)
    {
        try
        {
            var todos = EscopoConfigurado(await _inventario.ComponentesAsync(cancellationToken).ConfigureAwait(false));
            var filtrados = Filtrar(todos, assinatura, null);
            if (!string.IsNullOrWhiteSpace(assinatura) && filtrados.Count == 0)
            {
                return JsonSerializer.Serialize(new
                {
                    escolher = true,
                    mensagem = $"Nenhum componente de Application Insights na assinatura '{assinatura.Trim()}'.",
                    assinaturas = todos.Select(item => item.SubscriptionNome).Distinct(StringComparer.OrdinalIgnoreCase)
                }, Json);
            }

            var payload = new
            {
                escolher = filtrados.Select(item => item.SubscriptionId).Distinct(StringComparer.OrdinalIgnoreCase).Count() > 1
                    && string.IsNullOrWhiteSpace(assinatura),
                total = filtrados.Count,
                componentes = filtrados.Take(100).Select(item => new
                {
                    assinatura = item.SubscriptionNome,
                    assinaturaId = item.SubscriptionId,
                    componente = item.Nome,
                    resourceGroup = item.ResourceGroup,
                    localizacao = item.Localizacao
                })
            };
            return JsonSerializer.Serialize(payload, Json);
        }
        catch (Exception ex)
        {
            return Erro("Falha ao listar componentes de Application Insights.", ex);
        }
    }

    public async Task<string> UsuarioConectadoAsync(CancellationToken cancellationToken)
    {
        try
        {
            var token = await _credential.GetTokenAsync(Escopo, cancellationToken).ConfigureAwait(false);
            var identidade = LerIdentidade(token.Token);
            var payload = new
            {
                autenticado = true,
                upn = identidade.Upn,
                nome = identidade.Nome,
                expiraEm = token.ExpiresOn,
                subscriptionId = _config.SubscriptionId,
                tenantId = _config.TenantId,
                resourceGroup = _config.ResourceGroup
            };
            return JsonSerializer.Serialize(payload, Json);
        }
        catch (Exception ex)
        {
            return Erro("Falha ao obter token. Rode az login no tenant da assinatura e conclua o MFA. O login do portal não vale para o CLI.", ex);
        }
    }

    public Task<string> FalhasPorOperacaoAsync(string? assinatura, string? componente, int horas, string? operacao, string? papel, CancellationToken cancellationToken)
    {
        var kql = """
            requests
            | where success == false
            """ + FiltroContem("name", operacao) + FiltroContem("cloud_RoleName", papel) + """
            | summarize Falhas=count(), Usuarios=dcount(user_Id), Ultima=max(timestamp) by ResultCode=tostring(resultCode), Operacao=name, Papel=cloud_RoleName
            | top 30 by Falhas desc
            """;
        return ConsultarAsync(assinatura, componente, horas, kql, cancellationToken);
    }

    public Task<string> FalhasDeDependenciaAsync(string? assinatura, string? componente, int horas, string? nome, string? papel, CancellationToken cancellationToken)
    {
        var kql = """
            dependencies
            | where success == false
            """ + FiltroContem("name", nome) + FiltroContem("cloud_RoleName", papel) + """
            | summarize Falhas=count(), Ultima=max(timestamp) by Tipo=type, Nome=name, ResultCode=tostring(resultCode), Papel=cloud_RoleName
            | top 30 by Falhas desc
            """;
        return ConsultarAsync(assinatura, componente, horas, kql, cancellationToken);
    }

    public Task<string> ExcecoesAsync(string? assinatura, string? componente, int horas, string? texto, string? papel, CancellationToken cancellationToken)
    {
        var kql = """
            exceptions
            | where true
            """ + FiltroContem("outerMessage", texto) + FiltroContem("cloud_RoleName", papel) + """
            | summarize Falhas=count(), Ultima=max(timestamp) by Tipo=type, Mensagem=substring(tostring(outerMessage), 0, 240), Papel=cloud_RoleName
            | top 25 by Falhas desc
            """;
        return ConsultarAsync(assinatura, componente, horas, kql, cancellationToken);
    }

    public Task<string> AmostrasFalhaAsync(string? assinatura, string? componente, int horas, string operacao, string? papel, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(operacao))
        {
            return Task.FromResult("Informe a operação (nome da request ou trecho dele) para amostrar falhas.");
        }

        var kql = """
            requests
            | where success == false
            """ + FiltroContem("name", operacao) + FiltroContem("cloud_RoleName", papel) + """
            | project timestamp, name, resultCode, duration, cloud_RoleName, url, operation_Id
            | take 15
            """;
        return ConsultarAsync(assinatura, componente, horas, kql, cancellationToken);
    }

    public Task<string> ConsultarKqlAsync(string? assinatura, string? componente, int horas, string kql, CancellationToken cancellationToken)
    {
        if (!KqlGuard.TryValidate(kql, out var erro))
        {
            return Task.FromResult(erro);
        }

        return ConsultarAsync(assinatura, componente, horas, kql.Trim(), cancellationToken);
    }

    private async Task<string> ConsultarAsync(string? assinatura, string? componente, int horas, string kql, CancellationToken cancellationToken)
    {
        var resolucao = await ResolverAsync(assinatura, componente, cancellationToken).ConfigureAwait(false);
        if (resolucao.Resposta is not null)
        {
            return resolucao.Resposta;
        }

        var alvo = resolucao.Alvo!;
        var janela = InsightsConfig.ClampHoras(horas);
        var resourceId = new ResourceIdentifier(
            $"/subscriptions/{alvo.SubscriptionId}/resourceGroups/{alvo.ResourceGroup}/providers/microsoft.insights/components/{alvo.Componente}");

        try
        {
            var response = await _logs.QueryResourceAsync(
                resourceId,
                kql,
                new QueryTimeRange(TimeSpan.FromHours(janela)),
                new LogsQueryOptions { ServerTimeout = TimeSpan.FromSeconds(45) },
                cancellationToken).ConfigureAwait(false);

            return Formatar(alvo, janela, response.Value);
        }
        catch (Exception ex)
        {
            return Erro($"Falha ao consultar {alvo.Componente} na assinatura {alvo.SubscriptionNome}.", ex);
        }
    }

    private async Task<(AlvoConsulta? Alvo, string? Resposta)> ResolverAsync(
        string? assinatura,
        string? componente,
        CancellationToken cancellationToken)
    {
        var assinaturaEfetiva = string.IsNullOrWhiteSpace(assinatura) ? _config.SubscriptionId : assinatura;
        if (string.IsNullOrWhiteSpace(componente) && string.IsNullOrWhiteSpace(assinaturaEfetiva))
        {
            return (null, "Escolha a assinatura e o componente. Chame listar_assinaturas e depois listar_componentes.");
        }

        if (string.IsNullOrWhiteSpace(componente))
        {
            return (null, "Escolha o componente. Chame listar_componentes com a assinatura.");
        }

        try
        {
            var todos = EscopoConfigurado(await _inventario.ComponentesAsync(cancellationToken).ConfigureAwait(false));
            var filtrados = Filtrar(todos, assinaturaEfetiva, componente);
            if (filtrados.Count == 1)
            {
                var item = filtrados[0];
                return (new AlvoConsulta("", item.Nome, item.ResourceGroup, item.SubscriptionId, item.SubscriptionNome), null);
            }

            if (filtrados.Count == 0)
            {
                return (null, JsonSerializer.Serialize(new
                {
                    escolher = true,
                    mensagem = "Nenhum componente encontrado com essa assinatura e esse nome.",
                    dica = "Chame listar_assinaturas e listar_componentes."
                }, Json));
            }

            return (null, JsonSerializer.Serialize(new
            {
                escolher = true,
                mensagem = "Há mais de um componente. Escolha a assinatura e o componente e chame de novo.",
                opcoes = filtrados.Take(30).Select(item => new
                {
                    assinatura = item.SubscriptionNome,
                    assinaturaId = item.SubscriptionId,
                    componente = item.Nome,
                    resourceGroup = item.ResourceGroup,
                    localizacao = item.Localizacao
                })
            }, Json));
        }
        catch (Exception ex)
        {
            return (null, Erro("Não foi possível listar assinaturas e componentes para a escolha.", ex));
        }
    }

    private List<ComponenteAzure> EscopoConfigurado(IReadOnlyList<ComponenteAzure> todos)
    {
        IEnumerable<ComponenteAzure> consulta = todos;
        if (!string.IsNullOrWhiteSpace(_config.SubscriptionId))
        {
            consulta = consulta.Where(item => item.SubscriptionId.Equals(_config.SubscriptionId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(_config.ResourceGroup))
        {
            consulta = consulta.Where(item => item.ResourceGroup.Equals(_config.ResourceGroup, StringComparison.OrdinalIgnoreCase));
        }

        return consulta.ToList();
    }

    private static List<ComponenteAzure> Filtrar(IReadOnlyList<ComponenteAzure> todos, string? assinatura, string? componente)
    {
        IEnumerable<ComponenteAzure> consulta = todos;
        if (!string.IsNullOrWhiteSpace(assinatura))
        {
            var texto = assinatura.Trim();
            var exatos = todos.Where(item =>
                item.SubscriptionId.Equals(texto, StringComparison.OrdinalIgnoreCase)
                || item.SubscriptionNome.Equals(texto, StringComparison.OrdinalIgnoreCase)).ToList();
            consulta = exatos.Count > 0
                ? exatos
                : todos.Where(item => item.SubscriptionNome.Contains(texto, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(componente))
        {
            var texto = componente.Trim();
            var lista = consulta.ToList();
            var exatos = lista.Where(item => item.Nome.Equals(texto, StringComparison.OrdinalIgnoreCase)).ToList();
            consulta = exatos.Count > 0
                ? exatos
                : lista.Where(item => item.Nome.Contains(texto, StringComparison.OrdinalIgnoreCase));
        }

        return consulta.ToList();
    }

    private string Formatar(AlvoConsulta alvo, int horas, LogsQueryResult resultado)
    {
        var tabelas = new List<object>();
        foreach (var tabela in resultado.AllTables)
        {
            AdicionarTabela(tabelas, tabela);
        }

        var payload = new
        {
            assinatura = alvo.SubscriptionNome,
            assinaturaId = alvo.SubscriptionId,
            componente = alvo.Componente,
            resourceGroup = alvo.ResourceGroup,
            horas,
            status = resultado.Status.ToString(),
            erro = resultado.Error?.Message,
            tabelas
        };

        return JsonSerializer.Serialize(payload, Json);
    }

    private void AdicionarTabela(List<object> destino, LogsTable? tabela)
    {
        if (tabela is null)
        {
            return;
        }

        var colunas = tabela.Columns.Select(coluna => coluna.Name).ToArray();
        var linhas = new List<Dictionary<string, string?>>();
        var limite = Math.Min(tabela.Rows.Count, _config.MaxRows);
        for (var r = 0; r < limite; r++)
        {
            var row = tabela.Rows[r];
            var item = new Dictionary<string, string?>(colunas.Length, StringComparer.OrdinalIgnoreCase);
            for (var c = 0; c < colunas.Length; c++)
            {
                item[colunas[c]] = Limpar(colunas[c], row[c]?.ToString());
            }

            linhas.Add(item);
        }

        destino.Add(new
        {
            colunas,
            total = tabela.Rows.Count,
            exibidas = linhas.Count,
            linhas
        });
    }

    private static string? Limpar(string coluna, string? valor)
    {
        if (string.IsNullOrEmpty(valor))
        {
            return valor;
        }

        if (coluna.Equals("url", StringComparison.OrdinalIgnoreCase))
        {
            var interrogacao = valor.IndexOf('?', StringComparison.Ordinal);
            if (interrogacao >= 0)
            {
                valor = valor[..interrogacao];
            }
        }

        if (valor.StartsWith("eyJ", StringComparison.Ordinal) || valor.Contains("eyJ", StringComparison.Ordinal) && valor.Count(ch => ch == '.') >= 2 && valor.Length > 80)
        {
            return "[jwt omitido]";
        }

        return valor.Length <= 400 ? valor : valor[..400] + "...";
    }

    private static (string? Upn, string? Nome) LerIdentidade(string jwt)
    {
        var partes = jwt.Split('.');
        if (partes.Length < 2)
        {
            return (null, null);
        }

        var payload = partes[1].Replace('-', '+').Replace('_', '/');
        payload = (payload.Length % 4) switch
        {
            2 => payload + "==",
            3 => payload + "=",
            _ => payload
        };

        using var doc = JsonDocument.Parse(Convert.FromBase64String(payload));
        var raiz = doc.RootElement;
        return (Ler(raiz, "upn") ?? Ler(raiz, "unique_name") ?? Ler(raiz, "preferred_username"), Ler(raiz, "name"));
    }

    private static string? Ler(JsonElement raiz, string nome) =>
        raiz.TryGetProperty(nome, out var valor) ? valor.GetString() : null;

    private static string Erro(string contexto, Exception ex)
    {
        var mensagem = ex.Message.Contains("AADSTS50076", StringComparison.Ordinal)
            ? "O CLI exigiu MFA. Rode az login no tenant da assinatura. O login do portal não vale para o CLI."
            : ex.Message;
        if (mensagem.Length > 500)
        {
            mensagem = mensagem[..500];
        }

        return JsonSerializer.Serialize(new { ok = false, contexto, mensagem }, Json);
    }

    private static string FiltroContem(string coluna, string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return "\n";
        }

        return $"\n| where {coluna} contains {KqlGuard.Literal(texto.Trim())}\n";
    }
}
