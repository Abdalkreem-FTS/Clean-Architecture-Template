using CleanArchitecture.Domain.Common.Results;
using Shouldly;

namespace CleanArchitecture.Domain.UnitTests;

public sealed class ErrorTests
{
    [Fact]
    public void EachFactory_CarriesItsOwnType()
    {
        Error.Failure().Type.ShouldBe(ErrorType.Failure);
        Error.Unexpected().Type.ShouldBe(ErrorType.Unexpected);
        Error.Validation().Type.ShouldBe(ErrorType.Validation);
        Error.Conflict().Type.ShouldBe(ErrorType.Conflict);
        Error.NotFound().Type.ShouldBe(ErrorType.NotFound);
        Error.Unauthorized().Type.ShouldBe(ErrorType.Unauthorized);
        Error.Forbidden().Type.ShouldBe(ErrorType.Forbidden);
    }

    [Fact]
    public void AnError_KeepsTheCodeAndDescriptionItWasGiven()
    {
        var error = Error.NotFound("Users.NotFound", "No user was found with that identifier.");

        error.Code.ShouldBe("Users.NotFound");
        error.Description.ShouldBe("No user was found with that identifier.");
    }

    [Fact]
    public void TwoErrorsWithTheSameParts_AreEqual() =>
        Error.Conflict("a", "b").ShouldBe(Error.Conflict("a", "b"));

    [Fact]
    public void TwoErrorsDifferingOnlyByType_AreNotEqual() =>
        Error.Conflict("a", "b").ShouldNotBe(Error.NotFound("a", "b"));
}
