using CleanArchitecture.Api.Endpoints;
using CleanArchitecture.Api.Handlers;
using CleanArchitecture.Application;
using CleanArchitecture.Infrastructure.Persistence;
using CleanArchitecture.Infrastructure;
using EvolveDb;
using FluentValidation;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;
using Serilog;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration));

builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration);

// The validators live here now, next to the request records they validate.
builder.Services.AddValidatorsFromAssembly(typeof(Program).Assembly, includeInternalTypes: true);

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();

WebApplication app = builder.Build();

// Schema first, then the first administrator. Both run in process before the app serves
// anything, so the database has to be up and the connection's user needs DDL rights.
await using (AsyncServiceScope scope = app.Services.CreateAsyncScope())
{
    AppDbContext database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    Microsoft.Extensions.Logging.ILogger logger =
        scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Migrations");

    var evolve = new Evolve(
        database.Database.GetDbConnection(),
        message => logger.LogInformation("Evolve: {Message}", message))
    {
        // Beside the app rather than the working directory, so it does not matter where the
        // process was launched from.
        Locations = [Path.Combine(AppContext.BaseDirectory, "Migrations")],
        MetadataTableName = "schema_changelog",

        // Takes an advisory lock, so two instances starting together cannot both migrate.
        EnableClusterMode = true,
        IsEraseDisabled = true,
    };

    evolve.Migrate();

    logger.LogInformation("Database migration complete. {Applied} script(s) applied.", evolve.NbMigration);
}

await app.Services.SeedAdministratorAsync();

app.UseExceptionHandler();
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false })
    .AllowAnonymous();

app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") })
    .AllowAnonymous();

app.MapAuthenticationEndpoints();
app.MapUserEndpoints();

await app.RunAsync();
