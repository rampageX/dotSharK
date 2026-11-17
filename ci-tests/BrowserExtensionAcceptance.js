"use strict";

const assert = require("node:assert/strict");
const fs = require("node:fs");
const path = require("node:path");
const vm = require("node:vm");

const root = path.join(__dirname, "..", "BrowserExtension");
const script = fs.readFileSync(path.join(root, "background.js"), "utf8");
const manifest = JSON.parse(fs.readFileSync(path.join(root, "manifest.json"), "utf8"));
const registrations = {};
const menus = [];
const nativeMessages = [];
const chrome = {
  contextMenus: {
    removeAll(callback) { menus.length = 0; callback(); },
    create(menu) { menus.push(menu); },
    onClicked: { addListener(handler) { registrations.clicked = handler; } }
  },
  runtime: {
    lastError: null,
    onInstalled: { addListener(handler) { registrations.installed = handler; } },
    onStartup: { addListener(handler) { registrations.startup = handler; } },
    sendNativeMessage(host, message, callback) {
      nativeMessages.push({ host, message });
      callback({ ok: true });
    }
  }
};
vm.runInNewContext(script, { chrome, console }, { filename: "background.js" });

let checks = 0;
function check(value, message) {
  assert.ok(value, message);
  console.log("PASS " + (++checks) + " " + message);
}
check(manifest.manifest_version === 3 && manifest.permissions.includes("nativeMessaging") &&
      manifest.permissions.includes("contextMenus"), "Manifest V3 permissions preserved");
registrations.installed();
check(menus.length === 1 && menus[0].title === "发送到 dotSharK（静默下载）",
      "one concise silent-download menu");
check(["page", "video", "link"].every(context => menus[0].contexts.includes(context)),
      "menu is available on pages, videos and links");
registrations.startup();
check(menus.length === 1, "startup does not duplicate menu entries");

const item = { menuItemId: "send-to-dotshark" };
registrations.clicked({ ...item, pageUrl: "https://site.example/list" }, { url: "https://site.example/tab" });
check(nativeMessages.at(-1).message.url === "https://site.example/list",
      "page context sends video page URL");
registrations.clicked({ ...item, pageUrl: "https://site.example/list", srcUrl: "blob:video" });
check(nativeMessages.at(-1).message.url === "https://site.example/list",
      "video context ignores blob media source");
registrations.clicked({ ...item, pageUrl: "https://site.example/list", linkUrl: "https://site.example/watch?v=2" });
check(nativeMessages.at(-1).message.url === "https://site.example/watch?v=2",
      "thumbnail/link context sends target, not list page");
let count = nativeMessages.length;
registrations.clicked({ ...item, pageUrl: "https://site.example/list", linkUrl: "javascript:void(0)" });
check(nativeMessages.length === count, "non-HTTP link never falls back to wrong listing page");
registrations.clicked({ ...item, linkUrl: "https://site.example/watch?v=3" });
check(nativeMessages.at(-1).message.url === "https://site.example/watch?v=3",
      "link target works without pageUrl");
count = nativeMessages.length;
registrations.clicked({ ...item, pageUrl: "file:///C:/secret.txt" });
registrations.clicked({ menuItemId: "unrelated", pageUrl: "https://site.example/list" });
check(nativeMessages.length === count, "file URLs and other menu IDs ignored");
check(nativeMessages.every(entry => entry.host === "com.rampagex.dotshark"),
      "native messaging host unchanged");
console.log("PASS " + checks + " browser extension assertions");
