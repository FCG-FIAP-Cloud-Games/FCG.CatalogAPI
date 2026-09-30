# FCG.CatalogAPI

Fundação técnica do microsserviço CatalogAPI do FIAP Cloud Games, criada no **Card C13**, com .NET 8 e ASP.NET Core Controllers.

## Responsabilidades e estado da extração

Futuramente o serviço será responsável pelo catálogo de jogos, pedido de compra, biblioteca do usuário e concessão do jogo após pagamento. O processamento financeiro não é responsabilidade deste serviço.

Neste card nenhum domínio foi extraído do monólito: não há entidades Jogo, Categoria ou CategoriaJogo, casos de uso, pedidos, biblioteca, persistência ou integrações. O host inicia de forma independente, sem monólito, UsersAPI, UsersDB ou qualquer banco/broker.

Não há EF Core, PostgreSQL, migrations, RabbitMQ, Inbox/Outbox, Docker ou Kubernetes. As pastas vazias são preservadas por `.gitkeep`, sem abstrações artificiais.

## Estrutura

```text
FCG.CatalogAPI/
├── src/
│   ├── FCG.Catalog.Api/
│   │   ├── Authentication/
│   │   ├── Controllers/
│   │   ├── Program.cs
│   │   ├── appsettings.json
│   │   └── appsettings.Development.json
│   ├── FCG.Catalog.Application/
│   │   ├── Abstractions/Repositories/
│   │   ├── Rules/
│   │   └── UseCases/
│   ├── FCG.Catalog.Domain/
│   │   ├── Catalog/
│   │   ├── Orders/
│   │   └── Library/
│   └── FCG.Catalog.Infrastructure/
│       ├── Data/
│       ├── Repositories/
│       ├── Messaging/
│       └── IoC/
├── tests/
│   ├── FCG.Catalog.UnitTests/
│   └── FCG.Catalog.IntegrationTests/
├── FCG.Catalog.sln
├── Directory.Build.props
├── Directory.Packages.props
├── .gitignore
└── README.md
```

Cada diretório de projeto contém seu `.csproj`. Referências de produção:

- Api → Application e Infrastructure.
- Application → Domain.
- Infrastructure → Application e Domain.
- Domain → nenhuma referência de projeto ou pacote.

UnitTests referencia Application e Domain; IntegrationTests referencia Api. Todas as referências permanecem dentro desta solução, sem ciclos.

## Executar e validar

Pré-requisito: SDK .NET 8 ou posterior compatível, com runtime ASP.NET Core/.NET 8 instalado. Todos os projetos têm `TargetFramework=net8.0`; não é necessário instalar serviços externos.

Na raiz do repositório:

```powershell
dotnet restore
dotnet build
dotnet test

$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/FCG.Catalog.Api --no-launch-profile --urls http://localhost:5080
```

Em outro terminal:

```powershell
Invoke-WebRequest http://localhost:5080/health
```

`GET /health` é público e retorna HTTP 200 com `Healthy`. É um health check do host; não verifica dependências externas neste card.

Swagger UI: `http://localhost:5080/swagger/index.html`. Documento OpenAPI: `http://localhost:5080/swagger/v1/swagger.json`. Ambos disponíveis somente em Development. Sem controllers de negócio neste card, o documento inicialmente não contém operações. O endpoint de health é mapeado via Health Checks.

O pipeline inclui Problem Details, tratamento de exceções, páginas de status, autenticação e autorização. Futuros controllers protegidos devem usar `[Authorize]`.

Testes de integração hospedam a API e verificam health sem chave em Development/Production, Swagger por ambiente, Problem Details e autenticação local básica em um controller exclusivo dos testes. As chaves dos testes são geradas em memória e descartadas. UnitTests está preparado, mas sem testes até existirem regras de negócio.

## Configuração por ambiente

O host usa a configuração padrão do ASP.NET Core: `appsettings.json`, `appsettings.{Environment}.json`, variáveis de ambiente e argumentos da linha de comando. Variáveis usam `__` para separar níveis e sobrescrevem os arquivos JSON, por exemplo `Jwt__Issuer`, `Jwt__Audience`, `Jwt__ClockSkewSeconds` e `Jwt__PublicKeyPem`. `ASPNETCORE_ENVIRONMENT` seleciona o ambiente; na ausência de configuração o padrão é Production. `ASPNETCORE_URLS` também pode definir os endereços do host.

`appsettings.Development.json` ajusta somente os logs. Não contém credenciais. O `.gitignore` ignora arquivos usuais de chave e configurações locais; `appsettings.Local.json` não é carregado automaticamente.

## JWT: base do C13

Configuração padrão:

```json
"Jwt": {
  "Issuer": "FIAP.CloudGames",
  "Audience": "FIAP.CloudGames.Api",
  "ClockSkewSeconds": 30,
  "PublicKeyPem": ""
}
```

`AddAuthentication`/`AddJwtBearer` estão preparados para **RS256**, com validação de assinatura, emissor, audiência, expiração e tolerância de 30 segundos. O algoritmo fica restrito a RS256 no código; não há segredo simétrico, chave privada, Authority, discovery ou chamada HTTP ao UsersAPI.

Uma única chave pública RSA em formato PEM pode ser fornecida externamente por `Jwt__PublicKeyPem`, com quebras de linha reais (não a sequência literal `\n`). Por exemplo, quando houver uma chave pública distribuída fora deste repositório:

```powershell
$env:Jwt__PublicKeyPem = Get-Content -Raw "C:\config\catalog-public.pem"
```

Reinicie o host após alterar a chave. Uma chave fornecida inválida ou privada causa erro na inicialização. Nenhuma chave real acompanha este repositório.

**Sem chave pública, o scaffold inicia normalmente e health/Swagger continuam acessíveis; tokens não são aceitos em endpoints protegidos.** Com uma chave pública fornecida, a base já valida assinaturas RS256 localmente. O UsersAPI será o único emissor. A estratégia completa de validação efetiva do ambiente, distribuição/rotação, múltiplas chaves e seleção por `kid` permanece no **C14**; nada disso foi implementado aqui.

## Dependências

Versões centralizadas em `Directory.Packages.props`:

| Pacote | Versão | Uso |
| --- | --- | --- |
| Microsoft.AspNetCore.Authentication.JwtBearer | 8.0.28 | JWT local |
| Swashbuckle.AspNetCore | 6.9.0 | Swagger/OpenAPI |
| Microsoft.AspNetCore.Mvc.Testing | 8.0.28 | Host de integração |
| Microsoft.NET.Test.Sdk | 17.11.1 | Execução dos testes |
| xunit | 2.9.3 | Framework de testes |
| xunit.runner.visualstudio | 2.8.2 | Descoberta e execução xUnit |

Controllers, Health Checks e Problem Details usam o framework compartilhado ASP.NET Core, sem pacotes adicionais. `Directory.Build.props` concentra net8.0, nullable e implicit usings.
