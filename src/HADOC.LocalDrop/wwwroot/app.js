(() => {
  const $ = (selector) => document.querySelector(selector);
  const token = new URLSearchParams(location.search).get("token");
  const connectScreen = $("#connectScreen");
  const workspace = $("#workspace");
  const pairingMessage = $("#pairingMessage");
  const connectionBadge = $("#connectionBadge");
  const messageStream = $("#messageStream");
  const seenSharedFiles = new Set();
  let socket = null;
  let connected = false;
  let refreshTimer = null;
  let completedUploads = 0;

  function deviceId() {
    try {
      let id = localStorage.getItem("hadoc-localdrop-device-id");
      if (!id) {
        id = (crypto.randomUUID ? crypto.randomUUID() : "mobile-" + Date.now() + "-" + Math.random().toString(16).slice(2));
        localStorage.setItem("hadoc-localdrop-device-id", id);
      }
      return id;
    } catch (_) {
      return "mobile-" + Date.now() + "-" + Math.random().toString(16).slice(2);
    }
  }

  function phoneName() {
    const ua = navigator.userAgent || "";
    if (/Android/i.test(ua)) return "Android phone";
    if (/iPhone|iPad/i.test(ua)) return "iPhone / iPad";
    return "Mobile device";
  }

  async function api(path, options) {
    const opts = options || {};
    const headers = Object.assign({ "X-LocalDrop-Token": token || "" }, opts.headers || {});
    const response = await fetch(path, Object.assign({}, opts, { headers }));
    if (!response.ok) {
      let reason = "";
      try { reason = await response.text(); } catch (_) { }
      throw new Error(reason || ("Request failed (" + response.status + ")"));
    }
    return response;
  }

  function setConnection(label, good) {
    $("#connectionText").textContent = label;
    connectionBadge.classList.toggle("offline", !good);
  }

  function showConnectError(error) {
    pairingMessage.textContent = error && error.message ? error.message : String(error);
    setConnection("NOT CONNECTED", false);
    $("#retryButton").classList.remove("hidden");
  }

  function addSystemMessage(text) {
    const node = document.createElement("div");
    node.className = "system-message";
    const icon = document.createElement("span");
    icon.className = "system-icon";
    icon.textContent = "✓";
    const label = document.createElement("span");
    label.textContent = text;
    node.append(icon, label);
    messageStream.append(node);
    messageStream.scrollTop = messageStream.scrollHeight;
  }

  function addChatMessage(sender, text, mine, sentAt) {
    const wrap = document.createElement("article");
    wrap.className = "message" + (mine ? " mine" : "");
    const meta = document.createElement("div");
    meta.className = "message-meta";
    const time = sentAt ? new Date(sentAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }) :
      new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
    meta.textContent = sender + " · " + time;
    const bubble = document.createElement("div");
    bubble.className = "bubble";
    bubble.textContent = text;
    wrap.append(meta, bubble);
    messageStream.append(wrap);
    messageStream.scrollTop = messageStream.scrollHeight;
  }

  function formatSize(bytes) {
    const units = ["B", "KB", "MB", "GB", "TB"];
    let value = Number(bytes) || 0;
    let unit = 0;
    while (value >= 1024 && unit < units.length - 1) { value /= 1024; unit++; }
    return value.toFixed(unit ? 1 : 0) + " " + units[unit];
  }

  function addFileMessage(file, direction, options) {
    const opts = options || {};
    const wrap = document.createElement("article");
    wrap.className = "message file-message" + (direction === "outgoing" ? " mine" : "");
    const meta = document.createElement("div");
    meta.className = "message-meta";
    meta.textContent = (direction === "outgoing" ? "YOU → PC" : "PC → YOU") + " · " +
      new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });

    const bubble = document.createElement("div");
    bubble.className = "bubble file-bubble";
    const top = document.createElement("div");
    top.className = "file-top";
    const glyph = document.createElement("div");
    glyph.className = "file-glyph";
    glyph.textContent = direction === "outgoing" ? "↑" : "↓";
    const title = document.createElement("div");
    title.className = "file-title";
    const name = document.createElement("strong");
    name.textContent = file.name;
    const size = document.createElement("small");
    size.textContent = formatSize(file.size);
    title.append(name, size);
    top.append(glyph, title);
    bubble.append(top);

    let fill = null;
    let status = null;
    if (opts.progress) {
      const track = document.createElement("div");
      track.className = "transfer-track";
      fill = document.createElement("div");
      fill.className = "transfer-fill";
      track.append(fill);
      status = document.createElement("small");
      status.className = "transfer-status";
      status.textContent = "Preparing transfer…";
      bubble.append(track, status);
    }
    if (opts.downloadId) {
      const button = document.createElement("button");
      button.className = "download-action";
      button.type = "button";
      button.textContent = "Download to phone";
      button.addEventListener("click", () => downloadFile(file, button));
      bubble.append(button);
    }
    wrap.append(meta, bubble);
    messageStream.append(wrap);
    messageStream.scrollTop = messageStream.scrollHeight;
    return {
      progress: function(percent, message) {
        if (fill) fill.style.width = Math.max(0, Math.min(100, percent)) + "%";
        if (status && message) status.textContent = message;
      }
    };
  }

  async function connect() {
    if (!token) {
      showConnectError(new Error("Pairing code is missing. Please scan the QR code again."));
      return;
    }
    $("#retryButton").classList.add("hidden");
    pairingMessage.textContent = "Securing the local connection…";
    setConnection("CONNECTING", false);
    try {
      const response = await api("/api/connect", {
        method: "POST",
        headers: { "X-Device-Id": deviceId(), "X-Device-Name": phoneName() }
      });
      const result = await response.json();
      $("#pcTitle").textContent = result.pcName || "Your PC";
      connectScreen.classList.add("hidden");
      workspace.classList.remove("hidden");
      setConnection("CONNECTED", true);
      connected = true;
      addSystemMessage("Connected to " + (result.pcName || "your PC") + ". Send a message or add files below.");
      await refreshFiles();
      openSocket();
      refreshTimer = setInterval(refreshFiles, 5000);
    } catch (error) {
      showConnectError(error);
    }
  }

  function openSocket() {
    if (!connected) return;
    const scheme = location.protocol === "https:" ? "wss://" : "ws://";
    const next = new WebSocket(scheme + location.host + "/ws?token=" + encodeURIComponent(token));
    socket = next;
    next.addEventListener("open", () => setConnection("LIVE", true));
    next.addEventListener("message", (event) => {
      try {
        const msg = JSON.parse(event.data);
        addChatMessage(msg.sender || "PC", msg.text || "", false, msg.sentAt);
      } catch (_) { }
    });
    next.addEventListener("close", () => {
      if (!connected) return;
      setConnection("RECONNECTING", false);
      setTimeout(() => {
        if (connected && socket === next) openSocket();
      }, 1600);
    });
    next.addEventListener("error", () => setConnection("RECONNECTING", false));
  }

  async function refreshFiles() {
    if (!connected) return;
    try {
      const files = await (await api("/api/files")).json();
      files.forEach((file) => {
        if (seenSharedFiles.has(file.id)) return;
        seenSharedFiles.add(file.id);
        addFileMessage({
          id: file.id,
          name: file.name,
          size: file.size,
          sizeLabel: file.sizeLabel
        }, "incoming", { downloadId: file.id });
      });
    } catch (_) { }
  }

  async function downloadFile(file, button) {
    button.disabled = true;
    button.textContent = "Downloading…";
    const card = button.closest(".file-bubble");
    const status = document.createElement("small");
    status.className = "transfer-status";
    card.append(status);
    try {
      const response = await api("/api/download/" + encodeURIComponent(file.id));
      const blob = await response.blob();
      const objectUrl = URL.createObjectURL(blob);
      const anchor = document.createElement("a");
      anchor.href = objectUrl;
      anchor.download = file.name;
      document.body.append(anchor);
      anchor.click();
      anchor.remove();
      setTimeout(() => URL.revokeObjectURL(objectUrl), 30000);
      status.textContent = "Downloaded · " + formatSize(blob.size);
      button.textContent = "Saved";
    } catch (error) {
      status.textContent = "Download failed: " + error.message;
      button.textContent = "Try again";
    } finally {
      button.disabled = false;
    }
  }

  function uploadFile(file) {
    if (!connected || !token) return;
    const transfer = addFileMessage(file, "outgoing", { progress: true });
    const request = new XMLHttpRequest();
    request.open("POST", "/api/upload?name=" + encodeURIComponent(file.name));
    request.setRequestHeader("X-LocalDrop-Token", token);
    request.setRequestHeader("Content-Type", "application/octet-stream");
    request.upload.addEventListener("progress", (event) => {
      if (!event.lengthComputable) return;
      const percent = Math.round(event.loaded / event.total * 100);
      transfer.progress(percent, percent + "% · " + formatSize(event.loaded) + " / " + formatSize(event.total));
    });
    request.addEventListener("load", () => {
      if (request.status >= 200 && request.status < 300) {
        let result = {};
        try { result = JSON.parse(request.responseText || "{}"); } catch (_) { }
        completedUploads++;
        $("#transferSummary").textContent = completedUploads + " FILE" + (completedUploads === 1 ? "" : "S") + " SENT";
        transfer.progress(100, "Delivered to PC · " + formatSize(file.size));
        addSystemMessage(file.name + " was saved on your PC.");
      } else {
        let reason = request.responseText || ("HTTP " + request.status);
        try { reason = JSON.parse(reason).error || reason; } catch (_) { }
        transfer.progress(0, "Transfer failed · " + reason);
      }
    });
    request.addEventListener("error", () => transfer.progress(0, "Connection error · check Wi-Fi and retry"));
    request.addEventListener("abort", () => transfer.progress(0, "Transfer cancelled"));
    transfer.progress(0, "Uploading · " + formatSize(file.size));
    request.send(file);
  }

  function sendChat() {
    const input = $("#chatInput");
    const text = input.value.trim();
    if (!text) return;
    if (!socket || socket.readyState !== WebSocket.OPEN) {
      addSystemMessage("Chat is reconnecting. Please send again in a moment.");
      return;
    }
    socket.send(JSON.stringify({ text: text }));
    addChatMessage("YOU", text, true);
    input.value = "";
    input.focus();
  }

  $("#chatForm").addEventListener("submit", (event) => {
    event.preventDefault();
    sendChat();
  });
  $("#chatInput").addEventListener("keydown", (event) => {
    if (event.key === "Enter" && !event.shiftKey) {
      event.preventDefault();
      sendChat();
    }
  });
  $("#addFilesButton").addEventListener("click", () => $("#fileInput").click());
  $("#fileInput").addEventListener("change", (event) => {
    Array.from(event.target.files || []).forEach(uploadFile);
    event.target.value = "";
  });
  $("#retryButton").addEventListener("click", connect);
  window.addEventListener("beforeunload", () => {
    connected = false;
    if (refreshTimer) clearInterval(refreshTimer);
    if (socket) socket.close();
  });

  connect();
})();
