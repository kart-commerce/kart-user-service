# syntax=docker/dockerfile:1

# Build context must be the kart-commerce parent directory, not this repo, because
# Kart.User.* projects cross-repo-reference kart-shared/src/Kart.Shared.* (no published NuGet
# feed exists yet — kart-shared/README.md). Build from kart-commerce/ with:
#   docker build -f kart-user-service/Dockerfile -t kart-user-service:latest .
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY kart-user-service/KartUserService.sln kart-user-service/
COPY kart-user-service/Directory.Build.props kart-user-service/
COPY kart-shared/Directory.Build.props kart-shared/
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
COPY kart-shared/src/Kart.Shared.Configuration/Kart.Shared.Configuration.csproj kart-shared/src/Kart.Shared.Configuration/
COPY kart-shared/src/Kart.Shared.Messaging/Kart.Shared.Messaging.csproj kart-shared/src/Kart.Shared.Messaging/
# The cache mount persists extracted NuGet packages under a stable id shared by every other
# kart-*-service Dockerfile, so restore stays fast (no re-download) even on a cache-miss here
# (e.g. after a .csproj change) as long as some other service's build already warmed it.
RUN --mount=type=cache,target=/root/.nuget/packages,id=nuget-packages \
    dotnet restore kart-user-service/src/Api/Kart.User.Api.csproj

# Scoped to src/ + contracts/ from each repo instead of the previous whole-directory
# `COPY kart-user-service/ kart-user-service/` / `COPY kart-shared/ kart-shared/` -- those also
# pulled in tests/, README.md, scripts/, kart-shared's own tests/ and docs, etc., so editing any
# of that busted this layer (and the publish below) even though none of it reaches the published
# output. contracts/ is kept because Kart.User.Api.csproj copies message-bus-manifest.json from
# it into the publish output as a <Content> item.
COPY kart-user-service/src/ kart-user-service/src/
COPY kart-user-service/contracts/ kart-user-service/contracts/
COPY kart-shared/src/ kart-shared/src/
# --no-restore only skips re-resolving the dependency graph -- publish still reads the actual
# package DLLs from the global packages folder, so it needs the same cache mount as restore
# above (the mount isn't part of the image; without it here this folder is empty again).
RUN --mount=type=cache,target=/root/.nuget/packages,id=nuget-packages \
    dotnet publish kart-user-service/src/Api/Kart.User.Api.csproj -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

ENTRYPOINT ["dotnet", "Kart.User.Api.dll"]
