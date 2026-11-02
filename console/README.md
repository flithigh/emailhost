# Flitflap Email Host - Tenant Client Console

Enterprise CLI management console for the **Flitflap Email Host** platform, built with .NET 9, PostgreSQL dual-tier architecture, Garage S3 integration, and Spectre.Console, implementing the specifications in `doc/doc.md` (Section 2.B.2).

---

## Architecture & Project Structure

```
console/
├── Data/
│   ├── FlitflapMasterDbContext.cs # EF Core context for Global Master DB (Tenants, DomainsRegistry, IpPools)
│   └── FlitflapTenantDbContext.cs # EF Core context for Tenant Sharded DB (Mailboxes, DKIM keys, MessageIndex)
├── Entities/
│   ├── MasterEntities.cs         # Master schema (Tenant, DomainRegistry, IpPool, StaffAuditLog)
│   └── TenantEntities.cs         # Tenant schema (TenantMailbox, DkimKey, MessageIndex, DomainFilter)
├── Security/
│   ├── BCryptPasswordHasher.cs   # Industry-standard BCrypt password hashing for mail accounts
│   ├── DkimService.cs            # 2048-bit RSA DKIM key pair generation & DNS TXT formatting
│   └── IPasswordHasher.cs        # Contract for password hashing
├── Views/
│   ├── AddDomainView.cs          # Multi-tenant domain wizard & 2048-bit RSA DKIM/DNS record generator
│   ├── AddMailboxView.cs         # Mailbox provisioning with 5 GB / 20 GB quota ceilings
│   ├── ConnectDomainView.cs      # One-click public DNS verification (TXT & MX lookup)
│   ├── ConsoleTheme.cs           # Spectre.Console styling, brand banners, and theme helpers
│   ├── DatabaseStatusView.cs     # PostgreSQL dual-tier and Garage S3 cluster health diagnostics
│   ├── ManageDomainView.cs       # Master registry inspector, DNS records viewer, and deletion
│   ├── RemoveMailView.cs         # Secure mailbox deletion
│   └── ResetPasswordView.cs      # Masked input password reset with BCrypt hashing
├── appsettings.json              # Dual-tier PostgreSQL, Garage S3, and SQLite fallback config
├── console.csproj                # .NET 9 project configuration
├── Program.cs                    # Generic Host, DI setup, auto-migration, and interactive menu loop
└── README.md
```

---

## Features Implemented (`doc/doc.md` Section 2.B.2)

1. **Dual-Tier Database Support**:
   - **Global Master Database (`flitflap_master`)**: Manages tenant organizations, subscription tiers, and global domain routing registries.
   - **Tenant Sharded Database (`flitflap_tenant`)**: Isolates tenant mailboxes, 2048-bit RSA DKIM keys, mailbox filters, and message metadata indexes.
   - **Automatic Dual-Tier Local Fallback**: Seamlessly falls back to local SQLite databases (`flitflap_master.db` and `flitflap_tenant.db`) if the remote PostgreSQL cluster is offline.
2. **2048-bit RSA DKIM & DNS Generation Wizard**:
   - Generates cryptographically secure 2048-bit RSA key pairs (`v=DKIM1; k=rsa; p=...`).
   - Automatically outputs ready-to-publish DNS records:
     - `MX`: `mail.flitflap.com` (Priority 10)
     - `TXT (Verification)`: Domain ownership token
     - `TXT (SPF)`: `v=spf1 mx include:spf.flitflap.com ~all`
     - `TXT (DKIM)`: `default._domainkey.{domain}` with base64 RSA public key
     - `TXT (DMARC)`: `_dmarc.{domain}` (`v=DMARC1; p=quarantine; rua=mailto:dmarc@{domain}`)
     - `CNAME (Webmail)`: `webmail.{domain}` -> `webmail.flitflap.com`
3. **One-Click Domain DNS Verification**:
   - Queries public DNS resolvers (Google DNS, Cloudflare) for TXT ownership and MX host records.
   - Includes manual staging bypass for local testing.
4. **Mailbox Provisioning with Quota Ceilings**:
   - Provisions mail accounts with BCrypt-hashed credentials.
   - Supports configurable quota ceilings (5 GB Standard, 20 GB Pro, custom).
5. **Storage & Database Diagnostics**:
   - Verifies connectivity to the Global Master DB and Tenant Sharded DB.
   - Verifies Garage S3 Object Storage endpoint (`http://localhost:3900`).
   - Displays real-time metrics for tenants, domains, mailboxes, DKIM keys, and stored messages.

---

## How to Run

From this directory:
```bash
dotnet run
```

Or from the workspace root:
```bash
dotnet run --project console
```
