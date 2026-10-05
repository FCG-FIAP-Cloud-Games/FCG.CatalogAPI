# FCG.CatalogAPI

Microsserviço do FIAP Cloud Games responsável pelo catálogo de jogos, pedidos de compra e biblioteca do usuário.

## Responsabilidades

- Criar, consultar, listar e atualizar jogos.
- Registrar pedidos com o preço atual do catálogo e idempotência.
- Consultar os jogos adquiridos pelo usuário autenticado.
- Publicar `OrderPlacedEvent` no RabbitMQ por meio de uma Outbox persistida no PostgreSQL.

## Stack

.NET 8, ASP.NET Core, EF Core 8 com Npgsql/PostgreSQL, RabbitMQ.Client, JWT RS256, Swagger e xUnit. As versões dos pacotes estão em `Directory.Packages.props`.

## Pré-requisitos

- .NET SDK 8 e runtime ASP.NET Core 8.
- PostgreSQL acessível e banco exclusivo do CatalogAPI.
- RabbitMQ acessível, com usuário e virtual host configurados, para publicar eventos.
- Chave pública RSA e JWT emitido por um emissor confiável para acessar rotas protegidas.

Execute os comandos abaixo na raiz do repositório. Os exemplos de configuração usam PowerShell; substitua os placeholders antes de executá-los.

## Configuração

Forneça as configurações por variáveis de ambiente. O `appsettings.Development.json` altera apenas os níveis de log.

| Variável | Uso / padrão versionado |
| --- | --- |
| `ConnectionStrings__CatalogDatabase` | Conexão PostgreSQL do catálogo; vazia por padrão. |
| `RabbitMq__Host` | Host do broker; `localhost`. |
| `RabbitMq__Port` | Porta AMQP; `5672`. |
| `RabbitMq__VirtualHost` | Virtual host; `/`. |
| `RabbitMq__Username` | Usuário do broker; vazio por padrão. |
| `RabbitMq__Password` | Senha do broker; vazia por padrão. |
| `RabbitMq__Exchange` | Exchange de publicação; `OrderPlacedEvent`. |
| `Outbox__Enabled` | Habilita o publicador em segundo plano; `true`. |
| `Jwt__Issuer` | Emissor aceito; `FIAP.CloudGames`. |
| `Jwt__Audience` | Audiência aceita; `FIAP.CloudGames.Api`. |
| `Jwt__ClockSkewSeconds` | Tolerância de horário, não negativa; `30` segundos. |
| `Jwt__PublicKeys__0__Kid` | Identificador da chave pública, correspondente ao `kid` do token. |
| `Jwt__PublicKeys__0__PublicKeyPem` | Conteúdo PEM da chave pública RSA; coleção de chaves vazia por padrão. |
| `ASPNETCORE_ENVIRONMENT` | Use `Development` para habilitar Swagger. |

```powershell
$env:ConnectionStrings__CatalogDatabase = 'Host=<host>;Port=<porta>;Database=<banco-catalogo>;Username=<usuario>;Password=<senha>'
$env:RabbitMq__Host = '<host>'
$env:RabbitMq__Port = '5672'
$env:RabbitMq__VirtualHost = '<vhost>'
$env:RabbitMq__Username = '<usuario>'
$env:RabbitMq__Password = '<senha>'
$env:RabbitMq__Exchange = 'OrderPlacedEvent'
$env:Outbox__Enabled = 'true'
$env:Jwt__Issuer = '<issuer>'
$env:Jwt__Audience = '<audience>'
$env:Jwt__PublicKeys__0__Kid = '<kid>'
$env:Jwt__PublicKeys__0__PublicKeyPem = Get-Content -Raw '<caminho-public-key.pem>'
```

O PEM deve conter quebras de linha reais. Para mais chaves, use índices `1`, `2`, etc., com `Kid` único. Configure somente chaves públicas e reinicie a aplicação após alterar essa configuração. Não grave credenciais no repositório.

## PostgreSQL e migrations

O CatalogAPI usa banco próprio para catálogo, pedidos, aquisições e Outbox. Configure `ConnectionStrings__CatalogDatabase` antes dos comandos EF. O usuário precisa de permissão para alterar o schema; se o banco ainda não existir, precisa também de `CREATEDB`, ou o banco deve ser criado previamente.

```powershell
dotnet restore
dotnet tool restore

# Listar migrations
dotnet ef migrations list --project src/FCG.Catalog.Infrastructure --startup-project src/FCG.Catalog.Api --context CatalogDbContext

# Aplicar migrations
dotnet ef database update --project src/FCG.Catalog.Infrastructure --startup-project src/FCG.Catalog.Api --context CatalogDbContext

# Verificar divergências entre o modelo e a última migration
dotnet ef migrations has-pending-model-changes --project src/FCG.Catalog.Infrastructure --startup-project src/FCG.Catalog.Api --context CatalogDbContext
```

O manifesto local fornece `dotnet-ef` 8.0.28. A aplicação não aplica migrations automaticamente ao iniciar.

## RabbitMQ e Outbox

```text
CatalogAPI → Outbox → OrderPlacedEvent → RabbitMQ
```

Configure a conexão pelas variáveis `RabbitMq__*`. O publicador declara o exchange configurado como `fanout`, durável e sem exclusão automática. O usuário do broker precisa de acesso ao virtual host e permissão para declarar o exchange e publicar.

A API não cria filas nem bindings. Provisione uma fila e seu binding ao exchange para receber os eventos. Sem rota disponível ou com falha no broker, as mensagens continuam pendentes para novas tentativas.

Pedido e evento são gravados juntos no banco; um serviço em segundo plano publica a Outbox. A entrega pode se repetir, portanto consumidores devem deduplicar por `EventId`. Com `Outbox__Enabled=false`, a publicação fica desativada, mas novos pedidos continuam gravando eventos na Outbox.

## Autenticação

Envie `Authorization: Bearer <token>` nas rotas protegidas. O CatalogAPI não emite tokens: obtenha um JWT do emissor correspondente à configuração.

- A validação é local, com RS256, chave pública selecionada pelo `kid`, issuer, audience e validade do token. `exp` é obrigatório.
- A identidade vem de um único claim `sub`, que deve ser um GUID válido e não vazio.
- A autorização usa o claim `role`; `Administrador` diferencia maiúsculas e minúsculas.
- Não existe chamada síncrona ao UsersAPI para validar o token.
- Sem chaves públicas configuradas, o host inicia, mas nenhum JWT é aceito. Chaves incompletas, privadas, inválidas ou com `Kid` duplicado impedem a inicialização.

Rotas protegidas retornam `401` sem token válido e `403` quando o usuário não tem a permissão necessária.

## Como rodar

Após configurar o ambiente e aplicar as migrations:

```powershell
dotnet restore
dotnet build
$env:ASPNETCORE_ENVIRONMENT = 'Development'
dotnet run --project src/FCG.Catalog.Api --no-launch-profile --urls 'http://localhost:<porta>'
```

Substitua `<porta>` por uma porta livre. O repositório não possui `launchSettings.json` nem fixa uma URL de escuta; o comando acima define a URL para essa execução.

- Health: `GET /health`, público. Verifica apenas o host, sem validar PostgreSQL ou RabbitMQ.
- Swagger: `/swagger/index.html` e `/swagger/v1/swagger.json`, disponíveis somente em `Development`.

## Endpoints

IDs nas rotas são GUIDs. Nos exemplos HTTP, use a URL definida ao iniciar a API.

### Jogos

| Método | Rota | Autenticação | Descrição |
| --- | --- | --- | --- |
| GET | `/api/v1/jogos` | Pública | Listar jogos com paginação. |
| GET | `/api/v1/jogos/{id}` | Pública | Consultar jogo por ID. |
| POST | `/api/v1/jogos` | JWT, role `Administrador` | Criar jogo. |
| PUT | `/api/v1/jogos/{id}` | JWT, role `Administrador` | Atualizar jogo. |

Paginação: `?pagina=1&tamanhoPagina=20`, com páginas a partir de 1 e tamanho entre 1 e 100.

Corpo JSON de criação e atualização:

```json
{ "titulo": "Jogo exemplo", "descricao": "Descrição", "faixaEtaria": "12", "preco": 49.90 }
```

Título obrigatório, com até 150 caracteres, e preço não negativo. Descrição e faixa etária são opcionais; os limites de armazenamento são 500 e 2 caracteres, respectivamente.

### Pedidos

| Método | Rota | Autenticação | Descrição |
| --- | --- | --- | --- |
| POST | `/api/v1/pedidos` | JWT | Criar ou recuperar pedido pela chave de idempotência. |
| GET | `/api/v1/pedidos/{id}` | JWT, titular ou `Administrador` | Consultar pedido. |

### Biblioteca

| Método | Rota | Autenticação | Descrição |
| --- | --- | --- | --- |
| GET | `/api/v1/biblioteca` | JWT | Listar a própria biblioteca. |

### Health

| Método | Rota | Autenticação | Descrição |
| --- | --- | --- | --- |
| GET | `/health` | Pública | Verificar se o host responde. |

## Fluxo de pedidos

```text
POST /api/v1/pedidos → preço obtido pelo CatalogAPI → Pedido PendingPayment
                    → Outbox → OrderPlacedEvent
```

Exemplo de requisição:

```http
POST /api/v1/pedidos
Authorization: Bearer <token>
Idempotency-Key: <uuid-nao-vazio>
Content-Type: application/json

{ "jogoId": "<guid-do-jogo>" }
```

O usuário vem do `sub`; o preço vem do catálogo e a moeda é `BRL`. A criação retorna `202` com `Location` para consultar o pedido.

- `Idempotency-Key` é obrigatório e deve conter um único UUID não vazio.
- Repetir a chave para o mesmo usuário e jogo recupera o pedido original: `202` se pendente ou `200` se finalizado. Usá-la para outro jogo retorna `409`.
- Uma nova chave para jogo já adquirido ou com pedido pendente retorna `409`, sem criar outro pedido. Jogo inativo retorna `409`; inexistente, `404`.
- Criar um pedido não concede o jogo imediatamente. O projeto não contém consumidor de pagamento nem integração que finalize pedidos e conceda jogos automaticamente.

## Biblioteca do usuário

`GET /api/v1/biblioteca` usa exclusivamente o `sub` do JWT e retorna apenas a biblioteca do próprio usuário, inclusive para administradores. A posse é representada por `Aquisicao` no banco do catálogo.

A resposta contém `gameId`, `title` e `acquiredAt`, ou uma lista vazia. Não há paginação nem endpoint público de concessão de jogos.

## Testes

```powershell
dotnet test
```

A solução inclui testes unitários e de integração. Quando `ConnectionStrings__CatalogDatabase` está definida, testes de leitura verificam o schema desse banco; mantenha suas migrations aplicadas.

### Testes de integração externos

Alguns testes usam PostgreSQL e RabbitMQ reais. Configure somente ambientes de desenvolvimento descartáveis:

| Variável | Configuração |
| --- | --- |
| `CATALOG_TEST_CONNECTION` | Connection string PostgreSQL de manutenção; usuário com permissão para criar e remover bancos de teste. |
| `RABBITMQ_TEST_CONNECTION` | URI `amqp://<usuario>:<senha>@<host>:<porta>/<vhost>` de um virtual host exclusivo, vazio e descartável. Também exige `CATALOG_TEST_CONNECTION`. |

Após configurar essas variáveis, execute `dotnet test`. Sem as variáveis necessárias, os respectivos testes externos são ignorados.
