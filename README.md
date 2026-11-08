# FCG.CatalogAPI

## 1. Apresentação

Microsserviço do FIAP Cloud Games responsável pelo catálogo de jogos, pedidos de compra e biblioteca do usuário.

### Responsabilidades

- Criar, consultar, listar e atualizar jogos.
- Registrar pedidos com o preço atual do catálogo e idempotência.
- Consultar os jogos adquiridos pelo usuário autenticado.
- Publicar `OrderPlacedEvent` no RabbitMQ por meio de uma Outbox persistida no PostgreSQL.
- Consumir `PaymentProcessedEvent` de forma idempotente, finalizar pedidos e conceder jogos aprovados.

## 2. Arquitetura e tecnologias

.NET 8, ASP.NET Core, EF Core 8 com Npgsql/PostgreSQL, RabbitMQ.Client, JWT RS256, Swagger e xUnit. Versões centralizadas em `Directory.Packages.props`.

| Projeto | Responsabilidade |
| --- | --- |
| `src/FCG.Catalog.Api` | HTTP, contratos, Swagger, autenticação e inicialização. |
| `src/FCG.Catalog.Application` | Casos de uso e interfaces. |
| `src/FCG.Catalog.Domain` | Entidades e regras de negócio. |
| `src/FCG.Catalog.Infrastructure` | PostgreSQL, EF Core, Outbox, Inbox e RabbitMQ. |

Users emite tokens; Catalog mantém jogos, pedidos e aquisições; Payments processa pagamentos e devolve o resultado por eventos. A validação JWT não realiza login nem uma consulta síncrona ao Users por requisição.

## 3. Pré-requisitos

- Git e PowerShell; acesso aos repositórios.
- SDK .NET 8 para execução local e testes; acesso ao NuGet para restore.
- PostgreSQL com banco exclusivo do Catalog e usuário autorizado a aplicar o schema.
- RabbitMQ com usuário e virtual host configurados para a mensageria.
- UsersAPI configurada e disponível para cadastro/login e fornecimento da chave pública.
- PaymentsAPI e suas filas/bindings ativos para concluir o fluxo até a biblioteca.
- Docker Desktop com containers Linux para usar Docker; kubectl e acesso ao cluster somente para Kubernetes.

Em qualquer pasta do PowerShell, verifique as ferramentas do modo escolhido:

```powershell
git --version
dotnet --list-sdks
docker version
```

Confirme SDK `8.0.x` e, para Docker, as seções Client e Server. Docker não é obrigatório para executar via .NET com dependências já provisionadas.

## 4. Clonagem e instalação

Na pasta onde deseja guardar os projetos, execute (o endereço abaixo corresponde ao remoto desta cópia):

```powershell
git clone https://github.com/gapima/FCG.CatalogAPI.git
Set-Location FCG.CatalogAPI
Test-Path ./FCG.Catalog.sln
dotnet restore ./FCG.Catalog.sln
dotnet build ./FCG.Catalog.sln --no-restore
```

`Test-Path` deve retornar `True`; restore e build devem terminar sem erros. Se já clonou, entre na raiz que contém `FCG.Catalog.sln`. Os comandos seguintes usam essa raiz, salvo indicação explícita. O repositório oficial de autenticação é [FCG.UsersAPI](https://github.com/FCG-FIAP-Cloud-Games/FCG.UsersAPI).

Escolha um modo: container avulso com dependências existentes (seção 6), host .NET (seção 7), ou composição integrada (seção 6). Não execute instâncias diferentes na mesma porta.

## 5. Variáveis de ambiente

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
| `RabbitMq__ConsumerEnabled` | Habilita o consumer de pagamentos; `true`. |
| `RabbitMq__PaymentProcessedExchange` | Exchange fanout de pagamentos; `PaymentProcessedEvent`. |
| `RabbitMq__PaymentProcessedQueue` | Fila própria do Catalog; `catalog-payment-processed`. |
| `RabbitMq__PaymentProcessedErrorQueue` | Fila de erro; `catalog-payment-processed-error`. |
| `Outbox__Enabled` | Habilita o publicador em segundo plano; `true`. |
| `Jwt__Issuer` | Emissor aceito; `FIAP.CloudGames`. |
| `Jwt__Audience` | Audiência aceita; `FIAP.CloudGames.Api`. |
| `Jwt__ClockSkewSeconds` | Tolerância de horário, não negativa; `30` segundos. |
| `Jwt__PublicKeys__0__Kid` | Identificador da chave pública, correspondente ao `kid` do token. |
| `Jwt__PublicKeys__0__PublicKeyPem` | Conteúdo PEM da chave pública RSA; alternativa ao arquivo. |
| `Jwt__PublicKeys__0__PublicKeyPath` | Caminho absoluto do arquivo PEM público, lido uma vez no startup; alternativa ao conteúdo inline. |
| `DemonstracaoLocal__Habilitada` | `false` por padrão. Em `Development`, `true` aplica as migrations antes de abrir HTTP e iniciar os workers. |
| `ASPNETCORE_ENVIRONMENT` | Use `Development` para habilitar Swagger. |
| `Jwt__JwksUri` | URL confiável do JWKS; vazia por padrão. Alternativa exclusiva ao PEM. |
| `Jwt__JwksRefreshSeconds` | Atualização do cache; `60`, mínimo `5`. |
| `Jwt__JwksMaxAgeSeconds` | Validade do cache; `900`, maior que o intervalo de atualização. |
| `ASPNETCORE_HTTP_PORTS` | No Dockerfile, `8080`; no host use `--urls` explicitamente. |

PostgreSQL é necessário para os dados; RabbitMQ e Payments são necessários para concluir a compra. Para aceitar JWTs, configure **JWKS ou PEM**, nunca ambos. `Issuer` e `Audience` devem coincidir com Users. `RabbitMq__UseTls` não é uma opção implementada pelo Catalog, mesmo quando aparece no Compose; não a use como garantia de TLS.

As variáveis PowerShell valem para processos iniciados na mesma sessão. O .NET não carrega arquivos `.env` automaticamente. O Compose usa seu próprio `.env` para interpolar o YAML e não herda automaticamente toda configuração da sessão para o container.

### Configurar execução no host

Na raiz do Catalog, na mesma sessão dos comandos .NET, substitua os valores entre `<...>` por recursos existentes:

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:ConnectionStrings__CatalogDatabase = 'Host=<host>;Port=<porta>;Database=<banco-catalogo>;Username=<usuario>;Password=<senha>'
$env:RabbitMq__Host = '<host-do-broker>'
$env:RabbitMq__Port = '5672'
$env:RabbitMq__VirtualHost = '/'
$env:RabbitMq__Username = '<usuario-do-broker>'
$env:RabbitMq__Password = '<senha-do-broker>'
$env:RabbitMq__Exchange = 'OrderPlacedEvent'
$env:RabbitMq__ConsumerEnabled = 'true'
$env:Outbox__Enabled = 'true'
$env:Jwt__Issuer = 'FIAP.CloudGames'
$env:Jwt__Audience = 'FIAP.CloudGames.Api'
# Users no host, com a porta padrão do Compose:
$env:Jwt__JwksUri = 'http://localhost:5088/.well-known/jwks.json'
Invoke-RestMethod $env:Jwt__JwksUri
```

O último comando deve retornar `keys` com a chave RSA pública. Ajuste a URL se Users estiver em outro endereço. Remova configurações `Jwt__PublicKeys__*` da sessão antes de usar JWKS. HTTP só é aceito em `Development`; fora dele, JWKS exige HTTPS. A URL não pode conter credenciais, query string ou fragmento. Redirecionamentos não são seguidos.

Alternativa PEM: deixe `Jwt__JwksUri` ausente/vazia, configure `Jwt__PublicKeys__0__Kid` com o `kid` do emissor e **exatamente uma** origem:

```powershell
Remove-Item Env:Jwt__JwksUri -ErrorAction SilentlyContinue
$env:Jwt__PublicKeys__0__Kid = '<kid-da-chave-do-Users>'
$env:Jwt__PublicKeys__0__PublicKeyPath = (Resolve-Path '<arquivo-publico.pem>').Path
# Alternativa ao Path (não configure as duas):
# $env:Jwt__PublicKeys__0__PublicKeyPem = Get-Content -Raw '<arquivo-publico.pem>'
```

O PEM inline exige quebras de linha reais. Para outras chaves, use índices `1`, `2` etc., com `Kid` único. Somente chaves públicas RSA são aceitas; o volume privado do Users nunca deve ser montado no Catalog. PEM é lido no startup e exige reinício após rotação. Arquivo ausente, chave privada/inválida, origens simultâneas ou `Kid` duplicado impedem a inicialização. Sem PEM nem JWKS, o host pode iniciar, mas nenhum JWT é aceito.

No JWKS, a atualização ocorre em segundo plano; a validação do token permanece local. Em falhas, apenas o cache ainda válido é utilizado. `kid` desconhecido pode disparar atualização, mas não torna o token imediatamente válido: repita após o cache atualizar.


## 6. Execução com Docker

### Container avulso

O Dockerfile publica a API e escuta em `8080`, como usuário não root. Ele não cria PostgreSQL, RabbitMQ nem Users. Na raiz do Catalog, configure as variáveis da seção 5 para endereços acessíveis **de dentro do container**. Em Docker Desktop, dependências no host usam `host.docker.internal`; `localhost` aponta para o próprio container. PostgreSQL e RabbitMQ precisam ter portas publicadas/acessíveis.

Exemplo usando JWKS do Users publicado no host. A porta externa `5080` é escolhida explicitamente neste exemplo; não é uma porta padrão do projeto:

```powershell
$env:Jwt__JwksUri = 'http://host.docker.internal:5088/.well-known/jwks.json'
# Ajuste também ConnectionStrings__CatalogDatabase e RabbitMq__Host para o container.
# Banco existente; aplica o schema antes do HTTP, somente em Development:
$env:DemonstracaoLocal__Habilitada = 'true'
docker build -t fcg-catalog-api .
docker run --rm --name fcg-catalog-local -p 127.0.0.1:5080:8080 `
  -e ASPNETCORE_ENVIRONMENT -e ConnectionStrings__CatalogDatabase `
  -e RabbitMq__Host -e RabbitMq__Port -e RabbitMq__VirtualHost `
  -e RabbitMq__Username -e RabbitMq__Password -e RabbitMq__Exchange `
  -e RabbitMq__ConsumerEnabled -e Outbox__Enabled `
  -e Jwt__Issuer -e Jwt__Audience -e Jwt__JwksUri `
  -e DemonstracaoLocal__Habilitada fcg-catalog-api
```

O terminal permanece acompanhando a API. Em outro PowerShell, verifique:

```powershell
docker ps --filter name=fcg-catalog-local
docker logs --tail 100 fcg-catalog-local
Invoke-WebRequest http://localhost:5080/health -UseBasicParsing
```

Espere HTTP 200 após carregar as chaves. Acesse [Swagger local](http://localhost:5080/swagger/index.html). `Ctrl+C` encerra o container avulso; `--rm` remove esse container, sem apagar bancos externos.

### Composição integrada da infraestrutura

O Compose não fica neste repositório. A composição consultada está em [FCG-FIAP-Repo-Infra](https://github.com/FCG-FIAP-Cloud-Games/FCG-FIAP-Repo-Infra), pasta `docker-compose`.

Na pasta pai dos projetos, clone se necessário e entre nela:

```powershell
git clone https://github.com/FCG-FIAP-Cloud-Games/FCG-FIAP-Repo-Infra.git
Set-Location ./FCG-FIAP-Repo-Infra/docker-compose
```

Antes de iniciar, configure o ambiente dessa infraestrutura conforme seu README: `POSTGRES_ADMIN_PASSWORD`; `USERS_DB_NAME/USER/PASSWORD`, `CATALOG_DB_NAME/USER/PASSWORD`, `PAYMENTS_DB_NAME/USER/PASSWORD`, `NOTIFICATIONS_DB_NAME/USER/PASSWORD`; `RABBITMQ_USER/PASSWORD`; `USERS_IMAGE`, `CATALOG_IMAGE`, `PAYMENTS_IMAGE` e `SMTP_USER`, `SMTP_PASSWORD`, `SMTP_FROM`, obrigatórias para Notifications nessa composição. São variáveis de interpolação do Compose, distintas das opções .NET. Use segredos próprios, sem versioná-los. O bootstrap Users exige o banco de demonstração `fcg_users_demo`. Não reutilize volumes de outra composição inadvertidamente: ambas as cópias consultadas usam o nome `fcg-demo`.

Com os requisitos da infraestrutura preparados, nessa pasta:

```powershell
docker compose config --quiet
# Prossiga somente se a validação anterior terminar sem erro.
docker compose up -d
docker compose ps -a
docker compose logs --tail 100 usersapi catalogapi paymentsapi
```

A validação silenciosa evita imprimir configurações resolvidas com segredos. Confirme serviços em execução, Users saudável e migrations concluídas nos logs do Catalog. Imagens configuradas precisam conter o bootstrap/exportação de chave compatível com esses manifests; a existência de uma tag não comprova que contém o código local.

| Aspecto | Compose do repositório Infra | Compose da pasta superior do workspace consultado |
| --- | --- | --- |
| Catalog no host | `5081` (ou `FCG_CATALOG_HTTP_PORT`) | `5081` (ou override) |
| Users no host | `5088` (ou `FCG_USERS_HTTP_PORT`) | `5088` (ou override) |
| Chave Catalog | PEM por arquivo `/data/jwt-publica/public.pem`, volume somente leitura | PEM inline por `FCG_JWT_PUBLIC_KEY_PEM` |
| Migrations Catalog | `DemonstracaoLocal__Habilitada=true` | Flag não configurada; aplicar separadamente |
| Dependência Users | Aguarda Users saudável | Não aguarda Users |
| JWKS | Não configurado | Não configurado |

No Compose Infra, Users exporta a chave pública; o Catalog não recebe o volume privado. Os dois serviços usam issuer `FIAP.CloudGames`, audience `FIAP.CloudGames.Api` e `kid=users-demo-v1`. A composição superior não acompanha um clone do Catalog e não deve ser tratada como caminho de instalação portátil. Nenhuma dessas configurações comprova o estado de um ambiente em execução.

PostgreSQL (`5432`) e AMQP (`5672`) ficam internos nas composições consultadas; a porta de gerenciamento RabbitMQ `15672` não substitui AMQP. Portanto, uma API iniciada no host não pode simplesmente usar `localhost:5432/5672` para esses containers sem exposição adicional. O modo local da seção 7 pressupõe dependências acessíveis ao host.

## 7. Execução local com .NET 8

Na raiz do Catalog, configure a seção 5 e aplique as migrations da seção 8 antes de executar. Alternativamente, com o banco já criado, use a flag de demonstração para aplicar migrations automaticamente.

```powershell
$env:ASPNETCORE_ENVIRONMENT = 'Development'
$env:DemonstracaoLocal__Habilitada = 'true'
dotnet run --project src/FCG.Catalog.Api --no-launch-profile --urls 'http://localhost:5080'
```

`5080` é a porta escolhida por `--urls`, não uma configuração versionada. Não há `launchSettings.json` no Catalog. Espere o log de escuta HTTP; em outro terminal:

```powershell
Invoke-WebRequest http://localhost:5080/health/live -UseBasicParsing
Invoke-WebRequest http://localhost:5080/health -UseBasicParsing
Invoke-RestMethod 'http://localhost:5080/api/v1/jogos?pagina=1&tamanhoPagina=20'
```

Os checks devem retornar 200; a listagem deve retornar `itens`, `pagina` e `tamanhoPagina`, inclusive com `itens` vazio em banco novo. A listagem verifica também acesso ao schema. `/health/live` verifica o host; `/health` inclui prontidão do cache somente quando JWKS está configurado (503 sem chaves utilizáveis). Nenhum dos dois verifica PostgreSQL ou RabbitMQ. Em PEM, um health 200 não prova que o JWT do Users será aceito.

## 8. Banco de dados e migrations

O CatalogAPI usa banco próprio para catálogo, pedidos, aquisições, Outbox e Inbox. Configure `ConnectionStrings__CatalogDatabase` antes dos comandos EF. O usuário precisa de permissão para alterar o schema; se o banco ainda não existir, precisa também de `CREATEDB`, ou o banco deve ser criado previamente.

```powershell
dotnet tool restore

# Listar migrations
dotnet ef migrations list --project src/FCG.Catalog.Infrastructure --startup-project src/FCG.Catalog.Api --context CatalogDbContext

# Aplicar migrations
dotnet ef database update `
  --project src/FCG.Catalog.Infrastructure `
  --startup-project src/FCG.Catalog.Api `
  --context CatalogDbContext

# Verificar divergências entre o modelo e a última migration
dotnet ef migrations has-pending-model-changes --project src/FCG.Catalog.Infrastructure --startup-project src/FCG.Catalog.Api --context CatalogDbContext
```

O manifesto local fornece `dotnet-ef` 8.0.28. Por padrão, a aplicação não altera o schema ao iniciar. Para a demonstração integrada, configure `ASPNETCORE_ENVIRONMENT=Development` e `DemonstracaoLocal__Habilitada=true`: o Catalog aplica suas migrations existentes antes de iniciar HTTP, Outbox e o consumer. A infraestrutura deve criar previamente o banco e o usuário com permissão de DDL; não é necessário executar os comandos EF manualmente nesse modo.

A aplicação usa um advisory lock de sessão do PostgreSQL para serializar suas migrations em inicializações concorrentes. Ao reiniciar, somente migrations pendentes são aplicadas e dados existentes são preservados. Falhas de conexão, de permissão ou de migration impedem o startup. A flag inválida ou habilitada fora de `Development` é rejeitada; para produção, mantenha o processo de migração controlado separadamente.

Após `database update`, `migrations list` não deve indicar migrations pendentes. Execute os comandos na raiz do Catalog com as variáveis da seção 5 na mesma sessão. O banco da demonstração precisa existir antes do startup automático: a abertura da conexão ocorre antes de `MigrateAsync`. Não confunda migrations do Catalog com as de Users ou Payments.

## 9. Swagger e autenticação

O Swagger do Catalog existe somente em `Development`: [host/container avulso](http://localhost:5080/swagger/index.html) ou [Compose integrado](http://localhost:5081/swagger/index.html). O documento OpenAPI está em `/swagger/v1/swagger.json`.

### Como autenticar na CatalogAPI utilizando a UsersAPI

1. Inicie a UsersAPI com banco e chaves preparados. Na composição integrada, abra [Swagger da UsersAPI](http://localhost:5088/swagger/index.html). Se a porta foi sobrescrita, ajuste a URL. Users exige `Development` **e** `Swagger__Enabled=true`; Catalog exige somente `Development`.
2. Expanda **POST /api/v1/usuarios**, clique em **Try it out**, preencha o JSON e clique em **Execute**. Exemplo completo com dados fictícios (troque CPF/e-mail por dados de teste válidos e ainda não cadastrados; substitua a senha):

```json
{
  "nome": "Pessoa de Teste",
  "cpf": "11144477735",
  "dataNascimento": "1995-05-20",
  "email": "pessoa.catalogo@example.com",
  "senha": "<senha-de-teste-forte>"
}
```

Use nome com pelo menos três caracteres e senha com pelo menos oito caracteres; as demais validações são informadas na resposta da UsersAPI. O sucesso do cadastro retorna 201. Em 400, corrija os campos apontados; em conflito, use outro cadastro de teste ou faça login no existente. O perfil não pertence ao contrato público: esse cadastro cria `Usuario`, nunca `Administrador`.

3. Em **POST /api/v1/auth/login**, use **Try it out → Execute**, com o mesmo e-mail e a senha efetivamente escolhida:

```json
{
  "email": "pessoa.catalogo@example.com",
  "senha": "<senha-escolhida-no-cadastro>"
}
```

4. Espere 200 e localize **`accessToken`** em **Response body**. Copie somente seu valor, sem aspas. Não confunda com `refreshToken`. Exemplo estrutural completo, com tokens omitidos e valores ilustrativos:

```json
{
  "accessToken": "<JWT-retornado-pelo-login>",
  "refreshToken": "<refresh-token-retornado-pelo-login>",
  "tokenType": "Bearer",
  "expiresIn": 900,
  "expiresAt": "2026-11-08T15:15:00+00:00",
  "usuario": {
    "id": "11111111-1111-4111-8111-111111111111",
    "nome": "Pessoa de Teste",
    "email": "pessoa.catalogo@example.com",
    "perfilId": "22222222-2222-4222-8222-222222222222",
    "perfil": "Usuario"
  }
}
```

5. Abra o Swagger do Catalog na porta do modo escolhido. Clique em **Authorize**, cole apenas o `accessToken` no campo **Bearer**, clique em **Authorize** e **Close**. Não digite o prefixo `Bearer` nesse campo.
6. Execute **GET /api/v1/biblioteca → Try it out → Execute**: espere 200 e uma lista, inicialmente vazia. Isso comprova que o token do Users foi aceito. Também é possível criar pedidos como usuário comum. Em clientes HTTP, envie `Authorization: Bearer <accessToken>`.
7. Para criar/atualizar jogos, faça login na UsersAPI com uma conta já provisionada como **Administrador**. Na demonstração integrada, o bootstrap Users cria o primeiro administrador em banco novo, antes do HTTP; se já existir um, preserva sua senha. Obtenha as credenciais de demonstração com o responsável pelo ambiente, confira `usuario.perfil = Administrador` no login e substitua o token no **Authorize** do Catalog (use **Logout** nesse diálogo antes de colar outro token). Nenhuma credencial é reproduzida aqui. Não há promoção pelo cadastro público nem endpoint de login administrativo no Catalog. Se o bootstrap não foi executado, o responsável precisa provisionar a conta pelo procedimento administrativo da UsersAPI.

O JWT é emitido pela UsersAPI, com validade de **15 minutos** na configuração integrada (`expiresIn=900`). Catalog valida localmente RS256, assinatura/chave por `kid`, issuer, audience e validade; exige `exp` e exatamente um `sub` GUID não vazio. O claim `role` precisa ser exatamente `Administrador` para criação/atualização de jogos. Administradores também podem consultar pedidos de terceiros, mas a biblioteca continua limitada ao próprio `sub`.

### Expiração, refresh, 401 e 403

Compare `expiresAt` com o horário atual (o offset faz parte da data). O claim `exp` também representa a expiração, em segundos Unix; decodificar o payload não valida a assinatura. Não envie tokens a decodificadores externos. O Catalog permite a tolerância configurada de 30 segundos, que não deve ser usada como extensão de sessão.

- **401:** token ausente, expirado ou inválido (assinatura, chave, issuer, audience ou identidade). Não conclua que todo 401 é expiração: verifique `expiresAt` e a configuração.
- **403:** token aceito, mas operação não autorizada; por exemplo, usuário comum criando jogo ou consultando pedido de outra pessoa.

Para renovar, no Swagger Users execute **POST /api/v1/auth/refresh**:

```json
{
  "refreshToken": "<refresh-token-atual-retornado-pelo-Users>"
}
```

A resposta possui o mesmo formato do login. Guarde o novo par de tokens: o refresh gira e o anterior não deve ser reutilizado. A configuração integrada prevê sete dias para refresh. Se a renovação retornar 401, faça login novamente. Atualize o **Authorize** do Catalog com o novo `accessToken`; a troca não ocorre automaticamente entre os dois Swaggers.

## 10. Endpoints disponíveis

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
| GET | `/health` | Pública | Prontidão; inclui cache no modo JWKS. |
| GET | `/health/live` | Pública | Host, sem o check de prontidão JWKS. |

### Idempotency-Key e pedidos

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
- Criar um pedido não concede o jogo imediatamente. O consumer de pagamento finaliza o pedido e concede o jogo somente quando recebe `Approved`.

No Swagger desta versão, o plugin gera e preserva a chave por intenção de compra na sessão do navegador; o campo manual fica oculto. **Execute** repete a mesma intenção para a mesma conta/jogo. Após timeout, repita sem trocar conta/jogo nem limpar a sessão. Após rejeição, **Nova intenção de compra** prepara outra chave; não use enquanto o resultado anterior for desconhecido. Se a imagem executada não contiver o plugin, preencha manualmente um UUID gerado por `[guid]::NewGuid().ToString()` e reutilize-o nos retries. O exemplo estático do OpenAPI não é uma nova chave a cada envio.

### Biblioteca do usuário

`GET /api/v1/biblioteca` usa exclusivamente o `sub` do JWT e retorna apenas a biblioteca do próprio usuário, inclusive para administradores. A posse é representada por `Aquisicao` no banco do catálogo.

A resposta contém `gameId`, `title` e `acquiredAt`, ou uma lista vazia. Não há paginação nem endpoint público de concessão de jogos.

## 11. Exemplo completo de utilização

### Pelo Swagger

1. **Autenticar:** siga cadastro/login da seção 9; mantenha o token do usuário comprador e obtenha separadamente o token administrativo.
2. **Consultar catálogo:** no Swagger Catalog, `GET /api/v1/jogos`, página 1 e tamanho 20 → Execute → 200. Essa operação é pública.
3. **Criar jogo:** autorize com o token administrativo, execute `POST /api/v1/jogos` com o JSON abaixo. Espere 201 e copie o `id` da resposta.

```json
{
  "titulo": "Aventura FIAP",
  "descricao": "Jogo fictício para validar a compra integrada",
  "faixaEtaria": "12",
  "preco": 49.90
}
```

4. **Consultar jogo criado:** execute `GET /api/v1/jogos/{id}` usando o ID retornado; espere 200 e confira título/preço.
5. **Criar pedido:** troque o **Authorize** para o token do comprador. Em `POST /api/v1/pedidos`, informe o JSON abaixo, substituindo o GUID pelo `id` real. O plugin cuida da chave conforme a seção 10. Espere 202, anote `orderId` e `Location`.

```json
{
  "jogoId": "33333333-3333-4333-8333-333333333333"
}
```

6. **Consultar status:** execute `GET /api/v1/pedidos/{id}` com **orderId**, não com o ID do jogo. O corpo possui `orderId`, `gameId`, `price`, `currency`, `status`, `createdAt` e `updatedAt`. Inicialmente o status é `PendingPayment`; se o processamento já terminou, pode aparecer `Paid` ou `Rejected`.
7. **Verificar pagamento:** aguarde e repita o GET. `Paid` comprova que Catalog consumiu uma aprovação e confirmou a transação; `Rejected` indica rejeição sem concessão. Catalog não tem endpoint para aprovar pagamento. A conclusão depende de Payments consumindo `OrderPlacedEvent` e publicando `PaymentProcessedEvent`, RabbitMQ acessível e consumer Catalog habilitado. O preço do exemplo não garante aprovação em todos os ambientes: a regra pertence ao Payments. Não foi necessário presumir um endpoint HTTP desse serviço.
8. **Consultar biblioteca:** com o mesmo token do comprador, execute `GET /api/v1/biblioteca`. Após `Paid`, espere um item com `gameId` igual ao jogo comprado, `title` e `acquiredAt`. Antes da aprovação ou após rejeição, essa compra não concede o jogo. Consultar com o token administrativo mostraria a biblioteca do administrador.

### Repetir chamadas no PowerShell

Após os logins pelo Swagger, em qualquer pasta, preencha os tokens apenas na sessão local. `Read-Host` evita colocar seus valores literais no histórico de comandos; não compartilhe a sessão ou sua saída:

```powershell
$catalogUrl = 'http://localhost:5081' # Compose; use 5080 no exemplo local
$tokenUsuario = Read-Host 'Cole o accessToken do comprador'
$tokenAdmin = Read-Host 'Cole o accessToken do administrador'
Invoke-RestMethod "$catalogUrl/api/v1/jogos?pagina=1&tamanhoPagina=20"
$jogo = Invoke-RestMethod "$catalogUrl/api/v1/jogos" -Method Post `
  -Headers @{ Authorization = "Bearer $tokenAdmin" } -ContentType 'application/json' `
  -Body (@{ titulo='Aventura FIAP'; descricao='Jogo de teste'; faixaEtaria='12'; preco=49.90 } | ConvertTo-Json)
Invoke-RestMethod "$catalogUrl/api/v1/jogos/$($jogo.id)"
$chaveCompra = [guid]::NewGuid().ToString()
$headersCompra = @{ Authorization = "Bearer $tokenUsuario"; 'Idempotency-Key' = $chaveCompra }
$corpoPedido = @{ jogoId = $jogo.id } | ConvertTo-Json
$pedido = Invoke-RestMethod "$catalogUrl/api/v1/pedidos" -Method Post `
  -Headers $headersCompra -ContentType 'application/json' -Body $corpoPedido
$pedido
# Repita este GET até observar o resultado; não crie nova intenção para consultar.
Invoke-RestMethod "$catalogUrl/api/v1/pedidos/$($pedido.orderId)" -Headers $headersCompra
Invoke-RestMethod "$catalogUrl/api/v1/biblioteca" -Headers $headersCompra
```

Em timeout do POST, repita a mesma chamada com `$headersCompra` e `$corpoPedido`, sem gerar outra chave. Reexecutar o bloco completo cria outro jogo/intenção, portanto não é um procedimento de retry.

## 12. Fluxo de compra e mensageria

```text
Comprador → Catalog → pedido PendingPayment + Outbox (mesma transação)
                    → RabbitMQ / OrderPlacedEvent → Payments
                    ← RabbitMQ / PaymentProcessedEvent
                    → Paid + aquisição na biblioteca, ou Rejected
```

### RabbitMQ e Outbox

```text
CatalogAPI → Outbox → OrderPlacedEvent → RabbitMQ
```

Configure a conexão pelas variáveis `RabbitMq__*`. O publicador declara o exchange configurado como `fanout`, durável e sem exclusão automática. O usuário do broker precisa de acesso ao virtual host e permissão para declarar o exchange e publicar.

Para `OrderPlacedEvent`, provisione a fila do destinatário e seu binding ao exchange para receber os eventos. Sem rota disponível ou com falha no broker, as mensagens continuam pendentes para novas tentativas.

Pedido e evento são gravados juntos no banco; um serviço em segundo plano publica a Outbox. A entrega pode se repetir, portanto consumidores devem deduplicar por `EventId`. Com `Outbox__Enabled=false`, a publicação fica desativada, mas novos pedidos continuam gravando eventos na Outbox.

### Consumo de pagamentos

```text
PaymentProcessedEvent → CatalogAPI → Pedido Paid/Rejected → Biblioteca quando Approved
```

O consumer usa RabbitMQ.Client, declara exchange fanout e filas duráveis, sem exclusão automática, e consome com prefetch 1 e ACK manual. A fila do Catalog é independente de qualquer fila de Notifications. `RabbitMq__ConsumerEnabled=false` desativa apenas esse consumer. A conexão usa as mesmas opções `RabbitMq__Host/Port/VirtualHost/Username/Password` da Outbox. Falhas no broker provocam reconexão a cada 2 segundos sem impedir o startup HTTP.

O transporte aceito é `application/vnd.masstransit+json`: o adapter da Infrastructure extrai `message` do envelope MassTransit. Domain/Application não conhecem o envelope. `amount` aceita string decimal invariável (como `"99.90"`) ou número JSON decimal, sem `double` nem arredondamento. Content-Type desconhecido, envelope incompleto e contratos inválidos são permanentes.

A idempotência usa `message.eventId` e o consumer estável `Catalog.PaymentProcessedEvent` na tabela `inbox_messages`, com índice único por consumer/evento. O `MessageId` AMQP não substitui o EventId. A correlação de negócio vem de `message.correlationId`.

O processamento abre uma transação e adquire o mesmo advisory transaction lock de criação de pedidos, por usuário/jogo. Reconsulta Inbox, pedido e posse após o lock; compara usuário, jogo, preço contratado e moeda persistidos no pedido. A moeda deve ter três letras ASCII maiúsculas e coincidir exatamente com o pedido; não há conversão cambial ou consulta ao preço atual para validar o pagamento.

`Approved` grava Paid, aquisição com PedidoId e Inbox em um único commit. `Rejected` grava Rejected e Inbox sem conceder jogo. Estados terminais compatíveis preservam UpdatedAt; resultados contraditórios vão para erro. Aquisições históricas válidas são preservadas; vínculos inconsistentes de PedidoId são rejeitados.

O ACK de sucesso ocorre após commit. Falhas permanentes são transferidas diretamente para `catalog-payment-processed-error`. Falhas de infraestrutura têm três tentativas totais, com esperas assíncronas de 200 e 400 ms. O header `x-catalog-payment-attempt` começa em 1 quando ausente e é incrementado na republicação persistente para a própria fila. `x-catalog-payment-error` registra apenas o código/tipo técnico.

Retry e fila de erro preservam os bytes originais, MessageId, CorrelationId, ContentType e headers, sem gerar EventId. O consumer confirma a publicação com `mandatory` e publisher confirms antes de ACK da entrega transferida. Se a transferência falhar, a entrega fica sem ACK e a conexão é recriada. Quedas entre publicação e ACK podem duplicar entregas; a Inbox protege os efeitos de negócio. Indisponibilidade do próprio broker mantém a entrega pendente até reconexão.

Logs incluem EventId, CorrelationId, PaymentId, OrderId, Status, tentativa e resultado, sem payload completo ou credenciais.

## 13. Testes automatizados

Execute na raiz do Catalog, com SDK .NET 8. Sucesso exige ausência de falhas; confira também quantos testes foram ignorados, pois isso limita a cobertura realizada.

```powershell
dotnet test
```

A solução inclui testes unitários e de integração. Quando `ConnectionStrings__CatalogDatabase` está definida, testes de leitura verificam o schema desse banco; mantenha suas migrations aplicadas.

### Testes de integração externos

Alguns testes usam PostgreSQL e RabbitMQ reais. `CATALOG_TEST_CONNECTION` e `RABBITMQ_TEST_CONNECTION` são exclusivas dos testes de integração e não configuram a execução normal da API. Configure somente ambientes de desenvolvimento descartáveis:

| Variável | Configuração |
| --- | --- |
| `CATALOG_TEST_CONNECTION` | Connection string PostgreSQL de manutenção; usuário com permissão para criar e remover bancos de teste. |
| `RABBITMQ_TEST_CONNECTION` | URI `amqp://<usuario>:<senha>@<host>:<porta>/<vhost>` de um virtual host exclusivo, vazio e descartável. Também exige `CATALOG_TEST_CONNECTION`. |

Após configurar essas variáveis, execute `dotnet test`. Sem as variáveis necessárias, os respectivos testes externos são ignorados.

Os testes de pagamento criam bancos e filas exclusivos, verificam rollback, redelivery, ACK, retry, fila de erro e concorrência com o POST. Para exercitar também o publisher real do Payments, sem editar seus fontes nem adicionar MassTransit ao Catalog:

```powershell
./tests/Invoke-PaymentsInterop.ps1 -PaymentsRepository '../FCG.PaymentsAPI'
```

O script compila o contrato e publisher existentes em um executável temporário com MassTransit 8.3.6 e publica no exchange oficial do vhost de testes. O teste captura e valida o envelope real, o commit e o ACK. Esse teste isola o publisher; não executa o fluxo HTTP/banco/Outbox completo do Payments. Alternativamente, `PAYMENTS_TEST_PUBLISHER` pode apontar para esse executável DLL para incluir a interoperabilidade em `dotnet test`.

## 14. Execução no Kubernetes

A infraestrutura possui `k8s/kustomization.yaml` e namespace `fcg-demo`. Porém, nesta cópia consultada, `k8s/catalog-api/catalog-api-deployment.yml` e `k8s/users-api/users-api-deployment.yml` estão **vazios (0 bytes)**. Assim, não há Deployment/Service/portas/probes do Catalog e Users verificáveis para orientar uma implantação reproduzível. O ConfigMap compartilhado contém issuer/audience e configurações de assinatura do Users, mas isso não comprova a configuração de validação JWT/JWKS de um pod Catalog.

Na raiz do repositório Infra, os comandos abaixo são apenas inspeção de um cluster já existente:

```powershell
kubectl config current-context
kubectl get deployments,pods,services -n fcg-demo
```

Confirme o contexto antes de qualquer alteração. Não há comando de `apply`, rollout ou port-forward do Catalog validável com esses arquivos vazios. O responsável pela infraestrutura precisa completar/confirmar manifests, nomes dos recursos, imagens, Secrets, ConfigMaps, Services, probes e estratégia de migrations. Depois, poderá definir o acesso via Service/Ingress ou port-forward com as portas realmente declaradas.

Em Kubernetes, hostnames e volumes do Compose não são automaticamente reutilizados. Para JWKS, use URL do Users acessível ao pod, sem PEM simultâneo; fora de Development, HTTPS é obrigatório. Para PEM, monte apenas a chave pública e use caminho absoluto legível. Produção exige migrations controladas separadamente: a flag de demonstração é recusada fora de Development. `/health` é apropriado para prontidão JWKS e `/health/live` exclui esse check; ambos deixam de verificar banco/broker. Swagger do Catalog não é habilitado fora de Development.

A antiga afirmação de uma imagem Kubernetes específica publicada em determinada data não foi mantida como instrução operacional: os manifests disponíveis não sustentam essa garantia.

## 15. Troubleshooting

| Sintoma | Verificação / solução |
| --- | --- |
| `dockerDesktopLinuxEngine` / Docker indisponível | Inicie Docker Desktop no modo Linux e repita `docker version`. |
| Porta ocupada | Use outra porta em `--urls` ou no lado esquerdo de `-p`; ajuste as URLs dos exemplos. |
| PostgreSQL inacessível | Confira hostname do ponto de vista do processo/container, porta, banco e permissões. DNS `postgres` do Compose não resolve no host. |
| Tabela/relação não existe | Aplique migrations no banco correto (seção 8); não confunda o Compose que habilita bootstrap com aquele que não o habilita. |
| Startup falha na migration | Banco deve existir; valide conexão e DDL. Flag exige Development e valor booleano. Consulte os logs sem divulgar segredos. |
| Swagger 404 | Catalog exige Development; Users também exige `Swagger__Enabled=true`. Confirme porta e versão executada. |
| Health 503 em JWKS | Confira URL do Users, DNS/rede, formato JWKS e cache. Aguarde atualização; `/health/live` pode continuar 200. |
| PEM impede startup | Confira montagem pública, caminho absoluto, leitura, `kid` único e somente uma origem por chave. Não monte a privada. |
| JWT retorna 401 | Confira expiração, issuer, audience, `kid`, chave correspondente, `sub` e Bearer. PEM rotacionado exige reinício; JWKS precisa de cache válido. |
| JWT retorna 403 | Use a conta com a permissão necessária; login de usuário comum não autoriza criar/atualizar jogos. |
| Cadastro/login falha | Leia os erros de validação; use cadastro de teste válido e senha correta. Administrador existente não tem senha redefinida pelo bootstrap. |
| Pedido retorna 400 | Informe `jogoId` GUID não vazio e exatamente um `Idempotency-Key` UUID não vazio. |
| Pedido retorna 409 | Confira posse, pedido pendente, jogo inativo ou chave reutilizada para outro jogo. Consulte o pedido original. |
| Pedido permanece PendingPayment | Confira Outbox habilitada, conectividade AMQP, permissões, filas/bindings de Payments e consumer Catalog. Health 200 não prova mensageria funcional. |
| Publicação `Unroutable` | Falta uma fila/rota no exchange do evento. Provisione o destinatário; a Outbox mantém a tentativa pendente. |
| Biblioteca vazia | Confirme token do comprador, `Paid` e consumer. Criar pedido não concede o jogo. |
| Swagger bloqueia retry | Mantenha conta/jogo e sessionStorage; após resultado desconhecido consulte/recupere o pedido. Não force nova intenção para contornar o bloqueio. |
| Testes externos ignorados | Configure as conexões de teste descartáveis descritas na seção 13. |

Na pasta do Compose utilizado, `docker compose ps -a` e `docker compose logs --tail 100 catalogapi usersapi paymentsapi` ajudam no diagnóstico. No container avulso use `docker logs --tail 100 fcg-catalog-local`; em .NET acompanhe o terminal da API. Correlacione `OrderId`, `EventId` e `CorrelationId`, sem divulgar tokens, senhas ou payloads completos.

### Limites da validação e pendências

As instruções foram confrontadas com código e configurações locais, incluindo contratos da UsersAPI e as duas composições identificadas. Não foi validado um fluxo em execução, a disponibilidade das imagens/remotos nem o estado real de um cluster. A consulta ao endereço GitHub oficial da UsersAPI não retornou conteúdo nesta revisão; seus contratos foram conferidos na cópia local disponível. Permanecem pendentes os manifests Kubernetes vazios e a confirmação de quais revisões/imagens estão implantadas. Credenciais administrativas devem ser obtidas com o responsável pelo ambiente.
