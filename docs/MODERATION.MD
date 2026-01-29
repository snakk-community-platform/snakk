moderation.md

snakk – Moderation Model

GOALS

- Keep moderation understandable and predictable
- Avoid overreach
- Prefer closure over silent manipulation
- Preserve context and auditability

---

ROLE HIERARCHY

- Platform admin
- Hub owner
- Hub moderator
- Space owner
- Space moderator
- Regular user
- Read-only user

Rules:
- Higher roles override lower roles
- Space-level rules cannot override platform-wide rules

---

MODERATION SCOPE

Platform admin:
- Global enforcement
- Abuse handling
- Hub approval (optional)

Hub owner / moderator:
- Moderate all spaces in hub
- Manage space ownership

Space owner / moderator:
- Moderate discussions and posts within space
- Define space-specific rules

---

MODERATION ACTIONS

Post-level:
- Report post
- Hide post (soft)
- Unhide post
- Soft delete post

Discussion-level:
- Lock discussion
- Archive discussion
- Pin discussion
- Close discussion (final state)

User-level:
- Warning
- Temporary mute
- Space ban
- Hub ban
- Global ban (platform admin only)

---

DEBATE FORMAT RULES

- Debate positions are defined by the author
- Moderators CANNOT:
  - Remove a position
  - Change sides on behalf of users

Moderators MAY:
- Lock the discussion
- Close the discussion
- Disable debate format mid-thread
- Ask author to recreate the discussion

Rationale:
- Removing positions is editorial overreach
- Closure is preferred to silent modification

---

REPORTING FLOW

1. User reports content
2. Content enters moderation queue
3. Moderator reviews:
   - Content
   - Context
   - Prior actions
4. Moderator acts
5. Action recorded in audit log

---

APPEALS

- No formal appeal system
- Users may contact moderators
- Decisions are final unless escalated by moderators

Rationale:
- Keep moderation lightweight
- Avoid bureaucratic overhead

---

AUDIT LOG

All moderation actions must record:
- Moderator ID
- Target (post / discussion / user)
- Action taken
- Timestamp
- Optional note

Audit log is immutable.

---

NON-GOALS

- Shadow banning
- Silent content ranking
- Algorithmic moderation
- Automated punishments
