using System;
using System.Net.Http;
using System.Threading.Tasks;
using CleanArchitecture.Api.IntegrationTests.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace CleanArchitecture.Api.IntegrationTests;

public sealed class ApiTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder(TestSettings.PostgresImage)
        .WithDatabase(TestSettings.DatabaseName)
        .WithUsername(TestSettings.DatabaseUser)
        .WithPassword(TestSettings.DatabasePassword)
        .Build();

    private NpgsqlConnection _connection = null!;

    public string ConnectionString => _database.GetConnectionString();

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();

        using (HttpClient warmUp = CreateClient())
        {
            await warmUp.GetAsync(new Uri(Routes.Users.Me, UriKind.Relative), TestContext.Current.CancellationToken);
        }

        _connection = new NpgsqlConnection(ConnectionString);
        await _connection.OpenAsync(TestContext.Current.CancellationToken);
    }

    public async Task ResetAsync()
    {
        await using NpgsqlCommand command = _connection.CreateCommand();
        command.CommandText = "TRUNCATE users RESTART IDENTITY CASCADE";

        await command.ExecuteNonQueryAsync();
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
        await _connection.DisposeAsync();
        await base.DisposeAsync();
        await _database.DisposeAsync();

        GC.SuppressFinalize(this);
    }
}
