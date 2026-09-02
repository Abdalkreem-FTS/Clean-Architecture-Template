using Shouldly;
using TestResult = NetArchTest.Rules.TestResult;

namespace CleanArchitecture.ArchitectureTests;

internal static class ArchitectureAssertions
{
    public static void ShouldBeSuccessful(this TestResult result, string because)
    {
        string[] offenders = [.. result.FailingTypeNames ?? []];

        result.IsSuccessful.ShouldBeTrue(
            $"{because}{Environment.NewLine}Offending types:{Environment.NewLine}  "
            + string.Join(Environment.NewLine + "  ", offenders));
    }
}
