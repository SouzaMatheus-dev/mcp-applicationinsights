# McpApplicationInsights

[![NuGet](https://img.shields.io/nuget/v/McpApplicationInsights?logo=nuget&logoColor=white&label=NuGet&color=004880)](https://www.nuget.org/packages/McpApplicationInsights)
[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![C#](https://img.shields.io/badge/C%23-12-239120?logo=csharp&logoColor=white)](https://learn.microsoft.com/dotnet/csharp/)
[![MCP](https://img.shields.io/badge/MCP-1.4-111111)](https://modelcontextprotocol.io)
[![License](https://img.shields.io/github/license/SouzaMatheus-dev/mcp-applicationinsights?color=blue)](LICENSE)

![Falhas](https://img.shields.io/badge/Falhas-leitura-be123c)
![Dependências](https://img.shields.io/badge/Depend%C3%AAncias-leitura-0369a1)
![Exceções](https://img.shields.io/badge/Exce%C3%A7%C3%B5es-leitura-7c3aed)
![Kusto](https://img.shields.io/badge/Kusto-leitura-0f766e)
![Login](https://img.shields.io/badge/Auth-CLI%20local-1d4ed8)

Servidor [MCP](https://modelcontextprotocol.io) somente leitura para Application Insights. Quem usa escolhe a assinatura e o componente visíveis no próprio login. O pacote não escolhe ambiente sozinho.

A credencial fica na máquina (`az login`). O pacote não guarda segredo, assinatura nem token.

Repositório: https://github.com/SouzaMatheus-dev/mcp-applicationinsights

## Escopo

Leitura de falhas de request, falhas de dependência, exceções e consulta Kusto no componente escolhido.

Criar alerta, alterar recurso ou gravar telemetria continua fora deste servidor.

## Requisitos

| Item | Detalhe |
| --- | --- |
| .NET | SDK 8 para desenvolver. A ferramenta instalada usa o runtime exigido pelo `dotnet tool` |
| Login | `az login` concluído, inclusive o MFA, no tenant da assinatura |
| Rede | HTTPS até o inventário de recursos e até a consulta de logs |
| Conta | Acesso de leitura à assinatura e ao componente |

O login do portal não vale para o CLI. Se `usuario_conectado` falhar, rode `az login` de novo.

## Instalação

```powershell
dotnet tool install --global McpApplicationInsights
dotnet tool update --global McpApplicationInsights
```

O comando instalado é `mcp-applicationinsights`.

No Cursor, com a ferramenta global:

```json
{
  "mcpServers": {
    "appinsights": {
      "command": "mcp-applicationinsights"
    }
  }
}
```

Ou via `dnx`, sem instalação prévia. `dnx` exige SDK 10. Em máquina com SDK 8 ou 9, use a ferramenta global.

```json
{
  "mcpServers": {
    "appinsights": {
      "command": "dnx",
      "args": ["McpApplicationInsights", "--yes"]
    }
  }
}
```

## Configuração

Nada disso entra no repositório. Variável vazia significa o padrão.

| Variável | Padrão | Função |
| --- | --- | --- |
| `AZURE_TENANT_ID` | login padrão do CLI | Tenant usado na credencial |
| `AZURE_SUBSCRIPTION_ID` | todas as assinaturas visíveis | Restringe a lista e a consulta |
| `APPINSIGHTS_RESOURCE_GROUP` | todos os grupos | Restringe os componentes |
| `APPINSIGHTS_MAX_ROWS` | `40` | Teto de linhas devolvidas, de 1 a 100 |

Exemplo no Cursor, quando a pessoa já sabe o tenant:

```json
{
  "mcpServers": {
    "appinsights": {
      "command": "mcp-applicationinsights",
      "env": {
        "AZURE_TENANT_ID": "",
        "AZURE_SUBSCRIPTION_ID": "",
        "APPINSIGHTS_RESOURCE_GROUP": ""
      }
    }
  }
}
```

Deixe o valor só na configuração local do cliente. Não commite tenant, assinatura nem token.

## Uso

1. `listar_assinaturas` e espere a escolha.
2. `listar_componentes` com a assinatura escolhida e espere o componente.
3. Só então consulte falhas, exceções ou Kusto.

Se a resposta vier com `escolher: true`, apresente as opções e pare. Não chute assinatura nem componente.

## Catálogo

Todas as ferramentas são leitura. A janela vai de 1 a 168 horas. O padrão é 24.

| Ferramenta | Uso |
| --- | --- |
| `listar_assinaturas` | Assinaturas do login, para a pessoa escolher |
| `listar_componentes` | Componentes da assinatura escolhida |
| `usuario_conectado` | Identidade do token, sem devolver o JWT |
| `falhas_por_operacao` | Falhas de request, no recorte da aba de operações |
| `falhas_de_dependencia` | Dependências com falha |
| `excecoes` | Exceções agrupadas |
| `amostras_falha` | Até 15 requests com falha de uma operação |
| `consultar_kql` | Kusto de leitura no componente escolhido |

`consultar_kql` recusa comando de controle (`.drop`, `.set`, `.delete`, `.create`, `.alter`, `.ingest`).

## Desenvolvimento

```powershell
dotnet test McpApplicationInsights.slnx
```

A publicação no NuGet dispara na tag `v*`. O workflow `.github/workflows/publish-nuget.yml` testa, empacota e envia com Trusted Publishing, na conta `matneves`. O passo a passo da primeira vez está em [docs/publicacao-nuget.md](docs/publicacao-nuget.md).

## Limitações

- A consulta só começa depois da assinatura e do componente. Nome repetido em mais de uma assinatura devolve a lista, em vez de escolher sozinho.
- Listas de inventário mostram até 100 componentes. Cada tabela de resultado respeita `APPINSIGHTS_MAX_ROWS`.
- A query string de URL sai das amostras. JWT encontrado no resultado vira `[jwt omitido]`.
- Este pacote não cobre escrita: alerta, recurso e telemetria continuam fora daqui.

## Solução de problemas

| Sintoma | O que fazer |
| --- | --- |
| `Falha ao obter token` | Rode `az login` no tenant da assinatura e conclua o MFA. |
| `escolher: true` | Mostre as opções e chame de novo com a assinatura e o componente escolhidos. |
| `Nenhum componente encontrado` | Confira o nome em `listar_componentes`. O filtro por ambiente, se existir, está em `AZURE_SUBSCRIPTION_ID` e `APPINSIGHTS_RESOURCE_GROUP`. |
| `Bloqueado: só consulta de leitura` | A Kusto tem comando de controle. Reescreva como leitura. |
| HTTP no inventário | A conta não enxerga a assinatura, ou o login do CLI expirou. |

## Licença

[MIT](LICENSE).
