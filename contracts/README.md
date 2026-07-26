# Contracts

`api-contract.yaml` is a synced copy of the approved contract owned by
`kart-platform/docs/services/kart-user-service/api-contract.yaml` (the
source of truth). It is vendored here so `tests/ContractTests` can validate
this service's actual HTTP responses against it in this repo's own CI,
without a cross-repo checkout. Update it only by re-copying the upstream
file after a new contract revision is approved there — never edit it
directly in this repo.

`message-bus-manifest.json` is the runtime topology declaration consumed at
startup by `Infrastructure/Messaging/RabbitMqTopologyProvisioner.cs` (same
schema/shape as `kart-identity-service/contracts/message-bus-manifest.json`
— exchanges/externalExchanges/publishedEvents/queues/deadLetterQueues,
with a TTL-ladder retry queue per consumed event). It elaborates
`kart-platform/docs/services/kart-user-service/message-bus-manifest.json`
(the doc-level manifest, which only sketches the identity-events consumer
queue) into the concrete per-consumer-queue/retry-tier shape
`docs/services/kart-user-service/event-contract.md` specifies exactly:
`UserRegistered` 3x retry -> `user.user-registered.dlq`, `UserAccountUpdated`
2x retry -> `user.user-account-updated.dlq`. Update it only by re-deriving
from the upstream docs after a manifest revision is approved there.
