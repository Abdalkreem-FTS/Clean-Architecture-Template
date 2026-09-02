namespace CleanArchitecture.Api.IntegrationTests.Configuration;

internal static class TestSettings
{
    public static string PostgresImage => FromEnvironment("CLEANARCHITECTURE_TESTS_POSTGRES_IMAGE", "postgres:17-alpine");

    public static string DatabaseName => FromEnvironment("CLEANARCHITECTURE_TESTS_DATABASE", "cleanarchitecture_tests");

    public static string DatabaseUser => FromEnvironment("CLEANARCHITECTURE_TESTS_DB_USER", "postgres");

    public static string DatabasePassword => FromEnvironment("CLEANARCHITECTURE_TESTS_DB_PASSWORD", "postgres");

    public const string SigningKey = "integration-test-signing-key-0123456789-not-a-secret";

    public const string EnvironmentName = "Testing";

    public const string AuthenticationScheme = "Bearer";

    private static string FromEnvironment(string variable, string fallback) =>
        Environment.GetEnvironmentVariable(variable) is { Length: > 0 } value ? value : fallback;
}
