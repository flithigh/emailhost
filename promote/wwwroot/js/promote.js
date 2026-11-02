// Flitflap Promote & Conversion Application - React 18
const { useState, useEffect } = React;
const h = React.createElement;

function PromoteApp() {
  // Pricing Calculator State
  const [domains, setDomains] = useState(2);
  const [mailboxes, setMailboxes] = useState(15);
  const [tier, setTier] = useState("Pro"); // Starter, Pro, Enterprise
  const [annualBilling, setAnnualBilling] = useState(true);

  // Deliverability Test Tool State
  const [testDomain, setTestDomain] = useState("google.com");
  const [testResult, setTestResult] = useState(null);
  const [testLoading, setTestLoading] = useState(false);

  // Signup Modal State
  const [signupModal, setSignupModal] = useState(false);
  const [signupForm, setSignupForm] = useState({ organizationName: "", domainName: "", adminEmail: "", password: "", selectedTier: "Pro" });
  const [signupLoading, setSignupLoading] = useState(false);
  const [signupSuccess, setSignupSuccess] = useState(null);
  const [signupError, setSignupError] = useState("");

  // Calculate Price
  const basePrice = tier === "Enterprise" ? 149 : tier === "Pro" ? 49 : 19;
  const includedDomains = tier === "Enterprise" ? 20 : tier === "Pro" ? 5 : 1;
  const includedBoxes = tier === "Enterprise" ? 250 : tier === "Pro" ? 50 : 10;

  const extraDomains = Math.max(0, domains - includedDomains);
  const extraBoxes = Math.max(0, mailboxes - includedBoxes);

  const rawMonthly = basePrice + (extraDomains * 5.0) + (extraBoxes * 1.5);
  const finalPrice = annualBilling ? Math.round(rawMonthly * 0.8) : Math.round(rawMonthly);

  // Deliverability Test Handler
  const handleRunDeliverabilityTest = async () => {
    if (!testDomain) return;
    setTestLoading(true);
    setTestResult(null);
    try {
      const res = await fetch(`/api/promote/deliverability?domain=${encodeURIComponent(testDomain)}`);
      if (res.ok) {
        setTestResult(await res.json());
      }
    } catch (e) {
      console.error(e);
    } finally {
      setTestLoading(false);
    }
  };

  // Signup Handler
  const handleSignup = async (e) => {
    if (e) e.preventDefault();
    setSignupLoading(true);
    setSignupError("");
    try {
      const res = await fetch("/api/promote/signup", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ ...signupForm, selectedTier: tier })
      });
      if (res.ok) {
        setSignupSuccess(await res.json());
      } else {
        const err = await res.json();
        setSignupError(err.message || "Signup failed. Please try again.");
      }
    } catch (err) {
      setSignupError(err.message);
    } finally {
      setSignupLoading(false);
    }
  };

  return h("div", null,
    // Header
    h("header", { className: "landing-header" },
      h("div", { className: "logo-text" },
        h("span", null, "⚡"),
        h("span", null, "Flitflap Email Host")
      ),
      h("nav", { className: "nav-links" },
        h("a", { className: "nav-link", href: "#features" }, "Features"),
        h("a", { className: "nav-link", href: "#calculator" }, "Pricing Calculator"),
        h("a", { className: "nav-link", href: "#deliverability" }, "Free Deliverability Tool"),
        h("a", { className: "nav-link", href: "http://localhost:5002", target: "_blank" }, "Webmail Login"),
        h("a", { className: "nav-link", href: "http://localhost:5001", target: "_blank" }, "Operator Admin")
      ),
      h("button", { className: "btn btn-primary", onClick: () => setSignupModal(true) }, "Get Started Free")
    ),

    // Hero Section
    h("section", { className: "hero-section" },
      h("div", { className: "hero-badge" },
        h("span", null, "★"),
        h("span", null, "Enterprise Email Architecture (doc.md Specification)")
      ),
      h("h1", { className: "hero-title" },
        "Next-Generation Cloud Email Hosting & Delivery"
      ),
      h("p", { className: "hero-subtitle" },
        "Distributed multi-tenant email platform powered by PostgreSQL dual-tier isolation, Garage S3 object storage, and automated 2048-bit RSA DKIM cryptographic signing."
      ),
      h("div", { style: { display: "flex", gap: "1rem", justifyContent: "center", marginBottom: "3rem" } },
        h("button", { className: "btn btn-primary", style: { padding: "0.85rem 2rem", fontSize: "1rem" }, onClick: () => setSignupModal(true) }, "Start 14-Day Free Trial"),
        h("a", { className: "btn btn-secondary", style: { padding: "0.85rem 2rem", fontSize: "1rem", textDecoration: "none" }, href: "#deliverability" }, "Test Your Domain Health")
      ),

      // SLA & Tech badges
      h("div", { style: { display: "flex", justifyContent: "center", gap: "2.5rem", color: "#94a3b8", fontSize: "0.85rem" } },
        h("div", null, "🔒 2048-bit RSA DKIM & DMARC"),
        h("div", null, "📦 Garage S3 NVMe Mail Store"),
        h("div", null, "⚡ 99.99% High Availability SLA"),
        h("div", null, "🛡️ ClamAV & Heuristic Spam Filter")
      )
    ),

    // Product Features
    h("section", { id: "features", className: "section" },
      h("div", { className: "section-header" },
        h("h2", { className: "section-title" }, "Engineered for Extreme Reliability & Scale"),
        h("p", { className: "section-subtitle" }, "Separation of internal operator tools and consumer email clients.")
      ),

      h("div", { className: "feature-grid" },
        [
          { icon: "🗄️", title: "Dual-Tier PostgreSQL Storage", desc: "Global Master database isolates customer metadata while sharded tenant databases store mailboxes, indexes, and trace logs." },
          { icon: "📦", title: "Garage S3 Object Storage", desc: "Raw RFC822 MIME payloads and heavy attachments stream from distributed Garage S3 clusters, keeping databases lightning fast." },
          { icon: "🔑", title: "Automated 2048-bit RSA DKIM", desc: "Outbound emails are cryptographically signed with tenant RSA keys, and inbound DKIM signatures are verified in real time." },
          { icon: "🛡️", title: "DMARC & Anti-Spam Pipeline", desc: "Strict DMARC policy enforcement (reject/quarantine), heuristic spam scoring, and ClamAV TCP daemon attachment scanning." },
          { icon: "🚀", title: "RFC 822 Mail Tracer", desc: "End-to-end hop diagnostic tracer providing complete visibility into acceptance, queue timings, and delivery status." },
          { icon: "✉️", title: "Modern Glassmorphism Webmail", desc: "React 18 single-page application with rich HTML composer, real-time unread badges, address book, and signature manager." }
        ].map((f, i) => h("div", { key: i, className: "feature-card" },
          h("div", { className: "feature-icon" }, f.icon),
          h("h3", { style: { fontSize: "1.2rem", fontWeight: 700, marginBottom: "0.5rem" } }, f.title),
          h("p", { style: { color: "#94a3b8", fontSize: "0.9rem" } }, f.desc)
        ))
      )
    ),

    // Interactive Pricing Calculator
    h("section", { id: "calculator", className: "section" },
      h("div", { className: "section-header" },
        h("h2", { className: "section-title" }, "Interactive Pricing Calculator"),
        h("p", { className: "section-subtitle" }, "Transparent, predictable pricing based on your organization's exact needs.")
      ),

      h("div", { className: "pricing-container" },
        // Sliders
        h("div", null,
          h("div", { className: "slider-group" },
            h("div", { className: "slider-label" },
              h("span", null, "Hosted Domains:"),
              h("span", { style: { color: "#38bdf8" } }, `${domains} Domains`)
            ),
            h("input", {
              type: "range", min: 1, max: 25, value: domains, className: "slider-input",
              onChange: e => setDomains(parseInt(e.target.value))
            })
          ),

          h("div", { className: "slider-group" },
            h("div", { className: "slider-label" },
              h("span", null, "Active Mailboxes:"),
              h("span", { style: { color: "#38bdf8" } }, `${mailboxes} Mailboxes`)
            ),
            h("input", {
              type: "range", min: 1, max: 200, value: mailboxes, className: "slider-input",
              onChange: e => setMailboxes(parseInt(e.target.value))
            })
          ),

          h("div", { style: { marginBottom: "1.5rem" } },
            h("div", { style: { fontWeight: 600, marginBottom: "0.5rem" } }, "Storage & Reliability Tier:"),
            h("div", { style: { display: "flex", gap: "0.5rem" } },
              ["Starter", "Pro", "Enterprise"].map(t => h("button", {
                key: t,
                className: `btn ${tier === t ? "btn-primary" : "btn-secondary"}`,
                style: { flex: 1, fontSize: "0.85rem" },
                onClick: () => setTier(t)
              }, t))
            )
          ),

          h("div", { style: { display: "flex", alignItems: "center", gap: "0.5rem", fontSize: "0.85rem" } },
            h("input", { type: "checkbox", checked: annualBilling, onChange: e => setAnnualBilling(e.target.checked) }),
            h("span", null, "Annual billing (Save 20% on monthly charges)")
          )
        ),

        // Price Output
        h("div", { style: { display: "flex", flexDirection: "column", justifyContent: "center", borderLeft: "1px solid var(--border)", paddingLeft: "2.5rem" } },
          h("div", { style: { fontSize: "0.9rem", color: "#94a3b8", textTransform: "uppercase", letterSpacing: "0.05em" } }, `${tier} Plan Estimated Cost`),
          h("div", { style: { fontSize: "3.5rem", fontWeight: 800, color: "#fff", margin: "0.5rem 0" } },
            `$${finalPrice}`,
            h("span", { style: { fontSize: "1rem", color: "#94a3b8", fontWeight: 400 } }, "/month")
          ),
          h("ul", { style: { listStyle: "none", color: "#cbd5e1", fontSize: "0.85rem", lineHeight: 1.8, marginBottom: "1.5rem" } },
            h("li", null, `✔ ${tier === "Enterprise" ? "1 TB" : tier === "Pro" ? "250 GB" : "50 GB"} NVMe Garage S3 Storage`),
            h("li", null, `✔ ${domains} Custom Domains with Automated DNS`),
            h("li", null, `✔ ${mailboxes} Full-Featured Mailbox Accounts`),
            h("li", null, "✔ 2048-bit RSA DKIM, SPF & DMARC Enforced"),
            h("li", null, "✔ 99.99% Uptime SLA Guarantee")
          ),
          h("button", { className: "btn btn-primary", style: { padding: "0.85rem" }, onClick: () => setSignupModal(true) }, `Choose ${tier} Plan`)
        )
      )
    ),

    // Free Deliverability & MX Tool
    h("section", { id: "deliverability", className: "section" },
      h("div", { className: "section-header" },
        h("h2", { className: "section-title" }, "Free Domain Deliverability & MX Test Tool"),
        h("p", { className: "section-subtitle" }, "Instant DNS diagnostic for MX records, SPF syntax, DKIM keys, and DMARC alignment.")
      ),

      h("div", { className: "card", style: { maxWidth: "800px", margin: "0 auto", padding: "2.5rem" } },
        h("div", { style: { display: "flex", gap: "0.5rem", marginBottom: "1.5rem" } },
          h("input", {
            className: "input",
            placeholder: "Enter domain name (e.g. yourcompany.com or google.com)",
            value: testDomain,
            onChange: e => setTestDomain(e.target.value),
            style: { margin: 0 }
          }),
          h("button", { className: "btn btn-primary", onClick: handleRunDeliverabilityTest, disabled: testLoading },
            testLoading ? "Analyzing..." : "Analyze DNS Health"
          )
        ),

        testResult && h("div", null,
          h("div", { style: { display: "flex", justifyContent: "space-between", alignItems: "center", borderBottom: "1px solid var(--border)", paddingBottom: "1rem", marginBottom: "1.5rem" } },
            h("div", null,
              h("div", { style: { fontSize: "0.85rem", color: "#94a3b8" } }, `Domain Health for ${testResult.domain}`),
              h("div", { style: { fontSize: "1.5rem", fontWeight: 700, color: "#fff" } }, `${testResult.healthRating} (${testResult.overallScore}/100)`)
            ),
            h("div", {
              style: {
                width: 56, height: 56, borderRadius: "50%",
                background: testResult.overallScore >= 75 ? "#10b981" : testResult.overallScore >= 40 ? "#f59e0b" : "#ef4444",
                display: "flex", alignItems: "center", justifyContent: "center",
                fontWeight: 800, fontSize: "1.2rem", color: "#fff"
              }
            }, `${testResult.overallScore}`)
          ),

          h("div", { style: { display: "flex", flexDirection: "column", gap: "0.75rem", marginBottom: "1.5rem" } },
            testResult.checks.map((c, i) => h("div", {
              key: i,
              style: { background: "#080d1a", border: "1px solid var(--border)", padding: "0.9rem", borderRadius: "8px" }
            },
              h("div", { style: { display: "flex", justifyContent: "space-between", marginBottom: "0.3rem" } },
                h("span", { style: { fontWeight: 700, color: "#fff" } }, c.check),
                h("span", { style: { color: c.passed ? "#34d399" : "#f87171", fontWeight: 600, fontSize: "0.8rem" } }, c.passed ? "✔ PASS" : "✖ ACTION REQUIRED")
              ),
              h("div", { style: { fontSize: "0.8rem", color: "#94a3b8", marginBottom: "0.2rem" } }, `Found: ${c.discovered}`),
              h("div", { style: { fontSize: "0.75rem", color: "#cbd5e1" } }, c.advice)
            ))
          ),

          h("div", { style: { background: "rgba(56, 189, 248, 0.1)", border: "1px solid rgba(56, 189, 248, 0.3)", padding: "1rem", borderRadius: "8px", color: "#38bdf8", fontSize: "0.85rem", textAlign: "center" } },
            testResult.summaryRecommendation
          )
        )
      )
    ),

    // Footer
    h("footer", { style: { borderTop: "1px solid var(--border)", padding: "3rem 4rem", background: "#060913", textAlign: "center", color: "#64748b", fontSize: "0.85rem" } },
      h("div", { style: { marginBottom: "1rem" } },
        "Flitflap Email Host Ecosystem: ",
        h("a", { href: "http://localhost:5000", style: { color: "#38bdf8", margin: "0 0.5rem" } }, "Core SMTP MTA"),
        h("a", { href: "http://localhost:5001", style: { color: "#38bdf8", margin: "0 0.5rem" } }, "Super Admin"),
        h("a", { href: "http://localhost:5002", style: { color: "#38bdf8", margin: "0 0.5rem" } }, "Webmail Client"),
        h("a", { href: "http://localhost:5003", style: { color: "#38bdf8", margin: "0 0.5rem" } }, "Staff Support Desk")
      ),
      h("div", null, "© 2026 Flitflap Cloud Infrastructure. All rights reserved.")
    ),

    // Signup Modal
    signupModal && h("div", { className: "modal-overlay" },
      h("div", { className: "modal-content" },
        signupSuccess ? h("div", { style: { textAlign: "center" } },
          h("div", { style: { fontSize: "3rem", marginBottom: "1rem" } }, "🎉"),
          h("h3", { style: { fontSize: "1.4rem", fontWeight: 700, color: "#fff", marginBottom: "0.5rem" } }, "Organization Provisioned!"),
          h("p", { style: { color: "#94a3b8", fontSize: "0.9rem", marginBottom: "1.5rem" } },
            `Welcome ${signupSuccess.organizationName}. Your primary domain ${signupSuccess.domainName} is registered.`
          ),
          h("div", { style: { background: "#080d1a", border: "1px solid var(--border)", padding: "1rem", borderRadius: "8px", fontSize: "0.8rem", color: "#cbd5e1", textAlign: "left", marginBottom: "1.5rem" } },
            h("div", null, h("b", null, "Admin Email: "), signupSuccess.adminEmail),
            h("div", null, h("b", null, "Domain: "), signupSuccess.domainName),
            h("div", null, h("b", null, "DNS Verification Token: "), signupSuccess.verificationToken)
          ),
          h("div", { style: { display: "flex", gap: "0.5rem" } },
            h("a", { className: "btn btn-primary", style: { flex: 1 }, href: signupSuccess.webmailLoginUrl, target: "_blank" }, "Launch Webmail"),
            h("button", { className: "btn btn-secondary", style: { flex: 1 }, onClick: () => { setSignupModal(false); setSignupSuccess(null); } }, "Done")
          )
        )
        : h("form", { onSubmit: handleSignup },
          h("div", { style: { display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: "1rem" } },
            h("h3", { style: { fontSize: "1.3rem", fontWeight: 700, color: "#fff" } }, "Self-Service Onboarding"),
            h("button", { type: "button", className: "btn-secondary", style: { padding: "4px 8px" }, onClick: () => setSignupModal(false) }, "✕")
          ),
          h("p", { style: { fontSize: "0.85rem", color: "#94a3b8", marginBottom: "1.25rem" } },
            `Provision your new organization under the ${tier} Plan.`
          ),

          signupError && h("div", { style: { padding: "0.6rem", background: "rgba(239, 68, 68, 0.15)", color: "#f87171", borderRadius: "6px", fontSize: "0.8rem", marginBottom: "1rem" } }, signupError),

          h("div", { style: { display: "flex", flexDirection: "column", gap: "0.75rem", marginBottom: "1.5rem" } },
            h("div", null,
              h("label", { style: { fontSize: "0.8rem", color: "#94a3b8" } }, "Organization Name"),
              h("input", { className: "input", placeholder: "Acme Corporation", required: true, value: signupForm.organizationName, onChange: e => setSignupForm({ ...signupForm, organizationName: e.target.value }) })
            ),
            h("div", null,
              h("label", { style: { fontSize: "0.8rem", color: "#94a3b8" } }, "Primary Domain Name"),
              h("input", { className: "input", placeholder: "acme.com", required: true, value: signupForm.domainName, onChange: e => setSignupForm({ ...signupForm, domainName: e.target.value }) })
            ),
            h("div", null,
              h("label", { style: { fontSize: "0.8rem", color: "#94a3b8" } }, "Administrator Email"),
              h("input", { type: "email", className: "input", placeholder: "admin@acme.com", required: true, value: signupForm.adminEmail, onChange: e => setSignupForm({ ...signupForm, adminEmail: e.target.value }) })
            ),
            h("div", null,
              h("label", { style: { fontSize: "0.8rem", color: "#94a3b8" } }, "Administrator Password"),
              h("input", { type: "password", className: "input", placeholder: "••••••••", required: true, value: signupForm.password, onChange: e => setSignupForm({ ...signupForm, password: e.target.value }) })
            )
          ),

          h("button", { type: "submit", className: "btn btn-primary", style: { width: "100%", padding: "0.85rem" }, disabled: signupLoading },
            signupLoading ? "Provisioning Organization..." : "Create Organization & Domain"
          )
        )
      )
    )
  );
}

const root = ReactDOM.createRoot(document.getElementById("root"));
root.render(h(PromoteApp));

