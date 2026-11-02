# Flitflap Promote Application (`flitflap.com`)

The public-facing showcase, marketing, and self-service customer conversion gateway for **Flitflap Email Host** (`net9.0`), implementing the specifications in `doc/doc.md` (Section 2.B.1) and `doc/todo_pro.md`.

---

## Architecture & Project Structure

```
promote/
├── Data/
│   └── FlitflapMasterDbContext.cs     # Global Master DB (tenants, domains, admin users)
├── Endpoints/
│   └── PromoteEndpoints.cs            # REST APIs for pricing, deliverability test, and onboarding
├── Entities/
│   └── MasterEntities.cs              # Schema models (Tenant, DomainRegistry, TenantUser, StaffAuditLog)
├── Security/
│   ├── BCryptPasswordHasher.cs        # Secure admin account password hashing
│   └── IPasswordHasher.cs             # Password hashing abstraction
├── Services/
│   ├── IDeliverabilityTestService.cs  # Live DNS deliverability scanner (MX, SPF, DKIM, DMARC)
│   └── ITenantOnboardingService.cs    # Self-service tenant registration & domain provisioning
├── wwwroot/
│   ├── css/promote.css                # Modern high-conversion landing page theme
│   ├── js/promote.js                  # React 18 interactive showcase, calculator & test widget
│   └── index.html                     # Landing page entry point
├── appsettings.json                   # PostgreSQL Master DB, SQLite fallback, and pricing models
├── Program.cs                         # ASP.NET Core 9 WebApplication listening on port 5004
├── .gitignore
├── README.md
└── promote.csproj                     # .NET 9 project file
```

---

## Key Capabilities Implemented (`doc/doc.md` Section 2.B.1)

1. **Product Showcase**:
   - Modern, high-conversion landing page presenting Flitflap's hybrid storage architecture (dual-tier PostgreSQL + Garage S3 object store), 2048-bit RSA DKIM signing, and 99.99% SLA guarantee.
2. **Interactive Pricing Calculator**:
   - Dynamic sliders for hosted domains (1 to 25+) and mailboxes (5 to 200+).
   - Instant cost projections across `Starter`, `Pro`, and `Enterprise` tiers with annual discount toggle.
3. **Free Public MX & Deliverability Test Tool**:
   - Real-time DNS scanner querying MX records, SPF (`v=spf1`), DMARC (`_dmarc.{domain}`), and DKIM selectors using `DnsClient`.
   - Computes an instant Deliverability Score (0-100%) and itemized PASS/FAIL recommendations.
4. **Self-Service Onboarding Gateway (Conversion Funnel)**:
   - Onboarding wizard capturing organization name, primary domain, and administrator credentials.
   - Automatically provisions the `Tenant`, registers the `DomainRegistry` with verification token, and creates the administrative account in the Global Master DB.

---

## How to Run

From this directory:
```bash
dotnet run
```

Or from the workspace root:
```bash
dotnet run --project promote
```

Open **`http://localhost:5004`** in your browser.

