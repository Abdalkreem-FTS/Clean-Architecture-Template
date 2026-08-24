using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using CleanArchitecture.Api.IntegrationTests.Configuration;
using CleanArchitecture.Domain.Users;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;

namespace CleanArchitecture.Api.IntegrationTests;

[Collection(nameof(ApiTestCollection))]
public abstract class BaseApiTest(ApiTestFactory factory) : IAsyncLifetime
{
    protected static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    protected ApiTestFactory Factory { get; } = factory;

    protected HttpClient Client { get; private set; } = null!;

    protected static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Every client this test handed out, so none of them outlives the test that asked for one.
    private readonly List<HttpClient> _clients = [];

    // Read off the running application rather than restated here, so a change to the lockout
    // policy in appsettings.json moves the test that exhausts it too.
    protected int MaxFailedAccessAttempts =>
        Factory.Services.GetRequiredService<IOptions<IdentityOptions>>().Value.Lockout.MaxFailedAccessAttempts;

    public ValueTask InitializeAsync()
    {
        Client = Factory.CreateClient();

        return new ValueTask(Factory.ResetAsync());
    }

    public ValueTask DisposeAsync()
    {
        foreach (HttpClient client in _clients)
        {
            client.Dispose();
        }

        _clients.Clear();
        Client.Dispose();

        // Kept here, unlike in the sealed ApiTestFactory: this type can be inherited, so a
        // derived test class that introduced a finalizer would need this call to exist (CA1816).
        GC.SuppressFinalize(this);

        return ValueTask.CompletedTask;
    }

    protected static Uri Url(string relative) => new(relative, UriKind.Relative);

    protected WebApplicationFactory<Program> FactoryWith(params (string Key, string Value)[] settings) =>
        Factory.WithWebHostBuilder(builder =>
        {
            foreach ((string key, string value) in settings)
            {
                builder.UseSetting(key, value);
            }
        });


    protected Task<Guid> RegisterAsync(string email, string password = TestUsers.Password) =>
        RegisterAsync(Client, email, password);

    protected static async Task<Guid> RegisterAsync(
        HttpClient client,
        string email,
        string password = TestUsers.Password)
    {
        using HttpResponseMessage response = await client.PostAsJsonAsync(
            Url(Routes.Authentication.Register),
            new { email, password, firstName = TestUsers.FirstName, lastName = TestUsers.LastName },
            Ct);

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<RegisteredResponse>(Json, Ct))!.Id;
    }

    protected static Task<HttpResponseMessage> PostLoginAsync(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync(Url(Routes.Authentication.Login), new { email, password }, Ct);

    protected Task<HttpResponseMessage> PostLoginAsync(string email, string password) =>
        PostLoginAsync(Client, email, password);

    protected async Task<AuthPayload> LoginAsync(string email, string password = TestUsers.Password)
    {
        using HttpResponseMessage response = await PostLoginAsync(Client, email, password);

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<AuthPayload>(Json, Ct))!;
    }

    protected static Task<HttpResponseMessage> PostRefreshAsync(HttpClient client, string refreshToken) =>
        client.PostAsJsonAsync(Url(Routes.Authentication.Refresh), new { refreshToken }, Ct);

    protected Task<HttpResponseMessage> PostRefreshAsync(string refreshToken) =>
        PostRefreshAsync(Client, refreshToken);

    protected Task<HttpResponseMessage> PostLogoutAsync(string refreshToken) =>
        Client.PostAsJsonAsync(Url(Routes.Authentication.Logout), new { refreshToken }, Ct);

    protected async Task<SignedInUser> SignInAsync(string email, bool asAdmin = false)
    {
        Guid userId = await RegisterAsync(email);

        if (asAdmin)
        {
            await GrantAdminAsync(userId);
        }

        AuthPayload payload = await LoginAsync(email);

        return new SignedInUser(userId, payload.AccessToken, payload.RefreshToken, Authenticated(payload.AccessToken));
    }

    // The caller gets a client, not the job of disposing it — the test owns it and closes it
    // when it ends.
    protected HttpClient Authenticated(string accessToken)
    {
        HttpClient client = Factory.CreateClient();
        client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue(TestSettings.AuthenticationScheme, accessToken);

        _clients.Add(client);

        return client;
    }

    protected static HttpRequestMessage BearerRequest(HttpMethod method, string route, string accessToken)
    {
        HttpRequestMessage request = new(method, Url(route));
        request.Headers.Authorization =
            new AuthenticationHeaderValue(TestSettings.AuthenticationScheme, accessToken);

        return request;
    }

    // Straight into the table on purpose: granting admin through the API needs an admin to
    // already exist, and the suite truncates users between tests, so there never is one. Looked
    // up by name rather than by the id V0003 happens to seed, so reseeding differently does not
    // silently stop granting anything.
    protected Task GrantAdminAsync(Guid userId) => ExecuteAsync(
        """
        INSERT INTO user_roles (user_id, role_id)
        SELECT @user_id, id FROM roles WHERE name = @role
        ON CONFLICT DO NOTHING
        """,
        ("user_id", userId),
        ("role", Roles.Admin));

    protected Task ClearLockoutAsync(string email) => ExecuteAsync(
        "UPDATE users SET lockout_end = NULL WHERE email = @email",
        ("email", email));

    protected Task<long> CountUsersAsync() => ScalarAsync<long>("SELECT count(*) FROM users");

    protected Task<long> CountAccountsWithEmailAsync(string email) =>
        ScalarAsync<long>("SELECT count(*) FROM users WHERE email = @email", ("email", email));

    protected Task<long> CountActiveRefreshTokensAsync() =>
        ScalarAsync<long>("SELECT count(*) FROM refresh_tokens WHERE revoked_at_utc IS NULL");

    protected Task<string?> StoredRefreshTokenHashAsync() =>
        ScalarAsync<string?>("SELECT token_hash FROM refresh_tokens LIMIT 1");

    protected async Task ExecuteAsync(string sql, params (string Name, object Value)[] parameters)
    {
        await using NpgsqlConnection connection = new(Factory.ConnectionString);
        await connection.OpenAsync(Ct);

        await using NpgsqlCommand command = Command(connection, sql, parameters);

        await command.ExecuteNonQueryAsync(Ct);
    }

    protected async Task<T?> ScalarAsync<T>(string sql, params (string Name, object Value)[] parameters)
    {
        await using NpgsqlConnection connection = new(Factory.ConnectionString);
        await connection.OpenAsync(Ct);

        await using NpgsqlCommand command = Command(connection, sql, parameters);

        object? value = await command.ExecuteScalarAsync(Ct);

        return value is null or DBNull ? default : (T)value;
    }

    private static NpgsqlCommand Command(
        NpgsqlConnection connection,
        string sql,
        (string Name, object Value)[] parameters)
    {
        NpgsqlCommand command = connection.CreateCommand();
        command.CommandText = sql;

        foreach ((string name, object value) in parameters)
        {
            command.Parameters.AddWithValue(name, value);
        }

        return command;
    }

    protected sealed record RegisteredResponse(Guid Id);

    protected sealed record AuthPayload(
        Guid UserId,
        string AccessToken,
        DateTimeOffset AccessTokenExpiresAtUtc,
        string RefreshToken);

    protected sealed record SignedInUser(Guid UserId, string AccessToken, string RefreshToken, HttpClient Client);

    protected sealed record PagePayload<T>(T[] Items, int Page, int PageSize, int TotalCount);

    protected sealed record UserPayload(
        Guid Id,
        string Email,
        string FirstName,
        string LastName,
        string[] Roles,
        DateTimeOffset CreatedAtUtc,
        DateTimeOffset? LastLoginAtUtc);
}
