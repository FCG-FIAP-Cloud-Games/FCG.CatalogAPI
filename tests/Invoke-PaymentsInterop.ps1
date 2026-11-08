param([string]$PaymentsRepository = (Join-Path $PSScriptRoot '../../FCG.PaymentsAPI'))
$ErrorActionPreference = 'Stop'
$repo = (Resolve-Path -LiteralPath $PaymentsRepository).Path
$configuration = Get-Content -Raw -LiteralPath (Join-Path $repo 'src/FCG.Payments.Api/Messaging/PaymentsMessagingExtensions.cs')
if ($configuration -notmatch 'SetEntityName\("PaymentProcessedEvent"\)' -or $configuration -match 'UseRawJsonSerializer|ConfigureJsonSerializer') {
    throw 'Configuração do producer diverge da configuração validada. Inspecione antes de continuar.'
}
$versions = Get-Content -Raw -LiteralPath (Join-Path $repo 'Directory.Packages.props')
if ($versions -notmatch 'Include="MassTransit.RabbitMQ" Version="8.3.6"') {
    throw 'A versão do MassTransit difere de 8.3.6. Revalide o transporte.'
}
if (!$env:CATALOG_TEST_CONNECTION -or !$env:RABBITMQ_TEST_CONNECTION) {
    throw 'Configure CATALOG_TEST_CONNECTION e RABBITMQ_TEST_CONNECTION para ambientes descartáveis.'
}
$probe = Join-Path ([IO.Path]::GetTempPath()) ('catalog-c19-payments-probe-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $probe | Out-Null
$repoXml = [Security.SecurityElement]::Escape($repo.Replace('\', '/'))
$project = @"
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net8.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup><FrameworkReference Include="Microsoft.AspNetCore.App" />
    <PackageReference Include="MassTransit.RabbitMQ" Version="8.3.6" />
    <Compile Include="$repoXml/src/FCG.Payments.Application/Messaging/PaymentProcessedEvent.cs" Link="PaymentProcessedEvent.cs" />
    <Compile Include="$repoXml/src/FCG.Payments.Application/Messaging/IPaymentProcessedEventPublisher.cs" Link="IPaymentProcessedEventPublisher.cs" />
    <Compile Include="$repoXml/src/FCG.Payments.Api/Messaging/MassTransitPaymentProcessedEventPublisher.cs" Link="MassTransitPaymentProcessedEventPublisher.cs" />
  </ItemGroup>
</Project>
"@
Set-Content -LiteralPath (Join-Path $probe 'PaymentsPublisherProbe.csproj') -Value $project -Encoding utf8
$program = @'
using FCG.Payments.Application.Messaging;
using FCG.Payments.Api.Messaging;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

// Executa os fontes reais do contrato e publisher, vinculados em modo leitura.
var uri = new Uri(Environment.GetEnvironmentVariable("RABBITMQ_TEST_CONNECTION")!);
var credentials = uri.UserInfo.Split(':', 2).Select(Uri.UnescapeDataString).ToArray();
var exchange = Environment.GetEnvironmentVariable("PAYMENTS_TEST_EXCHANGE");
if (exchange != "PaymentProcessedEvent") throw new InvalidOperationException("Exchange não oficial.");
var message = JsonSerializer.Deserialize<PaymentProcessedEvent>(await Console.In.ReadToEndAsync())!;
var services = new ServiceCollection();
services.AddMassTransit(x => x.UsingRabbitMq((context, bus) =>
{
    bus.Host(uri.Host, (ushort)(uri.IsDefaultPort ? 5672 : uri.Port), Uri.UnescapeDataString(uri.AbsolutePath[1..]), host =>
    {
        host.Username(credentials[0]); host.Password(credentials[1]);
    });
    bus.Message<PaymentProcessedEvent>(m => m.SetEntityName("PaymentProcessedEvent"));
}));
services.AddScoped<IPaymentProcessedEventPublisher, MassTransitPaymentProcessedEventPublisher>();
await using var provider = services.BuildServiceProvider();
var bus = provider.GetRequiredService<IBusControl>();
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
await bus.StartAsync(timeout.Token);
try
{
    await using var scope = provider.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<IPaymentProcessedEventPublisher>().PublishAsync(message, timeout.Token);
}
finally { await bus.StopAsync(); }
'@
Set-Content -LiteralPath (Join-Path $probe 'Program.cs') -Value $program -Encoding utf8
dotnet build (Join-Path $probe 'PaymentsPublisherProbe.csproj')
if ($LASTEXITCODE -ne 0) { throw 'Falha ao compilar o publisher externo.' }
$previous = $env:PAYMENTS_TEST_PUBLISHER
try {
    $env:PAYMENTS_TEST_PUBLISHER = Join-Path $probe 'bin/Debug/net8.0/PaymentsPublisherProbe.dll'
    dotnet test (Join-Path $PSScriptRoot 'FCG.Catalog.IntegrationTests') --filter 'FullyQualifiedName~Publisher_Payments_real'
    if ($LASTEXITCODE -ne 0) { throw 'Interoperabilidade falhou. Não adapte o transporte sem analisar a divergência.' }
    Write-Output ('Publisher disponível para a suíte completa em: ' + $env:PAYMENTS_TEST_PUBLISHER)
}
finally { $env:PAYMENTS_TEST_PUBLISHER = $previous }
