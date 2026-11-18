# Firefox 系插件测试版

1. 解压完整测试包，退出正在运行的 dotSharK，用其中 dotSharK.exe 覆盖现有程序。
2. 在 Firefox 地址栏打开 `about:debugging#/runtime/this-firefox`，点击「临时载入附加组件」，选择此目录的 `manifest.json`。Floorp 如支持相同调试页面，也可使用此方法。
3. 点击插件图标，复制配对码；在 dotSharK 的「配置 → 插件注册」选择 Firefox 或 Floorp，粘贴并注册，回到插件点击「检测连接」。
4. 在网页、视频或链接上右键，选择「发送到 dotSharK（静默下载）」。链接右键发送目标，页面和视频右键发送所在网页地址。

Firefox 正式版只允许永久安装经过 Mozilla 签名的扩展；本测试包尚未签名，临时加载的扩展在浏览器退出后需要重新加载。不同 Firefox 衍生浏览器的签名及临时加载政策可能不同。程序移动位置后，请在 dotSharK 中重新注册。
