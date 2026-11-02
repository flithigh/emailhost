# Flitflap Email Host - Core SMTP MTA Engine

High-performance, asynchronous multi-tenant SMTP Mail Transfer Agent and delivery engine built for the **Flitflap Email Host** platform (`net9.0`), implementing the complete enterprise specifications defined in `doc/doc.md` (Section 2.A.3 & Section 3).

---

## Architecture & Project Structure

```
smtp/
├── Data/
│   ├── FlitflapMasterDbContext.cs # EF Core context for Global Master DB (Tenants, DomainsRegistry, IpPools)
│   └── FlitflapTenantDbContext.cs # EF Core context for Tenant Sharded DB (Mailboxes, DKIM keys, MessageIndex, Traces)
├── Entities/
│   ├── MasterEntities.cs         # Master tier models (Tenant, DomainRegistry, IpPool, StaffAuditLog)
│   └── TenantEntities.cs         # Tenant tier models (TenantMailbox, DkimKey, MessageIndex, DeliveryTraceLog)
├── Handlers/
│   └── SmtpSessionHandler.cs     # Asynchronous SMTP protocol state machine (EHLO, AUTH, STARTTLS, MAIL, RCPT, DATA)
├── Listeners/
│   └── SmtpListenerService.cs    # BackgroundService managing concurrent listeners on Ports 25, 587, 465
├── Security/
│   ├── AntiSpamFilter.cs         # Heuristic spam scoring, keyword analysis, SPF reputation verdict
│   ├── AntiVirusScanner.cs       # Dangerous attachment scanner + ClamAV TCP daemon (INSTREAM port 3310)
│   ├── BCryptPasswordHasher.cs   # SASL AUTH LOGIN / PLAIN credential verification
│   ├── DkimService.cs            # 2048-bit RSA DKIM signing (outbound) & public key verification (inbound)
│   ├── DmarcEvaluator.cs         # DMARC policy evaluator (reject, quarantine, none) & SPF/DKIM alignment
│   ├── SpooferDetector.cs        # Unauthenticated internal domain forgery protection & SPF lookup
│   └── TlsCertificateManager.cs   # Self-signed/custom X.509 TLS certificate generator & manager
├── Services/
│   ├── MessageRoutingService.cs  # Inbound pipeline (SPF/DKIM/DMARC/AntiSpam/AV/Garage) & outbound MX relay
│   └── QuotaManager.cs           # Mailbox quota ceiling validation (5 GB / 20 GB)
├── Storage/
│   └── GarageStorageService.cs   # Garage S3 Object Storage (flitflap-mail-store) + local filesystem fallback
├── appsettings.json              # Dual DBs, Garage S3, ClamAV, and port configurations
├── Program.cs                    # ASP.NET Core 9 WebApplication, background listeners, and Mail Tracer REST APIs
├── .gitignore
├── README.md
└── smtp.csproj                   # Project file (net9.0, AWSSDK.S3, MimeKit, Npgsql, EF Core)
```

---

## Key Features Implemented (`doc/doc.md`)

### 1. Hybrid Storage Architecture (PostgreSQL + Garage S3)
- **PostgreSQL**: Stores relational metadata, tenant users, domain records, message index pointers, and delivery trace logs.
- **Garage S3 (`flitflap-mail-store`)**: Stores raw `.eml` MIME bodies and attachments using the standard key schema:
  `{tenant_uuid}/{domain_name}/{mailbox_uuid}/{year}/{month}/{message_uuid}.eml`
- **Zero-Dependency Fallback**: If PostgreSQL or Garage S3 are not running locally, the engine automatically operates on local SQLite (`flitflap_master.db`, `flitflap_tenant.db`) and local filesystem emulation (`storage/garage_fallback/`).

### 2. Multi-Port SMTP Daemon
- **Port 25**: Standard inbound MTA mail exchange.
- **Port 587**: Message submission port with `STARTTLS` negotiation.
- **Port 465**: `SMTPS` with implicit SSL/TLS encryption.

### 3. Comprehensive Email Security Pipeline
- **SPF Verification**: Queries DNS SPF records and evaluates sender IP authorization.
- **2048-bit RSA DKIM**:
  - Outbound: Cryptographically signs emails using tenant's 2048-bit RSA private key via MimeKit `DkimSigner`.
  - Inbound: Validates DKIM signatures against the sender's public DNS TXT record.
- **DMARC Policy Enforcement**: Evaluates SPF/DKIM alignment and applies the domain's policy (`reject`, `quarantine`, `none`).
- **Anti-Spam Scoring**: Heuristic content analysis with scoring ($\ge 5.0$ moved to Spam, $\ge 9.0$ rejected with 554).
- **Anti-Virus & ClamAV**: Scans attachments for malicious extensions and connects to local ClamAV daemon (`INSTREAM` protocol on port 3310).
- **Anti-Spoofing**: Blocks unauthenticated external connections attempting to send from hosted domains.

### 4. Mail Tracer & Observability APIs
- `GET /api/tracer/{messageId}`: End-to-end diagnostic trace returning hop timings, SPF/DKIM/DMARC results, spam scores, and Garage S3 keys.
- `GET /api/status`: Real-time MTA daemon metrics and active listeners.
- `GET /api/storage/health`: Dual-tier PostgreSQL and Garage S3 cluster health check.
- `GET /api/tenants`: Active tenants and subscription tiers.
- `GET /api/domains`: Master domain registry.
- `GET /api/mailboxes`: Mailboxes and quota utilization.

---

## How to Run

From this directory:
```bash
dotnet run
```

Or from the workspace root:
```bash
dotnet run --project smtp
```

To run both `smtp` and `console` simultaneously, launch them in separate terminal windows.
