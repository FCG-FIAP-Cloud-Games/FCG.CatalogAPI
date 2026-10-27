using Microsoft.OpenApi.Models;
using FCG.Catalog.Api.OpenApi;
using FCG.Catalog.Infrastructure.IoC;
using FCG.Catalog.Api.IoC;
using FCG.Catalog.Api.Authentication;
using FCG.Catalog.Api.Initialization;

var builder = WebApplication.CreateBuilder(args);
LocalDemonstrationExtensions.IsEnabled(builder.Configuration, builder.Environment);

builder.Services.AddControllers();
builder.Services.AddCatalogApplication();
builder.Services.AddCatalogInfrastructure(builder.Configuration);
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.OperationFilter<BearerSecurityOperationFilter>();
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Informe o accessToken gerado pela UsersAPI."
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks();
builder.Services.AddCatalogAuthentication(builder.Configuration);
builder.Services.AddAuthorization();

var app = builder.Build();

// O host e os workers só iniciam depois de o schema estar pronto.
await app.PrepareLocalDemonstrationAsync();

app.UseMiddleware<FCG.Catalog.Api.CorrelationMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();
app.MapHealthChecks("/health/live", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = registration => !registration.Tags.Contains("ready")
}).AllowAnonymous();

app.Run();

// Permite hospedar a API nos testes de integração.
public partial class Program;
