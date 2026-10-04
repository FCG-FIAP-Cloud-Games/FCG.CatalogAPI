# FCG.CatalogAPI

Microsserviço .NET 8 do FIAP Cloud Games. C13 forneceu o host; C14 migrou domínio, operações e segurança; C15 adiciona persistência PostgreSQL e migrations próprias.

## Escopo atual (C14 + C15)

- Entidades `Jogo`, `Categoria` e `CategoriaJogo`, preservando propriedades, construtores, IDs e invariantes.
- `IRepositorioJogos`, comandos, consultas, resultados e handlers de criação, consulta por ID, listagem paginada e atualização.
- Contratos HTTP e `JogosController` preservados, com proteção explícita nas escritas.
- Persistência EF Core 8/Npgsql exclusiva para jogos e categorias. Sem compra, pedido, biblioteca ou broker.

## Rotas

| Método | Rota | Acesso | Respostas de negócio |
| --- | --- | --- | --- |
| POST | `/api/v1/jogos` | role `Administrador` | 201 e Location; 400 por dados inválidos |
| GET | `/api/v1/jogos/{id}` | Público | 200; 400 por GUID vazio; 404 se inexistente |
| GET | `/api/v1/jogos` | Público | 200; 400 por paginação inválida |
| PUT | `/api/v1/jogos/{id}` | role `Administrador` | 200; 400 por dados inválidos; 404 se inexistente |

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
