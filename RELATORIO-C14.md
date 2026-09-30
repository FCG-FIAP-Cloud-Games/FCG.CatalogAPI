# Relatório C14 — 30/09/2026

Implementação realizada exclusivamente em `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI`. Nenhum commit, staging ou push foi realizado. O worktree inicial ficou intacto.

## 1. Arquivos consultados no FCG

Os caminhos indicados em `src/Modules/Catalog` não contêm o código neste checkout (somente bin/obj). Foram usados os equivalentes atuais abaixo, sem consultar implementações de persistência ou outros módulos. A IoC compartilhada foi lida para identificar os quatro registros necessários; nenhum serviço de Identity foi migrado.
- `C:\Users\klonoa\Desktop\fiap\FCG\src\FIAP.CloudGames.Domain\Catalog\Entities\Categoria.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG\src\FIAP.CloudGames.Domain\Catalog\Entities\CategoriaJogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG\src\FIAP.CloudGames.Domain\Catalog\Entities\Jogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG\src\FIAP.CloudGames.Application\Catalog\Jogos\ComandoAtualizarJogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG\src\FIAP.CloudGames.Application\Catalog\Jogos\ComandoCriarJogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG\src\FIAP.CloudGames.Application\Catalog\Jogos\ConsultaListarJogos.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG\src\FIAP.CloudGames.Application\Catalog\Jogos\ConsultaObterJogoPorId.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG\src\FIAP.CloudGames.Application\Catalog\Jogos\ManipuladorAtualizarJogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG\src\FIAP.CloudGames.Application\Catalog\Jogos\ManipuladorCriarJogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG\src\FIAP.CloudGames.Application\Catalog\Jogos\ManipuladorListarJogos.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG\src\FIAP.CloudGames.Application\Catalog\Jogos\ManipuladorObterJogoPorId.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG\src\FIAP.CloudGames.Application\Catalog\Jogos\ResultadoAtualizarJogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG\src\FIAP.CloudGames.Application\Catalog\Jogos\ResultadoCriarJogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG\src\FIAP.CloudGames.Application\Catalog\Jogos\ResultadoListarJogos.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG\src\FIAP.CloudGames.Application\Catalog\Jogos\ResultadoObterJogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG\src\FIAP.CloudGames.Api\Controllers\Catalog\JogosController.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG\src\FIAP.CloudGames.Api\Contracts\Catalog\Jogos\RequisicaoAtualizarJogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG\src\FIAP.CloudGames.Api\Contracts\Catalog\Jogos\RequisicaoCriarJogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG\src\FIAP.CloudGames.Api\Contracts\Catalog\Jogos\RespostaJogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG\src\FIAP.CloudGames.Api\Contracts\Catalog\Jogos\RespostaListaJogos.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG\tests\FIAP.CloudGames.UnitTests\Catalog\Jogos\TestesManipuladorAtualizarJogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG\tests\FIAP.CloudGames.UnitTests\Catalog\Jogos\TestesManipuladorCriarJogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG\tests\FIAP.CloudGames.UnitTests\Catalog\Jogos\TestesManipuladorListarJogos.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG\tests\FIAP.CloudGames.UnitTests\Catalog\Jogos\TestesManipuladorObterJogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG\tests\FIAP.CloudGames.UnitTests\Catalog\Categorias\TestesCategoria.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG\src\FIAP.CloudGames.Application\Abstractions\Repositories\IRepositorioJogos.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG\src\FIAP.CloudGames.Application\IoC\ApplicationDependency.cs`

O arquivo Architure.md foi somente verificado por SHA-256 para confirmar a preservação da alteração preexistente; seu conteúdo não foi analisado.

## 2. Arquivos criados no CatalogAPI

- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\RELATORIO-C14.md`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\src\FCG.Catalog.Api\Contracts\Catalog\Jogos\RequisicaoAtualizarJogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\src\FCG.Catalog.Api\Contracts\Catalog\Jogos\RequisicaoCriarJogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\src\FCG.Catalog.Api\Contracts\Catalog\Jogos\RespostaJogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\src\FCG.Catalog.Api\Contracts\Catalog\Jogos\RespostaListaJogos.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\src\FCG.Catalog.Api\Controllers\Catalog\JogosController.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\src\FCG.Catalog.Api\IoC\ApplicationDependency.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\src\FCG.Catalog.Application\Abstractions\Repositories\IRepositorioJogos.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\src\FCG.Catalog.Application\Catalog\Jogos\ComandoAtualizarJogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\src\FCG.Catalog.Application\Catalog\Jogos\ComandoCriarJogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\src\FCG.Catalog.Application\Catalog\Jogos\ConsultaListarJogos.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\src\FCG.Catalog.Application\Catalog\Jogos\ConsultaObterJogoPorId.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\src\FCG.Catalog.Application\Catalog\Jogos\ManipuladorAtualizarJogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\src\FCG.Catalog.Application\Catalog\Jogos\ManipuladorCriarJogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\src\FCG.Catalog.Application\Catalog\Jogos\ManipuladorListarJogos.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\src\FCG.Catalog.Application\Catalog\Jogos\ManipuladorObterJogoPorId.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\src\FCG.Catalog.Application\Catalog\Jogos\ResultadoAtualizarJogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\src\FCG.Catalog.Application\Catalog\Jogos\ResultadoCriarJogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\src\FCG.Catalog.Application\Catalog\Jogos\ResultadoListarJogos.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\src\FCG.Catalog.Application\Catalog\Jogos\ResultadoObterJogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\src\FCG.Catalog.Domain\Catalog\Entities\Categoria.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\src\FCG.Catalog.Domain\Catalog\Entities\CategoriaJogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\src\FCG.Catalog.Domain\Catalog\Entities\Jogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\tests\FCG.Catalog.IntegrationTests\CatalogFactory.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\tests\FCG.Catalog.IntegrationTests\JogosApiTests.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\tests\FCG.Catalog.IntegrationTests\JwtConfigurationTests.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\tests\FCG.Catalog.IntegrationTests\JwtTests.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\tests\FCG.Catalog.UnitTests\Catalog\Categorias\TestesCategoria.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\tests\FCG.Catalog.UnitTests\Catalog\Jogos\TestesJogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\tests\FCG.Catalog.UnitTests\Catalog\Jogos\TestesLimitesManipuladores.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\tests\FCG.Catalog.UnitTests\Catalog\Jogos\TestesManipuladorAtualizarJogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\tests\FCG.Catalog.UnitTests\Catalog\Jogos\TestesManipuladorCriarJogo.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\tests\FCG.Catalog.UnitTests\Catalog\Jogos\TestesManipuladorListarJogos.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\tests\FCG.Catalog.UnitTests\Catalog\Jogos\TestesManipuladorObterJogo.cs`

## 3. Arquivos alterados no CatalogAPI

- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\README.md`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\src\FCG.Catalog.Api\Authentication\AuthenticationExtensions.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\src\FCG.Catalog.Api\Authentication\JwtOptions.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\src\FCG.Catalog.Api\Program.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\src\FCG.Catalog.Api\appsettings.json`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\tests\FCG.Catalog.IntegrationTests\HostTests.cs`
- `C:\Users\klonoa\Desktop\fiap\FCG.CatalogAPI\tests\FCG.Catalog.UnitTests\README.md`

## 4. Entidades migradas

Jogo, Categoria e CategoriaJogo. IDs Guid, propriedades, construtores e regras preservados; nenhuma entidade de usuário, pedido ou biblioteca. Paridade textual verificada em 19 arquivos de domínio, casos de uso e contratos: somente namespaces foram adaptados.

## 5. Casos de uso migrados

ManipuladorCriarJogo, ManipuladorObterJogoPorId, ManipuladorListarJogos e ManipuladorAtualizarJogo, com os respectivos comandos/consultas/resultados e IRepositorioJogos. Preservados título obrigatório/máximo 150, preço não negativo, normalização de strings, paginação padrão 1/20 e máximo 100, cancelamento e resultados de validação/não encontrado.

## 6. Contratos e controller

JogosController, RequisicaoCriarJogo, RequisicaoAtualizarJogo, RespostaJogo e RespostaListaJogos. Preservados corpo das respostas, Location da criação, ProblemDetails/ValidationProblemDetails e códigos de negócio. Acrescentados atributos de autorização e documentação de 401/403 nas escritas.

## 7. Rotas preservadas

| Rota | Acesso | Respostas de negócio |
| --- | --- | --- |
| POST /api/v1/jogos | Administrador | 201 / 400 |
| GET /api/v1/jogos/{id} | Público | 200 / 400 / 404 |
| GET /api/v1/jogos | Público | 200 / 400 |
| PUT /api/v1/jogos/{id} | Administrador | 200 / 400 / 404 |

Nenhum DELETE ou endpoint adicional de negócio. Swagger verificado com exatamente essas quatro operações.

## 8. Configuração JWT final

```json
"Jwt": {
  "Issuer": "FIAP.CloudGames",
  "Audience": "FIAP.CloudGames.Api",
  "ClockSkewSeconds": 30,
  "PublicKeys": []
}
```

Cada entrada externa tem Kid e PublicKeyPem. RS256 obrigatório; assinatura, issuer, audience, exp e nbf quando presente são validados. `sub` deve ser único, Guid válido e não vazio. `MapInboundClaims=false`, `NameClaimType=sub`, `RoleClaimType=role`. Nenhuma chave real versionada. Exemplos de configuração externa e rotação estão no README.

## 9. Seleção por kid

Dicionário ordinal somente com chaves públicas RSA confiáveis da configuração. kid ausente/desconhecido rejeitado; TryAllIssuerSigningKeys=false; não há fallback. Testados dois kids válidos, kid apontando para chave incorreta e diferença de maiúsculas/minúsculas. Lista vazia rejeita todos os tokens; configuração incompleta, duplicada, inválida ou privada falha ao iniciar.

## 10. Autorização

GETs têm AllowAnonymous. POST/PUT usam Authorize(Roles = "Administrador"). Sem token ou token inválido → 401; Usuario, role ausente ou com caixa diferente → 403; Administrador válido → criação/atualização. Não há acesso a UsersAPI/UsersDB.

## 11. Testes

- Unitários migrados: Categoria e quatro handlers, adaptando xUnit da fonte ao xUnit 2 do destino.
- Unitários novos: Jogo, invariantes, atualização sem mutação em erro, preservação de identidade/data/estado, limites de título, normalização, cancelamento e parâmetros.
- WebApplicationFactory com fake de IRepositorioJogos somente nos testes: GETs públicos, contratos, paginação, 201/Location, 200, 400, 404, 401 e 403.
- JWT em POST e PUT: RS256 válido, duas chaves, kid desconhecido/ausente/incorreto, assinatura inválida, RS512/HS256/sem assinatura, issuer/audience inválidos, expirado, exp ausente, nbf futuro, nbf ausente, tolerância de relógio e sub ausente/vazio/inválido/Guid.Empty/duplicado.
- Configuração JWT inválida e ausência de chave; chaves geradas apenas em memória nos testes.
- Health sem banco/chave em Development e Production; Swagger em Development e indisponível em Production; ProblemDetails.

## 12. Restore

`dotnet restore` no destino: sucesso, exit code 0. Todos os projetos atualizados para restauração.

## 13. Build

`dotnet build` no destino: sucesso, exit code 0, 0 avisos e 0 erros.

## 14. Resultado dos testes

`dotnet test` no destino: sucesso, exit code 0. **123 aprovados: 34 unitários e 89 de integração; 0 falhas e 0 ignorados.**

## 15. Preservação do FCG

Nenhum arquivo do FCG foi alterado por esta implementação. git status antes/depois manteve somente ` M Architure.md`, alteração preexistente. SHA-256 desse arquivo permaneceu `1A17B5B75219DE1377E67FA6D171256D5DD45872312CFBCAF97726D7CC285241`.

## 16. Persistência fora de escopo

Nenhum EF Core, DbContext, migration, PostgreSQL, connection string ou repositório de produção foi introduzido. Infrastructure permaneceu inalterada. Não foi copiada nem consultada a implementação EF da fonte.

## 17. Independência

ProjectReference/PackageReference verificados: somente projetos locais desta solução e pacotes já existentes. Nenhuma dependência com o monólito ou UsersAPI. Nenhum Authority, discovery, introspection ou chamada HTTP externa. As strings FIAP.CloudGames e FIAP.CloudGames.Api são exclusivamente o contrato de issuer/audience.

## 18. Decisões e desvios

- Fonte real em FIAP.CloudGames.*\Catalog em vez de src\Modules\Catalog, pois os caminhos solicitados não contêm código neste checkout.
- Destino Desktop\fiap\FCG.CatalogAPI usado conforme solicitação explícita; nenhum código gravado no worktree inicial.
- Composition root na API, com factories scoped para os handlers. Isso evita introduzir um pacote de DI na Application e permite iniciar /health e Swagger sem registrar repositório ou desabilitar a validação global do container.
- **Até o C15 registrar IRepositorioJogos, operações de catálogo dependentes de persistência retornam 500 por serviço ausente.** A funcionalidade do C14 foi verificada integralmente com fake nos testes. Nenhum fake foi registrado em produção.
- Interpretação de sub válido: um único Guid diferente de Guid.Empty, documentada e testada; nenhum código de Identity foi necessário.
- Regras existentes preservadas, inclusive Guid.Empty na consulta → 400 e atualização de ID inexistente → 404. Não foi inventada validação adicional no domínio.
- Nenhum pacote adicionado; nenhum commit, staging ou push.
- git diff --check passou; Git apenas informou conversão futura LF → CRLF conforme sua configuração Windows.

## 19. git diff --stat

Saída para arquivos já rastreados. Os novos arquivos ainda não rastreados não aparecem nesse comando; estão integralmente listados nas seções 2 e 20.

```text
 README.md                                          | 145 +++++++--------------
 .../Authentication/AuthenticationExtensions.cs     |  39 ++++--
 src/FCG.Catalog.Api/Authentication/JwtOptions.cs   |   9 +-
 src/FCG.Catalog.Api/Program.cs                     |   2 +
 src/FCG.Catalog.Api/appsettings.json               |   2 +-
 tests/FCG.Catalog.IntegrationTests/HostTests.cs    |   9 +-
 tests/FCG.Catalog.UnitTests/README.md              |   5 +-
 7 files changed, 96 insertions(+), 115 deletions(-)
```

## 20. git status --short --untracked-files=all

```text
 M README.md
 M src/FCG.Catalog.Api/Authentication/AuthenticationExtensions.cs
 M src/FCG.Catalog.Api/Authentication/JwtOptions.cs
 M src/FCG.Catalog.Api/Program.cs
 M src/FCG.Catalog.Api/appsettings.json
 M tests/FCG.Catalog.IntegrationTests/HostTests.cs
 M tests/FCG.Catalog.UnitTests/README.md
?? RELATORIO-C14.md
?? src/FCG.Catalog.Api/Contracts/Catalog/Jogos/RequisicaoAtualizarJogo.cs
?? src/FCG.Catalog.Api/Contracts/Catalog/Jogos/RequisicaoCriarJogo.cs
?? src/FCG.Catalog.Api/Contracts/Catalog/Jogos/RespostaJogo.cs
?? src/FCG.Catalog.Api/Contracts/Catalog/Jogos/RespostaListaJogos.cs
?? src/FCG.Catalog.Api/Controllers/Catalog/JogosController.cs
?? src/FCG.Catalog.Api/IoC/ApplicationDependency.cs
?? src/FCG.Catalog.Application/Abstractions/Repositories/IRepositorioJogos.cs
?? src/FCG.Catalog.Application/Catalog/Jogos/ComandoAtualizarJogo.cs
?? src/FCG.Catalog.Application/Catalog/Jogos/ComandoCriarJogo.cs
?? src/FCG.Catalog.Application/Catalog/Jogos/ConsultaListarJogos.cs
?? src/FCG.Catalog.Application/Catalog/Jogos/ConsultaObterJogoPorId.cs
?? src/FCG.Catalog.Application/Catalog/Jogos/ManipuladorAtualizarJogo.cs
?? src/FCG.Catalog.Application/Catalog/Jogos/ManipuladorCriarJogo.cs
?? src/FCG.Catalog.Application/Catalog/Jogos/ManipuladorListarJogos.cs
?? src/FCG.Catalog.Application/Catalog/Jogos/ManipuladorObterJogoPorId.cs
?? src/FCG.Catalog.Application/Catalog/Jogos/ResultadoAtualizarJogo.cs
?? src/FCG.Catalog.Application/Catalog/Jogos/ResultadoCriarJogo.cs
?? src/FCG.Catalog.Application/Catalog/Jogos/ResultadoListarJogos.cs
?? src/FCG.Catalog.Application/Catalog/Jogos/ResultadoObterJogo.cs
?? src/FCG.Catalog.Domain/Catalog/Entities/Categoria.cs
?? src/FCG.Catalog.Domain/Catalog/Entities/CategoriaJogo.cs
?? src/FCG.Catalog.Domain/Catalog/Entities/Jogo.cs
?? tests/FCG.Catalog.IntegrationTests/CatalogFactory.cs
?? tests/FCG.Catalog.IntegrationTests/JogosApiTests.cs
?? tests/FCG.Catalog.IntegrationTests/JwtConfigurationTests.cs
?? tests/FCG.Catalog.IntegrationTests/JwtTests.cs
?? tests/FCG.Catalog.UnitTests/Catalog/Categorias/TestesCategoria.cs
?? tests/FCG.Catalog.UnitTests/Catalog/Jogos/TestesJogo.cs
?? tests/FCG.Catalog.UnitTests/Catalog/Jogos/TestesLimitesManipuladores.cs
?? tests/FCG.Catalog.UnitTests/Catalog/Jogos/TestesManipuladorAtualizarJogo.cs
?? tests/FCG.Catalog.UnitTests/Catalog/Jogos/TestesManipuladorCriarJogo.cs
?? tests/FCG.Catalog.UnitTests/Catalog/Jogos/TestesManipuladorListarJogos.cs
?? tests/FCG.Catalog.UnitTests/Catalog/Jogos/TestesManipuladorObterJogo.cs
```
