// Flitflap Super Admin Console - React 18
const { useState, useEffect, useCallback } = React;
const h = React.createElement;

function AdminApp() {
  const [tab, setTab] = useState("overview"); // overview | tenants | infra | ip | abuse | audit
  const [overview, setOverview] = useState(null);
  const [tenants, setTenants] = useState([]);
  const [infra, setInfra] = useState(null);
  const [ips, setIps] = useState([]);
  const [rules, setRules] = useState([]);
  const [logs, setLogs] = useState([]);
  const [loading, setLoading] = useState(false);

  // Modals
  const [tenantModal, setTenantModal] = useState(false);
  const [ipModal, setIpModal] = useState(false);
  const [abuseModal, setAbuseModal] = useState(false);

  // Form states
  const [newTenant, setNewTenant] = useState({ name: "", subscriptionTier: "Enterprise", contactEmail: "", initialDomain: "" });
  const [newIp, setNewIp] = useState({ ipAddress: "", hostname: "", assignedTenantName: "Global Shared Pool" });
  const [newRule, setNewRule] = useState({ ruleType: "DomainBlacklist", pattern: "", reason: "" });

  const loadData = useCallback(async () => {
    setLoading(true);
    try {
      const [ovRes, tRes, infRes, ipRes, rRes, lRes] = await Promise.all([
        fetch("/api/admin/overview"),
        fetch("/api/admin/tenants"),
        fetch("/api/admin/infra"),
        fetch("/api/admin/ip-pools"),
        fetch("/api/admin/security/rules"),
        fetch("/api/admin/audit-logs?limit=50")
      ]);

      if (ovRes.ok) setOverview(await ovRes.json());
      if (tRes.ok) setTenants(await tRes.json());
      if (infRes.ok) setInfra(await infRes.json());
      if (ipRes.ok) setIps(await ipRes.json());
      if (rRes.ok) setRules(await rRes.json());
      if (lRes.ok) setLogs(await lRes.json());
    } catch (e) {
      console.error("Failed to load admin data", e);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { loadData(); }, [loadData]);

  // Actions
  const handleCreateTenant = async () => {
    if (!newTenant.name) return;
    await fetch("/api/admin/tenants", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(newTenant)
    });
    setTenantModal(false);
    setNewTenant({ name: "", subscriptionTier: "Enterprise", contactEmail: "", initialDomain: "" });
    loadData();
  };

  const handleUpgradeTier = async (id, tier) => {
    await fetch(`/api/admin/tenants/${id}/upgrade`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ tier })
    });
    loadData();
  };

  const handleSuspendTenant = async (id) => {
    const reason = prompt("Enter reason for suspension:", "Abuse complaints threshold exceeded");
    if (!reason) return;
    await fetch(`/api/admin/tenants/${id}/suspend`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ reason })
    });
    loadData();
  };

  const handleReactivateTenant = async (id) => {
    await fetch(`/api/admin/tenants/${id}/reactivate`, { method: "POST" });
    loadData();
  };

  const handleAddIp = async () => {
    if (!newIp.ipAddress) return;
    await fetch("/api/admin/ip-pools", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(newIp)
    });
    setIpModal(false);
    setNewIp({ ipAddress: "", hostname: "", assignedTenantName: "Global Shared Pool" });
    loadData();
  };

  const handleAuditAllIps = async () => {
    setLoading(true);
    await fetch("/api/admin/ip-pools/check-all", { method: "POST" });
    await loadData();
    setLoading(false);
  };

  const handleWarmup = async (id, stage) => {
    await fetch(`/api/admin/ip-pools/${id}/warmup`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ stage })
    });
    loadData();
  };

  const handleAddRule = async () => {
    if (!newRule.pattern) return;
    await fetch("/api/admin/security/rules", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(newRule)
    });
    setAbuseModal(false);
    setNewRule({ ruleType: "DomainBlacklist", pattern: "", reason: "" });
    loadData();
  };

  const handleDeleteRule = async (id) => {
    await fetch(`/api/admin/security/rules/${id}`, { method: "DELETE" });
    loadData();
  };

  return h("div", null,
    // Header
    h("header", { className: "admin-header" },
      h("div", { style: { display: "flex", alignItems: "center", gap: "1rem" } },
        h("div", { style: { fontWeight: 800, fontSize: "1.15rem", display: "flex", alignItems: "center", gap: "0.5rem" } },
          h("span", { style: { color: "#38bdf8" } }, "🛡️"),
          h("span", { style: { background: "linear-gradient(90deg, #38bdf8, #818cf8)", WebkitBackgroundClip: "text", WebkitTextFillColor: "transparent" } }, "Flitflap Super Admin"),
          h("span", { style: { fontSize: "0.7rem", padding: "2px 6px", borderRadius: "4px", background: "rgba(56, 189, 248, 0.15)", color: "#38bdf8" } }, "Operator Tier")
        ),

        // Nav tabs
        h("nav", { className: "admin-nav" },
          [
            { id: "overview", label: "Overview" },
            { id: "tenants", label: `Tenants (${tenants.length})` },
            { id: "infra", label: "Infrastructure & Garage" },
            { id: "ip", label: `IP Pools (${ips.length})` },
            { id: "abuse", label: `Abuse Rules (${rules.length})` },
            { id: "audit", label: "Staff Audit Trail" }
          ].map(t => h("button", {
            key: t.id,
            className: `nav-tab ${tab === t.id ? "active" : ""}`,
            onClick: () => setTab(t.id)
          }, t.label))
        )
      ),

      h("div", { style: { display: "flex", alignItems: "center", gap: "0.6rem" } },
        h("button", { className: "btn btn-secondary", onClick: handleAuditAllIps, disabled: loading }, "Audit Blacklists"),
        h("button", { className: "btn btn-secondary", onClick: loadData, disabled: loading }, loading ? "Refreshing..." : "Refresh")
      )
    ),

    // Main Content
    h("main", { style: { padding: "2rem", maxWidth: "1400px", margin: "0 auto" } },
      // Tab 1: Overview
      tab === "overview" && h("div", null,
        overview && h("div", { className: "grid-metrics" },
          h("div", { className: "card" },
            h("div", { className: "metric-title" }, "Total Tenants"),
            h("div", { className: "metric-value" }, overview.totalTenants),
            h("div", { style: { fontSize: "0.8rem", color: "#34d399", marginTop: "0.3rem" } }, `${overview.activeTenants} Active | ${overview.suspendedTenants} Suspended`)
          ),
          h("div", { className: "card" },
            h("div", { className: "metric-title" }, "Hosted Domains"),
            h("div", { className: "metric-value" }, overview.totalDomains),
            h("div", { style: { fontSize: "0.8rem", color: "#94a3b8", marginTop: "0.3rem" } }, "Master Registry Mappings")
          ),
          h("div", { className: "card" },
            h("div", { className: "metric-title" }, "Total Mailboxes"),
            h("div", { className: "metric-value" }, overview.totalMailboxes),
            h("div", { style: { fontSize: "0.8rem", color: "#38bdf8", marginTop: "0.3rem" } }, `${overview.totalMessages} Messages Indexed`)
          ),
          h("div", { className: "card" },
            h("div", { className: "metric-title" }, "Avg IP Reputation"),
            h("div", { className: "metric-value", style: { color: overview.avgIpReputation > 90 ? "#34d399" : "#f59e0b" } }, `${overview.avgIpReputation}%`),
            h("div", { style: { fontSize: "0.8rem", color: overview.blacklistedIps > 0 ? "#f87171" : "#34d399", marginTop: "0.3rem" } },
              overview.blacklistedIps > 0 ? `⚠️ ${overview.blacklistedIps} Blacklisted` : "✔ Clean on Spamhaus & SORBS"
            )
          )
        ),

        // Cluster Telemetry Status
        h("div", { className: "card", style: { marginBottom: "1.5rem" } },
          h("h3", { style: { fontSize: "1rem", fontWeight: 700, marginBottom: "1rem", color: "#fff" } }, "Cluster Subsystems & Health"),
          h("div", { style: { display: "grid", gridTemplateColumns: "repeat(auto-fit, minmax(280px, 1fr))", gap: "1rem" } },
            h("div", { style: { background: "#0a0f1d", padding: "1rem", borderRadius: "8px", border: "1px solid var(--border)" } },
              h("div", { style: { display: "flex", justifyContent: "space-between", marginBottom: "0.4rem" } },
                h("span", { style: { fontWeight: 600 } }, "PostgreSQL Master DB"),
                h("span", { className: "badge badge-active" }, "ONLINE")
              ),
              h("div", { style: { fontSize: "0.8rem", color: "#94a3b8" } }, "Dual-tier schema isolation active.")
            ),
            h("div", { style: { background: "#0a0f1d", padding: "1rem", borderRadius: "8px", border: "1px solid var(--border)" } },
              h("div", { display: "flex", justifyContent: "space-between", marginBottom: "0.4rem" } },
                h("span", { style: { fontWeight: 600 } }, "Garage S3 Object Store"),
                h("span", { className: `badge ${overview?.garageS3Healthy ? "badge-active" : "badge-suspended"}` }, overview?.garageS3Healthy ? "ONLINE" : "STANDBY")
              ),
              h("div", { style: { fontSize: "0.8rem", color: "#94a3b8" } }, "Bucket: flitflap-mail-store")
            ),
            h("div", { style: { background: "#0a0f1d", padding: "1rem", borderRadius: "8px", border: "1px solid var(--border)" } },
              h("div", { display: "flex", justifyContent: "space-between", marginBottom: "0.4rem" } },
                h("span", { style: { fontWeight: 600 } }, "Core SMTP MTA Daemon"),
                h("span", { className: `badge ${overview?.smtpEngineHealthy ? "badge-active" : "badge-tier"}` }, overview?.smtpEngineHealthy ? "RUNNING" : "STANDBY")
              ),
              h("div", { style: { fontSize: "0.8rem", color: "#94a3b8" } }, overview?.smtpEngineStatus)
            )
          )
        )
      ),

      // Tab 2: Tenants
      tab === "tenants" && h("div", null,
        h("div", { style: { display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1rem" } },
          h("h2", { style: { fontSize: "1.2rem", fontWeight: 700 } }, "Tenant & Subscription Lifecycle"),
          h("button", { className: "btn btn-primary", onClick: () => setTenantModal(true) }, "+ Provision Tenant")
        ),

        h("div", { className: "card" },
          h("table", { className: "data-table" },
            h("thead", null,
              h("tr", null,
                h("th", null, "Tenant Organization"),
                h("th", null, "Subscription Tier"),
                h("th", null, "Billing Status"),
                h("th", null, "Storage Ceiling"),
                h("th", null, "Domains"),
                h("th", null, "Created"),
                h("th", null, "Actions")
              )
            ),
            h("tbody", null,
              tenants.map(t => h("tr", { key: t.id },
                h("td", { style: { fontWeight: 600, color: "#fff" } }, t.name),
                h("td", null, h("span", { className: "badge badge-tier" }, t.subscriptionTier)),
                h("td", null, h("span", { className: `badge ${t.billingStatus === "Active" ? "badge-active" : "badge-suspended"}` }, t.billingStatus)),
                h("td", null, `${Math.round(t.storageLimitBytes / (1024 * 1024 * 1024))} GB`),
                h("td", null, t.domains.join(", ") || "None"),
                h("td", null, new Date(t.createdAtUtc).toLocaleDateString()),
                h("td", null,
                  h("div", { style: { display: "flex", gap: "0.3rem" } },
                    h("select", {
                      className: "input",
                      style: { width: "110px", padding: "2px 4px", fontSize: "0.75rem", margin: 0 },
                      value: t.subscriptionTier,
                      onChange: e => handleUpgradeTier(t.id, e.target.value)
                    },
                      h("option", { value: "Starter" }, "Starter"),
                      h("option", { value: "Pro" }, "Pro"),
                      h("option", { value: "Enterprise" }, "Enterprise")
                    ),
                    t.billingStatus === "Active"
                      ? h("button", { className: "btn btn-danger", style: { padding: "2px 6px", fontSize: "0.75rem" }, onClick: () => handleSuspendTenant(t.id) }, "Suspend")
                      : h("button", { className: "btn btn-secondary", style: { padding: "2px 6px", fontSize: "0.75rem", color: "#34d399" }, onClick: () => handleReactivateTenant(t.id) }, "Activate")
                  )
                )
              ))
            )
          )
        )
      ),

      // Tab 3: Infrastructure
      tab === "infra" && h("div", null,
        h("h2", { style: { fontSize: "1.2rem", fontWeight: 700, marginBottom: "1rem" } }, "Infrastructure, Storage Matrix & Cluster Telemetry"),
        infra && h("div", { style: { display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1.5rem" } },
          h("div", { className: "card" },
            h("h3", { style: { fontSize: "1rem", fontWeight: 700, marginBottom: "1rem", color: "#38bdf8" } }, "PostgreSQL Storage Tier"),
            h("div", { style: { display: "flex", flexDirection: "column", gap: "0.75rem", fontSize: "0.85rem" } },
              h("div", null, h("b", null, "Master DB: "), infra.masterDbHealthy ? "Connected (flitflap_master)" : "Offline"),
              h("div", null, h("b", null, "Provider: "), infra.masterDbProvider),
              h("div", null, h("b", null, "Tenant Shards: "), `${infra.totalDomains} registered domain databases`),
              h("div", null, h("b", null, "Mailboxes: "), `${infra.totalMailboxes} active accounts`),
              h("div", null, h("b", null, "Indexed Messages: "), `${infra.totalMessages} records`)
            )
          ),
          h("div", { className: "card" },
            h("h3", { style: { fontSize: "1rem", fontWeight: 700, marginBottom: "1rem", color: "#38bdf8" } }, "Garage S3 Object Storage"),
            h("div", { style: { display: "flex", flexDirection: "column", gap: "0.75rem", fontSize: "0.85rem" } },
              h("div", null, h("b", null, "Endpoint: "), infra.garageEndpoint),
              h("div", null, h("b", null, "Bucket: "), infra.garageBucket),
              h("div", null, h("b", null, "Health: "), infra.garageS3Healthy ? "Connected & Serving" : "Standby (Local File Emulation)"),
              h("div", null, h("b", null, "Path Schema: "), "{tenant}/{domain}/{mailbox}/{year}/{month}/{id}.eml")
            )
          )
        )
      ),

      // Tab 4: IP Pools
      tab === "ip" && h("div", null,
        h("div", { style: { display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1rem" } },
          h("h2", { style: { fontSize: "1.2rem", fontWeight: 700 } }, "Outbound IP Pools & Warm-Up Governance"),
          h("button", { className: "btn btn-primary", onClick: () => setIpModal(true) }, "+ Add Relay IP")
        ),

        h("div", { className: "card" },
          h("table", { className: "data-table" },
            h("thead", null,
              h("tr", null,
                h("th", null, "IP Address"),
                h("th", null, "Reverse DNS Hostname"),
                h("th", null, "Assigned Pool / Tenant"),
                h("th", null, "Warm-up Stage"),
                h("th", null, "Reputation Score"),
                h("th", null, "DNS Blacklist Audit"),
                h("th", null, "Actions")
              )
            ),
            h("tbody", null,
              ips.map(ip => h("tr", { key: ip.id },
                h("td", { style: { fontWeight: 600, color: "#fff" } }, ip.ipAddress),
                h("td", null, ip.hostname),
                h("td", null, ip.assignedTenantName),
                h("td", null,
                  h("select", {
                    className: "input",
                    style: { width: "120px", padding: "2px 4px", fontSize: "0.75rem", margin: 0 },
                    value: ip.warmupStage,
                    onChange: e => handleWarmup(ip.id, parseInt(e.target.value))
                  },
                    h("option", { value: 1 }, "Stage 1 (Init)"),
                    h("option", { value: 2 }, "Stage 2 (Ramping)"),
                    h("option", { value: 3 }, "Stage 3 (Warm)")
                  )
                ),
                h("td", null,
                  h("div", { style: { display: "flex", alignItems: "center", gap: "0.5rem" } },
                    h("span", { style: { fontWeight: 700, color: ip.reputationScore > 90 ? "#34d399" : "#f59e0b" } }, `${ip.reputationScore}%`),
                    h("div", { style: { width: 60, height: 4, background: "#334155", borderRadius: 2 } },
                      h("div", { style: { width: `${ip.reputationScore}%`, height: "100%", background: ip.reputationScore > 90 ? "#34d399" : "#f59e0b" } })
                    )
                  )
                ),
                h("td", null,
                  h("span", { className: `badge ${ip.isBlacklisted ? "badge-suspended" : "badge-active"}` }, ip.blacklistDetails)
                ),
                h("td", null,
                  h("button", {
                    className: "btn btn-secondary",
                    style: { padding: "2px 6px", fontSize: "0.75rem" },
                    onClick: async () => {
                      await fetch(`/api/admin/ip-pools/${ip.id}/check`, { method: "POST" });
                      loadData();
                    }
                  }, "Check RBL")
                )
              ))
            )
          )
        )
      ),

      // Tab 5: Abuse Rules
      tab === "abuse" && h("div", null,
        h("div", { style: { display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1rem" } },
          h("h2", { style: { fontSize: "1.2rem", fontWeight: 700 } }, "Global Abuse & Domain Blacklist Rules"),
          h("button", { className: "btn btn-primary", onClick: () => setAbuseModal(true) }, "+ Add Blacklist Rule")
        ),

        h("div", { className: "card" },
          h("table", { className: "data-table" },
            h("thead", null,
              h("tr", null,
                h("th", null, "Rule Type"),
                h("th", null, "Pattern / Matcher"),
                h("th", null, "Reason"),
                h("th", null, "Created"),
                h("th", null, "Action")
              )
            ),
            h("tbody", null,
              rules.map(r => h("tr", { key: r.id },
                h("td", null, h("span", { className: "badge badge-tier" }, r.ruleType)),
                h("td", { style: { fontWeight: 600, color: "#fff" } }, r.pattern),
                h("td", null, r.reason),
                h("td", null, new Date(r.createdAtUtc).toLocaleDateString()),
                h("td", null,
                  h("button", { className: "btn btn-danger", style: { padding: "2px 6px", fontSize: "0.75rem" }, onClick: () => handleDeleteRule(r.id) }, "Remove")
                )
              ))
            )
          )
        )
      ),

      // Tab 6: Audit Trail
      tab === "audit" && h("div", null,
        h("h2", { style: { fontSize: "1.2rem", fontWeight: 700, marginBottom: "1rem" } }, "Staff Audit Trail (Compliance & Governance)"),
        h("div", { className: "card" },
          h("table", { className: "data-table" },
            h("thead", null,
              h("tr", null,
                h("th", null, "Timestamp (UTC)"),
                h("th", null, "Staff Operator"),
                h("th", null, "Action"),
                h("th", null, "Target Tenant / Domain"),
                h("th", null, "Details")
              )
            ),
            h("tbody", null,
              logs.map(l => h("tr", { key: l.id },
                h("td", { style: { color: "#94a3b8" } }, new Date(l.timestampUtc).toLocaleString()),
                h("td", { style: { fontWeight: 600, color: "#38bdf8" } }, l.staffUser),
                h("td", null, h("span", { className: "badge badge-tier" }, l.action)),
                h("td", { style: { color: "#fff" } }, l.targetTenantOrDomain),
                h("td", null, l.details)
              ))
            )
          )
        )
      )
    ),

    // Provision Tenant Modal
    tenantModal && h("div", { className: "modal-overlay" },
      h("div", { className: "modal-box" },
        h("h3", { style: { fontSize: "1.1rem", fontWeight: 700, marginBottom: "1rem", color: "#fff" } }, "Provision New Tenant"),
        h("div", { style: { display: "flex", flexDirection: "column", gap: "0.75rem" } },
          h("div", null,
            h("label", { style: { fontSize: "0.8rem", color: "#94a3b8" } }, "Organization Name"),
            h("input", { className: "input", placeholder: "Acme Corp", value: newTenant.name, onChange: e => setNewTenant({ ...newTenant, name: e.target.value }) })
          ),
          h("div", null,
            h("label", { style: { fontSize: "0.8rem", color: "#94a3b8" } }, "Subscription Tier"),
            h("select", { className: "input", value: newTenant.subscriptionTier, onChange: e => setNewTenant({ ...newTenant, subscriptionTier: e.target.value }) },
              h("option", { value: "Starter" }, "Starter (50 GB limit)"),
              h("option", { value: "Pro" }, "Pro (250 GB limit)"),
              h("option", { value: "Enterprise" }, "Enterprise (1 TB limit)")
            )
          ),
          h("div", null,
            h("label", { style: { fontSize: "0.8rem", color: "#94a3b8" } }, "Billing Contact Email"),
            h("input", { className: "input", placeholder: "billing@acme.com", value: newTenant.contactEmail, onChange: e => setNewTenant({ ...newTenant, contactEmail: e.target.value }) })
          ),
          h("div", null,
            h("label", { style: { fontSize: "0.8rem", color: "#94a3b8" } }, "Primary Domain Name"),
            h("input", { className: "input", placeholder: "acme.com", value: newTenant.initialDomain, onChange: e => setNewTenant({ ...newTenant, initialDomain: e.target.value }) })
          )
        ),
        h("div", { style: { display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1.25rem" } },
          h("button", { className: "btn btn-secondary", onClick: () => setTenantModal(false) }, "Cancel"),
          h("button", { className: "btn btn-primary", onClick: handleCreateTenant }, "Create Tenant")
        )
      )
    ),

    // Add IP Modal
    ipModal && h("div", { className: "modal-overlay" },
      h("div", { className: "modal-box" },
        h("h3", { style: { fontSize: "1.1rem", fontWeight: 700, marginBottom: "1rem", color: "#fff" } }, "Allocate Outbound Relay IP"),
        h("div", { style: { display: "flex", flexDirection: "column", gap: "0.75rem" } },
          h("div", null,
            h("label", { style: { fontSize: "0.8rem", color: "#94a3b8" } }, "IP Address"),
            h("input", { className: "input", placeholder: "198.51.100.15", value: newIp.ipAddress, onChange: e => setNewIp({ ...newIp, ipAddress: e.target.value }) })
          ),
          h("div", null,
            h("label", { style: { fontSize: "0.8rem", color: "#94a3b8" } }, "PTR Hostname"),
            h("input", { className: "input", placeholder: "mail-relay-02.flitflap.com", value: newIp.hostname, onChange: e => setNewIp({ ...newIp, hostname: e.target.value }) })
          ),
          h("div", null,
            h("label", { style: { fontSize: "0.8rem", color: "#94a3b8" } }, "Pool / Dedicated Tenant"),
            h("input", { className: "input", placeholder: "Global Shared Pool", value: newIp.assignedTenantName, onChange: e => setNewIp({ ...newIp, assignedTenantName: e.target.value }) })
          )
        ),
        h("div", { style: { display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1.25rem" } },
          h("button", { className: "btn btn-secondary", onClick: () => setIpModal(false) }, "Cancel"),
          h("button", { className: "btn btn-primary", onClick: handleAddIp }, "Allocate IP")
        )
      )
    ),

    // Add Abuse Rule Modal
    abuseModal && h("div", { className: "modal-overlay" },
      h("div", { className: "modal-box" },
        h("h3", { style: { fontSize: "1.1rem", fontWeight: 700, marginBottom: "1rem", color: "#fff" } }, "Add Global Abuse / Blacklist Rule"),
        h("div", { style: { display: "flex", flexDirection: "column", gap: "0.75rem" } },
          h("div", null,
            h("label", { style: { fontSize: "0.8rem", color: "#94a3b8" } }, "Rule Type"),
            h("select", { className: "input", value: newRule.ruleType, onChange: e => setNewRule({ ...newRule, ruleType: e.target.value }) },
              h("option", { value: "DomainBlacklist" }, "Domain Blacklist"),
              h("option", { value: "TldBlacklist" }, "TLD Blacklist"),
              h("option", { value: "IpBlacklist" }, "Sender IP Blacklist")
            )
          ),
          h("div", null,
            h("label", { style: { fontSize: "0.8rem", color: "#94a3b8" } }, "Pattern (e.g. *.spammer.com or .top)"),
            h("input", { className: "input", placeholder: "*.spammer.com", value: newRule.pattern, onChange: e => setNewRule({ ...newRule, pattern: e.target.value }) })
          ),
          h("div", null,
            h("label", { style: { fontSize: "0.8rem", color: "#94a3b8" } }, "Reason"),
            h("input", { className: "input", placeholder: "High complaint / bounce rate", value: newRule.reason, onChange: e => setNewRule({ ...newRule, reason: e.target.value }) })
          )
        ),
        h("div", { style: { display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1.25rem" } },
          h("button", { className: "btn btn-secondary", onClick: () => setAbuseModal(false) }, "Cancel"),
          h("button", { className: "btn btn-primary", onClick: handleAddRule }, "Save Rule")
        )
      )
    )
  );
}

const root = ReactDOM.createRoot(document.getElementById("root"));
root.render(h(AdminApp));

