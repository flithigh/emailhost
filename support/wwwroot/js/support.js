// Flitflap Staff Support Console - React 18 Application
const { useState, useEffect, useCallback } = React;
const h = React.createElement;

function SupportApp() {
  const [tab, setTab] = useState("tracer"); // tracer | dns | tickets | audit
  const [traces, setTraces] = useState([]);
  const [selectedTraceId, setSelectedTraceId] = useState(null);
  const [traceDetail, setTraceDetail] = useState(null);
  const [searchQuery, setSearchQuery] = useState("");
  const [loading, setLoading] = useState(false);

  // DNS Inspector state
  const [diagnoseDomain, setDiagnoseDomain] = useState("flitflap.com");
  const [dnsResult, setDnsResult] = useState(null);
  const [dnsLoading, setDnsLoading] = useState(false);

  // Tickets state
  const [tickets, setTickets] = useState([]);
  const [ticketModal, setTicketModal] = useState(false);
  const [newTicket, setNewTicket] = useState({ domainName: "", contactEmail: "", subject: "", category: "DNS / Deliverability", priority: "Medium", description: "" });
  const [resolveModal, setResolveModal] = useState(null);
  const [resolveNotes, setResolveNotes] = useState("");

  // Quota adjustment state
  const [quotaAddress, setQuotaAddress] = useState("");
  const [quotaResult, setQuotaResult] = useState(null);
  const [quotaMsg, setQuotaMsg] = useState("");

  // Audit logs state
  const [auditLogs, setAuditLogs] = useState([]);

  // Load Initial Traces & Tickets
  const loadTraces = useCallback(async (q = "") => {
    setLoading(true);
    try {
      const url = q ? `/api/support/tracer?query=${encodeURIComponent(q)}` : "/api/support/tracer";
      const res = await fetch(url);
      if (res.ok) {
        const data = await res.json();
        setTraces(data);
        if (data.length > 0 && !selectedTraceId) {
          selectTrace(data[0].messageId);
        }
      }
    } finally {
      setLoading(false);
    }
  }, [selectedTraceId]);

  const selectTrace = async (msgId) => {
    setSelectedTraceId(msgId);
    try {
      const res = await fetch(`/api/support/tracer/${encodeURIComponent(msgId)}`);
      if (res.ok) setTraceDetail(await res.json());
    } catch (e) {
      console.warn("Trace detail error", e);
    }
  };

  const loadTickets = useCallback(async () => {
    const res = await fetch("/api/support/tickets");
    if (res.ok) setTickets(await res.json());
  }, []);

  const loadAuditLogs = useCallback(async () => {
    const res = await fetch("/api/support/audit-logs");
    if (res.ok) setAuditLogs(await res.json());
  }, []);

  useEffect(() => {
    loadTraces();
    loadTickets();
    loadAuditLogs();
  }, [loadTraces, loadTickets, loadAuditLogs]);

  // DNS Diagnostic
  const handleDiagnoseDns = async () => {
    if (!diagnoseDomain) return;
    setDnsLoading(true);
    try {
      const res = await fetch(`/api/support/dns/diagnose?domain=${encodeURIComponent(diagnoseDomain)}`);
      if (res.ok) setDnsResult(await res.json());
    } finally {
      setDnsLoading(false);
    }
  };

  // Quota lookup & grant
  const handleLookupQuota = async () => {
    if (!quotaAddress) return;
    setQuotaMsg("");
    const res = await fetch(`/api/support/mailbox/quota?address=${encodeURIComponent(quotaAddress)}`);
    if (res.ok) setQuotaResult(await res.json());
    else setQuotaMsg("Mailbox address not found.");
  };

  const handleGrantHeadroom = async (bytes, label) => {
    if (!quotaAddress) return;
    const res = await fetch("/api/support/mailbox/quota/adjust", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        address: quotaAddress,
        additionalBytes: bytes,
        reason: `Support granted ${label} temporary headroom`
      })
    });
    if (res.ok) {
      setQuotaMsg(`Successfully granted ${label} quota headroom!`);
      handleLookupQuota();
      loadAuditLogs();
    }
  };

  // Ticket create & resolve
  const handleCreateTicket = async () => {
    if (!newTicket.subject || !newTicket.domainName) return;
    await fetch("/api/support/tickets", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify(newTicket)
    });
    setTicketModal(false);
    setNewTicket({ domainName: "", contactEmail: "", subject: "", category: "DNS / Deliverability", priority: "Medium", description: "" });
    loadTickets();
    loadAuditLogs();
  };

  const handleResolveTicket = async () => {
    if (!resolveModal) return;
    await fetch(`/api/support/tickets/${resolveModal.id}/resolve`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ resolutionNotes: resolveNotes || "Issue resolved by support engineer." })
    });
    setResolveModal(null);
    setResolveNotes("");
    loadTickets();
    loadAuditLogs();
  };

  return h("div", null,
    // Header
    h("header", { className: "support-header" },
      h("div", { style: { display: "flex", alignItems: "center", gap: "1rem" } },
        h("div", { style: { fontWeight: 800, fontSize: "1.15rem", display: "flex", alignItems: "center", gap: "0.5rem" } },
          h("span", { style: { color: "#38bdf8" } }, "🎧"),
          h("span", { style: { background: "linear-gradient(90deg, #38bdf8, #818cf8)", WebkitBackgroundClip: "text", WebkitTextFillColor: "transparent" } }, "Flitflap Support Desk"),
          h("span", { style: { fontSize: "0.7rem", padding: "2px 6px", borderRadius: "4px", background: "rgba(56, 189, 248, 0.15)", color: "#38bdf8" } }, "Diagnostics & Compliance")
        ),

        // Nav tabs
        h("nav", { style: { display: "flex", gap: "0.4rem" } },
          [
            { id: "tracer", label: "Mail Tracer (Delivery Diagnostics)" },
            { id: "dns", label: "Tenant DNS Inspector" },
            { id: "tickets", label: `Tickets & Quota Triage (${tickets.filter(t => t.status === "Open").length})` },
            { id: "audit", label: "Staff Audit Trail" }
          ].map(t => h("button", {
            key: t.id,
            className: `nav-tab ${tab === t.id ? "active" : ""}`,
            onClick: () => setTab(t.id)
          }, t.label))
        )
      ),

      h("div", { style: { display: "flex", alignItems: "center", gap: "0.6rem" } },
        h("button", { className: "btn btn-secondary", onClick: () => { loadTraces(); loadTickets(); loadAuditLogs(); } }, "Refresh Data")
      )
    ),

    // Main Content
    h("main", { style: { padding: "1.5rem 2rem", maxWidth: "1400px", margin: "0 auto" } },
      // Tab 1: Mail Tracer
      tab === "tracer" && h("div", null,
        h("div", { style: { display: "flex", gap: "0.5rem", marginBottom: "1rem", maxWidth: "600px" } },
          h("input", {
            className: "input",
            placeholder: "Search trace by Message-ID, Sender, or Recipient...",
            value: searchQuery,
            onChange: e => setSearchQuery(e.target.value),
            onKeyDown: e => { if (e.key === "Enter") loadTraces(searchQuery); }
          }),
          h("button", { className: "btn btn-primary", onClick: () => loadTraces(searchQuery) }, "Trace")
        ),

        h("div", { style: { display: "grid", gridTemplateColumns: "380px 1fr", gap: "1.5rem", minHeight: "600px" } },
          // Left: Traces list
          h("div", { className: "card", style: { padding: "0.5rem", display: "flex", flexDirection: "column" } },
            h("div", { style: { padding: "0.75rem", borderBottom: "1px solid var(--border)", fontWeight: 600, fontSize: "0.85rem", color: "#94a3b8" } },
              `Recent Delivery Events (${traces.length})`
            ),
            h("div", { style: { flex: 1, overflowY: "auto", maxHeight: "650px" } },
              traces.length === 0 ? h("div", { style: { padding: "2rem", textAlign: "center", color: "#64748b" } }, "No traces recorded yet.")
              : traces.map(t => h("div", {
                  key: t.id,
                  style: {
                    padding: "0.85rem", borderBottom: "1px solid rgba(255,255,255,0.05)", cursor: "pointer",
                    background: selectedTraceId === t.messageId ? "rgba(56, 189, 248, 0.12)" : "transparent"
                  },
                  onClick: () => selectTrace(t.messageId)
                },
                  h("div", { style: { display: "flex", justifyContent: "space-between", marginBottom: "0.25rem" } },
                    h("span", { className: `badge badge-${t.eventStatus.toLowerCase()}` }, t.eventStatus),
                    h("span", { style: { fontSize: "0.75rem", color: "#64748b" } }, new Date(t.timestampUtc).toLocaleTimeString())
                  ),
                  h("div", { style: { fontWeight: 600, fontSize: "0.85rem", color: "#fff", textOverflow: "ellipsis", overflow: "hidden", whiteSpace: "nowrap" } },
                    t.messageId
                  ),
                  h("div", { style: { fontSize: "0.75rem", color: "#94a3b8", marginTop: "0.2rem" } },
                    `From: ${t.sender} → ${t.recipient}`
                  )
                ))
            )
          ),

          // Right: Detailed Hop Timeline
          h("div", { className: "card" },
            traceDetail ? h("div", null,
              h("div", { style: { borderBottom: "1px solid var(--border)", paddingBottom: "1rem", marginBottom: "1.5rem" } },
                h("div", { style: { display: "flex", justifyContent: "space-between", alignItems: "flex-start" } },
                  h("div", null,
                    h("div", { style: { fontSize: "0.8rem", color: "#94a3b8" } }, "Message-ID (RFC 822)"),
                    h("h2", { style: { fontSize: "1.2rem", fontWeight: 700, color: "#fff" } }, traceDetail.messageId)
                  ),
                  h("span", { className: `badge badge-${traceDetail.finalStatus.toLowerCase()}`, style: { fontSize: "0.85rem", padding: "4px 12px" } },
                    traceDetail.finalStatus
                  )
                ),
                h("div", { style: { display: "flex", gap: "1.5rem", marginTop: "1rem", fontSize: "0.85rem" } },
                  h("div", null, h("b", { style: { color: "#94a3b8" } }, "Sender: "), traceDetail.sender),
                  h("div", null, h("b", { style: { color: "#94a3b8" } }, "Recipient: "), traceDetail.recipient),
                  h("div", null, h("b", { style: { color: "#94a3b8" } }, "Client IP: "), traceDetail.clientIp)
                )
              ),

              // Visual Hop Progression
              h("h3", { style: { fontSize: "1rem", fontWeight: 600, marginBottom: "1.25rem", color: "#38bdf8" } }, "Delivery Hops Timeline"),
              h("div", null,
                traceDetail.hops.map((hop, idx) => h("div", { key: idx, className: "timeline-hop" },
                  h("div", { className: "hop-node" }, idx + 1),
                  h("div", { style: { background: "#0b1120", border: "1px solid var(--border)", padding: "0.9rem", borderRadius: "8px", flex: 1 } },
                    h("div", { style: { display: "flex", justifyContent: "space-between", marginBottom: "0.3rem" } },
                      h("span", { style: { fontWeight: 700, color: "#fff" } }, hop.stage),
                      h("span", { style: { fontSize: "0.75rem", color: "#64748b" } }, new Date(hop.timestampUtc).toLocaleTimeString())
                    ),
                    h("div", { style: { fontSize: "0.8rem", color: "#cbd5e1" } }, hop.details)
                  )
                ))
              )
            )
            : h("div", { style: { display: "flex", height: "100%", alignItems: "center", justifyContent: "center", color: "#64748b" } },
                "Select a message trace from the left panel to inspect the hop-by-hop pipeline."
              )
          )
        )
      ),

      // Tab 2: Tenant & DNS Inspector
      tab === "dns" && h("div", null,
        h("div", { style: { display: "flex", gap: "0.5rem", marginBottom: "1.5rem", maxWidth: "500px" } },
          h("input", {
            className: "input",
            placeholder: "Enter domain (e.g. flitflap.com or client.com)",
            value: diagnoseDomain,
            onChange: e => setDiagnoseDomain(e.target.value)
          }),
          h("button", { className: "btn btn-primary", onClick: handleDiagnoseDns, disabled: dnsLoading },
            dnsLoading ? "Querying..." : "Run DNS Diagnostic"
          )
        ),

        dnsResult && h("div", null,
          h("div", { className: "card", style: { marginBottom: "1.5rem" } },
            h("div", { style: { display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1rem" } },
              h("h3", { style: { fontSize: "1.1rem", fontWeight: 700, color: "#fff" } }, `Live DNS Health: ${dnsResult.domainName}`),
              h("span", { className: `badge ${dnsResult.overallHealthy ? "badge-delivered" : "badge-bounced"}` },
                dnsResult.overallHealthy ? "✔ All Critical Records Active" : "⚠️ Records Require Configuration"
              )
            ),

            h("table", { className: "data-table" },
              h("thead", null,
                h("tr", null,
                  h("th", null, "Record Type"),
                  h("th", null, "Hostname / Node"),
                  h("th", null, "Status"),
                  h("th", null, "Discovered Value"),
                  h("th", null, "Expected Flitflap Configuration")
                )
              ),
              h("tbody", null,
                dnsResult.records.map((r, i) => h("tr", { key: i },
                  h("td", { style: { fontWeight: 700, color: "#38bdf8" } }, r.recordType),
                  h("td", null, r.hostname),
                  h("td", null, h("span", { className: `badge ${r.isValid ? "badge-delivered" : "badge-bounced"}` }, r.isValid ? "PASS" : "FAIL")),
                  h("td", { style: { color: "#cbd5e1" } }, r.foundValue),
                  h("td", { style: { color: "#94a3b8" } }, r.expectedValue)
                ))
              )
            )
          ),

          // Assisted Copy-Paste Setup
          h("div", { style: { display: "grid", gridTemplateColumns: "1fr 1fr", gap: "1.5rem" } },
            h("div", { className: "card" },
              h("h4", { style: { fontSize: "0.95rem", fontWeight: 700, marginBottom: "0.5rem", color: "#38bdf8" } }, "Cloudflare DNS Quick-Fix Instructions"),
              h("pre", { style: { background: "#0b1120", padding: "1rem", borderRadius: "8px", fontSize: "0.8rem", color: "#a5b4fc", whiteSpace: "pre-wrap" } },
                dnsResult.cloudflareInstructions
              )
            ),
            h("div", { className: "card" },
              h("h4", { style: { fontSize: "0.95rem", fontWeight: 700, marginBottom: "0.5rem", color: "#38bdf8" } }, "GoDaddy / Namecheap DNS Quick-Fix Instructions"),
              h("pre", { style: { background: "#0b1120", padding: "1rem", borderRadius: "8px", fontSize: "0.8rem", color: "#a5b4fc", whiteSpace: "pre-wrap" } },
                dnsResult.goDaddyInstructions
              )
            )
          )
        )
      ),

      // Tab 3: Tickets & Quota Triage
      tab === "tickets" && h("div", null,
        h("div", { style: { display: "grid", gridTemplateColumns: "1fr 340px", gap: "1.5rem" } },
          // Left: Ticket queue
          h("div", null,
            h("div", { style: { display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1rem" } },
              h("h2", { style: { fontSize: "1.2rem", fontWeight: 700 } }, "Customer Support Ticket Queue"),
              h("button", { className: "btn btn-primary", onClick: () => setTicketModal(true) }, "+ Create Ticket")
            ),

            h("div", { className: "card" },
              h("table", { className: "data-table" },
                h("thead", null,
                  h("tr", null,
                    h("th", null, "Ticket #"),
                    h("th", null, "Domain"),
                    h("th", null, "Subject"),
                    h("th", null, "Category"),
                    h("th", null, "Priority"),
                    h("th", null, "Status"),
                    h("th", null, "Action")
                  )
                ),
                h("tbody", null,
                  tickets.map(t => h("tr", { key: t.id },
                    h("td", { style: { fontWeight: 700, color: "#38bdf8" } }, t.ticketNumber),
                    h("td", { style: { color: "#fff" } }, t.domainName),
                    h("td", null, t.subject),
                    h("td", null, t.category),
                    h("td", null, h("span", { className: `badge ${t.priority === "Urgent" ? "badge-bounced" : "badge-deferred"}` }, t.priority)),
                    h("td", null, h("span", { className: `badge ${t.status === "Resolved" ? "badge-delivered" : "badge-queued"}` }, t.status)),
                    h("td", null,
                      t.status !== "Resolved" && h("button", {
                        className: "btn btn-secondary",
                        style: { padding: "2px 8px", fontSize: "0.75rem" },
                        onClick: () => setResolveModal(t)
                      }, "Resolve")
                    )
                  ))
                )
              )
            )
          ),

          // Right: Quota Headroom Tool
          h("div", { className: "card", style: { alignSelf: "flex-start" } },
            h("h3", { style: { fontSize: "1rem", fontWeight: 700, marginBottom: "0.75rem", color: "#38bdf8" } }, "Mailbox Quota Adjustment"),
            h("p", { style: { fontSize: "0.8rem", color: "#94a3b8", marginBottom: "1rem" } },
              "Grant emergency quota expansion for tenants experiencing delivery rejections."
            ),

            h("div", { style: { display: "flex", flexDirection: "column", gap: "0.6rem" } },
              h("input", { className: "input", placeholder: "user@domain.com", value: quotaAddress, onChange: e => setQuotaAddress(e.target.value) }),
              h("button", { className: "btn btn-secondary", onClick: handleLookupQuota }, "Look Up Quota")
            ),

            quotaMsg && h("div", { style: { fontSize: "0.8rem", color: "#34d399", marginTop: "0.5rem" } }, quotaMsg),

            quotaResult && h("div", { style: { marginTop: "1rem", borderTop: "1px solid var(--border)", paddingTop: "0.75rem" } },
              h("div", { style: { display: "flex", justifyContent: "space-between", fontSize: "0.85rem", marginBottom: "0.4rem" } },
                h("span", null, "Used:"),
                h("b", null, `${Math.round(quotaResult.usedBytes / 1024 / 1024)} MB (${quotaResult.percentUsed}%)`)
              ),
              h("div", { style: { display: "flex", justifyContent: "space-between", fontSize: "0.85rem", marginBottom: "1rem" } },
                h("span", null, "Limit:"),
                h("b", null, `${Math.round(quotaResult.quotaBytes / (1024 * 1024 * 1024))} GB`)
              ),

              h("div", { style: { display: "flex", gap: "0.5rem" } },
                h("button", { className: "btn btn-success", style: { flex: 1, fontSize: "0.75rem" }, onClick: () => handleGrantHeadroom(2L * 1024L * 1024L * 1024L, "+2 GB") }, "+2 GB Headroom"),
                h("button", { className: "btn btn-success", style: { flex: 1, fontSize: "0.75rem" }, onClick: () => handleGrantHeadroom(5L * 1024L * 1024L * 1024L, "+5 GB") }, "+5 GB Headroom")
              )
            )
          )
        )
      ),

      // Tab 4: Staff Audit Trail
      tab === "audit" && h("div", null,
        h("h2", { style: { fontSize: "1.2rem", fontWeight: 700, marginBottom: "1rem" } }, "Staff Audit Trail (Compliance Log)"),
        h("div", { className: "card" },
          h("table", { className: "data-table" },
            h("thead", null,
              h("tr", null,
                h("th", null, "Timestamp (UTC)"),
                h("th", null, "Staff Operator"),
                h("th", null, "Action"),
                h("th", null, "Target Domain / Mailbox"),
                h("th", null, "Details")
              )
            ),
            h("tbody", null,
              auditLogs.map(l => h("tr", { key: l.id },
                h("td", { style: { color: "#94a3b8" } }, new Date(l.timestampUtc).toLocaleString()),
                h("td", { style: { fontWeight: 600, color: "#38bdf8" } }, l.staffUser),
                h("td", null, h("span", { className: "badge badge-queued" }, l.action)),
                h("td", { style: { color: "#fff" } }, l.targetTenantOrDomain),
                h("td", null, l.details)
              ))
            )
          )
        )
      )
    ),

    // Ticket Create Modal
    ticketModal && h("div", { className: "modal-overlay" },
      h("div", { className: "modal-box" },
        h("h3", { style: { fontSize: "1.1rem", fontWeight: 700, marginBottom: "1rem", color: "#fff" } }, "Open Customer Support Ticket"),
        h("div", { style: { display: "flex", flexDirection: "column", gap: "0.6rem" } },
          h("input", { className: "input", placeholder: "Domain Name (e.g. client.com)", value: newTicket.domainName, onChange: e => setNewTicket({ ...newTicket, domainName: e.target.value }) }),
          h("input", { className: "input", placeholder: "Customer Contact Email", value: newTicket.contactEmail, onChange: e => setNewTicket({ ...newTicket, contactEmail: e.target.value }) }),
          h("input", { className: "input", placeholder: "Issue Subject", value: newTicket.subject, onChange: e => setNewTicket({ ...newTicket, subject: e.target.value }) }),
          h("select", { className: "input", value: newTicket.category, onChange: e => setNewTicket({ ...newTicket, category: e.target.value }) },
            h("option", { value: "DNS / Deliverability" }, "DNS / Deliverability"),
            h("option", { value: "Mailbox Quota" }, "Mailbox Quota"),
            h("option", { value: "Authentication" }, "Authentication"),
            h("option", { value: "Bounced Mail" }, "Bounced Mail")
          ),
          h("select", { className: "input", value: newTicket.priority, onChange: e => setNewTicket({ ...newTicket, priority: e.target.value }) },
            h("option", { value: "Low" }, "Low Priority"),
            h("option", { value: "Medium" }, "Medium Priority"),
            h("option", { value: "High" }, "High Priority"),
            h("option", { value: "Urgent" }, "Urgent Priority")
          ),
          h("textarea", { className: "input", style: { height: "90px" }, placeholder: "Issue description and reproduction steps...", value: newTicket.description, onChange: e => setNewTicket({ ...newTicket, description: e.target.value }) })
        ),
        h("div", { style: { display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" } },
          h("button", { className: "btn btn-secondary", onClick: () => setTicketModal(false) }, "Cancel"),
          h("button", { className: "btn btn-primary", onClick: handleCreateTicket }, "Open Ticket")
        )
      )
    ),

    // Resolve Ticket Modal
    resolveModal && h("div", { className: "modal-overlay" },
      h("div", { className: "modal-box" },
        h("h3", { style: { fontSize: "1.1rem", fontWeight: 700, marginBottom: "0.5rem", color: "#fff" } }, `Resolve Ticket: ${resolveModal.ticketNumber}`),
        h("p", { style: { fontSize: "0.85rem", color: "#94a3b8", marginBottom: "1rem" } }, resolveModal.subject),
        h("textarea", { className: "input", style: { height: "100px" }, placeholder: "Document resolution actions taken...", value: resolveNotes, onChange: e => setResolveNotes(e.target.value) }),
        h("div", { style: { display: "flex", justifyContent: "flex-end", gap: "0.5rem", marginTop: "1rem" } },
          h("button", { className: "btn btn-secondary", onClick: () => setResolveModal(null) }, "Cancel"),
          h("button", { className: "btn btn-primary", onClick: handleResolveTicket }, "Confirm Resolution")
        )
      )
    )
  );
}

const root = ReactDOM.createRoot(document.getElementById("root"));
root.render(h(SupportApp));

