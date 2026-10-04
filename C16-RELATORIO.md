# Relatório C16 — Pedido de compra

Entrega em C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI, branch feature/c16-pedido-compra, sem commit ou push. Implementação preparada e validada também no worktree deste chat, atualizado para a base C15 (9e24a0f). O repositório antigo FCG não foi alterado.

1. **Arquivos criados (14, incluindo este relatório):**
   - src/FCG.Catalog.Domain/Orders/Pedido.cs
   - src/FCG.Catalog.Application/Orders/ContratosPedidos.cs
   - src/FCG.Catalog.Application/Orders/ManipuladorCriarPedido.cs
   - src/FCG.Catalog.Infrastructure/Repositories/RepositorioPedidos.cs
   - src/FCG.Catalog.Infrastructure/Data/EF/LockUsuarioJogo.cs
   - src/FCG.Catalog.Infrastructure/Data/EF/Mappings/PedidoMapping.cs
   - src/FCG.Catalog.Infrastructure/Data/EF/Migrations/20261004194925_AddPedidos.cs
   - src/FCG.Catalog.Infrastructure/Data/EF/Migrations/20261004194925_AddPedidos.Designer.cs
   - src/FCG.Catalog.Api/Controllers/PedidosController.cs
   - tests/Shared/PedidoFakes.cs
   - tests/FCG.Catalog.UnitTests/PedidosTests.cs
   - tests/FCG.Catalog.IntegrationTests/PedidosApiTests.cs
   - tests/FCG.Catalog.IntegrationTests/PostgreSqlPedidosTests.cs
   - C16-RELATORIO.md

2. **Arquivos alterados (12 no diff):**
   - README.md
   - src/FCG.Catalog.Api/IoC/ApplicationDependency.cs
   - src/FCG.Catalog.Infrastructure/IoC/InfrastructureDependency.cs
   - src/FCG.Catalog.Infrastructure/Data/EF/Context/CatalogDbContext.cs
   - src/FCG.Catalog.Infrastructure/Data/EF/Migrations/CatalogDbContextModelSnapshot.cs
   - src/FCG.Catalog.Infrastructure/Repositories/RepositorioJogos.cs
   - tests/FCG.Catalog.IntegrationTests/CatalogFactory.cs
   - tests/FCG.Catalog.IntegrationTests/CatalogModelTests.cs
   - tests/FCG.Catalog.IntegrationTests/FCG.Catalog.IntegrationTests.csproj
   - tests/FCG.Catalog.IntegrationTests/JogosApiTests.cs
   - tests/FCG.Catalog.IntegrationTests/PostgreSqlPersistenceTests.cs
   - tests/FCG.Catalog.UnitTests/FCG.Catalog.UnitTests.csproj

3. **Modelo:** Pedido contém Id, UserId, GameId e IdempotencyKey Guid; Price decimal; Currency string; StatusPedido; CreatedAt e UpdatedAt DateTimeOffset UTC. Setters privados; ID gerado no domínio; datas iguais na criação; preço não negativo com até duas casas.

4. **Estados:** PendingPayment inicial; Paid e Rejected terminais. Finalizar aceita apenas transição de pendente para terminal. Nenhum endpoint ou integração muda status.

5. **Criação:** valida UUIDs; procura chave; inicia READ COMMITTED; adquire lock; reconsulta chave; lê jogo sem tracking; valida ativo; consulta posse; procura pendente; cria; SaveChanges; commit. Saídas antecipadas desfazem a transação.

6. **UserId:** exclusivamente claim sub, validada pelo JWT do C14. DTO não possui UserId. Sem acesso a UsersAPI/UsersDB.

7. **Price/Currency:** preço atual do jogo copiado para o pedido; BRL definido no servidor. Alterações posteriores de preço não mudam Pedido.Price. Campos extras do payload não são usados.

8. **Idempotência:** UUID obrigatório e não vazio, com um único header. Escopo por usuário. Replay para o mesmo jogo devolve ID original; outro jogo retorna 409. Consulta precede posse e pendência. Replay pendente retorna 202; terminal, 200.

9. **Constraints:** unique (UserId, IdempotencyKey); unique parcial (UserId, GameId) WHERE "Status" = 'PendingPayment'; PK; FK GameId → jogos(id) com RESTRICT. Sem FK em UserId. Somente as duas violações de unicidade esperadas são traduzidas para replay/409, após rollback.

10. **Lock:** ILockUsuarioJogo + LockUsuarioJogo; pg_advisory_xact_lock(bigint) na mesma transação/contexto. Chave: SHA-256 do UTF-8 de catalog:usuario-jogo:v1:{userId:N}:{gameId:N}, primeiros oito bytes como Int64 big endian. Contrato documentado para reutilização exata pelo C19. Sem lock de produção em memória; liberação automática no commit/rollback.

11. **Posse/C18:** IConsultaBiblioteca.PossuiJogoAsync na Application, consumida pelo handler e validada com fake nos testes. Sem implementação falsa em produção. POST com entrada válida responde 503 enquanto C18 não registrar a implementação real. Essa limitação está documentada no README e no Problem Details.

12. **Persistência:** IRepositorioPedidos/RepositorioPedidos com consultas por ID, chave e pendência; transação e gravação; CancellationToken preservado. DbSet Pedidos, PedidoMapping, numeric(18,2), varchar(3), status textual, timestamptz. RepositorioJogos lê por ID sem tracking para evitar entidade stale na revalidação após lock.

13. **Migration:** 20261004194925_AddPedidos, gerada por EF e aplicada no PostgreSQL autorizado localhost:5433/fcg_catalog. Migration e snapshot inspecionados; apenas tabela pedidos adicionada, tabelas anteriores preservadas.

14. **POST /api/v1/pedidos:**

    | Situação | HTTP |
    | --- | --- |
    | Nova criação / replay PendingPayment | 202 + Location + mesmo OrderId no replay |
    | Replay Paid/Rejected | 200 |
    | Chave ausente, inválida, vazia ou jogoId vazio | 400 |
    | JWT ausente/inválido | 401 |
    | Jogo inexistente | 404 |
    | Inativo, adquirido, pendente duplicado ou chave em outro jogo | 409 |
    | IConsultaBiblioteca ainda não conectada pelo C18 | 503 |

15. **GET /api/v1/pedidos/{id}:** 200 para titular ou Administrador, 403 para outro usuário, 404 inexistente, 401 sem JWT válido. Resposta mínima não expõe UserId nem IdempotencyKey.

16. **Unitários:** 52 testes totais aprovados, incluindo domínio, preço congelado, moeda, datas, validações, terminais, replay anterior à posse, ordem transacional, alterações antes do lock e recuperação após conflito único.

17. **Integração:** 112 testes totais aprovados; matriz HTTP de pedidos, identidade pelo sub mesmo com payload adulterado, Location, autorização GET, CRUD existente de jogos, Swagger e JWT.

18. **PostgreSQL real:** quatro corridas HTTP com conexões independentes: mesma chave (um pedido/mesmo ID), chaves diferentes (202/409), usuários diferentes (dois pedidos), mesma chave/jogos diferentes (202/409). Barreira de testes e lock externo forçam sobreposição. Constraints, FK, índice parcial e liberação do lock por rollback/commit validados no banco. Bancos catalog_c15_test_<guid> e catalog_c16_test_<guid> criados/removidos; fcg_catalog não foi destruído. Inspeção read-only confirmou índices e única FK local.

19. **Restore/build/test:** aprovados no worktree; 52 unitários + 112 integração = 164, zero falhas e zero ignorados. Repetidos no diretório final, também aprovados com zero avisos/erros de compilação, usando --artifacts-path em diretório temporário para contornar cache obj preexistente sem permissão de escrita.

20. **EF:** database update aplicado; migrations list mostrou InitialCatalog e AddPedidos aplicadas; has-pending-model-changes informou ausência de alterações pendentes. git diff --check aprovado.

21. **Fora do escopo:** nenhuma implementação de PaymentsAPI, RabbitMQ, Outbox, Inbox, Aquisicao, biblioteca persistente, OrderPlacedEvent, consumers, concessão, Docker ou Kubernetes. FCG antigo intacto.

22. **Decisões/desvios:** POST indisponível com 503 até C18, conforme dependência explicitamente permitida. Foram usadas as variáveis persistidas de usuário, lidas diretamente de HKCU\Environment depois que a API Environment retornou vazio. Nenhuma credencial foi versionada. O diretório final continha prefixo de 255 caracteres não C# antes do conteúdo original intacto de JwtConfigurationTests.cs; uma cópia foi preservada fora do repositório e somente esse prefixo foi retirado para permitir compilar. Arquivo resultante igual ao HEAD, sem diff. Backup: C:\Users\klonoa\AppData\Local\Temp\JwtConfigurationTests-before-c16-repair-b8583e8832834feea204a66cfc3eafa0.txt. Cache obj original não foi apagado nem teve permissões modificadas.

23. **git diff --stat:** 12 arquivos rastreados alterados, 122 inserções e 16 exclusões. Esse comando não contabiliza os 14 arquivos novos, que permanecem untracked, sem staging.

24. **git status:** 12 arquivos modificados listados no item 2 e 14 arquivos novos listados no item 1. Sem commit, push ou staging. Branch final feature/c16-pedido-compra.

Saída de revisão antes da criação deste relatório (acrescentar ?? C16-RELATORIO.md ao status):
```text
README.md                                          | 40 +++++++++++++--
 src/FCG.Catalog.Api/IoC/ApplicationDependency.cs   |  2 +
 .../Data/EF/Context/CatalogDbContext.cs            |  2 +
 .../EF/Migrations/CatalogDbContextModelSnapshot.cs | 59 ++++++++++++++++++++++
 .../IoC/InfrastructureDependency.cs                |  5 ++
 .../Repositories/RepositorioJogos.cs               |  2 +-
 .../FCG.Catalog.IntegrationTests/CatalogFactory.cs |  4 +-
 .../CatalogModelTests.cs                           |  8 +--
 .../FCG.Catalog.IntegrationTests.csproj            |  1 +
 .../FCG.Catalog.IntegrationTests/JogosApiTests.cs  |  6 ++-
 .../PostgreSqlPersistenceTests.cs                  |  8 +--
 .../FCG.Catalog.UnitTests.csproj                   |  1 +
 12 files changed, 122 insertions(+), 16 deletions(-)
 M README.md
 M src/FCG.Catalog.Api/IoC/ApplicationDependency.cs
 M src/FCG.Catalog.Infrastructure/Data/EF/Context/CatalogDbContext.cs
 M src/FCG.Catalog.Infrastructure/Data/EF/Migrations/CatalogDbContextModelSnapshot.cs
 M src/FCG.Catalog.Infrastructure/IoC/InfrastructureDependency.cs
 M src/FCG.Catalog.Infrastructure/Repositories/RepositorioJogos.cs
 M tests/FCG.Catalog.IntegrationTests/CatalogFactory.cs
 M tests/FCG.Catalog.IntegrationTests/CatalogModelTests.cs
 M tests/FCG.Catalog.IntegrationTests/FCG.Catalog.IntegrationTests.csproj
 M tests/FCG.Catalog.IntegrationTests/JogosApiTests.cs
 M tests/FCG.Catalog.IntegrationTests/PostgreSqlPersistenceTests.cs
 M tests/FCG.Catalog.UnitTests/FCG.Catalog.UnitTests.csproj
?? src/FCG.Catalog.Api/Controllers/PedidosController.cs
?? src/FCG.Catalog.Application/Orders/
?? src/FCG.Catalog.Domain/Orders/Pedido.cs
?? src/FCG.Catalog.Infrastructure/Data/EF/LockUsuarioJogo.cs
?? src/FCG.Catalog.Infrastructure/Data/EF/Mappings/PedidoMapping.cs
?? src/FCG.Catalog.Infrastructure/Data/EF/Migrations/20261004194925_AddPedidos.Designer.cs
?? src/FCG.Catalog.Infrastructure/Data/EF/Migrations/20261004194925_AddPedidos.cs
?? src/FCG.Catalog.Infrastructure/Repositories/RepositorioPedidos.cs
?? tests/FCG.Catalog.IntegrationTests/PedidosApiTests.cs
?? tests/FCG.Catalog.IntegrationTests/PostgreSqlPedidosTests.cs
?? tests/FCG.Catalog.UnitTests/PedidosTests.cs
?? tests/Shared/
src/FCG.Catalog.Api/Controllers/PedidosController.cs
src/FCG.Catalog.Application/Orders/ContratosPedidos.cs
src/FCG.Catalog.Application/Orders/ManipuladorCriarPedido.cs
src/FCG.Catalog.Domain/Orders/Pedido.cs
src/FCG.Catalog.Infrastructure/Data/EF/LockUsuarioJogo.cs
src/FCG.Catalog.Infrastructure/Data/EF/Mappings/PedidoMapping.cs
src/FCG.Catalog.Infrastructure/Data/EF/Migrations/20261004194925_AddPedidos.Designer.cs
src/FCG.Catalog.Infrastructure/Data/EF/Migrations/20261004194925_AddPedidos.cs
src/FCG.Catalog.Infrastructure/Repositories/RepositorioPedidos.cs
tests/FCG.Catalog.IntegrationTests/PedidosApiTests.cs
tests/FCG.Catalog.IntegrationTests/PostgreSqlPedidosTests.cs
tests/FCG.Catalog.UnitTests/PedidosTests.cs
tests/Shared/PedidoFakes.cs
```

