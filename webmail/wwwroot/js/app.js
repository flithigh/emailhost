// Flitflap Webmail Client - React 18 Application
const { useState, useEffect, useCallback, useMemo } = React;
const h = React.createElement;

// SVG Icons Helper
const Icon = ({ name, size = 18, color = "currentColor" }) => {
  const icons = {
    mail: h("svg", { width: size, height: size, fill: "none", stroke: color, strokeWidth: 2, viewBox: "0 0 24 24" },
      h("rect", { width: 20, height: 16, x: 2, y: 4, rx: 2 }),
      h("path", { d: "m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7" })
    ),
    inbox: h("svg", { width: size, height: size, fill: "none", stroke: color, strokeWidth: 2, viewBox: "0 0 24 24" },
      h("polyline", { points: "22 12 16 12 14 15 10 15 8 12 2 12" }),
      h("path", { d: "M5.45 5.11 2 12v6a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2v-6l-3.45-6.89A2 2 0 0 0 16.76 4H7.24a2 2 0 0 0-1.79 1.11z" })
    ),
    star: h("svg", { width: size, height: size, fill: "currentColor", stroke: color, strokeWidth: 1, viewBox: "0 0 24 24" },
      h("polygon", { points: "12 2 15.09 8.26 22 9.27 17 14.14 18.18 21.02 12 17.77 5.82 21.02 7 14.14 2 9.27 8.91 8.26 12 2" })
    ),
    send: h("svg", { width: size, height: size, fill: "none", stroke: color, strokeWidth: 2, viewBox: "0 0 24 24" },
      h("line", { x1: 22, y1: 2, x2: 11, y2: 13 }),
      h("polygon", { points: "22 2 15 22 11 13 2 9 22 2" })
    ),
    fileText: h("svg", { width: size, height: size, fill: "none", stroke: color, strokeWidth: 2, viewBox: "0 0 24 24" },
      h("path", { d: "M14.5 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7.5L14.5 2z" }),
      h("polyline", { points: "14 2 14 8 20 8" })
    ),
    alertCircle: h("svg", { width: size, height: size, fill: "none", stroke: color, strokeWidth: 2, viewBox: "0 0 24 24" },
      h("circle", { cx: 12, cy: 12, r: 10 }),
      h("line", { x1: 12, y1: 8, x2: 12, y2: 12 }),
      h("line", { x1: 12, y1: 16, x2: 12.01, y2: 16 })
    ),
    trash: h("svg", { width: size, height: size, fill: "none", stroke: color, strokeWidth: 2, viewBox: "0 0 24 24" },
      h("polyline", { points: "3 6 5 6 21 6" }),
      h("path", { d: "M19 6v14a2 2 0 0 1-2 2H7a2 2 0 0 1-2-2V6m3 0V4a2 2 0 0 1 2-2h4a2 2 0 0 1 2 2v2" })
    ),
    users: h("svg", { width: size, height: size, fill: "none", stroke: color, strokeWidth: 2, viewBox: "0 0 24 24" },
      h("path", { d: "M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2" }),
      h("circle", { cx: 9, cy: 7, r: 4 }),
      h("path", { d: "M22 21v-2a4 4 0 0 0-3-3.87" }),
      h("path", { d: "M16 3.13a4 4 0 0 1 0 7.75" })
    ),
    settings: h("svg", { width: size, height: size, fill: "none", stroke: color, strokeWidth: 2, viewBox: "0 0 24 24" },
      h("circle", { cx: 12, cy: 12, r: 3 }),
      h("path", { d: "M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z" })
    ),
    refresh: h("svg", { width: size, height: size, fill: "none", stroke: color, strokeWidth: 2, viewBox: "0 0 24 24" },
      h("path", { d: "M21.5 2v6h-6M2.5 22v-6h6M2 11.5a10 10 0 0 1 18.8-4.3L21.5 8M22 12.5a10 10 0 0 1-18.8 4.2L2.5 16" })
    ),
    paperclip: h("svg", { width: size, height: size, fill: "none", stroke: color, strokeWidth: 2, viewBox: "0 0 24 24" },
      h("path", { d: "m21.44 11.05-9.19 9.19a6 6 0 0 1-8.49-8.49l8.57-8.57A4 4 0 1 1 18 8.84l-8.59 8.57a2 2 0 0 1-2.83-2.83l8.49-8.48" })
    ),
    reply: h("svg", { width: size, height: size, fill: "none", stroke: color, strokeWidth: 2, viewBox: "0 0 24 24" },
      h("polyline", { points: "9 17 4 12 9 7" }),
      h("path", { d: "M20 18v-2a4 4 0 0 0-4-4H4" })
    ),
    forward: h("svg", { width: size, height: size, fill: "none", stroke: color, strokeWidth: 2, viewBox: "0 0 24 24" },
      h("polyline", { points: "15 17 20 12 15 7" }),
      h("path", { d: "M4 18v-2a4 4 0 0 1 4-4h12" })
    ),
    shieldCheck: h("svg", { width: size, height: size, fill: "none", stroke: color, strokeWidth: 2, viewBox: "0 0 24 24" },
      h("path", { d: "M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10" }),
      h("path", { d: "m9 12 2 2 4-4" })
    ),
    plus: h("svg", { width: size, height: size, fill: "none", stroke: color, strokeWidth: 2, viewBox: "0 0 24 24" },
      h("line", { x1: 12, y1: 5, x2: 12, y2: 19 }),
      h("line", { x1: 5, y1: 12, x2: 19, y2: 12 })
    )
  };
  return icons[name] || null;
};

// Main App Component
function App() {
  const [user, setUser] = useState(() => {
    try {
      const saved = localStorage.getItem("flitflap_user");
      return saved ? JSON.parse(saved) : null;
    } catch { return null; }
  });
  const [token, setToken] = useState(() => localStorage.getItem("flitflap_token") || "");

  const [activeFolder, setActiveFolder] = useState("INBOX");
  const [activeView, setActiveView] = useState("mail"); // mail | contacts | settings
  const [folders, setFolders] = useState([]);
  const [messages, setMessages] = useState([]);
  const [selectedMsgId, setSelectedMsgId] = useState(null);
  const [selectedMsg, setSelectedMsg] = useState(null);
  const [search, setSearch] = useState("");
  const [loading, setLoading] = useState(false);
  const [composeState, setComposeState] = useState(null); // null or { mode, to, cc, bcc, subject, bodyHtml }
  const [contacts, setContacts] = useState([]);
  const [settings, setSettings] = useState(null);

  // Authentication API helper
  const apiFetch = useCallback(async (url, opts = {}) => {
    const headers = { "Content-Type": "application/json", ...opts.headers };
    if (token) headers["Authorization"] = `Bearer ${token}`;
    const res = await fetch(url, { ...opts, headers });
    if (res.status === 401) {
      localStorage.removeItem("flitflap_token");
      localStorage.removeItem("flitflap_user");
      setToken("");
      setUser(null);
      throw new Error("Session expired. Please log in again.");
    }
    return res;
  }, [token]);

  // Load Folders & Unread counts
  const loadFolders = useCallback(async () => {
    if (!token) return;
    try {
      const res = await apiFetch("/api/mail/folders");
      if (res.ok) {
        const data = await res.json();
        setFolders(data);
      }
    } catch (e) {
      console.warn("Folder stats error", e);
    }
  }, [apiFetch, token]);

  // Load Messages
  const loadMessages = useCallback(async (folder, query = "") => {
    if (!token) return;
    setLoading(true);
    try {
      const q = query ? `&search=${encodeURIComponent(query)}` : "";
      const res = await apiFetch(`/api/mail/messages?folder=${folder}${q}&pageSize=50`);
      if (res.ok) {
        const data = await res.json();
        setMessages(data.items || []);
        if (data.items && data.items.length > 0 && !selectedMsgId) {
          selectMessage(data.items[0].id);
        } else if (!data.items || data.items.length === 0) {
          setSelectedMsg(null);
        }
      }
    } catch (e) {
      console.warn("Messages load error", e);
    } finally {
      setLoading(false);
    }
  }, [apiFetch, token, selectedMsgId]);

  // Select Message Detail
  const selectMessage = async (id) => {
    setSelectedMsgId(id);
    try {
      const res = await apiFetch(`/api/mail/messages/${id}`);
      if (res.ok) {
        const data = await res.json();
        setSelectedMsg(data);
        // Update local unread status
        setMessages(prev => prev.map(m => m.id === id ? { ...m, isRead: true } : m));
        loadFolders();
      }
    } catch (e) {
      console.warn("Select msg error", e);
    }
  };

  // Toggle Star / Flag
  const toggleStar = async (e, id) => {
    e.stopPropagation();
    const current = messages.find(m => m.id === id);
    const newFlag = current ? !current.isFlagged : true;
    setMessages(prev => prev.map(m => m.id === id ? { ...m, isFlagged: newFlag } : m));
    if (selectedMsg && selectedMsg.id === id) {
      setSelectedMsg(prev => ({ ...prev, isFlagged: newFlag }));
    }
    await apiFetch(`/api/mail/messages/${id}/flag`, {
      method: "POST",
      body: JSON.stringify({ isFlagged: newFlag })
    });
    loadFolders();
  };

  // Move or Delete Message
  const deleteMessage = async (id) => {
    const res = await apiFetch(`/api/mail/messages/${id}`, { method: "DELETE" });
    if (res.ok) {
      setMessages(prev => prev.filter(m => m.id !== id));
      if (selectedMsgId === id) {
        setSelectedMsg(null);
        setSelectedMsgId(null);
      }
      loadFolders();
    }
  };

  // Login handler
  const handleLogin = async (address, password) => {
    const res = await fetch("/api/auth/login", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ address, password })
    });
    if (!res.ok) {
      throw new Error("Invalid email or password credentials.");
    }
    const data = await res.json();
    localStorage.setItem("flitflap_token", data.token);
    localStorage.setItem("flitflap_user", JSON.stringify(data.user));
    setToken(data.token);
    setUser(data.user);
  };

  // Logout handler
  const handleLogout = () => {
    localStorage.removeItem("flitflap_token");
    localStorage.removeItem("flitflap_user");
    setToken("");
    setUser(null);
  };

  useEffect(() => {
    if (token) {
      loadFolders();
      loadMessages(activeFolder, search);
    }
  }, [token, activeFolder, loadFolders, loadMessages, search]);

  // If not logged in, render Login Screen
  if (!user || !token) {
    return h(LoginScreen, { onLogin: handleLogin });
  }

  // Render Main Layout
  return h("div", { className: "app-container" },
    // App Header
    h("header", { className: "app-header" },
      h("div", { style: { display: "flex", alignItems: "center", gap: "0.75rem" } },
        h("div", { style: { display: "flex", alignItems: "center", gap: "0.5rem", fontWeight: 700, fontSize: "1.1rem" } },
          h(Icon, { name: "mail", size: 24, color: "#38bdf8" }),
          h("span", { style: { background: "linear-gradient(90deg, #38bdf8, #818cf8)", WebkitBackgroundClip: "text", WebkitTextFillColor: "transparent" } }, "Flitflap Mail"),
          h("span", { style: { fontSize: "0.65rem", padding: "2px 6px", borderRadius: "4px", background: "rgba(56, 189, 248, 0.15)", color: "#38bdf8", border: "1px solid rgba(56, 189, 248, 0.3)" } }, "v2.0")
        )
      ),

      // Global Search Bar
      h("div", { style: { width: "420px", position: "relative" } },
        h("input", {
          className: "input",
          placeholder: "Search emails by subject, sender, or content...",
          value: search,
          onChange: e => setSearch(e.target.value),
          onKeyDown: e => { if (e.key === "Enter") loadMessages(activeFolder, search); }
        })
      ),

      // Right User & Quota bar
      h("div", { style: { display: "flex", alignItems: "center", gap: "1rem" } },
        h("button", {
          className: "btn btn-ghost",
          title: "Refresh Messages",
          onClick: () => { loadFolders(); loadMessages(activeFolder, search); }
        }, h(Icon, { name: "refresh", size: 16 })),

        // Storage Quota indicator
        h("div", { style: { display: "flex", flexDirection: "column", gap: "3px", width: "120px" } },
          h("div", { style: { display: "flex", justifyContent: "space-between", fontSize: "0.7rem", color: "#94a3b8" } },
            h("span", null, "Storage"),
            h("span", null, `${Math.round(user.usedBytes / 1024 / 1024)} MB`)
          ),
          h("div", { style: { height: "4px", background: "#334155", borderRadius: "2px", overflow: "hidden" } },
            h("div", {
              style: {
                width: `${Math.min(100, Math.max(4, (user.usedBytes / user.quotaBytes) * 100))}%`,
                height: "100%",
                background: "#38bdf8"
              }
            })
          )
        ),

        // User Avatar Chip
        h("div", { style: { display: "flex", alignItems: "center", gap: "0.5rem" } },
          h("div", {
            style: {
              width: 32, height: 32, borderRadius: "50%",
              background: "linear-gradient(135deg, #0284c7, #4f46e5)",
              display: "flex", alignItems: "center", justifyContent: "center",
              fontWeight: 600, fontSize: "0.85rem", color: "#fff"
            }
          }, (user.displayName || user.address)[0].toUpperCase()),
          h("div", { style: { fontSize: "0.8rem", lineHeight: 1.2 } },
            h("div", { style: { fontWeight: 600 } }, user.displayName || user.address),
            h("div", { style: { fontSize: "0.7rem", color: "#94a3b8" } }, user.address)
          ),
          h("button", {
            className: "btn btn-ghost",
            style: { padding: "4px 8px", fontSize: "0.75rem", color: "#ef4444" },
            onClick: handleLogout,
            title: "Sign Out"
          }, "Logout")
        )
      )
    ),

    // App Body
    h("div", { className: "app-body" },
      // Left Sidebar
      h("aside", { className: "app-sidebar" },
        // New Message Button
        h("button", {
          className: "btn btn-primary",
          style: { width: "100%", marginBottom: "0.5rem", padding: "0.7rem" },
          onClick: () => setComposeState({ mode: "new", to: "", cc: "", bcc: "", subject: "", bodyHtml: "" })
        },
          h(Icon, { name: "plus", size: 18 }),
          h("span", null, "New Message")
        ),

        // Folders Nav
        h("div", { style: { display: "flex", flexDirection: "column", gap: "2px" } },
          [
            { id: "INBOX", name: "Inbox", icon: "inbox" },
            { id: "Starred", name: "Starred", icon: "star" },
            { id: "Sent", name: "Sent", icon: "send" },
            { id: "Drafts", name: "Drafts", icon: "fileText" },
            { id: "Spam", name: "Spam", icon: "alertCircle" },
            { id: "Trash", name: "Trash", icon: "trash" }
          ].map(f => {
            const stat = folders.find(fd => fd.folder.toLowerCase() === f.id.toLowerCase());
            const unread = stat ? stat.unreadCount : 0;
            const isAct = activeView === "mail" && activeFolder.toLowerCase() === f.id.toLowerCase();

            return h("div", {
              key: f.id,
              className: `nav-item ${isAct ? "active" : ""}`,
              onClick: () => { setActiveView("mail"); setActiveFolder(f.id); }
            },
              h("div", { style: { display: "flex", alignItems: "center", gap: "0.6rem" } },
                h(Icon, { name: f.icon, size: 16 }),
                h("span", null, f.name)
              ),
              unread > 0 && h("span", { className: "badge-count" }, unread)
            );
          })
        ),

        h("div", { style: { height: "1px", background: "var(--border-color)", margin: "0.5rem 0" } }),

        // Organization Navigation
        h("div", { style: { display: "flex", flexDirection: "column", gap: "2px" } },
          h("div", {
            className: `nav-item ${activeView === "contacts" ? "active" : ""}`,
            onClick: () => setActiveView("contacts")
          },
            h("div", { style: { display: "flex", alignItems: "center", gap: "0.6rem" } },
              h(Icon, { name: "users", size: 16 }),
              h("span", null, "Address Book")
            )
          ),
          h("div", {
            className: `nav-item ${activeView === "settings" ? "active" : ""}`,
            onClick: () => setActiveView("settings")
          },
            h("div", { style: { display: "flex", alignItems: "center", gap: "0.6rem" } },
              h(Icon, { name: "settings", size: 16 }),
              h("span", null, "Settings & Quota")
            )
          )
        ),

        h("div", { style: { marginTop: "auto", fontSize: "0.7rem", color: "#64748b", display: "flex", alignItems: "center", gap: "0.4rem" } },
          h("span", { style: { width: 8, height: 8, borderRadius: "50%", background: "#10b981" } }),
          h("span", null, "Garage S3 Mail Store Active")
        )
      ),

      // Center & Right View Panes
      activeView === "contacts" ? h(ContactsView, { apiFetch, onComposeTo: (email) => setComposeState({ mode: "new", to: email, subject: "", bodyHtml: "" }) })
      : activeView === "settings" ? h(SettingsView, { apiFetch, user, onUserUpdated: setUser })
      : h(React.Fragment, null,
          // Message List Pane
          h("div", { className: "msg-list-pane" },
            h("div", { className: "msg-list-toolbar" },
              h("span", { style: { fontWeight: 600, fontSize: "0.9rem", color: "#fff" } },
                activeFolder === "INBOX" ? "Inbox" : activeFolder,
                h("span", { style: { fontSize: "0.75rem", color: "#94a3b8", marginLeft: "0.5rem" } }, `(${messages.length})`)
              ),
              loading && h("span", { style: { fontSize: "0.75rem", color: "#38bdf8" } }, "Loading...")
            ),

            h("div", { className: "msg-list-scroll" },
              messages.length === 0 ? h("div", { style: { padding: "3rem 1.5rem", textAlign: "center", color: "#64748b" } }, "No messages in this folder.")
              : messages.map(m => h(MessageCard, {
                  key: m.id,
                  msg: m,
                  isSelected: selectedMsgId === m.id,
                  onSelect: () => selectMessage(m.id),
                  onToggleStar: (e) => toggleStar(e, m.id)
                }))
            )
          ),

          // Message Detail Reading Pane
          h("div", { className: "msg-detail-pane" },
            selectedMsg ? h(MessageReader, {
              msg: selectedMsg,
              onDelete: () => deleteMessage(selectedMsg.id),
              onReply: () => setComposeState({
                mode: "reply",
                to: selectedMsg.sender,
                subject: selectedMsg.subject.startsWith("Re:") ? selectedMsg.subject : `Re: ${selectedMsg.subject}`,
                bodyHtml: `<br><br><hr><p style="color:#64748b">On ${new Date(selectedMsg.receivedAtUtc).toLocaleString()}, ${selectedMsg.sender} wrote:</p><blockquote>${selectedMsg.htmlBody || selectedMsg.textBody}</blockquote>`
              }),
              onForward: () => setComposeState({
                mode: "forward",
                to: "",
                subject: selectedMsg.subject.startsWith("Fwd:") ? selectedMsg.subject : `Fwd: ${selectedMsg.subject}`,
                bodyHtml: `<br><br><hr><p style="color:#64748b">---------- Forwarded message ---------<br>From: ${selectedMsg.sender}<br>Subject: ${selectedMsg.subject}<br>Date: ${new Date(selectedMsg.receivedAtUtc).toLocaleString()}</p>${selectedMsg.htmlBody || selectedMsg.textBody}`
              })
            })
            : h("div", { style: { flex: 1, display: "flex", flexDirection: "column", alignItems: "center", justifyContent: "center", color: "#64748b", gap: "1rem" } },
                h(Icon, { name: "mail", size: 48, color: "#334155" }),
                h("p", null, "Select an email to view its contents")
              )
          )
        )
    ),

    // Compose Modal
    composeState && h(ComposeModal, {
      initialData: composeState,
      onClose: () => setComposeState(null),
      onSend: async (payload) => {
        const res = await apiFetch("/api/mail/send", { method: "POST", body: JSON.stringify(payload) });
        if (!res.ok) {
          const err = await res.json();
          throw new Error(err.message || "Failed to dispatch email.");
        }
        setComposeState(null);
        loadFolders();
        if (activeFolder === "Sent") loadMessages("Sent");
      },
      onSaveDraft: async (payload) => {
        await apiFetch("/api/mail/draft", { method: "POST", body: JSON.stringify(payload) });
        setComposeState(null);
        loadFolders();
      }
    })
  );
}

// Message Card Item
function MessageCard({ msg, isSelected, onSelect, onToggleStar }) {
  const dateStr = useMemo(() => {
    const d = new Date(msg.receivedAtUtc);
    const now = new Date();
    return d.toDateString() === now.toDateString()
      ? d.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })
      : d.toLocaleDateString([], { month: 'short', day: 'numeric' });
  }, [msg.receivedAtUtc]);

  return h("div", {
    className: `msg-card ${isSelected ? "selected" : ""} ${!msg.isRead ? "unread" : ""}`,
    onClick: onSelect
  },
    h("div", { style: { display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: "0.25rem" } },
      h("div", { style: { display: "flex", alignItems: "center", gap: "0.5rem" } },
        h("button", {
          className: "btn-ghost",
          style: { padding: 0, color: msg.isFlagged ? "#eab308" : "#475569" },
          onClick: onToggleStar
        }, h(Icon, { name: "star", size: 14 })),
        h("span", { style: { fontSize: "0.85rem", fontWeight: !msg.isRead ? 700 : 500, color: !msg.isRead ? "#fff" : "#cbd5e1" } },
          msg.sender
        )
      ),
      h("div", { style: { display: "flex", alignItems: "center", gap: "0.4rem" } },
        msg.hasAttachments && h(Icon, { name: "paperclip", size: 13, color: "#94a3b8" }),
        h("span", { style: { fontSize: "0.75rem", color: "#64748b" } }, dateStr)
      )
    ),
    h("div", { className: "msg-subject", style: { fontSize: "0.85rem", marginBottom: "0.2rem", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" } },
      msg.subject
    ),
    h("div", { style: { fontSize: "0.75rem", color: "#94a3b8", overflow: "hidden", textOverflow: "ellipsis", whiteSpace: "nowrap" } },
      msg.previewSnippet || "(No preview available)"
    )
  );
}

// Message Reader Pane
function MessageReader({ msg, onDelete, onReply, onForward }) {
  return h("div", { style: { display: "flex", flexDirection: "column", height: "100%" } },
    // Header
    h("div", { className: "msg-detail-header" },
      h("div", { style: { display: "flex", justifyContent: "space-between", alignItems: "flex-start", marginBottom: "0.75rem" } },
        h("h2", { style: { fontSize: "1.25rem", fontWeight: 700, color: "#fff" } }, msg.subject),
        h("div", { style: { display: "flex", gap: "0.4rem" } },
          h("button", { className: "btn btn-secondary", onClick: onReply }, h(Icon, { name: "reply", size: 14 }), "Reply"),
          h("button", { className: "btn btn-secondary", onClick: onForward }, h(Icon, { name: "forward", size: 14 }), "Forward"),
          h("a", { className: "btn btn-secondary", href: `/api/mail/messages/${msg.id}/eml`, download: true, title: "Download Raw RFC822 EML" }, "EML"),
          h("button", { className: "btn btn-danger", onClick: onDelete, title: "Delete Message" }, h(Icon, { name: "trash", size: 14 }))
        )
      ),

      // Sender / Recipient / Security Bar
      h("div", { style: { display: "flex", alignItems: "center", justifyContent: "space-between", flexWrap: "wrap", gap: "0.5rem" } },
        h("div", { style: { display: "flex", alignItems: "center", gap: "0.75rem" } },
          h("div", {
            style: {
              width: 36, height: 36, borderRadius: "50%",
              background: "#334155", display: "flex", alignItems: "center", justifyContent: "center",
              fontWeight: 700, color: "#38bdf8"
            }
          }, msg.sender[0].toUpperCase()),
          h("div", { style: { fontSize: "0.85rem" } },
            h("div", { style: { fontWeight: 600, color: "#fff" } }, msg.sender),
            h("div", { style: { fontSize: "0.75rem", color: "#94a3b8" } }, `To: ${msg.recipient}${msg.cc ? ` | Cc: ${msg.cc}` : ""}`)
          )
        ),

        // Security Verdict Chips
        h("div", { style: { display: "flex", alignItems: "center", gap: "0.4rem" } },
          h("span", {
            style: {
              fontSize: "0.7rem", padding: "2px 8px", borderRadius: "12px",
              background: msg.spfVerdict === "Pass" ? "rgba(16, 185, 129, 0.15)" : "rgba(245, 158, 11, 0.15)",
              color: msg.spfVerdict === "Pass" ? "#34d399" : "#fbbf24",
              border: "1px solid rgba(255,255,255,0.08)"
            }
          }, `SPF: ${msg.spfVerdict}`),
          h("span", {
            style: {
              fontSize: "0.7rem", padding: "2px 8px", borderRadius: "12px",
              background: msg.dkimVerdict === "Pass" ? "rgba(16, 185, 129, 0.15)" : "rgba(245, 158, 11, 0.15)",
              color: msg.dkimVerdict === "Pass" ? "#34d399" : "#fbbf24",
              border: "1px solid rgba(255,255,255,0.08)"
            }
          }, `DKIM: ${msg.dkimVerdict}`),
          h("span", {
            style: {
              fontSize: "0.7rem", padding: "2px 8px", borderRadius: "12px",
              background: "rgba(56, 189, 248, 0.15)", color: "#38bdf8",
              border: "1px solid rgba(56, 189, 248, 0.3)"
            }
          }, `Spam: ${msg.spamScore.toFixed(1)}`)
        )
      )
    ),

    // Attachments section if present
    msg.attachments && msg.attachments.length > 0 && h("div", {
      style: {
        padding: "0.75rem 1.5rem", background: "rgba(255, 255, 255, 0.02)",
        borderBottom: "1px solid var(--border-color)", display: "flex", gap: "0.5rem", flexWrap: "wrap", alignItems: "center"
      }
    },
      h("span", { style: { fontSize: "0.8rem", color: "#94a3b8", display: "flex", alignItems: "center", gap: "0.3rem" } },
        h(Icon, { name: "paperclip", size: 14 }), "Attachments:"
      ),
      msg.attachments.map(att => h("a", {
        key: att.fileName,
        href: `/api/mail/messages/${msg.id}/attachments/${encodeURIComponent(att.fileName)}`,
        download: att.fileName,
        className: "btn btn-secondary",
        style: { fontSize: "0.75rem", padding: "3px 8px" }
      }, `${att.fileName} (${Math.round(att.sizeBytes / 1024)} KB)`))
    ),

    // Email Body
    h("div", { className: "msg-detail-body" },
      msg.htmlBody ? h("div", {
        style: { color: "#e2e8f0", lineHeight: 1.6 },
        dangerouslySetInnerHTML: { __html: msg.htmlBody }
      })
      : h("pre", { style: { color: "#e2e8f0", whiteSpace: "pre-wrap", fontFamily: "inherit", lineHeight: 1.6 } }, msg.textBody)
    )
  );
}

// Compose Modal
function ComposeModal({ initialData, onClose, onSend, onSaveDraft }) {
  const [to, setTo] = useState(initialData.to || "");
  const [cc, setCc] = useState(initialData.cc || "");
  const [showCc, setShowCc] = useState(false);
  const [subject, setSubject] = useState(initialData.subject || "");
  const [bodyHtml, setBodyHtml] = useState(initialData.bodyHtml || "");
  const [sending, setSending] = useState(false);
  const [error, setError] = useState("");

  const handleSubmit = async (isDraft = false) => {
    if (!to && !isDraft) {
      setError("Please specify at least one recipient email address.");
      return;
    }
    setSending(true);
    setError("");
    try {
      const payload = { to, cc, subject, bodyHtml };
      if (isDraft) await onSaveDraft(payload);
      else await onSend(payload);
    } catch (err) {
      setError(err.message || "Failed to dispatch email.");
    } finally {
      setSending(false);
    }
  };

  return h("div", { className: "modal-overlay" },
    h("div", { className: "modal-content" },
      h("div", { style: { padding: "1rem 1.25rem", borderBottom: "1px solid var(--border-color)", display: "flex", justifyContent: "space-between", alignItems: "center" } },
        h("span", { style: { fontWeight: 600, color: "#fff" } }, initialData.mode === "reply" ? "Reply Message" : initialData.mode === "forward" ? "Forward Message" : "New Message"),
        h("button", { className: "btn-ghost", onClick: onClose }, "✕")
      ),

      error && h("div", { style: { padding: "0.5rem 1.25rem", background: "rgba(239, 68, 68, 0.15)", color: "#f87171", fontSize: "0.8rem" } }, error),

      h("div", { style: { padding: "1rem 1.25rem", display: "flex", flexDirection: "column", gap: "0.6rem", flex: 1 } },
        h("div", { style: { display: "flex", alignItems: "center", gap: "0.5rem" } },
          h("span", { style: { width: "50px", fontSize: "0.85rem", color: "#94a3b8" } }, "To:"),
          h("input", { className: "input", placeholder: "recipient@company.com", value: to, onChange: e => setTo(e.target.value) }),
          !showCc && h("button", { className: "btn-ghost", style: { fontSize: "0.75rem", padding: "4px" }, onClick: () => setShowCc(true) }, "Cc")
        ),

        showCc && h("div", { style: { display: "flex", alignItems: "center", gap: "0.5rem" } },
          h("span", { style: { width: "50px", fontSize: "0.85rem", color: "#94a3b8" } }, "Cc:"),
          h("input", { className: "input", placeholder: "copy@company.com", value: cc, onChange: e => setCc(e.target.value) })
        ),

        h("div", { style: { display: "flex", alignItems: "center", gap: "0.5rem" } },
          h("span", { style: { width: "50px", fontSize: "0.85rem", color: "#94a3b8" } }, "Subject:"),
          h("input", { className: "input", placeholder: "Subject", value: subject, onChange: e => setSubject(e.target.value) })
        ),

        h("textarea", {
          className: "input",
          style: { height: "240px", resize: "none", marginTop: "0.5rem" },
          placeholder: "Type your message here...",
          value: bodyHtml,
          onChange: e => setBodyHtml(e.target.value)
        })
      ),

      h("div", { style: { padding: "1rem 1.25rem", borderTop: "1px solid var(--border-color)", display: "flex", justifyContent: "space-between", alignItems: "center" } },
        h("div", null,
          h("button", { className: "btn btn-secondary", style: { marginRight: "0.5rem" }, onClick: () => handleSubmit(true), disabled: sending }, "Save Draft")
        ),
        h("div", { style: { display: "flex", gap: "0.5rem" } },
          h("button", { className: "btn btn-ghost", onClick: onClose }, "Discard"),
          h("button", { className: "btn btn-primary", onClick: () => handleSubmit(false), disabled: sending },
            sending ? "Sending..." : "Send Email"
          )
        )
      )
    )
  );
}

// Contacts View
function ContactsView({ apiFetch, onComposeTo }) {
  const [contacts, setContacts] = useState([]);
  const [newEmail, setNewEmail] = useState("");
  const [newName, setNewName] = useState("");

  const loadContacts = useCallback(async () => {
    const res = await apiFetch("/api/contacts");
    if (res.ok) setContacts(await res.json());
  }, [apiFetch]);

  useEffect(() => { loadContacts(); }, [loadContacts]);

  const handleAdd = async () => {
    if (!newEmail) return;
    await apiFetch("/api/contacts", {
      method: "POST",
      body: JSON.stringify({ name: newName || newEmail, email: newEmail })
    });
    setNewName("");
    setNewEmail("");
    loadContacts();
  };

  const handleDelete = async (id) => {
    await apiFetch(`/api/contacts/${id}`, { method: "DELETE" });
    loadContacts();
  };

  return h("div", { style: { flex: 1, padding: "2rem", overflowY: "auto" } },
    h("h2", { style: { fontSize: "1.3rem", fontWeight: 700, marginBottom: "1rem" } }, "Address Book & Contacts"),

    h("div", { style: { display: "flex", gap: "0.5rem", marginBottom: "1.5rem", maxWidth: "600px" } },
      h("input", { className: "input", placeholder: "Full Name", value: newName, onChange: e => setNewName(e.target.value) }),
      h("input", { className: "input", placeholder: "Email Address", value: newEmail, onChange: e => setNewEmail(e.target.value) }),
      h("button", { className: "btn btn-primary", onClick: handleAdd }, "Add Contact")
    ),

    h("div", { style: { display: "grid", gridTemplateColumns: "repeat(auto-fill, minmax(280px, 1fr))", gap: "1rem" } },
      contacts.map(c => h("div", {
        key: c.id,
        style: { background: "#1e293b", border: "1px solid var(--border-color)", padding: "1rem", borderRadius: "8px", display: "flex", justifyContent: "space-between", alignItems: "center" }
      },
        h("div", null,
          h("div", { style: { fontWeight: 600, color: "#fff" } }, c.name),
          h("div", { style: { fontSize: "0.8rem", color: "#94a3b8" } }, c.email)
        ),
        h("div", { style: { display: "flex", gap: "0.4rem" } },
          h("button", { className: "btn btn-secondary", style: { padding: "4px 8px", fontSize: "0.75rem" }, onClick: () => onComposeTo(c.email) }, "Email"),
          h("button", { className: "btn btn-danger", style: { padding: "4px 8px", fontSize: "0.75rem" }, onClick: () => handleDelete(c.id) }, "✕")
        )
      ))
    )
  );
}

// Settings View
function SettingsView({ apiFetch, user, onUserUpdated }) {
  const [signature, setSignature] = useState("");
  const [oooEnabled, setOooEnabled] = useState(false);
  const [oooSubject, setOooSubject] = useState("");
  const [oooMessage, setOooMessage] = useState("");
  const [oldPass, setOldPass] = useState("");
  const [newPass, setNewPass] = useState("");
  const [msg, setMsg] = useState("");

  useEffect(() => {
    apiFetch("/api/settings").then(async r => {
      if (r.ok) {
        const data = await r.json();
        setSignature(data.signatureHtml || "");
        setOooEnabled(data.outOfOfficeEnabled || false);
        setOooSubject(data.outOfOfficeSubject || "Out of Office Automatic Reply");
        setOooMessage(data.outOfOfficeMessage || "");
      }
    });
  }, [apiFetch]);

  const handleSaveSettings = async () => {
    await apiFetch("/api/settings", {
      method: "PUT",
      body: JSON.stringify({
        displayName: user.displayName,
        signatureHtml: signature,
        outOfOfficeEnabled: oooEnabled,
        outOfOfficeSubject: oooSubject,
        outOfOfficeMessage: oooMessage
      })
    });
    setMsg("Settings updated successfully!");
    setTimeout(() => setMsg(""), 3000);
  };

  const handleChangePassword = async () => {
    const res = await apiFetch("/api/auth/change-password", {
      method: "POST",
      body: JSON.stringify({ currentPassword: oldPass, newPassword: newPass })
    });
    if (res.ok) {
      setMsg("Password changed successfully!");
      setOldPass("");
      setNewPass("");
      setTimeout(() => setMsg(""), 3000);
    } else {
      const err = await res.json();
      setMsg(`Error: ${err.message}`);
    }
  };

  return h("div", { style: { flex: 1, padding: "2rem", overflowY: "auto", maxWidth: "700px" } },
    h("h2", { style: { fontSize: "1.3rem", fontWeight: 700, marginBottom: "1rem" } }, "Personal Account Settings"),

    msg && h("div", { style: { padding: "0.75rem", background: "rgba(16, 185, 129, 0.2)", color: "#34d399", borderRadius: "8px", marginBottom: "1rem" } }, msg),

    // Signature
    h("div", { style: { marginBottom: "1.5rem" } },
      h("label", { style: { display: "block", marginBottom: "0.4rem", fontWeight: 600 } }, "Email Signature (Appended to sent emails)"),
      h("textarea", { className: "input", style: { height: "100px" }, value: signature, onChange: e => setSignature(e.target.value) })
    ),

    // Out of Office Auto Responder
    h("div", { style: { marginBottom: "1.5rem", background: "#1e293b", padding: "1rem", borderRadius: "8px" } },
      h("div", { style: { display: "flex", alignItems: "center", justifyContent: "space-between", marginBottom: "0.75rem" } },
        h("span", { style: { fontWeight: 600 } }, "Out of Office Auto-Responder"),
        h("input", { type: "checkbox", checked: oooEnabled, onChange: e => setOooEnabled(e.target.checked) })
      ),
      oooEnabled && h("div", { style: { display: "flex", flexDirection: "column", gap: "0.5rem" } },
        h("input", { className: "input", placeholder: "Subject", value: oooSubject, onChange: e => setOooSubject(e.target.value) }),
        h("textarea", { className: "input", placeholder: "Automatic reply message", value: oooMessage, onChange: e => setOooMessage(e.target.value) })
      )
    ),

    h("button", { className: "btn btn-primary", style: { marginBottom: "2rem" }, onClick: handleSaveSettings }, "Save Preferences"),

    // Password Update
    h("div", { style: { borderTop: "1px solid var(--border-color)", paddingTop: "1.5rem" } },
      h("h3", { style: { fontSize: "1.1rem", fontWeight: 600, marginBottom: "0.75rem" } }, "Update Password"),
      h("div", { style: { display: "flex", flexDirection: "column", gap: "0.6rem", maxWidth: "400px" } },
        h("input", { type: "password", className: "input", placeholder: "Current Password", value: oldPass, onChange: e => setOldPass(e.target.value) }),
        h("input", { type: "password", className: "input", placeholder: "New Password (min 6 chars)", value: newPass, onChange: e => setNewPass(e.target.value) }),
        h("button", { className: "btn btn-secondary", onClick: handleChangePassword }, "Change Password")
      )
    )
  );
}

// Login Screen Component
function LoginScreen({ onLogin }) {
  const [address, setAddress] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState("");
  const [loading, setLoading] = useState(false);

  const handleSubmit = async (e) => {
    if (e) e.preventDefault();
    if (!address || !password) {
      setError("Please enter your email address and password.");
      return;
    }
    setLoading(true);
    setError("");
    try {
      await onLogin(address, password);
    } catch (err) {
      setError(err.message || "Failed to log in.");
    } finally {
      setLoading(false);
    }
  };

  const handleDemo = (email, pass) => {
    setAddress(email);
    setPassword(pass);
  };

  return h("div", {
    style: {
      display: "flex", alignItems: "center", justifyContent: "center",
      height: "100vh", background: "radial-gradient(circle at 50% 20%, #1e293b 0%, #0f172a 100%)"
    }
  },
    h("div", {
      className: "glass",
      style: { width: "100%", maxWidth: "420px", padding: "2.5rem", borderRadius: "16px", boxShadow: "0 25px 50px -12px rgba(0, 0, 0, 0.5)" }
    },
      h("div", { style: { textAlign: "center", marginBottom: "2rem" } },
        h("div", {
          style: {
            width: 54, height: 54, borderRadius: "14px",
            background: "linear-gradient(135deg, #0ea5e9, #3b82f6)",
            display: "inline-flex", alignItems: "center", justifyContent: "center",
            boxShadow: "0 10px 20px rgba(14, 165, 233, 0.3)", marginBottom: "1rem"
          }
        }, h(Icon, { name: "mail", size: 30, color: "#fff" })),
        h("h1", { style: { fontSize: "1.5rem", fontWeight: 700, color: "#fff", letterSpacing: "-0.02em" } }, "Flitflap Webmail"),
        h("p", { style: { fontSize: "0.85rem", color: "#94a3b8", marginTop: "0.25rem" } }, "Enterprise Mailbox Client (doc.md Section 2.B.3)")
      ),

      error && h("div", {
        style: {
          padding: "0.75rem", background: "rgba(239, 68, 68, 0.15)",
          border: "1px solid rgba(239, 68, 68, 0.3)", borderRadius: "8px",
          color: "#f87171", fontSize: "0.85rem", marginBottom: "1.25rem", textAlign: "center"
        }
      }, error),

      h("form", { onSubmit: handleSubmit, style: { display: "flex", flexDirection: "column", gap: "1rem" } },
        h("div", null,
          h("label", { style: { display: "block", fontSize: "0.8rem", color: "#94a3b8", marginBottom: "0.35rem" } }, "Email Address"),
          h("input", {
            type: "email",
            className: "input",
            placeholder: "user@domain.com",
            value: address,
            onChange: e => setAddress(e.target.value),
            required: true
          })
        ),
        h("div", null,
          h("label", { style: { display: "block", fontSize: "0.8rem", color: "#94a3b8", marginBottom: "0.35rem" } }, "Password"),
          h("input", {
            type: "password",
            className: "input",
            placeholder: "••••••••",
            value: password,
            onChange: e => setPassword(e.target.value),
            required: true
          })
        ),
        h("button", {
          type: "submit",
          className: "btn btn-primary",
          style: { padding: "0.75rem", marginTop: "0.5rem" },
          disabled: loading
        }, loading ? "Signing in..." : "Sign In to Webmail")
      ),

      // Demo quick buttons
      h("div", { style: { marginTop: "1.5rem", borderTop: "1px solid var(--border-color)", paddingTop: "1.25rem", textAlign: "center" } },
        h("div", { style: { fontSize: "0.75rem", color: "#64748b", marginBottom: "0.6rem" } }, "Default Provisioned Mailboxes:"),
        h("div", { style: { display: "flex", gap: "0.5rem", justifyContent: "center" } },
          h("button", {
            type: "button",
            className: "btn btn-secondary",
            style: { fontSize: "0.75rem", padding: "4px 10px" },
            onClick: () => handleDemo("info@flitflap.com", "Password123!")
          }, "info@flitflap.com"),
          h("button", {
            type: "button",
            className: "btn btn-secondary",
            style: { fontSize: "0.75rem", padding: "4px 10px" },
            onClick: () => handleDemo("demo@flitflap.com", "Password123!")
          }, "demo@flitflap.com")
        )
      )
    )
  );
}

// Mount React Root
const root = ReactDOM.createRoot(document.getElementById("root"));
root.render(h(App));

