using CleanArchitecture.Domain.Common.Results;
using Shouldly;

namespace CleanArchitecture.Domain.UnitTests;

public sealed class ResultTests
{
    private static readonly Error _notFound = Error.NotFound("Test.NotFound", "Nothing here.");
    private static readonly Error _conflict = Error.Conflict("Test.Conflict", "Already there.");

    [Fact]
    public void AValue_ConvertsToASuccess()
    {
        Result<string> result = "ada";

        result.IsSuccess.ShouldBeTrue();
        result.IsError.ShouldBeFalse();
        result.Value.ShouldBe("ada");
        result.Errors.ShouldBeEmpty();
    }

    [Fact]
    public void AnError_ConvertsToAFailure()
    {
        Result<string> result = _notFound;

        result.IsError.ShouldBeTrue();
        result.IsSuccess.ShouldBeFalse();
        result.TopError.ShouldBe(_notFound);
        result.Errors.ShouldBe([_notFound]);
    }

    [Fact]
    public void AListOfErrors_KeepsTheirOrder()
    {
        Result<string> result = new List<Error> { _notFound, _conflict };

        result.Errors.ShouldBe([_notFound, _conflict]);
        result.TopError.ShouldBe(_notFound);
    }

    [Fact]
    public void ReadingTheValueOfAFailure_Throws()
    {
        Result<string> result = _notFound;

        Should.Throw<InvalidOperationException>(() => result.Value);
    }

    [Fact]
    public void ReadingTheTopErrorOfASuccess_Throws()
    {
        Result<string> result = "ada";

        Should.Throw<InvalidOperationException>(() => result.TopError);
    }

    [Fact]
    public void ANullValue_IsRefused() =>
        Should.Throw<ArgumentNullException>(() => { Result<string> _ = (string)null!; });

    [Fact]
    public void AnEmptyErrorList_IsRefused() =>
        Should.Throw<ArgumentException>(() => { Result<string> _ = new List<Error>(); });

    [Fact]
    public void Match_OnASuccess_TakesTheValueBranch()
    {
        Result<string> result = "ada";

        result.Match(value => $"value:{value}", errors => $"errors:{errors.Count}").ShouldBe("value:ada");
    }

    [Fact]
    public void Match_OnAFailure_TakesTheErrorBranch()
    {
        Result<string> result = new List<Error> { _notFound, _conflict };

        result.Match(value => $"value:{value}", errors => $"errors:{errors.Count}").ShouldBe("errors:2");
    }
}
