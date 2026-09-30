using McpApplicationInsights.Services;

namespace McpApplicationInsights.Tests;

public class InsightsConfigTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(24, 24)]
    [InlineData(168, 168)]
    [InlineData(999, 168)]
    public void JanelaFicaEntreUmaHoraESeteDias(int horas, int esperado)
    {
        Assert.Equal(esperado, InsightsConfig.ClampHoras(horas));
    }

    [Fact]
    public void TetoDeLinhasRespeitaOAmbiente()
    {
        var anterior = Environment.GetEnvironmentVariable("APPINSIGHTS_MAX_ROWS");
        try
        {
            Environment.SetEnvironmentVariable("APPINSIGHTS_MAX_ROWS", "12");
            var config = new InsightsConfig();
            Assert.Equal(12, config.MaxRows);
        }
        finally
        {
            Environment.SetEnvironmentVariable("APPINSIGHTS_MAX_ROWS", anterior);
        }
    }
}
