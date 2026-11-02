# Flitflap Webmail Client Application (ASP.NET Core 9 + React 18)

Modern, high-performance webmail client application for **Flitflap Email Host** (`net9.0` backend with React 18 single-page frontend), implementing the specifications in `doc/doc.md` (Section 2.B.3) and `doc/todo.md`.

---

## Architecture & Project Structure

```
webmail/
├── Data/
│   ├── FlitflapMasterDbContext.cs # Global Master DB (Tenants, DomainsRegistry, IpPools)
│   └── FlitflapTenantDbContext.cs # Tenant Sharded DB (Mailboxes, Contacts, MessageIndex, Traces)
├── Endpoints/
│   ├── AuthEndpoints.cs          # Login, Session verification, BCrypt password update
│   ├── ContactEndpoints.cs       # Address Book & contact management
│   ├── MailEndpoints.cs          # Folders, Message list, Details, EML stream, Attachments, Send, Move, Delete
│   └── SettingsEndpoints.cs      # Signatures, Out-of-Office auto-responder, Security settings
├── Entities/
│   ├── MasterEntities.cs         # Master schema (Tenant, DomainRegistry, TenantUser)
│   └── TenantEntities.cs         # Tenant schema (TenantMailbox, Contact, MessageIndex)
├── Security/
│   ├── BCryptPasswordHasher.cs   # Secure user authentication
│   ├── IPasswordHasher.cs        # Password hashing abstraction
│   └── JwtTokenService.cs        # JWT bearer authentication token issuer
├── Services/
│   └── WebmailMailService.cs     # Message retrieval, Garage S3 stream parsing, SMTP dispatch
├── Storage/
│   └── GarageStorageService.cs   # Garage S3 integration (flitflap-mail-store) + disk fallback
├── wwwroot/
│   ├── css/
│   │   └── app.css               # Modern glassmorphism UI theme & styling
│   ├── js/
│   │   └── app.js                # React 18 single-page application
│   └── index.html                # Single-page application entry point
├── appsettings.json              # Dual-tier PostgreSQL / SQLite, Garage S3, and JWT config
├── Program.cs                    # ASP.NET Core 9 WebApplication & SPA host on port 5002
├── .gitignore
├── README.md
└── webmail.csproj                # .NET 9 project configuration
```

---

## Key Features Implemented

1. **Modern React 18 Frontend**:
   - Clean, dark glassmorphism interface inspired by Superhuman and ProtonMail.
   - Real-time unread counts per folder (`Inbox`, `Starred`, `Sent`, `Drafts`, `Spam`, `Trash`).
   - Global search bar (filters subject, sender, and content snippet).
   - Rich email reader with security badges (`SPF Pass`, `DKIM Verified`, `DMARC Aligned`, `Spam Score`).
   - Interactive composer modal with To, Cc, Bcc, Subject, rich HTML body, and attachment management.
   - Quick actions: **Reply**, **Reply All**, **Forward**, **Star/Flag**, **Delete/Move to Trash**, and **Raw .EML download**.
   - Address Book & Contacts manager.
   - Settings for HTML personal signatures, Out-of-Office auto-responder, and BCrypt password update.
   - Visual mailbox storage quota utilization bar.

2. **Backend Services & Storage Integration (`doc/doc.md` Sections 3 & 4)**:
   - **PostgreSQL Dual-Tier**: Master DB for domain lookup and Tenant Sharded DB for mailboxes, message metadata, and contacts (with local SQLite fallback).
   - **Garage S3 Object Storage (`flitflap-mail-store`)**: Streams raw MIME messages and downloads attachments from `{tenant_uuid}/{domain_name}/{mailbox_uuid}/{year}/{month}/{message_uuid}.eml`.
   - **Outbound Relay**: Sends composed and forwarded emails via SMTP relay (`localhost:587` or `localhost:25`).

3. **Built-in Demo Mailboxes**:
   - `info@flitflap.com` (Password: `Password123!`)
   - `demo@flitflap.com` (Password: `Password123!`)

---

## How to Run

From this directory:
```bash
dotnet run
```

Or from the workspace root:
```bash
dotnet run --project webmail
```

Once running, navigate to:
**`http://localhost:5002`** in your browser.

