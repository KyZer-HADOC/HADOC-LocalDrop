(() => {
  const qs = (selector) => document.querySelector(selector);
  const token = new URLSearchParams(location.search).get("token");
  const pairingPanel = qs("#pairingPanel");
  const workspace = qs("#workspace");
  const pairingMessage = qs("#pairingMessage");
  const connectionText = qs("#connectionText");
  const deviceIdKey = "hadoc-localdrop-device-id";
  const deviceId = localStorage.getItem(deviceIdKey) || createId();
  localStorage.setItem(deviceIdKey, deviceId);
  let socket = null;
  let connected = false;

  function createId() {
    if (crypto.randomUUID) return crypto.randomUUID();
    return "device-" + Date.now() + "-" + Math.random().toString(16).slice(2);
  }
  function deviceName() {
    const ua = navigator.userAgent || "";
    if (/Android/i.test(ua)) return "Android phone";
    if (/iPhone|iPad/i.test(ua)) return "iPhone / iPad";
    return "Mobile device";
  }
  function headers(extra) { return Object.assign({ "X-LocalDrop-Token": token }, extra || {}); }
  async function api(path, options) {
    const response = await fetch(path, Object.assign({}, options || {}, {
      headers: headers((options && options.headers) || {})
    }));
    if (!response.ok) {
      const text = await response.text();
      throw new Error(text || ("Request failed (" + response.status + ")"));
    }
    return response;
  }
  function showError(error) {
    pairingMessage.textContent = error && error.message ? error.message : String(error);
    connectionText.textContent = "Not connected";
    qs("#retryButton").classList.remove("hidden");
  }
  async function connect() {
    if (!token) {
      showError(new Error("No pairing token was found. Scan the QR code shown in the Windows app."));
      return;
    }
    pairingMessage.textContent = "Pairing with your PC…";
    try {
      const response = await api("/api/connect", {
        method: "POST",
        headers: { "X-Device-Id": deviceId, "X-Device-Name": deviceName() }
      });
      const result = await response.json();
      qs("#pcTitle").textContent = result.pcName || "Your PC";
      pairingPanel.classList.add("hidden");
      workspace.classList.remove("hidden");
      connectionText.textContent = "Paired";
      connected = true;
      await refreshFiles();
      openSocket();
    } catch (error) { showError(error); }
  }
  function openSocket() {
    const scheme = location.protocol === "https:" ? "wss://" : "ws://";
    socket = new WebSocket(scheme + location.host + "/ws?token=" + encodeURIComponent(token));
    socket.addEventListener("open", () => { connectionText.textContent = "Live"; });
    socket.addEventListener("message", (event) => {
      try {
        const message = JSON.parse(event.data);
        appendMessage(message.sender || "PC", message.text || "", false, message.sentAt);
      } catch (_) { }
    });
    socket.addEventListener("close", () => {
      connectionText.textContent = "Paired · reconnecting";
      if (connected) setTimeout(() => { if (connected) openSocket(); }, 1800);
    });
    socket.addEventListener("error", () => { connectionText.textContent = "Reconnecting"; });
  }
  function formatSize(bytes) {
    const units = ["B", "KB", "MB", "GB", "TB"];
    let value = Number(bytes) || 0;
    let unit = 0;
    while (value >= 1024 && unit < units.length - 1) { value /= 1024; unit++; }
    return value.toFixed(unit === 0 ? 0 : 1) + " " + units[unit];
  }
  async function refreshFiles() {
    try {
      const files = await (await api("/api/files")).json();
      const list = qs("#sharedFiles");
      list.replaceChildren();
      if (!files.length) {
        list.innerHTML = '<div class="empty-state">No files have been added on the PC yet.</div>';
        return;
      }
      files.forEach((file) => {
        const row = document.createElement("div");
        row.className = "file-row";
        const icon = document.createElement("div");
        icon.className = "file-icon";
        icon.textContent = "↓";
        const meta = document.createElement("div");
        meta.className = "file-meta";
        const name = document.createElement("strong");
        name.textContent = file.name;
        const size = document.createElement("small");
        size.textContent = file.sizeLabel || formatSize(file.size);
        meta.append(name, size);
        const button = document.createElement("button");
        button.className = "download-button";
        button.textContent = "Download";
        button.addEventListener("click", () => downloadFile(file, button));
        row.append(icon, meta, button);
        list.append(row);
      });
    } catch (_) {
      qs("#sharedFiles").innerHTML = '<div class="empty-state">Could not load shared files. Refresh and try again.</div>';
    }
  }
  async function downloadFile(file, button) {
    button.disabled = true;
    button.textContent = "Loading…";
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
      button.textContent = "Saved";
    } catch (error) {
      button.textContent = "Retry";
      alert("Download failed: " + error.message);
    } finally {
      button.disabled = false;
      if (button.textContent !== "Saved") button.textContent = "Download";
    }
  }
  function uploadFile(file) {
    const row = document.createElement("div");
    row.className = "transfer-row";
    const meta = document.createElement("div");
    meta.className = "file-meta";
    const name = document.createElement("strong");
    name.textContent = file.name;
    const status = document.createElement("small");
    status.textContent = "Waiting…";
    const track = document.createElement("div");
    track.className = "progress-track";
    const fill = document.createElement("div");
    fill.className = "progress-fill";
    track.append(fill);
    meta.append(name, status, track);
    row.append(meta);
    qs("#uploadList").prepend(row);

    const request = new XMLHttpRequest();
    request.open("POST", "/api/upload?name=" + encodeURIComponent(file.name));
    request.setRequestHeader("X-LocalDrop-Token", token);
    request.setRequestHeader("Content-Type", "application/octet-stream");
    request.upload.addEventListener("progress", (event) => {
      if (!event.lengthComputable) return;
      const percent = Math.round(event.loaded / event.total * 100);
      fill.style.width = percent + "%";
      status.textContent = percent + "% · " + formatSize(event.loaded) + " / " + formatSize(event.total);
    });
    request.addEventListener("load", () => {
      if (request.status >= 200 && request.status < 300) {
        fill.style.width = "100%";
        status.textContent = "Sent · " + formatSize(file.size);
      } else status.textContent = "Failed · " + request.status;
    });
    request.addEventListener("error", () => { status.textContent = "Connection lost"; });
    request.addEventListener("abort", () => { status.textContent = "Cancelled"; });
    status.textContent = "Uploading · " + formatSize(file.size);
    request.send(file);
  }
  function appendMessage(sender, text, mine, sentAt) {
    const messages = qs("#messages");
    const empty = messages.querySelector(".empty-state");
    if (empty) empty.remove();
    const bubble = document.createElement("div");
    bubble.className = "bubble" + (mine ? " mine" : "");
    const heading = document.createElement("small");
    const time = sentAt ? new Date(sentAt).toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" }) :
      new Date().toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
    heading.textContent = sender + " · " + time;
    const body = document.createElement("p");
    body.textContent = text;
    bubble.append(heading, body);
    messages.append(bubble);
    messages.scrollTop = messages.scrollHeight;
  }

  qs("#fileInput").addEventListener("change", (event) => {
    Array.from(event.target.files || []).forEach(uploadFile);
    event.target.value = "";
  });
  const dropZone = qs("#dropZone");
  ["dragenter", "dragover"].forEach((name) => dropZone.addEventListener(name, (event) => {
    event.preventDefault();
    dropZone.classList.add("drag");
  }));
  ["dragleave", "drop"].forEach((name) => dropZone.addEventListener(name, (event) => {
    event.preventDefault();
    dropZone.classList.remove("drag");
  }));
  dropZone.addEventListener("drop", (event) => {
    Array.from(event.dataTransfer.files || []).forEach(uploadFile);
  });
  qs("#refreshFiles").addEventListener("click", refreshFiles);
  qs("#retryButton").addEventListener("click", connect);
  document.querySelectorAll(".tab").forEach((tab) => {
    tab.addEventListener("click", () => {
      document.querySelectorAll(".tab").forEach((item) => item.classList.toggle("active", item === tab));
      document.querySelectorAll(".tab-panel").forEach((panel) =>
        panel.classList.toggle("hidden", panel.id !== tab.dataset.tab));
    });
  });
  qs("#chatForm").addEventListener("submit", (event) => {
    event.preventDefault();
    const input = qs("#chatInput");
    const text = input.value.trim();
    if (!text) return;
    if (!socket || socket.readyState !== WebSocket.OPEN) {
      alert("Chat is reconnecting. Please try again in a moment.");
      return;
    }
    socket.send(JSON.stringify({ text }));
    appendMessage("Phone", text, true);
    input.value = "";
  });
  window.addEventListener("beforeunload", () => {
    connected = false;
    if (socket) socket.close();
  });
  connect();
})();
