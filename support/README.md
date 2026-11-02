# Flitflap Staff Support Application (`support.flitflap.com`)

The diagnostic, ticketing, and compliance console for **Flitflap Email Host** (`net9.0`), implementing the specifications in `doc/doc.md` (Section 2.A.2).

---

## Architecture & Project Structure

```
support/
├── Data/
│   ├── FlitflapMasterDbContext.cs     # Global Master DB (tenants, domains, staff audit logs)
│   └── FlitflapTenantDbContext.cs     # Tenant Sharded DB (mailboxes, message indexes, traces, tickets)
├── Endpoints/
│   └── SupportEndpoints.cs            # REST APIs for Mail Tracer, DNS diagnostics, tickets, and quota headroom
├── Entities/
│   ├── MasterEntities.cs              # Schema models (Tenant, DomainRegistry, StaffAuditLog)
│   └── TenantEntities.cs              # Schema models (TenantMailbox, MessageIndex, DeliveryTraceLog, SupportTicket)
├── Services/
│   ├── IMailTracerDiagnosticService.cs # 4-hop delivery timeline and telemetry inspection
│   ├── ISupportTicketService.cs       # Ticketing queue, status resolution, and quota headroom adjustments
│   └── ITenantDiagnosticService.cs    # Live public DNS verification and assisted configuration instructions
├── wwwroot/
│   ├── css/support.css                # Support desk glassmorphism dark theme
│   ├── js/support.js                  # React 18 support console SPA
│   └── index.html                     # Web console entry point
├── appsettings.json                   # Dual-tier PostgreSQL / SQLite and SMTP engine tracer config
├── Program.cs                         # ASP.NET Core 9 WebApplication listening on port 5003
├── .gitignore
├── README.md
└── support.csproj                     # .NET 9 project file
```

---

## Key Capabilities Implemented (`doc/doc.md` Section 2.A.2)

1. **Delivery Diagnostics (Mail Tracer)**:
   - Search delivery events by `Message-ID`, sender, or recipient.
   - 4-Stage Visual Hop Timeline:
     - **Hop 1: Inbound Reception**: Client IP, TLS 1.3 handshake, MTA submission port.
     - **Hop 2: Security & Authentication Check**: Real-time SPF, DKIM crypto, DMARC alignment, spam score, and ClamAV status.
     - **Hop 3: Storage Matrix Hand-off**: Garage S3 object storage pointer (`flitflap-mail-store`) and DB index.
     - **Hop 4: Final Mailbox Delivery**: Status confirmation (`Delivered`, `Filtered`, `Deferred`, `Bounced`).

2. **Tenant Lookup & Assisted DNS Repair**:
   - Inspects tenant domains and queries live public DNS resolvers (Google Public DNS, Cloudflare).
   - Validates **MX**, **SPF**, **DKIM**, and **DMARC** records with granular PASS/FAIL pills.
   - Automatically generates copy-paste setup instructions tailored for **Cloudflare**, **GoDaddy**, **Route 53**, and **Namecheap**.

3. **Quota & Support Ticket Management**:
   - Ticket queue with priority triage (`Urgent`, `High`, `Medium`, `Low`) and categories (`DNS / Deliverability`, `Mailbox Quota`, `Authentication`, `Bounced Mail`).
   - Resolution documentation workflow.
   - **Emergency Quota Headroom Tool**: Instant lookup of mailbox usage with one-click **+2 GB** or **+5 GB** temporary headroom grant.

4. **Strict Staff Audit Trail**:
   - Every support engineer action (viewing a mail trace, adjusting quota, running diagnostics, resolving a ticket) is immutably logged with the operator's email and timestamp.

---

## How to Run

From this directory:
```bash
dotnet run
```

Or from the workspace root:
```bash
dotnet run --project support
```

Open **`http://localhost:5003`** in your browser.

