# Relatório C15 — persistência e migrations do CatalogAPI

Implementação em C:UsersklonoaDesktopiapFCG.CatalogAPI. Nenhuma alteração no monólito, commit ou push.

## 1. Arquivos consultados no FCG

Os caminhos src/Modules/Catalog informados não contêm os fontes neste checkout. Foram consultados somente os equivalentes necessários em src/FIAP.CloudGames.Infrastructure:

- Data/EF/Context/PostgresqlDbContext.cs
- Data/EF/Mappings/Catalog/CategoriaMapping.cs
- Data/EF/Mappings/Catalog/MapeamentoCategoriaJogo.cs
- Data/EF/Mappings/Catalog/MapeamentoJogo.cs
- Repositories/Catalog/RepositorioJogos.cs
- IoC/InfrastructureDependency.cs

Não foram copiadas credenciais, contextos compartilhados ou migrations antigas. A localização foi feita por listagens pontuais e nomes rastreados pelo Git, sem leitura integral dos repositórios.

## 2. Arquivos criados no CatalogAPI

- .config/dotnet-tools.json
- src/FCG.Catalog.Infrastructure/Data/EF/Context/CatalogDbContext.cs
- src/FCG.Catalog.Infrastructure/Data/EF/Mappings/CategoriaMapping.cs
- src/FCG.Catalog.Infrastructure/Data/EF/Mappings/MapeamentoCategoriaJogo.cs
- src/FCG.Catalog.Infrastructure/Data/EF/Mappings/MapeamentoJogo.cs
- src/FCG.Catalog.Infrastructure/Data/EF/Migrations/20261004192230_InitialCatalog.cs
- src/FCG.Catalog.Infrastructure/Data/EF/Migrations/20261004192230_InitialCatalog.Designer.cs
- src/FCG.Catalog.Infrastructure/Data/EF/Migrations/CatalogDbContextModelSnapshot.cs
- src/FCG.Catalog.Infrastructure/IoC/InfrastructureDependency.cs
- src/FCG.Catalog.Infrastructure/Repositories/RepositorioJogos.cs
- tests/FCG.Catalog.IntegrationTests/CatalogModelTests.cs
- tests/FCG.Catalog.IntegrationTests/PostgreSqlPersistenceTests.cs
- RELATORIO-C15.md

## 3. Arquivos alterados

Directory.Packages.props; README.md; src/FCG.Catalog.Api/FCG.Catalog.Api.csproj; src/FCG.Catalog.Api/IoC/ApplicationDependency.cs (comentário); src/FCG.Catalog.Api/Program.cs; src/FCG.Catalog.Api/appsettings.json; src/FCG.Catalog.Infrastructure/FCG.Catalog.Infrastructure.csproj; tests/FCG.Catalog.IntegrationTests/CatalogFactory.cs (exportação de chave pública efêmera e comentário, sem enfraquecer testes).

## 4. CatalogDbContext

Somente DbSet<Jogo> Jogos, DbSet<Categoria> Categorias e DbSet<CategoriaJogo> CategoriasJogos. Três ApplyConfiguration explícitos; nenhuma descoberta automática de mappings de outros módulos.

## 5. Mappings

Preservados nomes de tabelas/colunas, Guid/uuid com ValueGeneratedNever, required/nullable, limites 150/500/2 para título/descrição/faixa etária, 200 para categoria, numeric(18,2), boolean com default true e timestamp with time zone. As entidades e IDs do C14 não foram alterados.

## 6. RepositorioJogos

Implementa IRepositorioJogos somente com CatalogDbContext. Consulta por ID rastreada; listagem AsNoTracking ordenada por título com Skip/Take; criação com Add e SaveChangesAsync; atualização rastreada ou Update para entidade detached seguida de SaveChangesAsync. CancellationToken preservado em todas as operações.

## 7. Pacotes

Directory.Packages.props: Microsoft.EntityFrameworkCore 8.0.28, Microsoft.EntityFrameworkCore.Design 8.0.28, Npgsql.EntityFrameworkCore.PostgreSQL 8.0.11. Design privado no startup project; EF/Npgsql em Infrastructure. Ferramenta local dotnet-ef 8.0.28 no manifesto. TargetFramework permanece net8.0.

## 8. Connection string

ConnectionStrings:CatalogDatabase, sobrescrita por ConnectionStrings__CatalogDatabase. Appsettings contém somente valor vazio; nenhuma senha padrão/real versionada. README documenta configuração, restore da ferramenta e comandos completos. Banco informado pelo usuário: localhost:5433/fcg_catalog, usuário postgres.

## 9. DI

Program chama AddCatalogInfrastructure. CatalogDbContext e IRepositorioJogos → RepositorioJogos registrados scoped. Health/Swagger permanecem independentes da conexão; erro claro ao resolver o contexto sem configuração. Sem migration automática no startup.

## 10. Migration

20261004192230_InitialCatalog gerada pelo EF, com Designer e snapshot próprios. Up e snapshot inspecionados explicitamente. has-pending-model-changes confirmou ausência de diferenças.

## 11. Tabelas, constraints e índices

- jogos: PK pk_jogos; id uuid obrigatório; titulo varchar(150) obrigatório; descricao varchar(500) nullable; faixa_etaria varchar(2) nullable; preco numeric(18,2) obrigatório; ativo boolean obrigatório default true; data_cadastro timestamptz obrigatório.
- tb_Categorias: PK PK_tb_Categorias; Id uuid; Nome varchar(200), ambos obrigatórios.
- rel_CategoriaJogo: PK PK_rel_CategoriaJogo; Id, JogoId, CategoriaId uuid obrigatórios.
- FK_rel_CategoriaJogo_jogos_JogoId e FK_rel_CategoriaJogo_tb_Categorias_CategoriaId, ambas ON DELETE CASCADE.
- IX_rel_CategoriaJogo_CategoriaId e índice único IX_rel_CategoriaJogo_JogoId_CategoriaId.
- __EFMigrationsHistory é infraestrutura do EF, não entidade de domínio.
- Nenhum CHECK novo foi inventado: preservadas as constraints da referência.

## 12. Ownership / ausência de Users

Modelo e migration contêm exclusivamente Jogo/Categoria/CategoriaJogo. Sem Usuario, Perfil, Token, Permissao, Autorizacao, Aquisicao, LogUsuario, Pedido, Inbox ou Outbox. Sem FK externa, UsersDB, chamadas ao UsersAPI ou ProjectReference para monólito.

## 13. Restore

dotnet restore concluído com sucesso.

## 14. Build

dotnet build concluído com zero avisos e zero erros.

## 15. Testes

Execução final com as duas variáveis carregadas: 34 testes unitários + 92 testes de integração = 126 aprovados, zero falhas, zero ignorados. Testes existentes C14 preservados. git diff --check sem problemas.

## 16. migrations list

No PostgreSQL autorizado localhost:5433/fcg_catalog, a migration 20261004192230_InitialCatalog apareceu Pending antes da aplicação e aplicada depois. Sem migrations pendentes. Snapshot coerente com o modelo (has-pending-model-changes passou).

## 17. database update / schema

dotnet ef database update --project src/FCG.Catalog.Infrastructure --startup-project src/FCG.Catalog.Api --context CatalogDbContext concluiu com sucesso. Foram criadas jogos, tb_Categorias, rel_CategoriaJogo e __EFMigrationsHistory. A consulta real a pg_tables confirmou exatamente essas quatro tabelas em public. Histórico contém uma migration aplicada e nenhuma pendente.
O teste de integração também criou um banco totalmente novo catalog_c15_test_<guid>, aplicou a migration e confirmou reaplicação sem alterações; o banco descartável foi removido ao final. fcg_catalog foi preservado.

## 18. CRUD real

Teste via HTTP em WebApplicationFactory/TestServer com PostgreSQL real na porta 5433 e DI de produção, sem fake de persistência. Dados de teste ficaram exclusivamente no banco descartável, não em fcg_catalog.

- POST /api/v1/jogos → 201 com JWT Administrador.
- GET /api/v1/jogos/{id} público → jogo persistido e atualizado.
- GET /api/v1/jogos público → listagem persistida.
- PUT /api/v1/jogos/{id} → 200, preservando ID/Ativo/DataCadastro.
- POST sem token → 401; role não administrativa → 403; PUT sem token → 401.
- Resolução de IRepositorioJogos confirmou RepositorioJogos; novo contexto leu os dados gravados.
- Ordenação/paginação e atualização detached confirmadas no repositório.
- Categoria/Jogo persistidos; duplicação rejeitada por unicidade, jogo inexistente rejeitado por FK, título longo rejeitado pelo PostgreSQL; cascade de categoria confirmado.
- Health/Swagger → 200; suíte C14 confirma demais casos JWT e Swagger indisponível em Production.

## 19. Decisões e desvios

- Usado o destino explicitamente solicitado, em vez da worktree inicial da conversa.
- Adaptados os arquivos equivalentes da organização real do monólito.
- Sem Docker, cards futuros ou migração de dados.
- Testes C14 mantidos; fake exclusivo dos testes de contrato. No teste PostgreSQL, CatalogFactory fornece apenas um token assinado em memória, sem iniciar seu host/fake.
- Limites físicos herdados preservados. Os handlers C14 não validam comprimento de descrição/faixa etária; ultrapassá-los continua produzindo erro de persistência (sem introduzir mudança de contrato HTTP neste card).
- Não foi adicionada factory de design-time: startup project e variável de ambiente atendem ao tooling.
- Nenhum commit, staging ou push.

## 20. git diff --stat

O comando não inclui arquivos novos não rastreados; estes estão discriminados acima e em git status.
~~~text
 Directory.Packages.props                           |  3 ++
 README.md                                          | 59 +++++++++++++++++++---
 src/FCG.Catalog.Api/FCG.Catalog.Api.csproj         |  1 +
 src/FCG.Catalog.Api/IoC/ApplicationDependency.cs   |  2 +-
 src/FCG.Catalog.Api/Program.cs                     |  2 +
 src/FCG.Catalog.Api/appsettings.json               |  3 ++
 .../FCG.Catalog.Infrastructure.csproj              |  2 +
 .../FCG.Catalog.IntegrationTests/CatalogFactory.cs |  3 +-
 8 files changed, 66 insertions(+), 9 deletions(-)
~~~

## 21. git status

~~~text
 M Directory.Packages.props
 M README.md
 M src/FCG.Catalog.Api/FCG.Catalog.Api.csproj
 M src/FCG.Catalog.Api/IoC/ApplicationDependency.cs
 M src/FCG.Catalog.Api/Program.cs
 M src/FCG.Catalog.Api/appsettings.json
 M src/FCG.Catalog.Infrastructure/FCG.Catalog.Infrastructure.csproj
 M tests/FCG.Catalog.IntegrationTests/CatalogFactory.cs
?? .config/
?? RELATORIO-C15.md
?? src/FCG.Catalog.Infrastructure/Data/EF/
?? src/FCG.Catalog.Infrastructure/IoC/InfrastructureDependency.cs
?? src/FCG.Catalog.Infrastructure/Repositories/RepositorioJogos.cs
?? tests/FCG.Catalog.IntegrationTests/CatalogModelTests.cs
?? tests/FCG.Catalog.IntegrationTests/PostgreSqlPersistenceTests.cs
~~~
