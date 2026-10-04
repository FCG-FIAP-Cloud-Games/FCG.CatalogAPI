# FCG.CatalogAPI

Microsserviço .NET 8 do FIAP Cloud Games. C13 forneceu o host; C14 migrou domínio, operações e segurança; C15 adicionou PostgreSQL; C16 implementa pedidos de compra.

## Escopo atual (C14 + C15 + C16)

- Entidades `Jogo`, `Categoria` e `CategoriaJogo`, preservando propriedades, construtores, IDs e invariantes.
- `IRepositorioJogos`, comandos, consultas, resultados e handlers de criação, consulta por ID, listagem paginada e atualização.
- Contratos HTTP e `JogosController` preservados, com proteção explícita nas escritas.
- Persistência EF Core 8/Npgsql exclusiva para jogos, categorias e pedidos. Sem biblioteca persistente ou broker.

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

A API registra CatalogDbContext e IRepositorioJogos → RepositorioJogos com lifetime scoped. O contexto aplica explicitamente apenas os mappings de Jogo, Categoria e CategoriaJogo. Consultas por ID são rastreadas; listagem usa AsNoTracking, ordenação por título e paginação; criação/atualização chamam SaveChangesAsync. IDs continuam Guid com ValueGeneratedNever.

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

Não há publicação de OrderPlacedEvent, PaymentsAPI, RabbitMQ, Outbox, Inbox, Aquisicao ou concessão de jogo neste card.
