using CleanArchitecture.Domain.Users;
using Shouldly;
using Xunit;

namespace CleanArchitecture.Application.UnitTests;

public sealed class RolesTests
{
    [Theory]
    [InlineData("admin")]
    [InlineData("Admin")]
    [InlineData("USER")]
    [InlineData("user")]
    public void IsKnown_WithARoleTheApplicationShipsWith_IsTrue(string role) =>
        Roles.IsKnown(role).ShouldBeTrue();

    [Theory]
    [InlineData("superuser")]
    [InlineData("")]
    [InlineData(" admin")]
    [InlineData(null)]
    public void IsKnown_WithAnythingElse_IsFalse(string? role) =>
        Roles.IsKnown(role).ShouldBeFalse();

    [Fact]
    public void All_ContainsEveryDeclaredRole() =>
        Roles.All.ShouldBe([Roles.Admin, Roles.User], ignoreOrder: true);
}
