using CleanArchitecture.Api.IntegrationTests.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;

namespace CleanArchitecture.Api.IntegrationTests;

public sealed class ApiTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder(TestSettings.PostgresImage)
        .WithDatabase(TestSettings.DatabaseName)
        .WithUsername(TestSettings.DatabaseUser)
        .WithPassword(TestSettings.DatabasePassword)
        .Build();

    public string ConnectionString => _database.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();

        // Forces the host to build and start, which is what applies the migrations. Every test
        // that follows can assume the schema is there.
        using HttpClient warmUp = CreateClient();

        await warmUp.GetAsync(new Uri(Routes.Users.Me, UriKind.Relative), TestContext.Current.CancellationToken);
    }

    // Every user, and by cascade everything that hangs off one, between tests. Opens its own
    // connection like every other statement the suite runs, so no test depends on another
    // having left a connection open.
    public async Task ResetAsync()
    {
        await using NpgsqlConnection connection = new(ConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);

        await using NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = "TRUNCATE users CASCADE";

        await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(TestSettings.EnvironmentName);
        builder.UseSetting(SettingKeys.ConnectionString, ConnectionString);
        builder.UseSetting(SettingKeys.JwtSigningKey, TestSettings.SigningKey);
        builder.UseSetting(SettingKeys.SeedAdminEmail, string.Empty);
        builder.UseSetting(SettingKeys.SeedAdminPassword, string.Empty);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }
}
