# FCG.CatalogAPI

Microsserviço .NET 8 do FIAP Cloud Games. C13 forneceu o host; C14 migra o domínio e as operações de jogos, com autenticação JWT local e autorização administrativa.

## Escopo do C14

- Entidades `Jogo`, `Categoria` e `CategoriaJogo`, preservando propriedades, construtores, IDs e invariantes.
- `IRepositorioJogos`, comandos, consultas, resultados e handlers de criação, consulta por ID, listagem paginada e atualização.
- Contratos HTTP e `JogosController` preservados, com proteção explícita nas escritas.
- Nenhuma persistência, EF Core, DbContext, migration, conexão de banco, compra, pedido, biblioteca ou integração com broker.

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

O C15 deverá registrar uma implementação de `IRepositorioJogos`. Não há implementação de produção no C14. As factories scoped de handlers no composition root da API resolvem o repositório somente quando uma operação de catálogo é atendida. Isso mantém a validação padrão de DI habilitada e permite iniciar o host em Development/Production sem banco.

**Até o C15, operações de catálogo que precisam do repositório retornam 500 por dependência não registrada.** `/health` e Swagger funcionam sem repositório. Os testes registram um fake exclusivamente no assembly de testes via `WebApplicationFactory`; as rotas e casos de uso são exercitados integralmente com ele.

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

Os testes unitários cobrem domínio e os quatro handlers. Os testes de integração cobrem contratos, paginação, erros, 401/403, role administrativa, duas chaves por kid e tokens inválidos. Chaves RSA são geradas e descartadas somente em memória nos testes. Health e Swagger também são testados sem repositório e sem chaves configuradas.

Versões continuam centralizadas em `Directory.Packages.props`; nenhum pacote novo foi necessário.
