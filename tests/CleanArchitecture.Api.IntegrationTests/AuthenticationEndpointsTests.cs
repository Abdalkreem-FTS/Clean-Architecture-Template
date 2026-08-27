using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using CleanArchitecture.Api.IntegrationTests.Configuration;
using CleanArchitecture.Domain.Users;
using Microsoft.AspNetCore.Mvc.Testing;
using Shouldly;
using Xunit;

namespace CleanArchitecture.Api.IntegrationTests;

public sealed class AuthenticationEndpointsTests(ApiTestFactory factory) : BaseApiTest(factory)
{
    [Fact]
    public async Task Register_WithAValidRequest_CreatesTheAccountAndReturnsItsId()
    {
        using HttpResponseMessage response = await Client.PostAsJsonAsync(
            Url(Routes.Users.All),
            new
            {
                email = TestUsers.Ada,
                password = TestUsers.Password,
                firstName = TestUsers.FirstName,
                lastName = TestUsers.LastName,
            },
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);

        RegisteredResponse body = (await response.Content.ReadFromJsonAsync<RegisteredResponse>(Json, Ct))!;
        body.Id.ShouldNotBe(Guid.Empty);

        (await CountUsersAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Register_WithANewAccount_GrantsTheDefaultUserRole()
    {
        await RegisterAsync(TestUsers.Ada);

        AuthPayload payload = await LoginAsync(TestUsers.Ada);

        using HttpRequestMessage request = BearerRequest(HttpMethod.Get, Routes.Users.Me, payload.AccessToken);
        using HttpResponseMessage response = await Client.SendAsync(request, Ct);

        UserPayload user = (await response.Content.ReadFromJsonAsync<UserPayload>(Json, Ct))!;

        user.Roles.ShouldBe([Roles.User]);
    }

    [Fact]
    public async Task Register_WithAnAddressAlreadyTaken_ReturnsAConflict()
    {
        await RegisterAsync(TestUsers.Ada);

        using HttpResponseMessage response = await Client.PostAsJsonAsync(
            Url(Routes.Users.All),
            new
            {
                email = TestUsers.Ada,
                password = TestUsers.Password,
                firstName = TestUsers.FirstName,
                lastName = TestUsers.LastName,
            },
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_WithConcurrentRequestsForOneAddress_CreatesExactlyOneAccount()
    {
        const int concurrentAttempts = 8;

        HttpResponseMessage[] responses = await Task.WhenAll(
            Enumerable.Range(0, concurrentAttempts).Select(_ => Client.PostAsJsonAsync(
                Url(Routes.Users.All),
                new
                {
                    email = TestUsers.Ada,
                    password = TestUsers.Password,
                    firstName = TestUsers.FirstName,
                    lastName = TestUsers.LastName,
                },
                Ct)));

        try
        {
            responses.Count(response => response.StatusCode == HttpStatusCode.Created).ShouldBe(1);
            responses.Count(response => response.StatusCode == HttpStatusCode.Conflict)
                .ShouldBe(concurrentAttempts - 1);
        }
        finally
        {
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }
        }

        (await CountAccountsWithEmailAsync(TestUsers.Ada)).ShouldBe(1);
    }

    [Fact]
    public async Task Register_WithAMalformedAddress_ReturnsABadRequest()
    {
        using HttpResponseMessage response = await Client.PostAsJsonAsync(
            Url(Routes.Users.All),
            new
            {
                email = TestUsers.MalformedEmail,
                password = TestUsers.Password,
                firstName = TestUsers.FirstName,
                lastName = TestUsers.LastName,
            },
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithAPasswordBelowThePolicy_ReturnsABadRequest()
    {
        using HttpResponseMessage response = await Client.PostAsJsonAsync(
            Url(Routes.Users.All),
            new
            {
                email = TestUsers.Ada,
                password = TestUsers.WeakPassword,
                firstName = TestUsers.FirstName,
                lastName = TestUsers.LastName,
            },
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);

        (await CountUsersAsync()).ShouldBe(0);
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsAnAccessTokenAndARefreshToken()
    {
        Guid userId = await RegisterAsync(TestUsers.Ada);

        AuthPayload payload = await LoginAsync(TestUsers.Ada);

        payload.UserId.ShouldBe(userId);
        payload.AccessToken.ShouldNotBeNullOrWhiteSpace();
        payload.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        payload.AccessTokenExpiresAtUtc.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task Login_WithValidCredentials_StoresTheRefreshTokenOnlyAsAHash()
    {
        await RegisterAsync(TestUsers.Ada);

        AuthPayload payload = await LoginAsync(TestUsers.Ada);

        string? storedHash = await StoredRefreshTokenHashAsync();

        storedHash.ShouldNotBeNull();
        storedHash.ShouldNotBe(payload.RefreshToken);
    }

    [Fact]
    public async Task Login_WithTheWrongPassword_ReturnsUnauthorized()
    {
        await RegisterAsync(TestUsers.Ada);

        using HttpResponseMessage response = await PostLoginAsync(TestUsers.Ada, TestUsers.WrongPassword);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithAnUnknownAddress_ReturnsUnauthorized()
    {
        using HttpResponseMessage response = await PostLoginAsync(TestUsers.Unknown, TestUsers.Password);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithRepeatedFailures_LocksTheAccountOut()
    {
        await RegisterAsync(TestUsers.Ada);

        await ExhaustFailedAttemptsAsync(TestUsers.Ada);

        using HttpResponseMessage response = await PostLoginAsync(TestUsers.Ada, TestUsers.Password);

        // Same status a wrong password gets, deliberately: a distinct one here would let an
        // attacker tell which emails exist by brute-forcing until the response changes.
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_AfterTheLockoutWindowElapses_SucceedsAgain()
    {
        await RegisterAsync(TestUsers.LockedOut);

        await ExhaustFailedAttemptsAsync(TestUsers.LockedOut);

        using (HttpResponseMessage locked = await PostLoginAsync(TestUsers.LockedOut, TestUsers.Password))
        {
            locked.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }

        await ClearLockoutAsync(TestUsers.LockedOut);

        using HttpResponseMessage unlocked = await PostLoginAsync(TestUsers.LockedOut, TestUsers.Password);

        unlocked.StatusCode.ShouldBe(HttpStatusCode.OK, await unlocked.Content.ReadAsStringAsync(Ct));
    }
    [Fact]
    public async Task Startup_WithASeedConfigured_CreatesAnAdministratorThatCanSignIn()
    {
        await using WebApplicationFactory<Program> seeded = FactoryWith(SeedSettings);
        using HttpClient client = seeded.CreateClient();

        using HttpResponseMessage login = await PostLoginAsync(
            client,
            TestUsers.SeededAdmin,
            TestUsers.SeededAdminPassword);

        login.StatusCode.ShouldBe(HttpStatusCode.OK);

        AuthPayload payload = (await login.Content.ReadFromJsonAsync<AuthPayload>(Json, Ct))!;

        using HttpRequestMessage request = BearerRequest(HttpMethod.Get, Routes.Users.Me, payload.AccessToken);
        using HttpResponseMessage me = await client.SendAsync(request, Ct);

        UserPayload admin = (await me.Content.ReadFromJsonAsync<UserPayload>(Json, Ct))!;

        admin.Roles.ShouldContain(Roles.Admin);
    }

    [Fact]
    public async Task Startup_WithASeedThatAlreadyExists_IsIdempotent()
    {
        await using (WebApplicationFactory<Program> first = FactoryWith(SeedSettings))
        {
            using HttpClient warmUp = first.CreateClient();
            using HttpResponseMessage login = await PostLoginAsync(
                warmUp,
                TestUsers.SeededAdmin,
                TestUsers.SeededAdminPassword);

            login.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        await using WebApplicationFactory<Program> second = FactoryWith(SeedSettings);
        using HttpClient client = second.CreateClient();

        using HttpResponseMessage repeated = await PostLoginAsync(
            client,
            TestUsers.SeededAdmin,
            TestUsers.SeededAdminPassword);

        repeated.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await CountAccountsWithEmailAsync(TestUsers.SeededAdmin)).ShouldBe(1);
    }

    [Fact]
    public async Task Refresh_WithAValidToken_IssuesANewPair()
    {
        await RegisterAsync(TestUsers.Ada);
        AuthPayload first = await LoginAsync(TestUsers.Ada);

        using HttpResponseMessage response = await PutRefreshAsync(first.RefreshToken);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        AuthPayload second = (await response.Content.ReadFromJsonAsync<AuthPayload>(Json, Ct))!;

        second.UserId.ShouldBe(first.UserId);
        second.RefreshToken.ShouldNotBe(first.RefreshToken);
    }

    [Fact]
    public async Task Refresh_WithAnAlreadyUsedToken_ReturnsUnauthorized()
    {
        await RegisterAsync(TestUsers.Ada);
        AuthPayload payload = await LoginAsync(TestUsers.Ada);

        using (HttpResponseMessage first = await PutRefreshAsync(payload.RefreshToken))
        {
            first.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using HttpResponseMessage second = await PutRefreshAsync(payload.RefreshToken);

        second.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithConcurrentRequestsForOneToken_SucceedsExactlyOnce()
    {
        const int concurrentAttempts = 8;

        await RegisterAsync(TestUsers.Ada);
        AuthPayload payload = await LoginAsync(TestUsers.Ada);

        HttpResponseMessage[] responses = await Task.WhenAll(
            Enumerable.Range(0, concurrentAttempts).Select(_ => PutRefreshAsync(payload.RefreshToken)));

        try
        {
            responses.Count(response => response.StatusCode == HttpStatusCode.OK).ShouldBe(1);
            responses.Count(response => response.StatusCode == HttpStatusCode.Unauthorized)
                .ShouldBe(concurrentAttempts - 1);
        }
        finally
        {
            foreach (HttpResponseMessage response in responses)
            {
                response.Dispose();
            }
        }

        (await CountActiveRefreshTokensAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Refresh_WithAnExpiredButUnrevokedToken_ReturnsUnauthorized()
    {
        await using WebApplicationFactory<Program> shortLived =
            FactoryWith((SettingKeys.JwtRefreshTokenLifetime, ShortRefreshTokenLifetime));

        using HttpClient client = shortLived.CreateClient();

        await RegisterAsync(client, TestUsers.Ada);

        AuthPayload payload;

        using (HttpResponseMessage login = await PostLoginAsync(client, TestUsers.Ada, TestUsers.Password))
        {
            login.StatusCode.ShouldBe(HttpStatusCode.OK);
            payload = (await login.Content.ReadFromJsonAsync<AuthPayload>(Json, Ct))!;
        }

        // Expiry is compared in SQL against the injected clock, so waiting past it is exact.
        await Task.Delay(_shortRefreshTokenLifetimeSpan + _expiryMargin, Ct);

        using HttpResponseMessage refreshed = await PutRefreshAsync(client, payload.RefreshToken);

        refreshed.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        (await CountActiveRefreshTokensAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Refresh_WithAnUnknownToken_ReturnsUnauthorized()
    {
        using HttpResponseMessage response = await PutRefreshAsync(TestTokens.Unknown);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithAValidToken_RevokesTheRefreshToken()
    {
        await RegisterAsync(TestUsers.Ada);
        AuthPayload payload = await LoginAsync(TestUsers.Ada);

        using (HttpResponseMessage logout = await DeleteLogoutAsync(payload.RefreshToken))
        {
            logout.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        using HttpResponseMessage refresh = await PutRefreshAsync(payload.RefreshToken);

        refresh.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Logout_WithAnUnknownToken_ReturnsNoContent()
    {
        using HttpResponseMessage response = await DeleteLogoutAsync(TestTokens.Unknown);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private const string ShortRefreshTokenLifetime = "00:00:02";

    private static readonly TimeSpan _shortRefreshTokenLifetimeSpan = TimeSpan.FromSeconds(2);

    private static readonly TimeSpan _expiryMargin = TimeSpan.FromMilliseconds(250);

    private static (string Key, string Value)[] SeedSettings =>
    [
        (SettingKeys.SeedAdminEmail, TestUsers.SeededAdmin),
        (SettingKeys.SeedAdminPassword, TestUsers.SeededAdminPassword),
    ];

    private async Task ExhaustFailedAttemptsAsync(string email)
    {
        for (int attempt = 0; attempt < MaxFailedAccessAttempts; attempt++)
        {
            using HttpResponseMessage failed = await PostLoginAsync(email, TestUsers.WrongPassword);
        }
    }
}
