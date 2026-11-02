# Flitflap Super Admin Application (`admin.flitflap.com`)

The centralized operator-scope administrative application for **Flitflap Email Host** (`net9.0`), implementing the specifications in `doc/doc.md` (Section 2.A.1 & Section 3.1.A).

---

## Architecture & Project Structure

```
admin/
├── Data/
│   ├── FlitflapMasterDbContext.cs    # Global Master DB (Tenants, Domains, IP pools, Abuse rules, Audit logs)
│   └── FlitflapTenantDbContext.cs    # Read model for Tenant Shards telemetry
├── Endpoints/
│   └── AdminEndpoints.cs             # REST APIs for tenant lifecycle, IP governance, cluster health, and abuse rules
├── Entities/
│   └── MasterEntities.cs             # Schema models (Tenant, DomainRegistry, IpPool, StaffAuditLog, GlobalAbuseRule)
├── Services/
│   ├── IInfrastructureMonitorService.cs # Cluster health diagnostics across PostgreSQL, Garage S3, and SMTP Engine
│   ├── IIpPoolGovernanceService.cs     # IP warm-up automation, reputation tracking, and DNS RBL audits
│   ├── ISecurityAbuseService.cs        # Global domain/TLD blacklists and staff audit logging
│   └── ITenantLifecycleService.cs      # Tenant provisioning, tier upgrades, suspension, and termination
├── wwwroot/
│   ├── css/
│   │   └── admin.css                 # Sleek dark ops console theme
│   ├── js/
│   │   └── admin.js                  # React 18 administrative dashboard SPA
│   └── index.html                    # Web console entry point
├── appsettings.json                  # PostgreSQL Master DB, Garage S3, and SMTP Engine config
├── Program.cs                        # ASP.NET Core 9 WebApplication listening on port 5001
├── .gitignore
├── README.md
└── admin.csproj                      # .NET 9 project configuration
```

---

## Core Capabilities Implemented (`doc/doc.md` Section 2.A.1)

1. **Tenant & Subscription Lifecycle**:
   - Provision new tenant organizations with primary domain and initial plan.
   - Upgrade subscription plans dynamically (`Starter` 50 GB, `Pro` 250 GB, `Enterprise` 1 TB).
   - Suspend violating tenants with documented administrative reasons.
   - Reactivate and terminate tenant accounts.

2. **Infrastructure & Cluster Monitoring**:
   - Live status tracking of PostgreSQL Master DB (`flitflap_master`) and Tenant Sharded DBs.
   - Garage S3 Object Storage health check (`flitflap-mail-store` bucket connectivity).
   - Core SMTP MTA daemon health check on localhost:5000.
   - Aggregate disk allocation and message counts across all customer shards.

3. **IP Pool & Relay Governance**:
   - Outbound delivery IP allocation and PTR hostname tracking.
   - Automated 3-stage warm-up management (`Stage 1 Initial`, `Stage 2 Ramping`, `Stage 3 Fully Warm`).
   - Automated DNS Blacklist Audits: Queries Spamhaus (`zen.spamhaus.org`), Barracuda (`b.barracudacentral.org`), and SORBS (`dnsbl.sorbs.net`) via live DNS queries.
   - Continuous IP reputation score calculations (0.0 - 100.0%).

4. **Global Security & Abuse Control**:
   - Global Domain and TLD Blacklists (e.g. blocking `.click` or abusive spam sender domains platform-wide).
   - Strict Staff Audit Trail: Logs all administrative events (tenant creation, plan upgrades, IP allocations, rule changes) with operator identity and timestamp.

---

## How to Run

From this directory:
```bash
dotnet run
```

Or from the workspace root:
```bash
dotnet run --project admin
```

Open **`http://localhost:5001`** in your browser to access the Super Admin Console.

