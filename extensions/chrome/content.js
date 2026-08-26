(function () {
  const api = typeof browser !== "undefined" ? browser : chrome;

  function collect() {
    const urls = new Set();
    document.querySelectorAll("video, audio, source").forEach((el) => {
      ["src", "currentSrc"].forEach((k) => {
        const v = el[k];
        if (v && /^https?:/i.test(v)) urls.add(v);
      });
    });
    document.querySelectorAll("a[href]").forEach((a) => {
      const href = a.href || "";
      if (/\.(mp4|mkv|webm|mp3|m4a|flac|zip|pdf|exe)(\?|$)/i.test(href)) urls.add(href);
    });
    if (urls.size === 0) return;
    api.runtime.sendMessage({
      type: "media-found",
      urls: [...urls],
      pageUrl: location.href,
      title: document.title
    });
  }

  collect();
  const obs = new MutationObserver(() => {
    clearTimeout(obs._t);
    obs._t = setTimeout(collect, 800);
  });
  obs.observe(document.documentElement, { childList: true, subtree: true, attributes: true, attributeFilter: ["src"] });
})();
