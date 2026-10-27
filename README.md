# 校园网自动登录 🌐

Windows 校园网自动认证工具：自动填写账号密码、选择运营商，支持登录 Windows 后自动连接、托盘驻留和验证网址结果弹窗。

**版本：v1.9.1「验证网址弹窗」 · 作者：VIP666233 · 许可证：MIT**

本仓库对应 `校园网自动登录_v1.9.1_验证网址弹窗.zip`。源码从作者提供的该版 EXE 恢复，保留该版本的功能与界面；不包含后续休眠唤醒重连、多校适配或华电专用版改动。恢复方式和原文件校验值见 [SOURCE_PROVENANCE.md](SOURCE_PROVENANCE.md)。

## 📥 下载软件

文件名：`校园网自动登录pc.zip`

- [百度网盘下载](https://pan.baidu.com/s/1ejlgOEPxdq58o6HWRpK4nQ?pwd=fwmz)，提取码：`fwmz`
- [中国移动云盘下载](https://yun.139.com/shareweb/#/w/i/2yA8v4yT7Nrnf)

以上保留原仓库的成品下载链接。本仓库提供 v1.9.1「验证网址弹窗」版源码，也可以按下方步骤自行编译。

## ✨ 功能

- 自动填写校园网账号密码，识别校园网、中国移动、中国联通和中国电信。
- 登录 Windows 后等待网络就绪，再等待设定时间自动认证，默认 3 秒。
- 认证入口跳转后在同一次连接中继续登录。
- 点击连接、隐藏或退出时提示保存已修改的设置。
- 关闭窗口后驻留右下角通知区域，可从托盘彻底退出。
- 使用独立浏览器打开验证网站，完成后自动关闭。
- 右下角通知显示运营商及 `验证网址主机名官网登录成功`，支持滑入、滑出动画。
- 密码使用 Windows DPAPI 按当前用户加密保存。

## 🚀 从零开始使用

0. 从上面的网盘链接下载成品，或按下方方法自行编译。解压后运行 `校园网自动登录.exe`，保留旁边的 `.exe.config` 文件。
1. 连接校园 Wi-Fi 或校园网网线。只给校园 Wi-Fi 开启自动连接，取消其他 Wi-Fi 的自动连接。软件本身不切换 Wi-Fi，也不按学校 SSID 区分网络。
2. 填写本校认证地址、账号密码，并选择实际运营商。旧版默认显示 `http://9.9.9.9.com/`，程序实际访问时转换为 `http://9.9.9.9/`；其他地址按学校提供的信息填写。
3. 勾选“开机自动登录”，启动等待保持默认 3 秒，点击“保存设置”。
4. 点击一次“立即登录 / 测试”。如设置已更改，按提示选择是否保存。
5. 等待认证和网站验证完成，查看运行日志及右下角结果弹窗。默认验证网站为 `https://www.bilibili.com/`，可在高级设置修改。
6. 关闭窗口后软件留在通知区域，双击图标打开，右键“退出”彻底关闭。

更换软件位置后，运行新位置的 EXE 并保存设置，以更新开机启动路径。高级设置通常保持默认，需要适配其他页面时再填写控件 CSS 选择器、选项值或学校明确要求的账号后缀。

“官网登录成功”是此版本的提示文案，表示配置的验证网页加载成功，并非登录了该网站的用户账号。认证阶段最多 40 秒，后续网站验证另计。

## 🛠 编译

运行环境：Windows 10/11、.NET Framework 4.8，以及 Microsoft Edge 或 Google Chrome。

在仓库根目录打开 **Windows PowerShell**，执行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\build.ps1
```

生成文件位于：

```text
dist/
├── 校园网自动登录.exe
└── 校园网自动登录.exe.config
```

无需安装 NuGet 依赖、Node.js 或 .NET SDK 即可编译。脚本使用 Windows 自带的 .NET Framework C# 编译器。源码恢复时按 C# 5 输出，保留了部分编译器临时变量，因此编译会有少量已知警告，详见恢复说明。

## 🧪 测试

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\run-connectivity-tests.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\run-protocol-tests.ps1
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\tests\run-flow-tests.ps1
node .\tests\portal-navigation.test.cjs
```

最后一项需要 Node.js 18 或更新版本。测试使用虚拟数据、模拟浏览器接口或本机回环连接，不读取真实账号配置，也不修改开机启动项。GitHub Actions 会自动编译并运行上述测试。

源码保留了原版的 `--self-test` 开发入口；它会打开界面、启动浏览器并读取本机设置，不属于上述隔离测试。

## 📁 项目结构

| 路径 | 内容 |
| --- | --- |
| `src/MainForm.cs` | 设置窗口、运行日志、托盘和开机连接 |
| `src/Portal.js` | 网页表单识别、运营商选择和自动填写 |
| `src/LoginService.cs` | 登录流程、跳转和超时 |
| `src/BrowserSession.cs` | 独立浏览器会话及控制接口 |
| `src/ConnectivityCheck.cs` | 验证网页是否正常加载 |
| `src/ConnectionNotice.cs` | 运营商与验证结果文案 |
| `src/LoginNotification.cs` | 右下角通知及动画 |
| `src/Settings.cs` | 设置、密码加密与开机启动 |
| `src/assets/` | 原版图标和 Logo |
| `tests/` | 隔离回归测试 |
| `build.ps1` | Windows 编译脚本 |

设置和日志保存在 `%LOCALAPPDATA%\CampusNetLoginHelper`。源码包不包含个人配置、浏览器记录或账号密码。报告适配问题时，请隐藏截图、日志里的个人信息。

## 🏫 适用范围

这是指定的 v1.9.1 历史版本，保留原有高级设置里的 QQ 交流群信息。它适用于可识别的账号密码和运营商表单，不保证支持所有学校；没有后续版本的休眠唤醒重连和空白区域兼容逻辑。

## 📄 开源许可证

本项目采用 [MIT License](LICENSE)。允许使用、修改和分发，须保留版权及许可证声明。

Made by **VIP666233**
