realtime.md

snakk – Realtime Architecture

PURPOSE

Realtime exists to:
- Show new replies instantly
- Update read/unread state live
- Avoid polling
- Scale to thousands of concurrent users

Realtime is NOT used for:
- Chat-style typing indicators (optional, low priority)
- Presence tracking beyond “connected”
- Client-side state management

---

ARCHITECTURAL PRINCIPLES

- Realtime is infrastructure, not domain logic
- Core defines WHAT events exist
- Infrastructure defines HOW events are delivered
- Api owns connection lifecycle and subscription intent

---

CORE INTERFACES

Core defines the following interface:

- IRealtimePublisher
  - PublishNewPost
  - PublishPostEdited
  - PublishPostDeleted
  - PublishReadStateUpdated

Core knows NOTHING about:
- WebSockets
- SignalR
- Groups
- Connections

---

INFRASTRUCTURE IMPLEMENTATION

Realtime is implemented in:
- Snakk.Infrastructure.Realtime

Possible implementations:
- SignalR (initial)
- Raw WebSockets (future)
- Redis pub/sub backplane (future)

Infrastructure responsibilities:
- Track active connections
- Track group subscriptions
- Fan out events to subscribed connections

---

CONNECTION LIFECYCLE

1. Client opens page
2. Api bootstraps WebSocket connection
3. Client sends:
   - Anonymous or authenticated user ID
   - Current context (hub / space / discussion)
4. Infrastructure:
   - Registers connection
   - Subscribes connection to groups

5. Events are published
6. Infrastructure delivers events to connections
7. Client updates DOM via minimal JS + HTMX-compatible fragments
8. Connection closes naturally or on page unload

---

SUBSCRIPTION MODEL

Each WebSocket connection subscribes to multiple groups.

Groups:
- global
- hub:{hubId}
- space:{spaceId}
- discussion:{discussionId}

Example:
Entering discussion X in space Y under hub Z subscribes to:
- global
- hub:Z
- space:Y
- discussion:X

---

MULTIPLE BROWSER WINDOWS

- Each browser window/tab has its own connection
- Same user ID may have N concurrent connections
- Subscriptions are tracked per connection
- No attempt is made to deduplicate per user

This simplifies scaling and avoids cross-tab complexity.

---

READ / UNREAD INTEGRATION

- Read state updates are domain events
- When read state changes:
  - Core emits event
  - Infrastructure publishes realtime notification
  - Clients update unread indicators

No polling.
No client-side unread calculations.

---

FAILURE MODES

- If realtime is unavailable:
  - System still works via page reloads
  - HTMX navigation still functions
- Realtime is an enhancement, not a dependency

---

SCALING NOTES

- Api remains stateless
- Realtime layer can scale horizontally
- Backplane can be introduced later
- Group fan-out must be efficient
- Avoid per-user state in memory

---

NON-GOALS (V1)

- Guaranteed delivery
- Offline message queues
- Typing indicators by default
- Presence lists
