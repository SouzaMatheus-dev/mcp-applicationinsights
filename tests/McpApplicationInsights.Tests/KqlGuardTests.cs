using McpApplicationInsights.Services;

namespace McpApplicationInsights.Tests;

public class KqlGuardTests
{
    [Fact]
    public void ConsultaDeLeituraPassa()
    {
        var ok = KqlGuard.TryValidate("requests | where success == false | take 10", out var erro);

        Assert.True(ok);
        Assert.Equal("", erro);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void ConsultaVaziaFalha(string? kql)
    {
        var ok = KqlGuard.TryValidate(kql, out var erro);

        Assert.False(ok);
        Assert.Equal("Informe uma consulta Kusto.", erro);
    }

    [Theory]
    [InlineData(".drop table T")]
    [InlineData("requests; .set T <| requests")]
    [InlineData(".delete table T")]
    [InlineData(".create table T (x:string)")]
    [InlineData(".alter table T")]
    [InlineData(".ingest inline into table T")]
    public void ComandoDeControleFalha(string kql)
    {
        var ok = KqlGuard.TryValidate(kql, out var erro);

        Assert.False(ok);
        Assert.Contains("Bloqueado", erro);
    }

    [Fact]
    public void ConsultaLongaDemaisFalha()
    {
        var ok = KqlGuard.TryValidate(new string('a', 8001), out var erro);

        Assert.False(ok);
        Assert.Equal("Consulta acima de 8000 caracteres.", erro);
    }

    [Fact]
    public void LiteralEscapaAspas()
    {
        Assert.Equal("'o''brien'", KqlGuard.Literal("o'brien"));
    }
}
