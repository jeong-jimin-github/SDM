const api = typeof browser !== "undefined" ? browser : chrome;

const enabledEl = document.getElementById("enabled");
const interceptEl = document.getElementById("intercept");
const statusEl = document.getElementById("status");
const mediaEl = document.getElementById("media");

async function refresh() {
  const cfg = await api.storage.local.get({ enabled: true, intercept: true });
  enabledEl.checked = cfg.enabled !== false;
  interceptEl.checked = cfg.intercept !== false;
  const [tab] = await api.tabs.query({ active: true, currentWindow: true });
  const st = await api.runtime.sendMessage({ type: "get-status", tabId: tab?.id });
  statusEl.textContent = st?.connected
    ? `앱 연결됨${st.version ? " · v" + st.version : ""}`
    : "앱이 꺼져 있습니다. SDM을 실행하세요.";
  mediaEl.innerHTML = "";
  const hits = st?.media || [];
  if (!hits.length) {
    const li = document.createElement("li");
    li.textContent = "아직 감지된 미디어가 없습니다.";
    li.style.cursor = "default";
    mediaEl.appendChild(li);
    return;
  }
  for (const hit of hits) {
    const li = document.createElement("li");
    const short = (hit.url || "").split("?")[0];
    li.innerHTML = `<span>${escapeHtml(short.slice(-72))}</span><span class="mime">${escapeHtml(hit.mime || "")}</span>`;
    li.addEventListener("click", () => {
      api.runtime.sendMessage({ type: "download-url", url: hit.url, mime: hit.mime });
      statusEl.textContent = "SDM으로 보냈습니다.";
    });
    mediaEl.appendChild(li);
  }
}

function escapeHtml(s) {
  return s.replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[c]));
}

enabledEl.addEventListener("change", () => api.storage.local.set({ enabled: enabledEl.checked }));
interceptEl.addEventListener("change", () => api.storage.local.set({ intercept: interceptEl.checked }));
document.getElementById("open").addEventListener("click", () =>
  api.runtime.sendMessage({ type: "download-url", url: "open-app" }));
document.getElementById("refresh").addEventListener("click", refresh);
refresh();
