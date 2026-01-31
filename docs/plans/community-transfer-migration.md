# Community Transfer & Migration System

## Overview

Allow communities to migrate between Snakk installations, enabling users to:
- Move from self-hosted to shared multi-community instances
- Move from shared instances to self-hosted
- Transfer communities between different Snakk installations

This enables community portability and prevents vendor lock-in.

**Key Principle:** Content migrates immediately. User identity follows through opt-in claims.

## Use Cases

1. **Self-hosted → Multi-community**: User starts with single-community installation, later wants to join a platform with other communities
2. **Shared → Self-hosted**: User wants independence and moves their community to their own server
3. **Platform migration**: Community wants to move to a different Snakk host

## Architecture Overview

### Direct Server-to-Server Transfer
**API-to-API communication** (not file-based export/import)

**Benefits:**
- No file storage/management
- Real-time progress tracking
- Incremental validation
- Resume capability
- No disk space constraints

**Limitations:**
- Both servers must be online during transfer
- Source must stay online for 90-day claim window

### Source-Mediated Identity Claims
**GDPR-compliant approach** that transfers content without transferring personal data

**Flow:**
1. Content transfers anonymously (no PII to target)
2. Source maintains user→stub mapping privately
3. Users actively claim their identity on target
4. Consent demonstrated through account creation

## GDPR Compliance Strategy

### Privacy Policy Foundation

Snakk's privacy policy includes this clause:

```
DATA TRANSFERS AND COMMUNITY MIGRATIONS

If this community migrates to another platform or hosting
provider, your public content (posts, discussions) may be
transferred to maintain community continuity.

User data (email, profile information) is NOT transferred
without your explicit action.

During migration:
- Your posts transfer with anonymous attribution
- You choose whether to claim ownership on the new platform
- If you claim, you create an account and consent to the
  new platform's privacy policy
- If you don't claim, your posts stay permanently anonymous

You will be notified when migration occurs.
```

### Legal Basis

**For content transfer:** Legitimate interest (GDPR Article 6(1)(f))
- Community continuity is legitimate business interest
- Content was published publicly and voluntarily
- Transfers in same context (community discussions)
- Privacy-preserving (anonymous by default)

**For identity transfer:** Explicit consent (GDPR Article 6(1)(a))
- User actively chooses to claim posts
- Creates account on target = consent to new data controller
- Agrees to target's privacy policy during claim

### What Gets Transferred

**WITHOUT user consent (immediately):**
- ✅ Post content (text)
- ✅ Discussion structure
- ✅ Timestamps
- ✅ Public metadata
- ✅ Reactions (anonymized)

**ONLY with user consent (via claim):**
- ❌ Usernames
- ❌ Email addresses
- ❌ Profile information
- ❌ Private data
- ❌ Account credentials

### Key Compliance Points

1. **No PII transferred to target initially** - Only anonymous content
2. **User actively opts in to identity transfer** - Not automatic
3. **Clear consent demonstrated** - Account creation on target
4. **Default is privacy-preserving** - Anonymous unless claimed
5. **Users control their data** - Can stay anonymous forever

## Transfer Flow

### Phase 1: Setup (Manual)

**On Target Instance:**
1. Admin creates empty community (must be completely empty)
2. Community automatically created with `OwnerOnly = true` (only creator can see it)
3. Admin clicks "Accept Transfer" button
4. System generates one-time transfer auth key
5. Community marked as `IsAwaitingTransfer = true`
6. Community excluded from public queries while awaiting

**On Source Instance:**
1. Admin navigates to community settings
2. Clicks "Transfer Community"
3. Enters target URL and transfer auth key
4. System validates connection to target

### Phase 2: Pre-flight Checks

Both systems communicate to verify compatibility:

```
Preflight Checks:
- Schema compatibility (compare table structures)
- Target community is empty (!HasContent)
- Auth token is valid
- Transfer auth is active and not expired
- Required tables exist on both sides
- Disk space estimation on target
- Snakk version compatibility
- Network bandwidth check
```

**Schema Compatibility:**
- Export includes schema definition for required tables
- Target introspects its own schema
- All export columns must exist in target schema
- Extra columns on target are OK (will use defaults/nulls)
- Missing columns on target = incompatible, reject transfer
- Type mismatches = incompatible, reject transfer

If incompatible:
```
"Cannot transfer: Target schema is missing required columns:
- Users.AuthProvider (added in v1.2)
Please upgrade target instance to latest version."
```

### Phase 3: Anonymous Content Transfer

**Source locks community as read-only during transfer**

**Transfer happens in batches:**

```
Transfer Phases (in order):
1. Users → Anonymous stubs (no emails, no real usernames)
2. Community structure (hubs, spaces, roles, permissions)
3. Discussions (metadata, anonymous author stubs)
4. Posts (content with anonymous authors)
5. Reactions (linked to anonymous stubs)
6. Follows (linked to anonymous stubs)
7. Files/Attachments (avatars, uploads - if configured)

Each batch:
- Source sends records
- Target validates and inserts
- Target responds success/failure
- Source checkpoints progress
- Repeat until complete
```

**What target receives:**
```json
{
  "users": [
    {
      "stubId": "stub_a7f3k2",
      "displayName": "Community Member",
      "type": "ExternalStub",
      "sourceInstance": "community.example.com"
    }
  ],
  "posts": [
    {
      "content": "I recommend the HyperX Cloud II",
      "authorStubId": "stub_a7f3k2",
      "createdAt": "2026-01-15T10:30:00Z"
    }
  ]
}
```

**No personal data transferred.** Target cannot identify who owns which stub.

**Progress Tracking:**
```json
{
  "transferId": "abc123",
  "status": "in_progress",
  "progress": {
    "users": { "total": 1000, "transferred": 1000 },
    "discussions": { "total": 5000, "transferred": 2341 },
    "posts": { "total": 50000, "transferred": 12450 }
  }
}
```

### Phase 4: Post-flight Validation

Sanity checks after transfer completes:

```
Postflight Checks:
- Record counts match (users, discussions, posts, reactions)
- No orphaned references (all authorIds exist in users)
- Relationship integrity (follows/reactions point to valid entities)
- Timestamps preserved correctly
- File references valid (if files were transferred)
- Foreign key constraints satisfied
```

If validation fails:
- Option: Auto-rollback entire transfer
- Option: Mark specific issues, allow manual review
- Option: Complete with warnings, admin can fix

### Phase 5: Source Server Post-Migration UX

**State transitions:**
- Source: `Normal` → `InitiatingTransfer` → `Transferring` → `TransferredAway`
- Target: `AwaitingTransfer` → `ReceivingTransfer` → `TransferComplete` → `Active`

#### A. Persistent Banner for Logged-in Users

Shows on every page until user makes a choice:

```
┌─────────────────────────────────────────────────────────┐
│ ⚠️  Your Community Has Moved                            │
│                                                          │
│ [Community Name] is now at target.com                   │
│ Your 50 posts are there, currently shown anonymously.   │
│                                                          │
│ [Claim My Posts on New Site]  [Keep Anonymous]          │
│                                                          │
│ Learn more about the migration →                        │
└─────────────────────────────────────────────────────────┘
```

#### B. Old Discussion URLs (No Automatic Redirect)

When users visit old URLs on source:

```html
<!-- community.example.com/discussions/gaming-headsets-123 -->

┌─────────────────────────────────────────────────────────┐
│ 🚚 This Discussion Has Moved                            │
│                                                          │
│ "Best Gaming Headsets"                                  │
│ Started Jan 15, 2026 • 45 replies                      │
│                                                          │
│ This community is now hosted at target.com              │
│                                                          │
│ By visiting the new site, you'll be subject to their    │
│ privacy policy: target.com/privacy                      │
│                                                          │
│ [View Discussion on New Site]                           │
│                                                          │
│ ─────────────────────────────────────────────────────   │
│ 👤 You're logged in!                                    │
│ You have posts in this community that are currently     │
│ shown anonymously on the new site.                      │
│                                                          │
│ [Claim Your Posts]                                      │
└─────────────────────────────────────────────────────────┘
```

**No automatic redirect.** User consciously chooses to visit target.

**GDPR compliance:** User's visit to target is their choice, not forced by redirect.

### Phase 6: User Claims Identity

#### Option A: User Clicks "Claim My Posts"

**1. Source shows claim page:**
```
┌──────────────────────────────────────────────────────────┐
│ Claim Your Posts on target.com                           │
│                                                           │
│ You have 50 posts waiting to be claimed.                 │
│                                                           │
│ By claiming, you agree to:                               │
│ • Transfer your account data to target.com               │
│ • Your posts will show your username                     │
│ • Target.com becomes your data controller                │
│ • Target.com's privacy policy applies                    │
│                                                           │
│ Privacy Policy: Read target.com/privacy                  │
│                                                           │
│ [ ] I understand and agree to the above                  │
│                                                           │
│ [Claim My Posts]  [Keep Anonymous]                       │
└──────────────────────────────────────────────────────────┘
```

**2. User consents and clicks "Claim My Posts"**

**3. Source generates one-time token**
```csharp
var token = TokenGenerator.GenerateSecure();
// Expires in 24 hours
```

**4. Source calls target API:**
```csharp
POST target.com/api/claim/register-token
{
  "token": "xyz789",
  "stubId": "stub_a7f3k2",
  "expiresAt": "2026-01-31T10:30:00Z",
  "sourceCommunity": "community.example.com"
}
```

**5. Source redirects user to:**
```
target.com/claim?token=xyz789
```

**6. Target shows account creation page:**
```
┌──────────────────────────────────────────────────────────┐
│ Create Your Account                                       │
│                                                           │
│ You're claiming 50 posts from community.example.com      │
│                                                           │
│ Username: [alice]                                         │
│ Email: [alice@example.com]                                │
│ Password: [********]                                      │
│                                                           │
│ [ ] I agree to Terms of Service                          │
│ [ ] I agree to Privacy Policy                            │
│                                                           │
│ [Create Account & Claim Posts]                           │
└──────────────────────────────────────────────────────────┘
```

**7. Target processes claim:**
```csharp
- Create user account
- Link stubId → new userId
- Update all posts: authorStubId → authorUserId
- Posts now show real username
- Sign user in
```

**8. User sees their posts with proper attribution**

#### Option B: User Clicks "Keep Anonymous"

**1. Source records decision:**
```csharp
UPDATE TransferUserMappings
SET ClaimDecision = 'anonymous',
    DecisionMadeAt = NOW()
WHERE UserId = 'user_123'
```

**2. Source shows confirmation:**
```
┌──────────────────────────────────────────────────────────┐
│ Your Posts Will Stay Anonymous                           │
│                                                           │
│ Your posts on target.com will continue to be shown       │
│ as "Community Member".                                   │
│                                                           │
│ You can change this decision within 90 days by           │
│ logging in and choosing "Claim My Posts".                │
│                                                           │
│ [Visit New Community]  [Done]                            │
└──────────────────────────────────────────────────────────┘
```

**3. Banner disappears**

#### Option C: User Ignores (No Action)

**Default behavior:**
- Posts stay anonymous on target
- Banner keeps showing on source
- Can claim anytime within 90 days
- After 90 days, permanently anonymous

### Phase 7: Timeline & Notifications

```
T+0:     Migration complete
         - Source shows banners for all users
         - Target community visible to owner only

T+1:     Optional: Single notification email
         "Your community moved. Claim your posts if desired."

T+7:     Reminder email to unclaimed users
         "You have 83 days left to claim your posts"

T+30:    Another reminder

T+60:    Final reminder: "30 days left to claim"

T+90:    Claim window closes
         - Source can safely shut down OR
         - Source stays as read-only archive
         - Unclaimed users stay anonymous forever on target

T+90:    Owner can remove OwnerOnly flag on target
         - Community becomes publicly visible
```

**Email notification (optional, sent once):**
```
Subject: Your community has moved to [target]

Hi [Username],

Your community has migrated to target.com.

Your 50 posts are there, currently shown anonymously
to protect your privacy.

If you want to reclaim your posts and continue
participating with your identity:

→ Visit community.example.com and click "Claim My Posts"

Otherwise, no action needed - your posts will stay
anonymous and you can still view/participate on the
new site without an account.

You have 90 days to decide.

Questions? Reply to this email.
```

## User Identity & Username Collisions

### The Challenge
Users on source may not exist on target, or usernames may conflict.

### Solution: All Users Start as Anonymous Stubs

**During Transfer:**
- Every user becomes an anonymous stub on target
- No username conflicts (all are "Community Member")
- No collision detection needed initially

**During Claim:**
- User creates NEW account on target
- Chooses new username (may differ from source)
- Target validates username availability
- If taken, user chooses different one

**Result:**
- No forced username matching
- No collision issues
- Users can rebrand if desired
- Or keep same username if available

**Example:**
```
Source: Alice (@alice)
Target stub: Community Member (stub_a7f3k2)
Claim: Alice chooses @alice_gaming (original taken)
Result: Posts now show @alice_gaming
```

## State Machine

### Source Community States

| State | Description | Visible? | Writable? | Users Can Claim? |
|-------|-------------|----------|-----------|------------------|
| `Normal` | Active community | Yes | Yes | N/A |
| `InitiatingTransfer` | Pre-flight checks running | Yes | Yes | No |
| `Transferring` | Transfer in progress | Yes | No (read-only) | No |
| `TransferredAway` | Transfer complete | Yes (with banners) | No | Yes (90 days) |
| `Archived` | After claim window | Yes (read-only) | No | No |

### Target Community States

| State | Description | Visible? | Can Accept Transfer? |
|-------|-------------|----------|---------------------|
| `Normal` | Regular community | Yes | No |
| `AwaitingTransfer` | Empty, waiting | Owner only | Yes |
| `ReceivingTransfer` | Receiving data | Owner only | No |
| `TransferComplete` | Done, under review | Owner only | No |
| `Active` (OwnerOnly=false) | Public | Yes | No |

### State Timeouts

- `AwaitingTransfer` expires after 24 hours if no transfer initiated
- `ReceivingTransfer` can be manually cancelled/rolled back
- `TransferredAway` allows claims for 90 days
- After 90 days, source can shut down or stay as archive

## Query Exclusions

Communities in transfer states must be excluded from public queries:

**Source (after transfer):**
```csharp
// Community list pages
.Where(c => !c.IsTransferredAway)

// Direct access with banner
if (community.IsTransferredAway && user.IsAuthenticated)
{
    ShowClaimBanner(user);
}
```

**Target (during transfer):**
```csharp
// Hide from discovery until complete and public
.Where(c => !c.IsAwaitingTransfer
         && !c.IsReceivingTransfer
         && (!c.OwnerOnly || c.OwnerId == currentUserId))

// Owner can always view and monitor progress
if (IsOwnerOrAdmin)
{
    ShowProgressBar(transferStatus);
}
```

## API Endpoints

### Source Installation

```
POST /api/admin/communities/{id}/transfer/initiate
Body: { targetUrl, authToken }
Response: { transferId, status }

GET /api/admin/communities/{id}/transfer/status
Response: { progress, errors, canResume }

POST /api/admin/communities/{id}/transfer/resume
Body: { transferId }

POST /api/admin/communities/{id}/transfer/cancel

POST /api/transfer/claim/generate-token
Response: { token, claimUrl, expiresAt }

POST /api/transfer/claim/keep-anonymous
Body: { userId }
```

### Target Installation

```
POST /api/admin/communities/{id}/transfer/prepare
Response: { authToken, expiresAt }

POST /api/transfer/initiate
Body: { communityId, authToken, schema, estimatedRecords }
Response: { transferId, compatible, errors? }

POST /api/transfer/{id}/users
Body: [ { stubId, displayName: "Community Member", type: "ExternalStub" } ]

POST /api/transfer/{id}/discussions
Body: [ { publicId, title, authorStubId, ... } ]

POST /api/transfer/{id}/posts
Body: [ { publicId, content, authorStubId, ... } ]

POST /api/transfer/{id}/complete
Response: { success, validationErrors? }

POST /api/transfer/{id}/rollback

GET /api/transfer/{id}/status
Response: { progress, phase, errors }

POST /api/claim/register-token
Body: { token, stubId, expiresAt, sourceCommunity }
Response: { success }

GET /api/claim?token={token}
Response: Claim page HTML

POST /api/claim
Body: { token, username, email, password, agreeToTerms }
Response: { success, userId, redirect }
```

## Database Schema Changes

### Community Table

```sql
ALTER TABLE Communities ADD COLUMN IsAwaitingTransfer BOOLEAN DEFAULT FALSE;
ALTER TABLE Communities ADD COLUMN IsReceivingTransfer BOOLEAN DEFAULT FALSE;
ALTER TABLE Communities ADD COLUMN IsTransferredAway BOOLEAN DEFAULT FALSE;
ALTER TABLE Communities ADD COLUMN TransferSourceUrl VARCHAR(500) NULL;
ALTER TABLE Communities ADD COLUMN TransferTargetUrl VARCHAR(500) NULL;
ALTER TABLE Communities ADD COLUMN TransferredAt TIMESTAMP NULL;
```

### User Table

```sql
ALTER TABLE Users ADD COLUMN UserType VARCHAR(20) DEFAULT 'Local';
-- Values: 'Local', 'ExternalStub', 'Claimed'

ALTER TABLE Users ADD COLUMN SourceInstance VARCHAR(500) NULL;
-- For stubs: where they came from

ALTER TABLE Users ADD COLUMN StubId VARCHAR(50) NULL;
-- For stubs: their anonymous identifier
```

### New Tables

```sql
CREATE TABLE TransferSessions (
    Id VARCHAR(50) PRIMARY KEY,
    CommunityId VARCHAR(50) NOT NULL,
    SourceUrl VARCHAR(500) NOT NULL,
    TargetUrl VARCHAR(500) NOT NULL,
    Status VARCHAR(50) NOT NULL, -- Initiated, InProgress, Complete, Failed
    Phase VARCHAR(50), -- Users, Discussions, Posts, etc.
    Progress JSONB, -- Detailed progress per phase
    AuthToken VARCHAR(100) NOT NULL,
    ExpiresAt TIMESTAMP NOT NULL,
    StartedAt TIMESTAMP NOT NULL,
    CompletedAt TIMESTAMP NULL,
    ErrorLog JSONB NULL,
    FOREIGN KEY (CommunityId) REFERENCES Communities(PublicId)
);

CREATE TABLE TransferCheckpoints (
    Id SERIAL PRIMARY KEY,
    TransferSessionId VARCHAR(50) NOT NULL,
    Phase VARCHAR(50) NOT NULL,
    LastProcessedId VARCHAR(50) NOT NULL,
    Offset INT NOT NULL,
    CreatedAt TIMESTAMP NOT NULL,
    FOREIGN KEY (TransferSessionId) REFERENCES TransferSessions(Id)
);

CREATE TABLE TransferUserMappings (
    Id SERIAL PRIMARY KEY,
    TransferSessionId VARCHAR(50) NOT NULL,
    SourceUserId VARCHAR(50) NOT NULL,
    TargetStubId VARCHAR(50) NOT NULL,
    ClaimDecision VARCHAR(20) DEFAULT 'pending', -- pending, claimed, anonymous, ignored
    DecisionMadeAt TIMESTAMP NULL,
    ClaimTokenGenerated VARCHAR(100) NULL,
    ClaimedAt TIMESTAMP NULL,
    ClaimedAsUserId VARCHAR(50) NULL,
    FOREIGN KEY (TransferSessionId) REFERENCES TransferSessions(Id)
);

CREATE TABLE ClaimTokens (
    Token VARCHAR(100) PRIMARY KEY,
    StubId VARCHAR(50) NOT NULL,
    SourceCommunity VARCHAR(500),
    ExpiresAt TIMESTAMP NOT NULL,
    Claimed BOOLEAN DEFAULT FALSE,
    ClaimedByUserId VARCHAR(50),
    ClaimedAt TIMESTAMP,
    FOREIGN KEY (StubId) REFERENCES Users(PublicId)
);
```

## Edge Cases & Considerations

### 1. Transfer Failure Mid-Flight
**Scenario**: Network drops during post transfer

**Solution**:
- All changes wrapped in transaction per batch
- Checkpoint after each successful batch
- Resume from last checkpoint OR full rollback option

### 2. Source Goes Offline During Transfer
**Scenario**: Source server crashes mid-transfer

**Solution**:
- Target has partial data
- Transfer session expires after 24 hours
- Target auto-rolls back expired incomplete transfers
- Source can resume when back online (from checkpoint)

### 3. Source Goes Offline During Claim Window
**Scenario**: Source shuts down before 90 days

**Problem**: Users can't generate claim tokens

**Solution**:
- Document source MUST stay online for 90 days
- OR send all claim links via email before shutdown
- OR export claim mapping to target (with user consent?)

### 4. File Storage (Avatars, Attachments)
**Scenario**: Images and uploads need to move

**Approach**:
- Transfer files during migration, store in target's storage system
- Update URLs to point to target
- Files tied to anonymous stubs, become user's when claimed

### 5. Large Communities
**Scenario**: Community with 100k+ posts takes hours to transfer

**Solution**:
- Show realistic time estimates
- Chunked batching with rate limiting
- Progress bar with ETA
- Email notification when complete

### 6. Multiple Simultaneous Transfers
**Question**: Can one installation transfer multiple communities at once?

**Recommendation**:
- Limit to 1 outgoing + 1 incoming transfer per installation
- Prevent resource exhaustion
- Queue additional transfers

### 7. URL Slug Conflicts
**Current**: Requires identical slug

**Future Enhancement**: Allow slug remapping
```
Source: /gaming
Target: /gaming-community

All internal links rewritten during transfer
```

### 8. Cross-Instance Follows
**Scenario**: Alice followed Bob, Bob only existed in different community

**Solution**:
- Only transfer follows where target user exists in same community
- Drop cross-community follows (instance-specific)

### 9. Cached Pages / Archive.org

**The Correlation Risk:**
```
Before migration (cached publicly):
community.example.com/discussions/gaming-123
Author: Alice

After migration:
target.com/discussions/gaming-123
Author: Community Member (stub_xyz)

Third party can correlate: Same URL + content = Alice = stub_xyz
```

**Legal Analysis:**
- This is pseudonymization, not anonymization
- You're not disclosing the correlation yourself
- Third-party caching is outside your control
- The data was already public

**Not a GDPR breach IF:**
- You don't claim true anonymization
- You call it pseudonymization in disclosures
- You're transparent about the limitation
- You offer deletion for complete removal

**Disclosure language:**
```
"Your posts will be transferred with pseudonymous attribution.
Because your posts were previously public and may have been
cached by search engines, complete anonymization cannot be
guaranteed. For complete removal, choose 'Delete my data'."
```

### 10. User Wants Complete Deletion

**Scenario**: User doesn't want posts on target at all

**Solution**: Must happen BEFORE transfer
```
1. User requests deletion on source (before T-0)
2. Source removes from transfer batch
3. Posts not transferred to target
4. Or: Posts transferred but marked for deletion
5. Target deletes on completion
```

After transfer, deletion request goes to target (new data controller).

## Security Considerations

### Authentication
- One-time transfer tokens (expire after 24h)
- Mutual authentication: Source validates target, target validates source
- Transfer tokens scoped to specific community

### Authorization
- Only community owners can initiate transfers
- Target must explicitly accept (AwaitingTransfer state)
- Claim tokens single-use, time-limited (24h)

### Data Validation
- Sanitize all incoming data
- Validate foreign key relationships
- Check for injection attacks in transferred content
- Malware scan uploaded files

### Audit Trail
```sql
CREATE TABLE TransferAuditLog (
    Id SERIAL PRIMARY KEY,
    TransferSessionId VARCHAR(50),
    Action VARCHAR(100),
    UserId VARCHAR(50),
    Details JSONB,
    IpAddress VARCHAR(50),
    CreatedAt TIMESTAMP
);
```

Log all actions:
- Transfer initiated
- Batch transferred
- Errors occurred
- Claim tokens generated
- Posts claimed
- Decisions made

## Implementation Checklist

### Core Transfer System
- [ ] Database schema updates (communities, transfer sessions, checkpoints)
- [ ] User type system (Local, ExternalStub, Claimed)
- [ ] Schema introspection and compatibility checker
- [ ] Source: Transfer initiation API
- [ ] Target: Transfer receiving API
- [ ] Batch transfer logic (users, discussions, posts)
- [ ] Progress tracking and checkpointing
- [ ] Resume capability
- [ ] Rollback mechanism
- [ ] Post-flight validation

### User Experience
- [ ] Source: Persistent claim banner for logged-in users
- [ ] Source: Migration notice page (no auto-redirect)
- [ ] Source: Claim flow UI
- [ ] Source: "Keep anonymous" option
- [ ] Target: Claim landing page
- [ ] Target: Account creation with claim

### Identity System
- [ ] Source: User→stub mapping storage
- [ ] Source: Claim token generation
- [ ] Target: Claim token registration API
- [ ] Target: Claim token validation
- [ ] Target: Post re-attribution on claim
- [ ] Username availability check on claim

### Query Updates
- [ ] Exclude IsAwaitingTransfer from public queries
- [ ] Exclude IsTransferredAway from discovery
- [ ] Show banners for transferred communities
- [ ] Allow owner access during transfer states

### Admin Features
- [ ] Admin UI: Initiate transfer on source
- [ ] Admin UI: Accept transfer on target
- [ ] Admin UI: Progress monitoring on both ends
- [ ] Admin UI: Rollback controls
- [ ] Admin UI: Claim statistics
- [ ] Email notifications (transfer complete, claim reminders)

### Privacy & Compliance
- [ ] Privacy policy updates (migration clause)
- [ ] Legitimate interest assessment documentation
- [ ] Consent language for claims
- [ ] Audit logging
- [ ] Data export capability on target
- [ ] Deletion request handling on target

### Testing
- [ ] Unit tests for transfer logic
- [ ] Integration tests for source-target communication
- [ ] End-to-end tests with realistic data volumes
- [ ] Schema compatibility validation tests
- [ ] Claim flow tests
- [ ] Rollback tests
- [ ] Performance tests (large communities)

### Documentation
- [ ] Admin guide: How to initiate transfer
- [ ] Admin guide: How to accept transfer
- [ ] User guide: How to claim posts
- [ ] Privacy policy updates
- [ ] API documentation

## Related Documentation

- See `PROJECT-STRUCTURE.MD` for codebase organization
- See `MODERATION.MD` for handling moderation data during transfer
- See `REALTIME.MD` for SignalR considerations during transfer
- See `GDPR.MD` for broader privacy compliance

## Discussion Notes

This design emerged from extensive discussion on 2026-01-30 exploring community portability between Snakk instances.

**Key Insights:**
1. **Source-mediated claims** elegantly solve GDPR by keeping PII on source until user actively claims
2. **No automatic redirects** prevent unintentional data sharing with new controller
3. **Anonymous by default** respects privacy while preserving community integrity
4. **Active consent through account creation** demonstrates clear user choice
5. **Public data stays public** but identity transfer is opt-in

**Legal Foundation:**
- Legitimate interest for content transfer (community continuity)
- Explicit consent for identity transfer (account creation)
- Privacy policy covers migration scenario
- Users retain all rights (deletion, export, objection)

**The approach balances:**
- Community continuity (all content transfers)
- User privacy (no PII without consent)
- GDPR compliance (lawful basis for processing)
- Practical usability (no pre-consent required)
