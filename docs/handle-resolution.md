# Handle Resolution in the AT Protocol

This document explains the three-step process of resolving a human-readable handle to its underlying identity data in the AT Protocol (atproto).

---

## Overview

The AT Protocol uses a layered identity system:

| Step | Input | Output | Purpose |
|------|-------|--------|---------|
| 1 | Handle (e.g., `alice.bsky.social`) | DID (e.g., `did:plc:abc123...`) | Map human-readable name to persistent identifier |
| 2 | DID | DID Document | Retrieve cryptographic keys and service endpoints |
| 3 | DID Document | PDS, Handle, Public Key | Extract actionable identity information |

---

## Step 1: Handle → DID

A **handle** is a human-readable identifier like `threddyrex.org` or `alice.bsky.social`. The first step converts this to a **DID** (Decentralized Identifier), which is the permanent, cryptographic identifier for the account.

### Resolution Methods

There are **two** ways to resolve a handle to a DID:

#### 1A. DNS TXT Record

Query for a TXT record at `_atproto.{handle}`:

```
DNS TXT lookup: _atproto.threddyrex.org
Result: "did=did:web:threddyrex.org"
```

The TXT record contains `did={DID}`. This method allows users with their own domains to control their handle without hosting any HTTP infrastructure—just add a DNS record.

**Example using DNS-over-HTTPS:**
```
GET https://cloudflare-dns.com/dns-query?name=_atproto.threddyrex.org&type=TXT
Accept: application/dns-json
```

#### 1B. HTTP Well-Known Endpoint

Request the DID from `https://{handle}/.well-known/atproto-did`:

```
GET https://threddyrex.org/.well-known/atproto-did
Response: did:web:threddyrex.org
```

The response body is plain text containing just the DID. This method requires the handle domain to serve HTTP content.

### Key Differences Between Methods

| Method | Requires | Best For |
|--------|----------|----------|
| DNS | DNS control only | Custom domains without web hosting |
| HTTP | Web server | Handles where you control the server |

Implementations typically try DNS first, then fall back to HTTP.

> **Shortcut: Bluesky API**
> 
> Rather than performing DNS/HTTP resolution yourself, you can call the Bluesky public API:
> ```
> GET https://public.api.bsky.app/xrpc/com.atproto.identity.resolveHandle?handle=alice.bsky.social
> Response: { "did": "did:plc:abc123..." }
> ```
> This is a convenience endpoint—under the hood, it performs the same DNS and HTTP lookups described above. Useful for quick lookups, but adds a dependency on Bluesky's infrastructure.

---

## Step 2: DID → DID Document

A **DID** is a persistent identifier that never changes, even if the user changes their handle or moves to a different server. The second step resolves the DID to its **DID Document**, which contains identity metadata.

### DID Methods

The AT Protocol primarily uses two DID methods:

#### 2A. did:plc (PLC Directory)

Format: `did:plc:abc123xyz456...`

Resolution: Query the PLC Directory:

```
GET https://plc.directory/did:plc:abc123xyz456
```

**did:plc** is a custom DID method created for atproto. It's managed by a central directory (plc.directory) but designed to be migratable to other trust anchors in the future.

**Characteristics:**
- Centralized resolution (plc.directory)
- Supports key rotation and recovery
- Short, opaque identifier
- Cannot be verified without network access

#### 2B. did:web (Web-Based)

Format: `did:web:example.com`

Resolution: Fetch the DID Document from the domain:

```
GET https://example.com/.well-known/did.json
```

**did:web** is a W3C standard that uses existing web infrastructure.

**Characteristics:**
- Self-hosted resolution
- Domain owner controls identity
- Human-guessable from the DID
- Relies on DNS/TLS security model

### Key Differences Between DID Methods

| Aspect | did:plc | did:web |
|--------|---------|---------|
| Resolution URL | `plc.directory/{did}` | `{domain}/.well-known/did.json` |
| Control | PLC directory operators | Domain owner |
| Key Rotation | Built-in rotation/recovery | Manual document updates |
| Portability | Can migrate between operators | Tied to domain ownership |
| Offline Verification | No | No |

---

## Step 3: DID Document → Identity Data

The **DID Document** is a JSON-LD document containing everything needed to interact with the account.

### Example DID Document

```json
{
  "@context": [
    "https://www.w3.org/ns/did/v1",
    "https://w3id.org/security/multikey/v1"
  ],
  "id": "did:web:threddyrex.org",
  "alsoKnownAs": [
    "at://threddyrex.org"
  ],
  "verificationMethod": [
    {
      "id": "did:web:threddyrex.org#atproto",
      "type": "Multikey",
      "controller": "did:web:threddyrex.org",
      "publicKeyMultibase": "zDnaeb71qrw9t4L6U4LvrUrRbwm9CfdutZdi75NDiQafZBsK5"
    }
  ],
  "service": [
    {
      "id": "#atproto_pds",
      "type": "AtprotoPersonalDataServer",
      "serviceEndpoint": "https://pds04.dnproto.com"
    }
  ]
}
```

### Extracting Identity Information

| Field | Path | Purpose |
|-------|------|---------|
| **Handle** | `alsoKnownAs[0]` → remove `at://` | The declared handle for this identity |
| **Public Key** | `verificationMethod[].publicKeyMultibase` where `id` ends with `#atproto` | Verify signatures on commits |
| **PDS** | `service[].serviceEndpoint` where `type` = `AtprotoPersonalDataServer` | Where the user's data is stored |

### Handle Verification (Bidirectional Check)

To prevent handle takeover, implementations should verify bidirectionally:

1. **Handle → DID**: Resolve the handle to get a DID
2. **DID → Handle**: Look up the DID Document and extract `alsoKnownAs`
3. **Verify**: Confirm they match

If `alice.example.com` resolves to `did:plc:xyz`, but `did:plc:xyz` declares `bob.example.com` in `alsoKnownAs`, the handle is **invalid**.

---

## Summary: Complete Resolution Flow

```
┌─────────────────────────────────────────────────────────────────┐
│  Handle: threddyrex.org                                         │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
        ┌─────────────────────────────────────────────┐
        │ Step 1: Handle → DID                        │
        │   • DNS: _atproto.threddyrex.org TXT       │
        │   • HTTP: /.well-known/atproto-did         │
        │   • API: com.atproto.identity.resolveHandle│
        └─────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  DID: did:web:threddyrex.org                                    │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
        ┌─────────────────────────────────────────────┐
        │ Step 2: DID → DID Document                  │
        │   • did:plc → plc.directory/{did}          │
        │   • did:web → {host}/.well-known/did.json  │
        └─────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│  DID Document (JSON)                                            │
│    • id: did:web:threddyrex.org                                 │
│    • alsoKnownAs: at://threddyrex.org                           │
│    • verificationMethod: [{publicKeyMultibase: "zDnae..."}]    │
│    • service: [{type: "AtprotoPersonalDataServer",             │
│                 serviceEndpoint: "https://pds04.dnproto.com"}] │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
        ┌─────────────────────────────────────────────┐
        │ Step 3: Extract Identity Data               │
        │   • Handle: threddyrex.org                  │
        │   • Public Key: zDnae...                    │
        │   • PDS: pds04.dnproto.com                  │
        └─────────────────────────────────────────────┘
```

---

## References

- [AT Protocol Identity Specification](https://atproto.com/specs/identity)
- [DID Core W3C Specification](https://www.w3.org/TR/did-core/)
- [did:plc Method Specification](https://github.com/did-method-plc/did-method-plc)
