# Clean Architecture template

A `dotnet new` template: a four-layer ASP.NET Core API with authentication
already working, so a new service starts from a running, tested baseline instead of an empty
solution.

It ships ASP.NET Core Identity with JWT bearer tokens and role-based authorization,
PostgreSQL, SQL migrations applied by [Evolve](https://evolve-db.netlify.app/), and three test
suites.

## Projects

| Project | What it holds |
|---|---|
| `CleanArchitecture.Domain` | `Result` and `Error`, the role names, the domain errors, and the `RefreshToken` entity. No package references at all. |
| `CleanArchitecture.Application` | Use cases, and the ports Infrastructure implements. |
| `CleanArchitecture.Infrastructure` | PostgreSQL, Identity, JWT, Evolve. |
| `CleanArchitecture.Api` | Composition root, HTTP endpoints, the request/response records and their validators. |
| `CleanArchitecture.ArchitectureTests` | Fails the build if the dependency rule is broken. |
| `CleanArchitecture.Domain.UnitTests` | `Result`, `Error`, and the role names. No dependencies to substitute. |
| `CleanArchitecture.Application.UnitTests` | Use-case logic, every port substituted. |
| `CleanArchitecture.Api.IntegrationTests` | Real HTTP against a real PostgreSQL container. |

Dependencies point one way: `Api → Infrastructure → Application → Domain`.

Contracts follow one rule: `*Request` and `*Response` records live in `CleanArchitecture.Api`
and never leave it. The Application layer has its own types — `Registration`, `User`,
`AuthenticationTokens`, `Paged<T>` — and the endpoints map between the two. An architecture test
fails the build if a port starts speaking a `Request` or a `Response`.

This is ports and adapters, not DDD: ASP.NET Core Identity owns account policy — lockout,
password rules, email confirmation — and the Domain project holds the result type, the domain
errors and one entity. There are no aggregates here, and the layering is enforced by the
architecture tests rather than by convention.

`src/CleanArchitecture.Infrastructure/Migrations/*.sql` owns the schema. EF Core migrations
are deliberately not used; the `.sql` files are copied next to the app and applied by Evolve.

`Program.cs` runs them itself, in-process, before the app serves its first request, and seeds
the administrator straight after. So the database has to be reachable at startup and the
connection's user needs DDL rights — enough to create and alter tables, not only to read and
write rows. To apply schema changes out of band instead, delete that block and run Evolve as
its own step.

## Authentication

Access tokens are short-lived; refresh tokens rotate, so presenting one revokes it and issues a
replacement and each refresh token works exactly once. Replaying a spent token is refused with
401 and nothing further happens. A stricter policy treats a replay as evidence the token was
stolen and revokes every token that user holds — deliberately not implemented here, but the
place to add it is `RefreshTokenStore.RotateAsync`.

## Use it

```bash
dotnet new install .
dotnet new clean-architecture -n MyProject
```

`CleanArchitecture` is replaced by the project name throughout — namespaces, projects,
directories, the solution file, the database name and the JWT issuer.

## Run it

```bash
docker compose up --build
```

The API comes up on <http://localhost:8080>, migrates on startup, seeds an administrator, and
serves docs at <http://localhost:8080/scalar>.

## Test it

```bash
dotnet test                                                            # all three; needs Docker
dotnet test --project tests/CleanArchitecture.ArchitectureTests        # no Docker needed
dotnet test --project tests/CleanArchitecture.Application.UnitTests    # no Docker needed
dotnet test --project tests/CleanArchitecture.Api.IntegrationTests     # needs Docker
```

Each test project is a self-hosting executable built on Microsoft.Testing.Platform, so
`dotnet run --project tests/...` works too. The `test.runner` entry in `global.json` is what
points `dotnet test` at that runner instead of VSTest, which the .NET 10 SDK no longer
supports.