using CleanArchitecture.Application.Abstractions;
using CleanArchitecture.Application.Users;
using CleanArchitecture.Domain.Common.Results;
using CleanArchitecture.Domain.Users;
using NSubstitute;
using Shouldly;
using UserDto = CleanArchitecture.Application.Users.User;
using DomainUser = CleanArchitecture.Domain.Users.User;

namespace CleanArchitecture.Application.UnitTests;

public sealed class UserServiceTests
{
    private readonly IUserAccountService _accounts = Substitute.For<IUserAccountService>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly UserService _service;

    private static readonly Guid _userId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public UserServiceTests() => _service = new UserService(_accounts, _currentUser);

    [Fact]
    public async Task AssignRoleAsync_WithAnUnknownRole_RefusesWithoutTouchingTheStore()
    {
        Result<Success> result = await _service.AssignRoleAsync(_userId, "superuser", CancellationToken.None);

        result.IsError.ShouldBeTrue();
        result.TopError.Code.ShouldBe(UserErrors.RoleNotFound.Code);

        await _accounts.DidNotReceive().AssignRoleAsync(
            Arg.Any<Guid>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("Admin")]
    [InlineData("ADMIN")]
    [InlineData("admin")]
    public async Task AssignRoleAsync_WithAKnownRoleInAnyCase_NormalisesItToLowercase(string role)
    {
        _accounts.AssignRoleAsync(_userId, Roles.Admin, Arg.Any<CancellationToken>())
            .Returns(Result.Success);

        Result<Success> result = await _service.AssignRoleAsync(_userId, role, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();

        await _accounts.Received(1).AssignRoleAsync(_userId, Roles.Admin, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetCurrentAsync_WithAnAuthenticatedCaller_ReadsThatUser()
    {
        _currentUser.UserId.Returns(_userId);
        _accounts.FindByIdAsync(_userId, Arg.Any<CancellationToken>())
            .Returns(DomainUser.FromStorage(
                _userId,
                "ada@example.com",
                "Ada",
                "Lovelace",
                [Roles.User],
                DateTimeOffset.UnixEpoch,
                null));

        Result<UserDto> result = await _service.GetCurrentAsync(CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Id.ShouldBe(_userId);
    }

    [Fact]
    public async Task GetCurrentAsync_WithNoSubjectInTheToken_ReadsNobody()
    {
        _currentUser.UserId.Returns((Guid?)null);

        Result<UserDto> result = await _service.GetCurrentAsync(CancellationToken.None);

        result.IsError.ShouldBeTrue();

        await _accounts.DidNotReceive().FindByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }
}
