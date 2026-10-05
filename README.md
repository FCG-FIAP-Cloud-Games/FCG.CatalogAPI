# FCG.CatalogAPI

Microsserviço .NET 8 do FIAP Cloud Games. C13 forneceu o host; C14 migrou domínio, operações e segurança; C15 adicionou PostgreSQL; C16 implementa pedidos de compra; C17 adiciona publicação confiável com Outbox.

## Escopo atual (C14 + C15 + C16 + C17)

- Entidades `Jogo`, `Categoria` e `CategoriaJogo`, preservando propriedades, construtores, IDs e invariantes.
- `IRepositorioJogos`, comandos, consultas, resultados e handlers de criação, consulta por ID, listagem paginada e atualização.
- Contratos HTTP e `JogosController` preservados, com proteção explícita nas escritas.
- Persistência EF Core 8/Npgsql exclusiva para jogos, categorias e pedidos. Outbox própria e publisher RabbitMQ; biblioteca persistente permanece fora deste escopo.

## Rotas

| Método | Rota | Acesso | Respostas de negócio |
| --- | --- | --- | --- |
| POST | `/api/v1/jogos` | role `Administrador` | 201 e Location; 400 por dados inválidos |
| GET | `/api/v1/jogos/{id}` | Público | 200; 400 por GUID vazio; 404 se inexistente |
| GET | `/api/v1/jogos` | Público | 200; 400 por paginação inválida |
| PUT | `/api/v1/jogos/{id}` | role `Administrador` | 200; 400 por dados inválidos; 404 se inexistente |
| POST | `/api/v1/pedidos` | JWT válido | 202 pendente; 200 replay terminal; 400/404/409; 503 enquanto C18 não fornecer consulta de posse |
| GET | `/api/v1/pedidos/{id}` | Titular ou `Administrador` | 200; 403 para outro usuário; 404 inexistente |

POST/PUT retornam 401 sem token ou com token inválido; 403 com token válido sem a role exigida. O nome da role diferencia maiúsculas/minúsculas. IDs de rota usam a restrição `guid`; formato inválido não corresponde à rota (404).

Paginação: `pagina=1`, `tamanhoPagina=20`, máximo 100. Título obrigatório, normalizado com Trim nos handlers, máximo 150 caracteres; preço não negativo. Descrição e faixa etária opcionais são normalizadas. Atualizar preserva ID, data de cadastro e estado ativo. Não existem DELETE nem endpoints de categorias.

## Persistência e DI

A API registra CatalogDbContext e IRepositorioJogos → RepositorioJogos com lifetime scoped. O contexto aplica explicitamente os mappings de Jogo, Categoria, CategoriaJogo, Pedido e MensagemOutbox. Consultas por ID são rastreadas; listagem usa AsNoTracking, ordenação por título e paginação; criação/atualização chamam SaveChangesAsync. IDs continuam Guid com ValueGeneratedNever.

O banco pertence exclusivamente ao CatalogAPI. A migration inicial cria jogos, tb_Categorias e rel_CategoriaJogo, além do histórico padrão do EF (__EFMigrationsHistory). As duas FKs internas têm cascade e o par JogoId/CategoriaId é único. Não existem entidades, FKs, conexão ou referências de projeto para Users.

Health e Swagger não abrem conexão. Sem ConnectionStrings:CatalogDatabase, uma operação que resolve o contexto falha com mensagem de configuração. Não há migration automática na inicialização.

Referências: Api → Application e Infrastructure; Application → Domain; Infrastructure → Application e Domain. Sem referência ao monólito, UsersAPI ou UsersDB. Domain não depende de pacotes externos.

## JWT e rotação por kid

Configuração versionada, sem chaves reais:

```json
"Jwt": {
  "Issuer": "FIAP.CloudGames",
  "Audience": "FIAP.CloudGames.Api",
  "ClockSkewSeconds": 30,
  "PublicKeys": []
}
```

Cada entrada de `PublicKeys` possui `Kid` e `PublicKeyPem`. Forneça as chaves públicas confiáveis externamente, por exemplo:

```powershell
$env:Jwt__PublicKeys__0__Kid = "users-key-1"
$env:Jwt__PublicKeys__0__PublicKeyPem = Get-Content -Raw "C:\config\users-key-1-public.pem"
$env:Jwt__PublicKeys__1__Kid = "users-key-2"
$env:Jwt__PublicKeys__1__PublicKeyPem = Get-Content -Raw "C:\config\users-key-2-public.pem"
```

Use quebras de linha reais no PEM. A configuração antiga `Jwt:PublicKeyPem` foi substituída pela coleção. Reinicie o host após mudar a configuração. Para rotação, configure as duas chaves durante a sobreposição e remova a antiga após expiração dos tokens correspondentes.

- Apenas RS256, assinatura RSA, issuer, audience, exp obrigatório e nbf quando presente, com ClockSkew de 30 segundos.
- O `kid` seleciona exatamente uma chave local por comparação ordinal. Ausência, valor desconhecido ou assinatura que não corresponde à chave selecionada resulta em 401. Não há fallback ou uso de chaves/URLs do token.
- `sub` deve ser um único GUID válido diferente de Guid.Empty. Essa é a interpretação adotada no C14 para identificador válido; não há entidade de usuário nem consulta externa.
- Claims não são remapeadas: identidade usa `sub` e autorização usa `role`.
- Coleção vazia permite iniciar o host, mas nenhum token é aceito. Entradas incompletas, kids duplicados, PEM inválido ou privado falham na inicialização.
- Nenhuma chave privada, Authority, discovery, introspection, UsersAPI ou UsersDB é usada pela API.

## Executar e validar

Requer SDK compatível e runtime .NET/ASP.NET Core 8.

```powershell
dotnet restore
dotnet build
dotnet test
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/FCG.Catalog.Api --no-launch-profile --urls http://localhost:5080
```

`GET /health` retorna 200 e `Healthy`; verifica o host, sem banco. Em Development, Swagger está em `/swagger/index.html` (ou `/swagger`) e `/swagger/v1/swagger.json`. Em Production, Swagger retorna 404.

Os testes unitários cobrem domínio e os quatro handlers. Os testes de integração cobrem contratos, paginação, erros, 401/403, role administrativa, duas chaves por kid e tokens inválidos. Chaves RSA são geradas e descartadas somente em memória nos testes. Health e Swagger também são testados sem conexão configurada e sem chaves configuradas.

Versões de pacotes centralizadas em `Directory.Packages.props`.

## PostgreSQL local e migrations (C15)

Use um banco exclusivo do catálogo, nunca o banco do monólito ou UsersAPI. PostgreSQL deve estar disponível; este card não instala nem cria containers.
Configure externamente a connection string completa por ConnectionStrings__CatalogDatabase. O appsettings versionado contém somente uma string vazia.

Exemplo PowerShell (substitua host, usuário e nome do banco; a senha é solicitada sem eco e não é gravada em arquivo):

~~~powershell
$credential = Get-Credential -UserName postgres -Message "Credenciais do PostgreSQL local"
# Quoting SQL-style permite caracteres especiais na senha.
$passwordValue = $credential.GetNetworkCredential().Password.Replace("'", "''")
$env:ConnectionStrings__CatalogDatabase = "Host=localhost;Port=5433;Database=fcg_catalog;Username=postgres;Password='$passwordValue'"
Remove-Variable passwordValue, credential
dotnet tool restore
dotnet restore
dotnet build
dotnet ef migrations list --project src/FCG.Catalog.Infrastructure --startup-project src/FCG.Catalog.Api --context CatalogDbContext
dotnet ef database update --project src/FCG.Catalog.Infrastructure --startup-project src/FCG.Catalog.Api --context CatalogDbContext
dotnet test
~~~

Execute da raiz do repositório. Se o banco não existir, database update requer um usuário PostgreSQL com CREATEDB; alternativamente o administrador pode criar previamente um banco vazio pertencente ao usuário do catálogo. As migrations exigem permissão DDL nesse banco. O manifesto local fixa dotnet-ef 8.0.28; o startup project fornece o pacote Design. Nenhuma factory de design-time ou credencial padrão é necessária.

## Teste com PostgreSQL real

O teste PostgreSqlPersistenceTests usa o provider Npgsql e a API com o registro de produção, sem substituir o repositório. Cria um banco catalog_c15_test_<guid> a partir da conexão de manutenção informada, aplica migrations e remove apenas esse banco em finally. Requer CREATEDB e permissão para excluir o banco criado. A conexão de manutenção deve ser um banco local de desenvolvimento autorizado (por exemplo postgres).

~~~powershell
# Defina CATALOG_TEST_CONNECTION externamente com as credenciais de desenvolvimento.
dotnet test --filter FullyQualifiedName~PostgreSqlPersistenceTests
~~~

Sem CATALOG_TEST_CONNECTION, esse teste é explicitamente ignorado. Os testes do C14 continuam ativos. O teste de modelo Npgsql e geração de SQL não precisa de servidor.
O teste real verifica migrations, DI, POST/GET/listagem/PUT, JWT administrativo, leitura pública, dados persistidos em novo contexto, relação, unicidade, FK, tamanho do título, cascade e tabelas existentes.
A API mantém os limites físicos herdados: título 150, descrição 500, faixa etária 2, categoria 200 e preço numeric(18,2). Validações HTTP do C14 permanecem: descrição/faixa etária acima dos limites físicos são rejeitadas pelo banco, não por uma nova validação HTTP deste card.

O teste Configured_catalog_database_has_only_expected_schema é somente leitura e valida a base apontada por ConnectionStrings__CatalogDatabase após database update. Também é ignorado quando a variável não existe. Para executar toda a validação local usando o usuário postgres com CREATEDB:

~~~powershell
$env:CATALOG_TEST_CONNECTION = $env:ConnectionStrings__CatalogDatabase
dotnet test
~~~

## Pedidos (C16)

O POST recebe somente `{ "jogoId": "<guid>" }` e exige um único header `Idempotency-Key` UUID não vazio. UserId vem exclusivamente do `sub` validado pelo JWT; preço é uma cópia do preço atual do jogo e moeda é BRL. Campos extras no JSON não alteram essas fontes. O pedido nasce PendingPayment; Paid e Rejected são terminais. Não há endpoint para mudar status. A resposta contém OrderId, GameId, Price, Currency, Status, CreatedAt e UpdatedAt.

A mesma chave para o mesmo usuário/jogo recupera o original antes de verificar jogo, posse e pendência: 202 para PendingPayment e 200 para Paid/Rejected. Outra combinação de jogo com essa chave resulta em 409. Usuários diferentes podem reutilizar o UUID. Nova chave para jogo já possuído ou com pedido pendente resulta em 409. Jogo inexistente retorna 404; inativo retorna 409. 202 inclui Location `/api/v1/pedidos/{id}`. POST e GET exigem JWT, retornando 401 se ausente/inválido. GET permite titular e Administrador, retorna 403 para outro usuário e 404 para pedido inexistente.

**Dependência temporária do C18:** IConsultaBiblioteca está definida na Application, sem implementação/registro de produção. O POST com entrada válida retorna 503 Problem Details até essa implementação ser conectada. Os testes registram fakes somente nos hosts de teste. GET, jogos, health e Swagger permanecem disponíveis. Não criar uma implementação que sempre retorne false para habilitar o POST em produção.

Para uma chave nova, ManipuladorCriarPedido inicia transação READ COMMITTED, adquire ILockUsuarioJogo, reconsulta a chave, lê o jogo novamente sem tracking, consulta posse, reconsulta pendência, salva e faz commit. Dispose sem commit desfaz a transação. As duas constraints únicas reconhecidas são traduzidas para recuperação do original ou 409 após rollback, inclusive quando a mesma chave concorre por jogos diferentes.

LockUsuarioJogo usa `pg_advisory_xact_lock(bigint)` no mesmo CatalogDbContext/transação. Contrato para reutilização pelo C19: UTF-8 de `catalog:usuario-jogo:v1:{userId:N}:{gameId:N}`, UUIDs minúsculos, SHA-256, primeiros oito bytes interpretados como Int64 big endian. Colisões apenas serializam pares independentes; constraints preservam unicidade. O PostgreSQL libera o lock no commit/rollback. C19 deve reutilizar ILockUsuarioJogo e essa implementação, sem trocar algoritmo/namespace.

Migration `20261004194925_AddPedidos` adiciona somente `pedidos`: Price numeric(18,2), Currency varchar(3), Status varchar(20), datas timestamptz, UUIDs; unique `(UserId, IdempotencyKey)` e unique parcial `(UserId, GameId) WHERE "Status" = 'PendingPayment'`. A única FK de Pedido referencia `jogos(id)` com RESTRICT. Não há FK de usuário. Tabelas anteriores são preservadas.

`PostgreSqlPedidosTests` usa bancos `catalog_c16_test_<guid>` descartáveis e conexões independentes. Exercita HTTP concorrente para mesma chave, chaves diferentes, usuários diferentes e mesma chave/jogos diferentes; testa constraints diretamente e liberação do advisory lock por rollback/commit. Nunca remove fcg_catalog. O teste de schema autorizado é somente leitura. Requer CATALOG_TEST_CONNECTION e permissão CREATEDB, como no C15.

Para carregar variáveis persistidas no escopo User em uma nova sessão PowerShell:

```powershell
$env:ConnectionStrings__CatalogDatabase = [Environment]::GetEnvironmentVariable('ConnectionStrings__CatalogDatabase', 'User')
$env:CATALOG_TEST_CONNECTION = [Environment]::GetEnvironmentVariable('CATALOG_TEST_CONNECTION', 'User')
dotnet restore
dotnet build
dotnet ef database update --project src/FCG.Catalog.Infrastructure --startup-project src/FCG.Catalog.Api
dotnet test
dotnet ef migrations list --project src/FCG.Catalog.Infrastructure --startup-project src/FCG.Catalog.Api
dotnet ef migrations has-pending-model-changes --project src/FCG.Catalog.Infrastructure --startup-project src/FCG.Catalog.Api
git diff --check
```

O C16 não inclui mensageria. O C17 abaixo adiciona somente a publicação do OrderPlacedEvent.

## Publicação confiável (C17)

Decisão aprovada para C17: RabbitMQ.Client 7.1.2, Outbox própria e BackgroundService, sem MassTransit.

```text
POST /api/v1/pedidos
  → transação READ COMMITTED + advisory lock do C16
  → Pedido + SaveChanges
  → OrderPlacedEvent serializado uma vez + Outbox + SaveChanges
  → commit → resposta HTTP

OutboxWorker
  → reserva pendente no PostgreSQL → commit
  → publica no RabbitMQ fora da transação DB
  → confirm sem retorno/nack → nova transação → PublishedAt
```

Os dois SaveChanges da compra usam o mesmo CatalogDbContext scoped e a mesma transação. Falha ao gravar Outbox desfaz o Pedido. Replay de Idempotency-Key, inclusive após lock/conflito, não cria outra Outbox. Não há backfill para pedidos pré-C17. C18 continua responsável por IConsultaBiblioteca: o POST permanece 503 enquanto ela não estiver registrada em produção; fakes existem somente em testes.

### Contrato e identidade

JSON UTF-8 com propriedades PascalCase: EventId (Guid), CorrelationId (Guid), OccurredAt (DateTimeOffset UTC), Version (1), OrderId, UserId, GameId (Guid), Price (decimal), Currency (string). Price/Currency vêm do Pedido recém-persistido; OccurredAt usa CreatedAt UTC. O publisher envia o Payload armazenado sem reconstruí-lo nem consultar Jogo.

Idempotency-Key identifica a requisição de criação no escopo do usuário; EventId identifica o evento lógico. Retries não geram novos EventId. X-Correlation-ID: um UUID não vazio é reutilizado; ausente, inválido ou múltiplo gera novo UUID sem rejeitar a compra. O header é devolvido na resposta e injetado por abstração scoped. Replay HTTP pode ter outra correlação de requisição, mas preserva o evento/correlação original da Outbox.

A entrega é at-least-once: queda após confirmação e antes de PublishedAt pode produzir duplicata com o mesmo EventId/payload. Deduplicação do consumer é responsabilidade de Payments e não foi implementada aqui.

### Persistência e reservas

Tabela catalog_outbox: Id, EventId, EventType, Payload text, OccurredAt, PublishedAt, Attempts, OrderId, CorrelationId, NextAttemptAt, LastError, LockedUntil, LockToken. Datas são timestamptz. EventId e (OrderId, EventType) são únicos; Attempts >= 0; índice parcial de NextAttemptAt/OccurredAt para PublishedAt IS NULL.

Sem FK de Outbox para Pedido: permite retenção independente do histórico de entrega, evitando cascade ou bloqueio de limpeza futura. O fluxo de escrita garante ambos na mesma transação; nenhum dado depende de PaymentsDB.

Reserva em transação curta com FOR UPDATE SKIP LOCKED e UPDATE RETURNING, relógio PostgreSQL, LockToken e lease de 60s. Attempts é incrementado antes do envio. O worker reserva uma mensagem por vez, com timeout de publicação de 15s, para não deixar itens aguardando no lote perderem a reserva. Sucesso/falha exigem token correspondente e reserva ainda válida. Queda recupera a mensagem após expiração. Attempts satura em int.MaxValue para não interromper retries por overflow.

Backoff persistente: 2s, 5s, 15s, 30s, 60s e teto de 60s nas tentativas seguintes. Não há desistência definitiva nem alteração do Pedido para Rejected. Consulta ociosa/falha de banco espera 2s. LastError armazena categoria técnica limitada a 512 caracteres, sem texto arbitrário da exceção, URI, senha, JWT ou payload. Logs incluem EventId, CorrelationId, OrderId, EventType, Attempts e resultado. Retenção automática/expurgo não fazem parte deste card.

### RabbitMQ e topologia

Exchange OrderPlacedEvent: fanout, durable=true, autoDelete=false. O Catalog pode declará-lo ao abrir canal, mas não declara fila/binding de consumidor. Infraestrutura/Payments deve provisionar a fila durável payments-order-placed, não exclusiva, sem auto-delete, e seu binding antes do fluxo. Não usar TTL, auto-delete ou alternate exchange que mascare perda de rota sem uma decisão explícita posterior.

Publicação com mandatory=true, Persistent=true, MessageId=EventId e propriedade AMQP CorrelationId. O canal habilita publisherConfirmationsEnabled e publisherConfirmationTrackingEnabled. BasicPublishAsync é aguardado: RabbitMQ.Client 7.1.2 correlaciona basic.return com a publicação e lança PublishException (IsReturn=true); ACK posterior não anula o retorno. Não há delay arbitrário de espera por BasicReturn. Nack, timeout ou unroutable mantêm PublishedAt null. Confirm não é ACK de processamento do consumidor. Mandatory detecta ausência de qualquer rota; não prova entrega à fila específica se outras filas estiverem bindadas: validar topologia operacionalmente.

Conexão/canal são reutilizados e o acesso é serializado. Falha invalida ambos; o próximo retry abre nova conexão. Falha inicial também entra no retry. Broker ou banco indisponível não impede startup, /health ou Swagger: /health continua sendo liveness do host, não comprovação de entrega. Outbox__Enabled=false pausa apenas o worker, sem desligar a gravação transacional da Outbox.

Configuração externa (ASP.NET Core lê environment variables):

| Variável | Uso |
| --- | --- |
| ConnectionStrings__CatalogDatabase | Conexão PostgreSQL exclusiva do Catalog |
| RabbitMq__Host | Host do broker |
| RabbitMq__Port | Porta AMQP, padrão 5672 |
| RabbitMq__VirtualHost | Vhost autorizado |
| RabbitMq__Username | Credencial de serviço |
| RabbitMq__Password | Segredo fornecido externamente |
| RabbitMq__Exchange | OrderPlacedEvent |
| Outbox__Enabled | true por padrão |

Não há credencial padrão utilizável no appsettings. Esta implementação usa AMQP TCP; TLS e gestão de segredos de infraestrutura não foram adicionados neste card.

### Migrations e testes C17

Depois de configurar externamente a conexão autorizada:

```powershell
dotnet restore
dotnet build
dotnet tool restore
dotnet ef migrations list --project src/FCG.Catalog.Infrastructure --startup-project src/FCG.Catalog.Api
dotnet ef database update --project src/FCG.Catalog.Infrastructure --startup-project src/FCG.Catalog.Api
dotnet ef migrations has-pending-model-changes --project src/FCG.Catalog.Infrastructure --startup-project src/FCG.Catalog.Api
dotnet test
git diff --check
```

Migration 20261004202059_AddCatalogOutbox adiciona apenas a tabela e os índices da Outbox. Não altera pedidos existentes nem gera eventos retroativos.

PostgreSQL real: definir CATALOG_TEST_CONNECTION com conexão de manutenção local autorizada, com CREATEDB. Os testes criam/removem somente bancos catalog_c17_test_<guid>, como no C15/C16. ConnectionStrings__CatalogDatabase habilita os testes somente leitura de schema da base local migrada. Ausência de configuração produz skip explícito.

RabbitMQ real: definir RABBITMQ_TEST_CONNECTION como URI AMQP com credenciais externas e um vhost EXCLUSIVO, vazio e descartável de testes. A suíte requer também CATALOG_TEST_CONNECTION. Nunca apontar para vhost de produção/compartilhado. Ela se recusa a executar se OrderPlacedEvent ou payments-order-placed já existir, cria recursos com esses nomes e remove apenas os que criou. O proxy local aceita apenas amqp://; interrompe conectividade TCP e depois encaminha ao broker real, sem mock do protocolo.

```powershell
dotnet test --filter FullyQualifiedName~PostgreSqlOutboxTests
dotnet test --filter FullyQualifiedName~RabbitMqOutboxTests
```

Sem RABBITMQ_TEST_CONNECTION, o teste real é explicitamente ignorado. Ele cobre indisponibilidade/recuperação, basic.return sem binding, correção da rota, comparação byte a byte com Payload e duplicata após confirm sem PublishedAt. Testes unitários/fakes não substituem essa validação de broker. Não é necessário executar PaymentsAPI/consumer de negócio; o teste inspeciona mensagens via BasicGet.

C17 não implementa PaymentsAPI/PaymentsDB, consumers, PaymentProcessedEvent, Inbox, Aquisicao, concessão/biblioteca, Notifications, C18/C19, Compose ou Kubernetes. Operação deve monitorar pendências/idade/tentativas e corrigir conectividade/topologia; mensagem permanece recuperável com seus IDs originais.
