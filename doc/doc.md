# Product Architecture Document: Flitflap Email Host

---

## 1. Product Ecosystem Overview

**Flitflap Email Host** is a multi-tenant, distributed email hosting and delivery ecosystem. The system separates internal operations (Super Admin, Support, Core Mail Transfer Engine) from customer-facing operations (Public Landing, Tenant Console, End-User Webmail, and Developer/User Docs).

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
    │ • Super Admin Console   │                                           │ • Promote (Marketing)   │
    │ • Staff Support Console │                                           │ • Tenant Client Console │
    │ • SMTP / Core Engine    │                                           │ • Webmail Client App    │
    └─────────────────────────┘                                           │ • Documentation Portal  │
                                                                          └─────────────────────────┘

```

---

## 2. Application Architecture & Responsibilities

### A. Internal Applications (Flitflap Operator Tier)

#### 1. Super Admin Application (`admin.flitflap.com`)

*Target Audience: Platform Owners, System Architects, Infrastructure Engineers*

* **Tenant & Subscription Lifecycle:** Provision, upgrade, suspend, and terminate tenant accounts and billing plans.
* **Infrastructure Monitoring:** Live tracking of SMTP queues, outbound IP reputation, server health, system-wide disk allocation, and network telemetry.
* **IP Pool & Relay Governance:** Warm-up automation, IP rotation, and IP blacklist monitoring (Spamhaus, Barracuda, SORBS).
* **Global Security & Abuse Control:** Platform-wide bounce/complaint thresholds, global domain/tld blacklists, and security event logs.

#### 2. Staff Support Application (`support.flitflap.com`)

*Target Audience: Technical Support Engineers, Compliance Agents*

* **Tenant Lookup & Diagnostic View:** Read-only visibility into tenant routing errors, DNS propagation failures, and authentication logs.
* **Delivery Diagnostics (Mail Tracer):** Track single message delivery events by Message-ID (Accepted, Queued, Filtered, Deferred, Bounced, Delivered).
* **Quota & Ticket Management:** Temporary quota adjustments, ticket triage, and assisted verification for failing DKIM/SPF setups.
* **Audit Trail:** Strict logging of any support staff interaction with tenant configurations.

#### 3. SMTP & Core Mail Engine (`smtp.flitflap.com` / Ports 25, 587, 465)

*Target Audience: Automated Mail Transfer Agent (MTA) & Mail Submission Agent (MSA)*

* **Port Handling:**
* `Port 25`: Server-to-server inbound relay and inter-MTA transmission.
* `Port 587`: Authenticated client submission (STARTTLS required).
* `Port 465`: Implicit TLS submission.


* **Inbound Inspection Pipeline:**
* Protocol-level validation (HELO/EHLO validation, Reverse DNS PTR checks).
* SPF (`Sender Policy Framework`) check.
* DKIM (`DomainKeys Identified Mail`) cryptographic signature verification.
* DMARC policy enforcement and aggregate report generation.
* Anti-Spam / Bayesian heuristic classification.
* Anti-Virus inspection.


* **Outbound Pipeline:**
* Tenant authenticated routing.
* Automated DKIM signing using tenant private keys.
* MX resolution, smart retries with exponential backoff, and bounce generation.


* **Storage Pipeline:** Hands off accepted messages to persistent file/object storage and metadata to PostgreSQL.

---

### B. Consumer Applications (Customer Tier)

#### 1. Promote Application (`flitflap.com`)

*Target Audience: Prospective Customers, Businesses, Developers*

* **Product Showcase:** Presentation of hosting features, SLA guarantees, security certifications, and compliance details.
* **Interactive Pricing Calculator:** Dynamic cost modeling based on domains, mailboxes, and storage tiers.
* **MX / Deliverability Test Tool:** Free public widget for checking existing domain DNS health, SPF, and MX status.
* **Conversion Funnel:** Self-service registration and tenant onboarding gateway.

#### 2. Tenant Client Console (`console.flitflap.com`)

*Target Audience: Domain Owners, IT Administrators, Organization Managers*

* **Domain Lifecycle:**
* Domain registration, verification wizard, and DNS record generator (MX, SPF, DKIM, DMARC, CNAME, autodiscover).
* One-click DNS re-verification diagnostic.


* **Mailbox & Alias Management:**
* Provisioning email addresses (`user@domain.com`).
* Mail forwarding, catch-all routing, and group distribution lists.
* Password reset, multi-factor authentication (MFA) enforcement, and account lockouts.


* **Security & Routing Policies:**
* Domain-level spam thresholds, custom blocklists, and allowlists.
* Quota ceiling limits per mailbox (e.g., 5 GB, 20 GB).


* **API & SMTP Credentials:** Generation of tenant SMTP/IMAP credentials for transactional and programmatic access.

#### 3. Webmail Client Application (`mail.flitflap.com` or custom CNAME)

*Target Audience: End Users, Employees, Mailbox Account Holders*

* **Core Operations:** Modern inbox interface for compose, read, search, reply, forward, archive, and trash.
* **Message Handling:** Support for rich text/HTML composition, MIME attachments, threaded conversation view, and message flags.
* **Personal Organization:** Folders, tags, contact lists (address book), auto-responders (Out of Office), and personal signatures.
* **Account Settings:** User password change, active sessions list, and security preferences.

#### 4. Documentation Portal (`docs.flitflap.com`)

*Target Audience: IT Admins, Developers, End Users*

* **Setup Guides:** Step-by-step DNS records configuration guides for Cloudflare, GoDaddy, Namecheap, Route 53, etc.
* **Email Client Setup:** IMAP, POP3, and SMTP configuration guides for Outlook, Apple Mail, Thunderbird, Android, and iOS.
* **API Reference:** Developer docs for mail sending APIs, webhooks (inbound, delivery notifications), and management endpoints.
* **Troubleshooting Center:** Deliverability best practices, spam troubleshooting, and DNS propagation FAQ.

---

## 3. Data Storage & Multi-Tenancy Architecture

The storage layer couples **PostgreSQL** for relational metadata with **Garage Object Storage** (distributed, S3-compatible, lightweight object store) for raw mail bodies and attachments.

```
                    ┌────────────────────────────────────────────────────────┐
                    │               Storage & Database Matrix                │
                    └───────────────────────────┬────────────────────────────┘
                                                │
             ┌──────────────────────────────────┴──────────────────────────────────┐
             ▼                                                                     ▼
┌─────────────────────────┐                                           ┌─────────────────────────┐
│     PostgreSQL Tiers    │                                           │  Garage Object Storage  │
├─────────────────────────┤                                           ├─────────────────────────┤
│ • Main / Global DB      │                                           │ Bucket: `flitflap-mail` │
│ • Per-Tenant/Domain DBs │                                           │ Path: `{domain}/{id}/*` │
└─────────────────────────┘                                           └─────────────────────────┘

```

### 1. PostgreSQL Tier: Database Separation Model

#### A. Main / Global Database (`flitflap_master`)

Used exclusively by **Super Admin, Support, and Tenant Console auth**. Stores no private email message contents.

* **`tenants`**: Tenant identification, status, subscription tier, billing status, created timestamps.
* **`tenant_users`**: Administrative credentials (bcrypt/argon2) for logging into `console.flitflap.com`.
* **`domains_registry`**: Global index mapping `domain_name` $\rightarrow$ `database_connection_string` or `database_name`.
* **`ip_pools`**: Outbound delivery IPs, allocation status, and reputation metrics.
* **`staff_audit_logs`**: Internal administrative access and audit trail.

#### B. Tenant / Domain Sharded Databases (`domain-database-XXXXXXXX-yyyyyy-N`)

Each tenant or tenant cluster has an isolated database containing domain records, mailboxes, and email metadata. This ensures strict data segregation, horizontal scalability, and straightforward per-tenant backups.

* **`mailboxes`**:
* `id` (UUID), `address` (`info@client.com`), `password_hash`, `quota_bytes`, `used_bytes`, `is_active`.


* **`aliases`**:
* `id` (UUID), `source_alias`, `target_mailbox_id`.


* **`dkim_keys`**:
* `selector`, `private_key` (encrypted), `public_key`, `is_active`.


* **`messages_index`**:
* `id` (UUID, primary key).
* `mailbox_id` (UUID, foreign key).
* `folder` (`INBOX`, `Sent`, `Drafts`, `Trash`, `Spam`, custom).
* `sender`, `recipient`, `subject`, `preview_snippet`.
* `size_bytes`.
* `is_read`, `is_flagged`, `is_deleted`.
* `garage_storage_key` (pointer to Garage object store).
* `created_at`, `received_at`.


* **`domain_filters`**:
* Blacklists, whitelists, spam score thresholds.



---

### 2. File / Blob Storage: Garage Object Storage Integration

Raw email messages (`.eml` MIME structure) and binary attachments are saved in **Garage S3 Object Storage** to keep PostgreSQL databases lightweight and performant.

* **Storage Scheme:**
* Bucket: `flitflap-mail-store`
* Object Key Structure:
```
s3://flitflap-mail-store/{tenant_uuid}/{domain_name}/{mailbox_uuid}/{year}/{month}/{message_uuid}.eml

```




* **Separation of Metadata and Payload:**
* **PostgreSQL:** Searches, sorting, folders, unread flags, size calculations, and sender/subject indexes.
* **Garage:** Complete RFC 822 MIME raw message, inline images, and heavy binary attachments.


* **Advantages for Flitflap:**
* Garage runs efficiently on NVMe/SSD storage clusters with zero-lock-in standard S3 API.
* Backing up databases stays fast because large blobs do not inflate PostgreSQL WAL (Write-Ahead Logs) or table heaps.
* Deleting or expiring messages triggers a clean async deletion in Garage and removes the row from the tenant database.



---

## 4. System Boundaries & Communication Matrix

| Source App | Destination | Protocol / Channel | Purpose |
| --- | --- | --- | --- |
| **Promote App** | Main DB | HTTPS / REST | Tenant signup, account creation |
| **Console App** | Main DB | HTTPS / Direct | Tenant profile, billing, domain routing lookup |
| **Console App** | Tenant DB | SQL Connection Pool | Mailbox provisioning, password resets, quota edits |
| **SMTP Server** | Main DB | Direct Cache / Read | Check domain validity, active status, IP pool |
| **SMTP Server** | Tenant DB | SQL Connection Pool | Verify recipient, auth sender, insert metadata |
| **SMTP Server** | Garage Storage | S3 API (Port 3900/3902) | Write raw incoming/outgoing `.eml` payload |
| **Webmail App** | Tenant DB | HTTPS / REST | Load inbox view, flags, folder structures |
| **Webmail App** | Garage Storage | S3 API / Pre-signed URL | Stream message payload, attachment downloads |
| **Support App** | Main DB & Tenant DB | Read-only SQL Proxy | Audit and diagnostic log inspection |

---

## 5. Architectural Checklist Before Development

* [ ] Finalize the database naming and provisioning strategy (1 database per tenant vs. partitioned databases per cluster).
* [ ] Finalize encryption at rest for DKIM private keys stored in databases.
* [ ] Define Garage S3 retention and garbage collection lifecycle for deleted messages.
* [ ] Finalize token structure (JWT/OAuth2) shared between the Console and Webmail apps.