# Flitflap Email Host

[![.NET 9](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/)
[![React 18](https://img.shields.io/badge/Frontend-React%2018-blue.svg)](https://react.dev/)
[![Storage](https://img.shields.io/badge/Storage-PostgreSQL%20%2B%20Garage%20S3-teal.svg)](https://garagehq.deuxfleurs.fr/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

An enterprise-grade, distributed multi-tenant email hosting and delivery ecosystem built with **.NET 9**, **PostgreSQL dual-tier metadata isolation**, **Garage S3 Object Storage**, and **React 18**.

---

## 1. Ecosystem Overview

The platform strictly segregates **Operator-tier internal infrastructure** from **Customer-tier consumer applications**:

```
                             ┌───────────────────────────────────────────────┐
                             │             Flitflap Email Host               │
                             └──────────────────────┬────────────────────────┘
                                                    │
                 ┌──────────────────────────────────┴──────────────────────────────────┐
                 ▼                                                                     ▼
    ┌─────────────────────────┐                                           ┌─────────────────────────┐
    │     Internal Tier       │                                           │      Consumer Tier      │
    │    (Operator Scope)     │                                           │     (Customer Scope)    │
    ├─────────────────────────┤                                           ├─────────────────────────┤
    │ • admin/   (Super Admin)│                                           │ • promote/ (Showcase)   │
    │ • support/ (Staff Desk) │                                           │ • console/ (CLI Tenant) │
    │ • smtp/    (Core MTA)   │                                           │ • webmail/ (Webmail App)│
    └─────────────────────────┘                                           │ • doc/     (Specs)      │
                                                                          └─────────────────────────┘
```

---

## 2. Applications

| Application | Folder | Default Port / Mode | Description |
| :--- | :--- | :--- | :--- |
| **Super Admin Console** | [`admin/`](./admin) | `http://localhost:5001` | Tenant & subscription lifecycle, IP pool & warm-up governance, global abuse rules, and cluster health. |
| **Staff Support Desk** | [`support/`](./support) | `http://localhost:5003` | Delivery diagnostics (**Mail Tracer**), live DNS inspection with assisted fix instructions, and quota triage. |
| **Core SMTP MTA Engine** | [`smtp/`](./smtp) | Ports `25`, `587`, `465`, `5000` | High-throughput MTA daemon with 2048-bit RSA DKIM signing/verification, SPF, DMARC, anti-spam, and ClamAV. |
| **Promote & Showcase** | [`promote/`](./promote) | `http://localhost:5004` | Public landing page, interactive pricing calculator, free live MX & deliverability test tool, and self-service signup. |
| **Tenant Client Console** | [`console/`](./console) | Interactive CLI | Interactive terminal manager for registering domains, generating 2048-bit DKIM DNS keys, and provisioning mailboxes. |
| **Webmail Client App** | [`webmail/`](./webmail) | `http://localhost:5002` | Modern React 18 glassmorphism webmail client with rich HTML composition, folders, attachments, and address book. |

---

## 3. Storage Matrix Architecture

- **PostgreSQL Relational Tier**:
  - **Global Master DB (`flitflap_master`)**: Stores tenants, subscription tiers, global domain registry mappings, IP pools, and staff audit logs.
  - **Tenant Sharded DBs (`domain-database-XXX`)**: Isolates customer mailboxes, 2048-bit RSA DKIM keys, message indexes, and delivery traces.
  - **Zero-Dependency Local Fallback**: Automatically falls back to local dual-tier SQLite (`flitflap_master.db` & `flitflap_tenant.db`) if remote PostgreSQL is offline.
- **Garage S3 Object Storage (`flitflap-mail-store`)**:
  - Stores raw `.eml` MIME bodies and binary attachments under the path scheme:
    `s3://flitflap-mail-store/{tenant_uuid}/{domain_name}/{mailbox_uuid}/{year}/{month}/{message_uuid}.eml`

---

## 4. Quick Start

### Build All Projects
```bash
dotnet build console/console.csproj
dotnet build smtp/smtp.csproj
dotnet build webmail/webmail.csproj
dotnet build admin/admin.csproj
dotnet build support/support.csproj
dotnet build promote/promote.csproj
```

### Launch Services
Each application can be launched independently in its own terminal:

```bash
# Core SMTP Engine (Ports 25, 587, 465, REST 5000)
dotnet run --project smtp

# Super Admin Console (Port 5001)
dotnet run --project admin

# Webmail Client Application (Port 5002)
dotnet run --project webmail

# Staff Support Console (Port 5003)
dotnet run --project support

# Promote & Public Showcase (Port 5004)
dotnet run --project promote

# Tenant Client Console (Interactive CLI)
dotnet run --project console
```

---

## 5. Security & Deliverability Features
- **2048-bit RSA DKIM**: Outbound signing via MimeKit `DkimSigner` and inbound DNS verification.
- **SPF & DMARC**: Alignment verification and policy enforcement (`reject`, `quarantine`, `none`).
- **Anti-Spam & Anti-Virus**: Heuristic content scoring and ClamAV TCP daemon (`INSTREAM` on port 3310).
- **Anti-Spoofing**: Blocks forged external senders attempting to impersonate hosted tenant domains.
