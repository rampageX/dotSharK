# dotSharK UI V2 · Phase 1

基线: a82f382cac294b411b130178e14602b6479faff5

隔离的 .NET Framework 4.8 / C# 7.3 WinForms UI 基础层。
现有 MainForm / 下载器 / Tray 代码尚未修改。

Windows 编译:
dotnet build ui-v2\DotSharK.UiFoundation.csproj -c Release --nologo

app.manifest 在阶段 3–4 整合时嵌入最终 dotSharK.exe；当前 UI Foundation 是独立 DLL。
