FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY Directory.Build.props Directory.Packages.props .editorconfig ./
COPY src/CleanArchitecture.Domain/CleanArchitecture.Domain.csproj src/CleanArchitecture.Domain/
COPY src/CleanArchitecture.Application/CleanArchitecture.Application.csproj src/CleanArchitecture.Application/
COPY src/CleanArchitecture.Infrastructure/CleanArchitecture.Infrastructure.csproj src/CleanArchitecture.Infrastructure/
COPY src/CleanArchitecture.Api/CleanArchitecture.Api.csproj src/CleanArchitecture.Api/
RUN dotnet restore src/CleanArchitecture.Api/CleanArchitecture.Api.csproj

COPY src/ src/
RUN dotnet publish src/CleanArchitecture.Api/CleanArchitecture.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app

USER $APP_UID

COPY --from=build /app .
EXPOSE 8080
ENTRYPOINT ["dotnet", "CleanArchitecture.Api.dll"]
