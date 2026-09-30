using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using McpApplicationInsights.Services;
using McpApplicationInsights.Tools;

var builder = Host.CreateEmptyApplicationBuilder(settings: null);

builder.Logging.AddConsole(options =>
{
    options.LogToStandardErrorThreshold = LogLevel.Trace;
});

builder.Services.AddSingleton<InsightsConfig>();
builder.Services.AddSingleton<InsightsClient>();
builder.Services.AddSingleton<InsightsTools>();
builder.Services
    .AddMcpServer(options =>
    {
        options.ServerInstructions =
            "Application Insights somente leitura. Comece por listar_assinaturas e peça para a pessoa escolher. " +
            "Depois listar_componentes com a assinatura escolhida e peça o componente. " +
            "Só então chame falhas_por_operacao, falhas_de_dependencia, excecoes, amostras_falha ou consultar_kql. " +
            "Se a ferramenta devolver escolher=true, pare e apresente as opções. " +
            "consultar_kql é só leitura. Não peça segredo nem token.";
    })
    .WithStdioServerTransport()
    .WithToolsFromAssembly();

await builder.Build().RunAsync();
