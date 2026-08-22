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

public sealed class UserEndpointsTests(ApiTestFactory factory) : BaseApiTest(factory)
{
    [Fact]
    public async Task GetMe_WithoutAToken_ReturnsUnauthorized()
    {
        using HttpResponseMessage response = await Client.GetAsync(Url(Routes.Users.Me), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_WithASignedInUser_ReturnsTheirOwnRecord()
    {
        SignedInUser ada = await SignInAsync(TestUsers.Ada);

        using HttpResponseMessage response = await ada.Client.GetAsync(Url(Routes.Users.Me), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        UserPayload user = (await response.Content.ReadFromJsonAsync<UserPayload>(Json, Ct))!;

        user.Id.ShouldBe(ada.UserId);
        user.Email.ShouldBe(TestUsers.Ada);
        user.FirstName.ShouldBe(TestUsers.FirstName);
        user.LastLoginAtUtc.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetMe_WithASignedInUser_DoesNotExposeCredentialMaterial()
    {
        SignedInUser ada = await SignInAsync(TestUsers.Ada);

        using HttpResponseMessage response = await ada.Client.GetAsync(Url(Routes.Users.Me), Ct);
        string body = await response.Content.ReadAsStringAsync(Ct);

        body.ShouldNotContain("passwordHash");
        body.ShouldNotContain("securityStamp");
    }

    [Fact]
    public async Task GetMe_WithAnExpiredAccessToken_ReturnsUnauthorized()
    {
        await using WebApplicationFactory<Program> shortLived =
            FactoryWith((SettingKeys.JwtAccessTokenLifetime, ShortAccessTokenLifetime));

        using HttpClient client = shortLived.CreateClient();

        await RegisterAsync(client, TestUsers.Ada);

        AuthPayload payload;

        using (HttpResponseMessage login = await PostLoginAsync(client, TestUsers.Ada, TestUsers.Password))
        {
            payload = (await login.Content.ReadFromJsonAsync<AuthPayload>(Json, Ct))!;
        }

        (payload.AccessTokenExpiresAtUtc - DateTimeOffset.UtcNow).ShouldBeLessThan(
            TimeSpan.FromSeconds(30),
            $"the {SettingKeys.JwtAccessTokenLifetime} override did not apply");

        using (HttpRequestMessage before = BearerRequest(HttpMethod.Get, Routes.Users.Me, payload.AccessToken))
        using (HttpResponseMessage stillValid = await client.SendAsync(before, Ct))
        {
            stillValid.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        HttpStatusCode expired = await PollForStatusAsync(
            () => client.SendAsync(BearerRequest(HttpMethod.Get, Routes.Users.Me, payload.AccessToken), Ct),
            HttpStatusCode.Unauthorized,
            _pollBudget);

        expired.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetMe_WithATokenSignedByAnotherKey_ReturnsUnauthorized()
    {
        using HttpClient client = Authenticated(TestTokens.ForeignlySigned);

        using HttpResponseMessage response = await client.GetAsync(Url(Routes.Users.Me), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task ListUsers_WithANonAdministrator_ReturnsForbidden()
    {
        SignedInUser ada = await SignInAsync(TestUsers.Ada);

        using HttpResponseMessage response = await ada.Client.GetAsync(Url(Routes.Users.All), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListUsers_WithAnAdministrator_ReturnsEveryUser()
    {
        SignedInUser root = await SignInAsync(TestUsers.Root, asAdmin: true);
        await RegisterAsync(TestUsers.Ada);

        using HttpResponseMessage response = await root.Client.GetAsync(Url(Routes.Users.All), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        PagePayload<UserPayload> page = (await response.Content
            .ReadFromJsonAsync<PagePayload<UserPayload>>(Json, Ct))!;

        page.Items.Length.ShouldBe(2);
        page.TotalCount.ShouldBe(2);
        page.Page.ShouldBe(1);
        page.PageSize.ShouldBe(DefaultPageSize);
        page.Items.Select(user => user.Email).ShouldContain(TestUsers.Ada);
    }

    [Fact]
    public async Task ListUsers_WithAnOversizedPageSize_ClampsToTheMaximum()
    {
        SignedInUser root = await SignInAsync(TestUsers.Root, asAdmin: true);

        using HttpResponseMessage response = await root.Client.GetAsync(
            Url(Routes.Users.Page(size: OversizedPageSize)),
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        PagePayload<UserPayload> page = (await response.Content
            .ReadFromJsonAsync<PagePayload<UserPayload>>(Json, Ct))!;

        page.PageSize.ShouldBe(MaxPageSize);
    }

    [Fact]
    public async Task ListUsers_WithASecondPage_ReturnsTheRemainingUsers()
    {
        SignedInUser root = await SignInAsync(TestUsers.Root, asAdmin: true);
        await RegisterAsync(TestUsers.Ada);
        await RegisterAsync(TestUsers.Grace);

        using HttpResponseMessage response = await root.Client.GetAsync(
            Url(Routes.Users.Page(number: 2, size: 2)),
            Ct);

        PagePayload<UserPayload> page = (await response.Content
            .ReadFromJsonAsync<PagePayload<UserPayload>>(Json, Ct))!;

        page.TotalCount.ShouldBe(3);
        page.Items.Length.ShouldBe(1);
    }

    [Fact]
    public async Task GetUserById_WithANonAdministrator_ReturnsForbidden()
    {
        SignedInUser ada = await SignInAsync(TestUsers.Ada);
        Guid otherId = await RegisterAsync(TestUsers.Grace);

        using HttpResponseMessage response = await ada.Client.GetAsync(Url(Routes.Users.ById(otherId)), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task GetUserById_WithAnAdministrator_ReturnsTheUser()
    {
        SignedInUser root = await SignInAsync(TestUsers.Root, asAdmin: true);
        Guid adaId = await RegisterAsync(TestUsers.Ada);

        using HttpResponseMessage response = await root.Client.GetAsync(Url(Routes.Users.ById(adaId)), Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        UserPayload user = (await response.Content.ReadFromJsonAsync<UserPayload>(Json, Ct))!;
        user.Email.ShouldBe(TestUsers.Ada);
    }

    [Fact]
    public async Task GetUserById_WithAnUnknownId_ReturnsNotFound()
    {
        SignedInUser root = await SignInAsync(TestUsers.Root, asAdmin: true);

        using HttpResponseMessage response = await root.Client.GetAsync(
            Url(Routes.Users.ById(Guid.NewGuid())),
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AssignRole_WithAnAdministrator_GrantsTheRole()
    {
        SignedInUser root = await SignInAsync(TestUsers.Root, asAdmin: true);
        Guid adaId = await RegisterAsync(TestUsers.Ada);

        using (HttpResponseMessage response = await root.Client.PostAsJsonAsync(
            Url(Routes.Users.RolesOf(adaId)),
            new { role = Roles.Admin },
            Ct))
        {
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        using HttpResponseMessage reread = await root.Client.GetAsync(Url(Routes.Users.ById(adaId)), Ct);
        UserPayload ada = (await reread.Content.ReadFromJsonAsync<UserPayload>(Json, Ct))!;

        ada.Roles.ShouldContain(Roles.Admin);
    }

    [Fact]
    public async Task AssignRole_WithARoleTheUserAlreadyHas_ReturnsNoContent()
    {
        SignedInUser root = await SignInAsync(TestUsers.Root, asAdmin: true);
        Guid adaId = await RegisterAsync(TestUsers.Ada);

        using HttpResponseMessage response = await root.Client.PostAsJsonAsync(
            Url(Routes.Users.RolesOf(adaId)),
            new { role = Roles.User },
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task AssignRole_WithAnUnknownRole_ReturnsNotFound()
    {
        SignedInUser root = await SignInAsync(TestUsers.Root, asAdmin: true);
        Guid adaId = await RegisterAsync(TestUsers.Ada);

        using HttpResponseMessage response = await root.Client.PostAsJsonAsync(
            Url(Routes.Users.RolesOf(adaId)),
            new { role = UnknownRole },
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task AssignRole_WithANonAdministrator_ReturnsForbidden()
    {
        SignedInUser ada = await SignInAsync(TestUsers.Ada);
        Guid otherId = await RegisterAsync(TestUsers.Grace);

        using HttpResponseMessage response = await ada.Client.PostAsJsonAsync(
            Url(Routes.Users.RolesOf(otherId)),
            new { role = Roles.Admin },
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AssignRole_WithAnUnknownUser_ReturnsNotFound()
    {
        SignedInUser root = await SignInAsync(TestUsers.Root, asAdmin: true);

        using HttpResponseMessage response = await root.Client.PostAsJsonAsync(
            Url(Routes.Users.RolesOf(Guid.NewGuid())),
            new { role = Roles.Admin },
            Ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;
    private const int OversizedPageSize = 10_000;

    private const string UnknownRole = "superuser";

    private const string ShortAccessTokenLifetime = "00:00:02";

    private static readonly TimeSpan _pollBudget = TimeSpan.FromSeconds(15);
}
