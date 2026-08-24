using CleanArchitecture.Application.Abstractions;
using CleanArchitecture.Application.Authentication;
using CleanArchitecture.Application.Users;
using CleanArchitecture.Domain.Common.Results;
using CleanArchitecture.Domain.Users;
using NSubstitute;
using Shouldly;

namespace CleanArchitecture.Application.UnitTests;

public sealed class AuthenticationServiceTests
{
    private readonly IIdentityService _identity = Substitute.For<IIdentityService>();
    private readonly ITokenService _tokens = Substitute.For<ITokenService>();
    private readonly IRefreshTokenStore _refreshTokens = Substitute.For<IRefreshTokenStore>();
    private readonly AuthenticationService _service;

    private static readonly Guid _userId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    private static readonly User _user = new(
        _userId,
        "ada@example.com",
        "Ada",
        "Lovelace",
        [Roles.User],
        DateTimeOffset.UnixEpoch,
        null);

    public AuthenticationServiceTests()
    {
        _tokens.CreateAccessToken(Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<IReadOnlyList<string>>())
            .Returns(new AccessToken("access-token", DateTimeOffset.UnixEpoch.AddMinutes(5)));

        _tokens.CreateRefreshToken()
            .Returns(new RefreshTokenPair("raw", "hashed", DateTimeOffset.UnixEpoch.AddDays(14)));

        _tokens.Hash(Arg.Any<string>()).Returns(call => $"hash-of-{call.Arg<string>()}");

        _service = new AuthenticationService(_identity, _tokens, _refreshTokens);
    }

    [Fact]
    public async Task LoginAsync_WithValidCredentials_IssuesARefreshTokenForThatUser()
    {
        _identity.AuthenticateAsync("ada@example.com", "pw", Arg.Any<CancellationToken>())
            .Returns(_user);

        Result<AuthenticationTokens> result =
            await _service.LoginAsync("ada@example.com", "pw", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.UserId.ShouldBe(_userId);
        result.Value.AccessToken.ShouldBe("access-token");
        result.Value.RefreshToken.ShouldBe("raw");

        await _refreshTokens.Received(1).IssueAsync(
            _userId,
            "hashed",
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task LoginAsync_WithBadCredentials_IssuesNothing()
    {
        _identity.AuthenticateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(UserErrors.InvalidCredentials);

        Result<AuthenticationTokens> result =
            await _service.LoginAsync("ada@example.com", "wrong", CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.TopError.Code.ShouldBe(UserErrors.InvalidCredentials.Code);

        await _refreshTokens.DidNotReceive().IssueAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());

        _tokens.DidNotReceive().CreateAccessToken(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<IReadOnlyList<string>>());
    }

    [Fact]
    public async Task RefreshAsync_WithAValidToken_RotatesAndReturnsANewPair()
    {
        _refreshTokens.FindActiveUserIdAsync("hash-of-presented", Arg.Any<CancellationToken>())
            .Returns(_userId);

        _identity.FindByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(_user);

        _refreshTokens.RotateAsync(
                "hash-of-presented",
                "hashed",
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns(_userId);

        Result<AuthenticationTokens> result =
            await _service.RefreshAsync("presented", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.RefreshToken.ShouldBe("raw");
    }

    [Fact]
    public async Task RefreshAsync_WithATokenThatIsNotActive_SpendsNothingAndLooksNobodyUp()
    {
        _refreshTokens.FindActiveUserIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((Guid?)null);

        Result<AuthenticationTokens> result =
            await _service.RefreshAsync("stale", CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.TopError.Code.ShouldBe(UserErrors.InvalidRefreshToken.Code);

        await _identity.DidNotReceive().FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
        await _refreshTokens.DidNotReceive().RotateAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_WhenTheAccountNoLongerExists_LeavesThePresentedTokenAlone()
    {
        _refreshTokens.FindActiveUserIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_userId);

        _identity.FindByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(UserErrors.NotFound);

        Result<AuthenticationTokens> result =
            await _service.RefreshAsync("presented", CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.TopError.Code.ShouldBe(UserErrors.InvalidRefreshToken.Code);
        result.TopError.Code.ShouldNotBe(UserErrors.NotFound.Code);

        await _refreshTokens.DidNotReceive().RotateAsync(
            Arg.Any<string>(),
            Arg.Any<string>(),
            Arg.Any<DateTimeOffset>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RefreshAsync_WhenAnotherRequestWinsTheRotation_ReturnsTheGenericTokenError()
    {
        _refreshTokens.FindActiveUserIdAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_userId);

        _identity.FindByIdAsync(_userId, Arg.Any<CancellationToken>()).Returns(_user);

        _refreshTokens.RotateAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<DateTimeOffset>(),
                Arg.Any<CancellationToken>())
            .Returns((Guid?)null);

        Result<AuthenticationTokens> result =
            await _service.RefreshAsync("presented", CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.TopError.Code.ShouldBe(UserErrors.InvalidRefreshToken.Code);
    }

    [Fact]
    public async Task LogoutAsync_WithAnyToken_RevokesTheHashAndSucceeds()
    {
        Result<Success> result =
            await _service.LogoutAsync("presented", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        await _refreshTokens.Received(1).RevokeAsync("hash-of-presented", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_DelegatesToTheAccountStore()
    {
        Registration request = new("ada@example.com", "pw", "Ada", "Lovelace");
        _identity.RegisterAsync(request, Arg.Any<CancellationToken>()).Returns(_userId);

        Result<Guid> result = await _service.RegisterAsync(request, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(_userId);
    }
}
