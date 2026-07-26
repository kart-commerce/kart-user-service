# kart-user-service

Profile, address book, and preference management for the Kart platform (BRD §2.1 item 2) — plus
the internal GDPR-style erasure workflow (ADR-0016/ADR-0017). Built strictly against the approved
design docs in `kart-platform/docs/services/kart-user-service/` (requirement-spec, architecture,
ddd-model, database-design, event-contract, design-decisions, edge-cases, api-contract,
message-bus-manifest) and `kart-platform/docs/standards/kart-conventions.md`.

## Architecture

- **CQRS**: PostgreSQL is the strongly-consistent write model (`user_profiles`, `addresses`,
  `user_outbox_events`); MongoDB (`user_read_model`) is the eventually-consistent read
  projection, kept in sync via the platform's standard Outbox pattern. This service's write-model
  scale (50M registered users) doesn't call for read-side sharding the way Product/Search's
  100M-SKU catalog does (database-design.md) — the collection is still shard-ready (keyed by
  `_id` = `userId`), just not sharded today.
- **Messaging**: a config-driven RabbitMQ topology (`contracts/message-bus-manifest.json`),
  declared idempotently at startup and by every hosted service's own reconnect loop. Publishes
  `UserProfileUpdated`, `UserNotificationPreferenceUpdated`, `UserDataErased`; consumes
  `UserRegistered` and `UserAccountUpdated` from Identity.
- **One Outbox, two independent pollers**: `OutboxRelayHostedService` relays externally-documented
  events onto RabbitMQ; `ReadModelProjectionHostedService` is a separate in-process poller that
  rebuilds the Mongo document from current PostgreSQL state for *every* write (including the
  internal-only `contactCopy` reconciliation that must never be re-published externally, per
  ddd-model.md's Modeling Decision) — see `OutboxEvent`'s doc comment for why there are two
  independent completion markers (`PublishedAt`, `ProjectedAt`) on one row.
- **CQRS vertical slices** (MediatR) under `src/Application/Features/`, one per ticket
  (USR-1..USR-8 in `tickets.md`).
- **Result/Error pattern** (`Kart.Shared.Domain`) for expected business outcomes; exceptions are
  reserved for genuine infrastructure failures, translated once by `Kart.Shared.ErrorHandling`'s
  global exception handler — no local try/catch for translation anywhere in this codebase.
- **First Kart service to actually consume `Kart.Shared.*`** (Domain/ErrorHandling/Observability/
  Auditing) as real dependencies rather than hand-rolling a local equivalent, the way
  kart-identity-service/kart-category-service did before these packages existed.

## Running locally

Requires PostgreSQL, MongoDB, and RabbitMQ (see `src/Api/appsettings.Development.json` for
default local connection strings) plus a running kart-identity-service (this service validates
Identity-issued JWTs against its `/.well-known/jwks.json`, per `Jwt:JwksUri`).

```bash
dotnet run --project src/Api/Kart.User.Api.csproj
```

Apply the EF Core migration against a real PostgreSQL instance:

```bash
dotnet ef database update --project src/Infrastructure/Kart.User.Infrastructure.csproj --startup-project src/Infrastructure/Kart.User.Infrastructure.csproj
```

## Testing

```bash
dotnet test KartUserService.sln
```

- **UnitTests**: pure Domain aggregate logic + Application handlers against EF Core InMemory —
  no external dependencies.
- **IntegrationTests**: full HTTP pipeline via `WebApplicationFactory<Program>` — PostgreSQL
  swapped for Sqlite in-memory, MongoDB swapped for an ephemeral local `mongod` (Mongo2Go), JWT
  bearer auth swapped for a header-driven test scheme (`X-Test-Sub`/`X-Test-Scope`). RabbitMQ
  hosted services are removed (no real broker in this environment); event-consumer behavior is
  exercised by dispatching the same MediatR command the consumer would.
- **ContractTests**: asserts the vendored `contracts/api-contract.yaml` still defines the
  expected paths/operations/responses, then exercises the same behavior live over HTTP.

## Docker build

Cross-repo-references `kart-shared/src/Kart.Shared.*` (no published NuGet feed exists yet — see
`kart-shared/README.md`), so the build context must be the `kart-commerce` parent directory:

```bash
cd .. && docker build -f kart-user-service/Dockerfile -t kart-user-service:latest .
```
