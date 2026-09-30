# Publicação NuGet

| Campo | Valor |
| --- | --- |
| **PackageId** | `McpApplicationInsights` |
| **Tipo** | .NET Global Tool (`DotnetTool`) |
| **Comando CLI** | `mcp-applicationinsights` |
| **Target** | `net8.0` |
| **Conta** | `matneves` |

## Consumir

```powershell
dotnet tool install --global McpApplicationInsights
dotnet tool update --global McpApplicationInsights
```

## Primeira publicação

O pacote ainda não existe. Antes da tag `v0.1.0`, cadastre o Trusted Publishing em [nuget.org](https://www.nuget.org/account/publishers):

| Campo | Valor |
| --- | --- |
| Proprietário do GitHub | `SouzaMatheus-dev` |
| Repositório | `mcp-applicationinsights` |
| Arquivo do workflow | `.github/workflows/publish-nuget.yml` |
| Usuário NuGet | `matneves` |

Depois atualize `<Version>` em `src/McpApplicationInsights/McpApplicationInsights.csproj` e envie a tag:

```powershell
git tag v0.1.0
git push origin v0.1.0
```

O workflow testa, empacota e publica. A release no GitHub sai na mesma execução.
