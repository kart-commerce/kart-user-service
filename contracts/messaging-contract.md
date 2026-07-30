# Kart User Service — Messaging Contract

Source of truth: [`contracts/message-bus-manifest.json`](./message-bus-manifest.json). Nothing here is hand-maintained config — `RabbitMqTopologyProvisioner` (from `Kart.Shared.Messaging`) reads that manifest at startup and declares every exchange, queue, binding, DLQ, and retry tier from it. This doc is a human-readable index over that manifest plus the publisher/consumer manifests of the services it talks to.

Last verified: 2026-07-30, against the current state of the repo (not the platform design docs — see the caveats at the bottom).

## Exchanges owned by this service

| Exchange | Type | Durable | Purpose |
|---|---|---|---|
| `user.exchange` | topic | yes | All events published by User Service |
| `user.dlx` | topic | yes | Dead-letter exchange for User Service's own consumed events |

## Published events

| Event | Exchange | Routing Key | Exchange Type |
|---|---|---|---|
| `UserProfileUpdated` | `user.exchange` | `user.profile-updated` | topic |
| `UserNotificationPreferenceUpdated` | `user.exchange` | `user.notification-preference-updated` | topic |
| `UserDataErased` | `user.exchange` | `user.data-erased` | topic |

These three are the only *externally* published event types — the outbox `event_type` check constraint enumerates them plus one internal-only marker, `UserReadModelProjectionRequested`, which never goes over RabbitMQ (see [Internal read-model projection](#internal-read-model-projection-not-part-of-the-rabbitmq-topology) below).

- `UserProfileUpdated` fires from `AddAddressCommandHandler`, `RemoveAddressCommandHandler`, `UpdateAddressCommandHandler`, and `UpdateUserPreferencesCommandHandler`.
- `UserNotificationPreferenceUpdated` fires from `UpdateUserPreferencesCommandHandler`, alongside `UserProfileUpdated`, from the same preferences-update flow.
- `UserDataErased` fires on GDPR erasure.

## Who consumes what

### `UserProfileUpdated` and `UserNotificationPreferenceUpdated`

**No live consumers today.** No manifest anywhere in the monorepo binds a queue to `user.profile-updated` or `user.notification-preference-updated`, and no service's source code references either event name. The design doc (`kart-platform/docs/services/kart-user-service/event-contract.md`) names Analytics as the intended consumer of `UserProfileUpdated` and Notification as the intended consumer of `UserNotificationPreferenceUpdated` — but both `kart-analytics-service` and `kart-notification-service` are README-only stubs with no code. Both events are published into the void until those services exist.

### `UserDataErased`

| Consumer | Queue | Retry Ladder | Dead-Letter Queue |
|---|---|---|---|
| **Identity Service** | `identity.user-events.queue` | 30s → 60s → 120s → 240s → 480s (5 tiers) | `identity.user-events.dlq` (on `identity.dlx`) |
| **Cart Service** | `cart.user-data-erased.queue` | 30s → 60s → 120s → 300s → 600s (5 tiers) | on `cart.dlx` |

A manifest comment claims 7 intended downstream consumers total (Order, Notification, Analytics, Review, Recommendation, Wishlist, Identity) — only Identity and Cart are real; the other 5 named services are unimplemented stubs.

### Summary

| Event | Real Consumer(s) | Documented-but-unbuilt Consumer(s) |
|---|---|---|
| `UserProfileUpdated` | — none — | Analytics (stub repo, no code) |
| `UserNotificationPreferenceUpdated` | — none — | Notification (stub repo, no code) |
| `UserDataErased` | Identity Service, Cart Service | Order, Notification, Analytics, Review, Recommendation, Wishlist (all stub repos) |

## What this service consumes

| Consumed Event | From | Queue | Retry Ladder | Dead-Letter Queue |
|---|---|---|---|---|
| `UserRegistered` | Identity Service (`identity.exchange` / `identity.user.registered`) | `user.user-registered.queue` | 30s → 120s → 300s (3 tiers) | `user.user-registered.dlq` (on `user.dlx`) |
| `UserAccountUpdated` | Identity Service (`identity.exchange` / `identity.user-account.updated`) | `user.user-account-updated.queue` | 30s → 120s (2 tiers) | `user.user-account-updated.dlq` (on `user.dlx`) |

Both queues bind to Identity's external `identity.exchange` — User Service doesn't own that exchange, it only binds to it.

**Consumer implementation:**
- `UserRegisteredConsumerHostedService` (retry-count header `x-user-service-retry-count`) dispatches `CreateUserProfileOnRegistrationCommand`, which upserts on `userId` — redelivery of an already-created profile is a no-op.
- `UserAccountUpdatedConsumerHostedService` (same header) dispatches `ReconcileIdentityContactCopyCommand`, which handles out-of-order delivery: if `UserAccountUpdated` arrives before `UserRegistered`, it creates a shell profile rather than dropping the event, then applies the contact-copy update only if `updatedAt` is newer than what's stored. It deliberately does not re-publish `UserProfileUpdated` externally — the code comment notes Analytics is meant to receive `UserAccountUpdated` directly from Identity instead (aspirational, since Analytics doesn't exist yet).

## Retry & dead-letter mechanics

Same manifest-driven mechanism used platform-wide (`Kart.Shared.Messaging`): each queue declares its own retry ladder in this service's manifest. On handler failure, the consumer stamps a per-service header (`x-user-service-retry-count`), republishes to the next tier's retry queue (TTL-based, dead-lettering back to the origin queue on expiry), and once tiers are exhausted, nacks without requeue so RabbitMQ lands the message on the queue's declared terminal DLQ — always on the *consumer's* side, never a shared/global DLQ.

## Internal read-model projection (not part of the RabbitMQ topology)

`ReadModelProjectionHostedService` is a plain polling `BackgroundService` — no RabbitMQ connection at all — that runs every 2 seconds against this service's own outbox table (`user_outbox_events`), picking up rows with `ProjectedAt == null` (batch size 50). For each pending row's `userId`, it re-reads the current PostgreSQL write-model state and upserts the corresponding read model into MongoDB, then stamps `ProjectedAt` independently of `PublishedAt` (which only tracks the 3 externally-published event types via the RabbitMQ outbox relay). This is why the internal marker `UserReadModelProjectionRequested` exists: it drives this Mongo projection without ever going out over the exchange, and has no manifest entry.

## Caveats

- `kart-platform/docs/services/kart-user-service/message-bus-manifest.json` (design-intent copy) models a single combined queue (`user.identity-events.queue`) with one 30s retry tier for both `UserRegistered` and `UserAccountUpdated` — this does not match the actual implementation, which correctly uses two independently-retried, independently-DLQ'd queues per the platform's "never share a DLQ across event types" rule.
- `event-contract.md`'s retry counts for `UserRegistered` (3x) and `UserAccountUpdated` (2x) do match the real ladders exactly; only the queue-consolidation and the Analytics/Notification consumer claims are aspirational.
- Of the 19 services in the monorepo, only 10 have real implementations (kart-identity-service, kart-user-service, kart-cart-service, kart-category-service, kart-delivery-tracking-service, kart-inventory-service, kart-offer-service, kart-payment-service, kart-product-service, kart-search-service); the rest are README-only stubs.
