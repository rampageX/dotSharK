const status = document.getElementById("status");
document.getElementById("copy").onclick = async () => {
  try {
    await navigator.clipboard.writeText(chrome.runtime.id);
    status.textContent = "已复制。";
  } catch {
    status.textContent = "请手动复制：" + chrome.runtime.id;
  }
};
document.getElementById("test").onclick = () => {
  status.textContent = "正在检测…";
  chrome.runtime.sendNativeMessage("com.rampagex.dotshark", { action: "ping" }, reply => {
    const error = chrome.runtime.lastError;
    status.textContent = error ? "无法连接：" + error.message
      : reply && reply.ok ? "已连接 dotSharK。" : "请升级 dotSharK 后再检测。";
  });
};
