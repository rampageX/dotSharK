const status = document.getElementById("status");
document.getElementById("copy").onclick = async () => {
  try { await navigator.clipboard.writeText(browser.runtime.id); status.textContent = "已复制。"; }
  catch { status.textContent = "请手动复制：" + browser.runtime.id; }
};
document.getElementById("test").onclick = async () => {
  status.textContent = "正在检测…";
  try {
    const reply = await browser.runtime.sendNativeMessage("com.rampagex.dotshark", { action: "ping" });
    status.textContent = reply && reply.ok ? "已连接 dotSharK。" : "请升级 dotSharK 后再检测。";
  } catch (error) { status.textContent = "无法连接：" + error.message; }
};
