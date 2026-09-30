using System.Text.RegularExpressions;

namespace McpApplicationInsights.Services;

public static partial class KqlGuard
{
    public static bool TryValidate(string? kql, out string erro)
    {
        if (string.IsNullOrWhiteSpace(kql))
        {
            erro = "Informe uma consulta Kusto.";
            return false;
        }

        var texto = kql.Trim();
        if (texto.Length > 8000)
        {
            erro = "Consulta acima de 8000 caracteres.";
            return false;
        }

        if (ComandoDeControle().IsMatch(texto))
        {
            erro = "Bloqueado: só consulta de leitura. Comandos de controle (.drop, .set, .delete, .create, .alter, .ingest) não são aceitos.";
            return false;
        }

        erro = "";
        return true;
    }

    public static string Literal(string valor) => "'" + valor.Replace("'", "''", StringComparison.Ordinal) + "'";

    [GeneratedRegex(@"(?i)(^|[\s;])\.(drop|set|delete|create|alter|ingest|update|append|rename|cancel|execute|drop-table)\b")]
    private static partial Regex ComandoDeControle();
}
