using System;
using System.Globalization;

namespace CleanArchitecture.Api.IntegrationTests.Configuration;

// Every URL the suite calls, in one place — so a route rename in CleanArchitecture.Api/Endpoints is a single
// edit here rather than a search-and-replace across the tests.
internal static class Routes
{
    internal static class Authentication
    {
        private const string Group = "/api/authentication";

        public const string Register = Group + "/register";
        public const string Login = Group + "/login";
        public const string Refresh = Group + "/refresh";
        public const string Logout = Group + "/logout";
    }

    internal static class Users
    {
        public const string All = "/api/users";
        public const string Me = "/api/users/me";

        public static string ById(Guid id) =>
            string.Create(CultureInfo.InvariantCulture, $"{All}/{id}");

        public static string RolesOf(Guid id) =>
            string.Create(CultureInfo.InvariantCulture, $"{All}/{id}/roles");

        public static string Page(int? number = null, int? size = null)
        {
            string query = (number, size) switch
            {
                (null, null) => string.Empty,
                (not null, null) => string.Create(CultureInfo.InvariantCulture, $"?page={number}"),
                (null, not null) => string.Create(CultureInfo.InvariantCulture, $"?pageSize={size}"),
                _ => string.Create(CultureInfo.InvariantCulture, $"?page={number}&pageSize={size}"),
            };

            return All + query;
        }
    }

    internal static class Health
    {
        public const string Live = "/health/live";
        public const string Ready = "/health/ready";
    }
}
