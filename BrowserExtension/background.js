const MENU_ID = "send-to-dotshark";
const HOST = "com.rampagex.dotshark";

function setupMenu() {
  chrome.contextMenus.removeAll(() => {
    chrome.contextMenus.create({
      id: MENU_ID,
      title: "发送到 dotSharK（静默下载）",
      contexts: ["page", "video", "link"]
    });
  });
}

chrome.runtime.onInstalled.addListener(setupMenu);
chrome.runtime.onStartup.addListener(setupMenu);

chrome.contextMenus.onClicked.addListener((info, tab) => {
  if (info.menuItemId !== MENU_ID) return;

  // On a link/thumbnail, send its target URL, not the listing page.
  // On a page/video context, retain existing page-URL behavior (not srcUrl).
  // Do not silently fall back to the page for javascript:/blob: links.
  const url = info.linkUrl || info.pageUrl || (tab && tab.url) || "";
  if (!/^https?:\/\//i.test(url)) return;

  chrome.runtime.sendNativeMessage(HOST, { url }, response => {
    const error = chrome.runtime.lastError;
    if (error) {
      console.error("dotSharK Native Messaging failed:", error.message);
    } else if (!response || !response.ok) {
      console.error("dotSharK did not accept the URL:", response && response.error);
    }
  });
});
