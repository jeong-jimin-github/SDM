const api = typeof browser !== "undefined" ? browser : chrome;

const DEFAULT_EXTS = [
  ".zip", ".rar", ".7z", ".tar", ".gz", ".iso", ".bin",
  ".exe", ".msi", ".msix", ".apk", ".dmg",
  ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx", ".hwp",
  ".mp3", ".flac", ".wav", ".aac", ".ogg", ".m4a",
  ".mp4", ".mkv", ".avi", ".mov", ".webm", ".wmv", ".m4v",
  ".jpg", ".jpeg", ".png", ".webp", ".gif", ".psd",
  ".epub", ".torrent"
];

const SKIP_SCHEME = /^(blob:|data:|filesystem:|about:|chrome:|edge:|moz-extension:|chrome-extension:)/i;
const SKIP_MIME = /^(text\/html|text\/css|application\/javascript|application\/xhtml)/i;

const tabMedia = new Map();
let intercepting = new Set();

api.runtime.onInstalled.addListener(async () => {
  await api.storage.local.set({
    enabled: true,
    intercept: true,
    extensions: DEFAULT_EXTS
  });
  rebuildMenus();
  pingHost();
});

rebuildMenus();
pingHost();

function rebuildMenus() {
  Promise.resolve(api.contextMenus.removeAll()).then(() => {
    api.contextMenus.create({
      id: "sdm-link",
      title: "SDM으로 다운로드",
      contexts: ["link", "image", "video", "audio"]
    });
    api.contextMenus.create({
      id: "sdm-page",
      title: "이 페이지 미디어를 SDM으로",
      contexts: ["page"]
    });
  }).catch(() => { /* menus unavailable */ });
}

api.contextMenus.onClicked.addListener(async (info, tab) => {
  if (info.menuItemId === "sdm-open") {
    await sendToHost({ type: "open" });
    return;
  }
  if (info.menuItemId === "sdm-page") {
    const hits = tabMedia.get(tab?.id) || [];
    if (hits.length === 0) {
      await sendToHost({
        type: "add",
        url: tab?.url,
        pageUrl: tab?.url,
        pageTitle: tab?.title,
        userAgent: navigator.userAgent
      });
      return;
    }
    await sendToHost({
      type: "media",
      media: hits,
      pageUrl: tab?.url,
      pageTitle: tab?.title,
      browser: browserName()
    });
    await sendToHost({ type: "open" });
    return;
  }
  const url = info.srcUrl || info.linkUrl;
  if (!url) return;
  await captureAndSend(url, tab, info);
});

if (api.downloads.onDeterminingFilename) {
  api.downloads.onDeterminingFilename.addListener((item, suggest) => {
    interceptDetermining(item, suggest);
    return true;
  });
} else {
  api.downloads.onCreated.addListener((item) => interceptCreated(item));
}

async function interceptDetermining(item, suggest) {
  try {
    const cfg = await api.storage.local.get({ enabled: true, intercept: true, extensions: DEFAULT_EXTS });
    if (!cfg.enabled || !cfg.intercept || item.byExtensionId || !shouldIntercept(item, cfg.extensions)) {
      suggest();
      return;
    }
    const name = cleanFilename(item.filename);
    suggest({ filename: name || item.filename || "download" });
    await stealDownload(item, name);
  } catch (err) {
    console.warn("SDM intercept failed", err);
    try { suggest(); } catch (_) { /* ignore */ }
  }
}

async function interceptCreated(item) {
  const cfg = await api.storage.local.get({ enabled: true, intercept: true, extensions: DEFAULT_EXTS });
  if (!cfg.enabled || !cfg.intercept) return;
  if (item.byExtensionId) return;
  if (!shouldIntercept(item, cfg.extensions)) return;
  await stealDownload(item, cleanFilename(item.filename));
}

async function stealDownload(item, filename) {
  if (intercepting.has(item.id)) return;
  intercepting.add(item.id);
  try {
    await api.downloads.cancel(item.id);
    try { await api.downloads.erase({ id: item.id }); } catch (_) { /* ignore */ }
    const tab = item.tabId ? await safeTab(item.tabId) : null;
    await captureAndSend(item.finalUrl || item.url, tab, {
      filename,
      mime: item.mime,
      fileSize: item.fileSize > 0 ? item.fileSize : undefined,
      referrer: item.referrer
    });
  } finally {
    intercepting.delete(item.id);
  }
}

api.webRequest.onHeadersReceived.addListener(
  (details) => {
    if (details.tabId < 0) return;
    const mime = header(details.responseHeaders, "content-type") || "";
    if (!/video\/|audio\/|mpegurl|octet-stream|mp4|webm|mp3|aac/i.test(mime) &&
        !/\.(mp4|m3u8|mpd|webm|mkv|mp3|m4a)(\?|$)/i.test(details.url)) {
      return;
    }
    const sizeRaw = header(details.responseHeaders, "content-length");
    const hit = {
      url: details.url,
      mime,
      size: sizeRaw ? Number(sizeRaw) : undefined,
      pageUrl: details.initiator || details.originUrl,
      pageTitle: ""
    };
    const list = tabMedia.get(details.tabId) || [];
    if (!list.some((x) => x.url === hit.url)) {
      list.unshift(hit);
      tabMedia.set(details.tabId, list.slice(0, 80));
      api.tabs.get(details.tabId).then((tab) => sendToHost({
        type: "media",
        media: [{ ...hit, pageUrl: tab?.url || hit.pageUrl, pageTitle: tab?.title || "" }],
        browser: browserName()
      })).catch(() => { /* SDM may not be running */ });
    }
  },
  { urls: ["<all_urls>"] },
  ["responseHeaders"]
);

api.tabs.onRemoved.addListener((id) => tabMedia.delete(id));

api.runtime.onMessage.addListener((msg, sender, sendResponse) => {
  (async () => {
    if (msg?.type === "download-url" && (msg.url === "open-app" || !msg.url)) {
      await sendToHost({ type: "open" });
      sendResponse({ ok: true });
      return;
    }
    if (msg?.type === "media-found") {
      const tabId = sender.tab?.id;
      if (tabId == null) return;
      const list = tabMedia.get(tabId) || [];
      const added = [];
      for (const url of msg.urls || []) {
        if (!list.some((x) => x.url === url)) {
          const hit = {
            url,
            mime: msg.mime,
            pageUrl: msg.pageUrl,
            pageTitle: msg.title
          };
          list.unshift(hit);
          added.push(hit);
        }
      }
      tabMedia.set(tabId, list.slice(0, 80));
      if (added.length > 0) {
        await sendToHost({ type: "media", media: added, pageUrl: msg.pageUrl, pageTitle: msg.title });
      }
      sendResponse({ ok: true, count: list.length });
      return;
    }
    if (msg?.type === "get-status") {
      const ping = await pingHost();
      sendResponse({
        connected: !!ping?.ok,
        version: ping?.version,
        media: tabMedia.get(msg.tabId) || []
      });
      return;
    }
    if (msg?.type === "download-url") {
      const tabs = await api.tabs.query({ active: true, currentWindow: true });
      await captureAndSend(msg.url, tabs[0], { filename: msg.filename, mime: msg.mime });
      sendResponse({ ok: true });
    }
  })();
  return true;
});

function shouldIntercept(item, extensions) {
  const url = item.finalUrl || item.url || "";
  if (SKIP_SCHEME.test(url)) return false;
  if (item.mime && SKIP_MIME.test(item.mime)) return false;
  const name = basename(item.filename || url).toLowerCase();
  const dot = name.lastIndexOf(".");
  const ext = dot >= 0 ? name.slice(dot) : "";
  if (ext && extensions.includes(ext)) return true;
  if (item.mime && /^(application\/(zip|x-msdownload|octet-stream|pdf)|video\/|audio\/)/i.test(item.mime))
    return true;
  return false;
}

async function captureAndSend(url, tab, extra = {}) {
  const cookies = await cookieHeader(url);
  return sendToHost({
    type: "add",
    url,
    filename: extra.filename || cleanFilename(url.split("?")[0]),
    referrer: extra.referrer || tab?.url,
    pageUrl: tab?.url,
    pageTitle: tab?.title,
    cookies,
    userAgent: navigator.userAgent,
    mime: extra.mime,
    fileSize: extra.fileSize,
    browser: browserName()
  });
}

async function sendToHost(message) {
  const cfg = await loadConfig();
  const stored = await api.storage.local.get({ token: cfg.token || "" });
  message.token = stored.token || cfg.token || "";
  message.browser = message.browser || browserName();
  try {
    const res = await api.runtime.sendNativeMessage(cfg.nativeHost, message);
    if (res?.token) await api.storage.local.set({ token: res.token });
    return res;
  } catch (nativeErr) {
    try {
      const path = message.type === "ping" ? "ping" : message.type;
      const r = await fetch(`http://127.0.0.1:${cfg.port}/v1/${path}`, {
        method: message.type === "ping" ? "GET" : "POST",
        headers: {
          "Content-Type": "application/json",
          "X-SDM-Token": message.token || ""
        },
        body: message.type === "ping" ? undefined : JSON.stringify(message)
      });
      const res = await r.json();
      if (res?.token) await api.storage.local.set({ token: res.token });
      return res;
    } catch (httpErr) {
      console.warn("SDM unreachable", nativeErr, httpErr);
      return { ok: false, error: "host-offline" };
    }
  }
}

async function pingHost() {
  const res = await sendToHost({ type: "ping" });
  return res;
}

async function loadConfig() {
  try {
    const r = await fetch(api.runtime.getURL("config.json"));
    return await r.json();
  } catch {
    return { token: "", port: 47832, nativeHost: "com.sdm.host" };
  }
}

async function cookieHeader(url) {
  try {
    const cookies = await api.cookies.getAll({ url });
    return cookies.map((c) => `${c.name}=${c.value}`).join("; ");
  } catch {
    return "";
  }
}

async function safeTab(id) {
  try { return await api.tabs.get(id); }
  catch { return null; }
}

function header(headers, name) {
  const h = (headers || []).find((x) => x.name.toLowerCase() === name);
  return h?.value;
}

function basename(path) {
  if (!path) return "";
  return path.replace(/\\/g, "/").split("/").pop();
}

function cleanFilename(name) {
  const base = basename(name || "");
  if (!base) return "";
  const stripped = base.replace(/\.crdownload$/i, "").replace(/\.partial$/i, "").replace(/\.tmp$/i, "");
  if (/^unconfirmed\b/i.test(stripped)) return "";
  return stripped;
}

function browserName() {
  const ua = navigator.userAgent;
  if (ua.includes("Edg/")) return "Edge";
  if (ua.includes("Firefox/")) return "Firefox";
  if (ua.includes("Brave") || ua.includes("Chrome/")) return "Chrome";
  return "Browser";
}
