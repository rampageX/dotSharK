const MENU_ID = "send-to-dotshark";
const HOST = "com.rampagex.dotshark";

function setupMenu() {
  chrome.contextMenus.removeAll(() => {
    chrome.contextMenus.create({
      id: MENU_ID,
      title: "发送到 dotSharK（最高画质 · AV1 优先）",
      contexts: ["page", "video"]
    });
  });
}

chrome.runtime.onInstalled.addListener(setupMenu);
chrome.runtime.onStartup.addListener(setupMenu);

chrome.contextMenus.onClicked.addListener((info, tab) => {
  if (info.menuItemId !== MENU_ID) return;

  // Send the page URL, not the media-stream URL (m3u8/blob/etc.).
  const url = info.pageUrl || (tab && tab.url) || "";
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
