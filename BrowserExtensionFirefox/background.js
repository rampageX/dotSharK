const MENU_ID = "send-to-dotshark";
const HOST = "com.rampagex.dotshark";
async function setupMenu() {
  await browser.contextMenus.removeAll();
  browser.contextMenus.create({ id: MENU_ID, title: "发送到 dotSharK（静默下载）", contexts: ["page", "video", "link"] });
}
browser.runtime.onInstalled.addListener(setupMenu);
browser.runtime.onStartup.addListener(setupMenu);
browser.contextMenus.onClicked.addListener((info, tab) => {
  if (info.menuItemId !== MENU_ID) return;
  const url = info.linkUrl || info.pageUrl || (tab && tab.url) || "";
  if (!/^https?:\/\//i.test(url)) return;
  browser.runtime.sendNativeMessage(HOST, { url }).then(response => {
    if (!response || !response.ok) console.error("dotSharK did not accept the URL:", response && response.error);
  }).catch(error => console.error("dotSharK Native Messaging failed:", error.message));
});
