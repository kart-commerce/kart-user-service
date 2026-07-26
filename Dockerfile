# Build context must be the kart-commerce parent directory, not this repo, because
# Kart.User.* projects cross-repo-reference kart-shared/src/Kart.Shared.* (no published NuGet
# feed exists yet — kart-shared/README.md). Build from kart-commerce/ with:
#   docker build -f kart-user-service/Dockerfile -t kart-user-service:latest .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY kart-user-service/KartUserService.sln kart-user-service/
COPY kart-user-service/src/Api/Kart.User.Api.csproj kart-user-service/src/Api/
COPY kart-user-service/src/Application/Kart.User.Application.csproj kart-user-service/src/Application/
COPY kart-user-service/src/Domain/Kart.User.Domain.csproj kart-user-service/src/Domain/
COPY kart-user-service/src/Infrastructure/Kart.User.Infrastructure.csproj kart-user-service/src/Infrastructure/
COPY kart-user-service/tests/UnitTests/Kart.User.UnitTests.csproj kart-user-service/tests/UnitTests/
COPY kart-user-service/tests/IntegrationTests/Kart.User.IntegrationTests.csproj kart-user-service/tests/IntegrationTests/
COPY kart-user-service/tests/ContractTests/Kart.User.ContractTests.csproj kart-user-service/tests/ContractTests/
COPY kart-shared/src/Kart.Shared.Domain/Kart.Shared.Domain.csproj kart-shared/src/Kart.Shared.Domain/
COPY kart-shared/src/Kart.Shared.ErrorHandling/Kart.Shared.ErrorHandling.csproj kart-shared/src/Kart.Shared.ErrorHandling/
COPY kart-shared/src/Kart.Shared.Observability/Kart.Shared.Observability.csproj kart-shared/src/Kart.Shared.Observability/
COPY kart-shared/src/Kart.Shared.Auditing/Kart.Shared.Auditing.csproj kart-shared/src/Kart.Shared.Auditing/
RUN dotnet restore kart-user-service/src/Api/Kart.User.Api.csproj

COPY kart-user-service/ kart-user-service/
COPY kart-shared/ kart-shared/
RUN dotnet publish kart-user-service/src/Api/Kart.User.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Kart.User.Api.dll"]
