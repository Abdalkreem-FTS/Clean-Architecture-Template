namespace CleanArchitecture.Api.IntegrationTests.Configuration;

internal static class TestTokens
{
    public const string Unknown = "not-a-token-anyone-ever-issued";

    public const string ForeignlySigned =
        "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJzdWIiOiIxMjM0In0.invalid-signature";
}
