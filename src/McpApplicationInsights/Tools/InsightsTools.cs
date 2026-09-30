using System.ComponentModel;
using ModelContextProtocol.Server;
using McpApplicationInsights.Services;

namespace McpApplicationInsights.Tools;

[McpServerToolType]
public sealed class InsightsTools(InsightsClient client)
{
    [McpServerTool(Name = "listar_assinaturas"), Description("Lista as assinaturas visíveis para o login. A pessoa escolhe uma antes de consultar.")]
    public Task<string> ListarAssinaturas(CancellationToken cancellationToken) =>
        client.ListarAssinaturasAsync(cancellationToken);

    [McpServerTool(Name = "listar_componentes"), Description("Lista os Application Insights da assinatura escolhida. Sem assinatura, mostra os componentes visíveis para o login.")]
    public Task<string> ListarComponentes(
        [Description("Nome ou id da assinatura. Vazio lista as visíveis no escopo configurado.")] string assinatura = "",
        CancellationToken cancellationToken = default) =>
        client.ListarComponentesAsync(assinatura, cancellationToken);

    [McpServerTool(Name = "usuario_conectado"), Description("Mostra a identidade do login usada nas consultas, sem devolver o token. Se falhar, o MFA do az login ainda não foi concluído.")]
    public Task<string> UsuarioConectado(CancellationToken cancellationToken) =>
        client.UsuarioConectadoAsync(cancellationToken);

    [McpServerTool(Name = "falhas_por_operacao"), Description("Falhas de request. Informe a assinatura e o componente escolhidos. Se houver mais de um, devolve as opções.")]
    public Task<string> FalhasPorOperacao(
        [Description("Nome ou id da assinatura escolhida.")] string assinatura = "",
        [Description("Nome do componente de Application Insights.")] string componente = "",
        [Description("Janela em horas, de 1 a 168. Padrão 24.")] int horas = 24,
        [Description("Trecho do nome da operação. Vazio lista as principais.")] string operacao = "",
        [Description("Trecho de cloud_RoleName. Vazio não filtra.")] string papel = "",
        CancellationToken cancellationToken = default) =>
        client.FalhasPorOperacaoAsync(assinatura, componente, horas, operacao, papel, cancellationToken);

    [McpServerTool(Name = "falhas_de_dependencia"), Description("Falhas de dependência na assinatura e no componente escolhidos.")]
    public Task<string> FalhasDeDependencia(
        [Description("Nome ou id da assinatura escolhida.")] string assinatura = "",
        [Description("Nome do componente de Application Insights.")] string componente = "",
        [Description("Janela em horas, de 1 a 168. Padrão 24.")] int horas = 24,
        [Description("Trecho do nome da dependência.")] string nome = "",
        [Description("Trecho de cloud_RoleName. Vazio não filtra.")] string papel = "",
        CancellationToken cancellationToken = default) =>
        client.FalhasDeDependenciaAsync(assinatura, componente, horas, nome, papel, cancellationToken);

    [McpServerTool(Name = "excecoes"), Description("Exceções na assinatura e no componente escolhidos.")]
    public Task<string> Excecoes(
        [Description("Nome ou id da assinatura escolhida.")] string assinatura = "",
        [Description("Nome do componente de Application Insights.")] string componente = "",
        [Description("Janela em horas, de 1 a 168. Padrão 24.")] int horas = 24,
        [Description("Trecho da mensagem da exceção.")] string texto = "",
        [Description("Trecho de cloud_RoleName. Vazio não filtra.")] string papel = "",
        CancellationToken cancellationToken = default) =>
        client.ExcecoesAsync(assinatura, componente, horas, texto, papel, cancellationToken);

    [McpServerTool(Name = "amostras_falha"), Description("Amostra até 15 requests com falha. Exige a operação e a assinatura/componente escolhidos.")]
    public Task<string> AmostrasFalha(
        [Description("Nome ou trecho da operação.")] string operacao,
        [Description("Nome ou id da assinatura escolhida.")] string assinatura = "",
        [Description("Nome do componente de Application Insights.")] string componente = "",
        [Description("Janela em horas, de 1 a 168. Padrão 24.")] int horas = 24,
        [Description("Trecho de cloud_RoleName. Vazio não filtra.")] string papel = "",
        CancellationToken cancellationToken = default) =>
        client.AmostrasFalhaAsync(assinatura, componente, horas, operacao, papel, cancellationToken);

    [McpServerTool(Name = "consultar_kql"), Description("Kusto de leitura no componente escolhido. Se o nome existir em mais de uma assinatura, devolve as opções.")]
    public Task<string> ConsultarKql(
        [Description("Consulta Kusto de leitura.")] string kql,
        [Description("Nome ou id da assinatura escolhida.")] string assinatura = "",
        [Description("Nome do componente de Application Insights.")] string componente = "",
        [Description("Janela em horas, de 1 a 168. Padrão 24.")] int horas = 24,
        CancellationToken cancellationToken = default) =>
        client.ConsultarKqlAsync(assinatura, componente, horas, kql, cancellationToken);
}
