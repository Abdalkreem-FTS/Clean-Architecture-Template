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
| `CleanArchitecture.Domain` | Result and Error types, entities, domain errors. No package references at all. |
| `CleanArchitecture.Application` | Use cases, and the ports Infrastructure implements. |
| `CleanArchitecture.Infrastructure` | PostgreSQL, Identity, JWT, Evolve. |
| `CleanArchitecture.Api` | Composition root and HTTP endpoints. |
| `CleanArchitecture.ArchitectureTests` | Fails the build if the dependency rule is broken. |
| `CleanArchitecture.Application.UnitTests` | Use-case logic, every port substituted. |
| `CleanArchitecture.Api.IntegrationTests` | Real HTTP against a real PostgreSQL container. |

Dependencies point one way: `Api → Infrastructure → Application → Domain`.

`db/migrations/*.sql` owns the schema. EF Core migrations are deliberately not used.

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
dotnet run --project tests/CleanArchitecture.ArchitectureTests       # no Docker needed
dotnet run --project tests/CleanArchitecture.Application.UnitTests   # no Docker needed
dotnet run --project tests/CleanArchitecture.Api.IntegrationTests    # needs Docker
```