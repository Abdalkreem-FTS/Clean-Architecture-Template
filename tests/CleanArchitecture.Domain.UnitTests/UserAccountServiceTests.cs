using CleanArchitecture.Domain.Common.Results;
using CleanArchitecture.Domain.Users;
using NSubstitute;
using Shouldly;

namespace CleanArchitecture.Domain.UnitTests;

// Covers the ordering AuthenticateAsync runs its checks in, and which error each one maps to.
// That ordering is a product decision (a locked-out account must not be distinguishable from a
// wrong password, an unconfirmed email must not stop a good password from clearing the failed
// count) so it belongs here, against a substituted IUserRepository, rather than behind Docker.
public sealed class UserAccountServiceTests
{
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly FixedTimeProvider _clock = new(DateTimeOffset.UnixEpoch);
    private readonly UserAccountService _service;

    public UserAccountServiceTests() => _service = new UserAccountService(_users, _clock);

    [Fact]
    public async Task AuthenticateAsync_WithAnUnknownEmail_ReturnsInvalidCredentialsWithoutTouchingPasswordOrLockout()
    {
        _users.FindByEmailAsync("ada@example.com", Arg.Any<CancellationToken>()).Returns((User?)null);

        Result<User> result = await _service.AuthenticateAsync("ada@example.com", "pw", CancellationToken.None);

        result.TopError.Code.ShouldBe(UserErrors.InvalidCredentials.Code);

        await _users.DidNotReceive().VerifyPasswordAsync(
            Arg.Any<User>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthenticateAsync_WithAWrongPassword_ReturnsInvalidCredentialsWithoutCheckingLockout()
    {
        User user = AUser();
        _users.FindByEmailAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);
        _users.VerifyPasswordAsync(user, "wrong", Arg.Any<CancellationToken>()).Returns(false);

        Result<User> result = await _service.AuthenticateAsync(user.Email, "wrong", CancellationToken.None);

        result.TopError.Code.ShouldBe(UserErrors.InvalidCredentials.Code);

        await _users.DidNotReceive().IsLockedOutAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthenticateAsync_WithALockedOutAccount_ReturnsTheSameErrorAWrongPasswordWouldWithoutResettingFailedAttempts()
    {
        User user = AUser();
        _users.FindByEmailAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);
        _users.VerifyPasswordAsync(user, "pw", Arg.Any<CancellationToken>()).Returns(true);
        _users.IsLockedOutAsync(user, Arg.Any<CancellationToken>()).Returns(true);

        Result<User> result = await _service.AuthenticateAsync(user.Email, "pw", CancellationToken.None);

        // Same code as a wrong password, deliberately: a distinct one here would let an attacker
        // tell which emails exist by brute-forcing until the response changes.
        result.TopError.Code.ShouldBe(UserErrors.InvalidCredentials.Code);

        await _users.DidNotReceive().ResetFailedAttemptsAsync(Arg.Any<User>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthenticateAsync_WithAnUnconfirmedEmail_StillResetsFailedAttemptsBeforeRefusing()
    {
        User user = AUser();
        _users.FindByEmailAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);
        _users.VerifyPasswordAsync(user, "pw", Arg.Any<CancellationToken>()).Returns(true);
        _users.IsLockedOutAsync(user, Arg.Any<CancellationToken>()).Returns(false);
        _users.RequiresEmailConfirmationAsync(user, Arg.Any<CancellationToken>()).Returns(true);

        Result<User> result = await _service.AuthenticateAsync(user.Email, "pw", CancellationToken.None);

        result.TopError.Code.ShouldBe(UserErrors.EmailNotConfirmed.Code);

        // The whole point of resetting before this check: a correct password from an
        // unconfirmed account must still clear the failed-attempt count, or it locks itself
        // out purely by existing.
        await _users.Received(1).ResetFailedAttemptsAsync(user, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AuthenticateAsync_WithAGoodPassword_RecordsTheLoginAndReturnsTheUser()
    {
        User user = AUser();
        _users.FindByEmailAsync(user.Email, Arg.Any<CancellationToken>()).Returns(user);
        _users.VerifyPasswordAsync(user, "pw", Arg.Any<CancellationToken>()).Returns(true);
        _users.IsLockedOutAsync(user, Arg.Any<CancellationToken>()).Returns(false);
        _users.RequiresEmailConfirmationAsync(user, Arg.Any<CancellationToken>()).Returns(false);

        Result<User> result = await _service.AuthenticateAsync(user.Email, "pw", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.LastLoginAtUtc.ShouldBe(_clock.GetUtcNow());

        await _users.Received(1).RecordLoginAsync(user, _clock.GetUtcNow(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_OnSuccess_GrantsTheDefaultUserRole()
    {
        _users.CreateAsync(Arg.Any<User>(), "pw", Arg.Any<CancellationToken>())
            .Returns(call => call.Arg<User>());
        _users.AssignRoleAsync(Arg.Any<User>(), Roles.User, Arg.Any<CancellationToken>())
            .Returns(Result.Success);

        Result<Guid> result =
            await _service.RegisterAsync("ada@example.com", "pw", "Ada", "Lovelace", CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        await _users.Received(1).AssignRoleAsync(Arg.Any<User>(), Roles.User, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_WhenCreationFails_NeverAssignsARole()
    {
        _users.CreateAsync(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(UserErrors.EmailNotUnique);

        Result<Guid> result =
            await _service.RegisterAsync("ada@example.com", "pw", "Ada", "Lovelace", CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.TopError.Code.ShouldBe(UserErrors.EmailNotUnique.Code);

        await _users.DidNotReceive().AssignRoleAsync(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task AssignRoleAsync_WithAUserWhoAlreadyHasTheRole_SucceedsWithoutWritingAgain()
    {
        User user = User.FromStorage(
            Guid.NewGuid(),
            "ada@example.com",
            "Ada",
            "Lovelace",
            [Roles.Admin],
            DateTimeOffset.UnixEpoch,
            null);

        _users.FindByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);

        Result<Success> result = await _service.AssignRoleAsync(user.Id, Roles.Admin, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        await _users.DidNotReceive().AssignRoleAsync(Arg.Any<User>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    private static User AUser() => User.FromStorage(
        Guid.NewGuid(),
        "ada@example.com",
        "Ada",
        "Lovelace",
        [Roles.User],
        DateTimeOffset.UnixEpoch,
        null);

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }
}
