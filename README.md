# Wave Link Win10 自动发布仓库 · Auto-release for Wave Link (Win10)

## 用途 / Purpose

本仓库负责**自动检测 Elgato 官方 Wave Link 的最新 Windows 版本**，下载其 MSIX 安装包，从配套的补丁仓库构建出打过补丁的安装器，最终把**完整安装包**打包并发布到 GitHub Release。

This repository automatically detects the latest Windows version of Elgato Wave Link from the official release notes, downloads its MSIX, builds the patched installer from the companion patch repo, and publishes a complete install package to GitHub Releases.

## 两个仓库的分工 / Two-repo split

| 角色 Role | 仓库 Repo | 内容 Contents |
| --- | --- | --- |
| 补丁仓库 Patch repo | [`DRDRDRRDDRDR/wavelink-win10-driver`](https://github.com/DRDRDRRDDRDR/wavelink-win10-driver) | 打过补丁的安装器源码、官方驱动、脚本、说明文档（MinVersion 22000→19041、自签证书、驱动安装等） |
| 自动发版仓库 Auto-release repo (本仓库) | [`DRDRDRRDDRDR/wavelink-win10-autorelease`](https://github.com/DRDRDRRDDRDR/wavelink-win10-autorelease) | 自动检测 + 构建 + 打包 + 发版的工作流与说明 |

- **补丁仓库**：存放源码与补丁本身，不负责自动发版。
- **本仓库（自动发版）**：负责自动检测、构建、打包、发布。

## 原理 / How it works

1. 每日 **06:00 UTC**（或手动 `workflow_dispatch`）触发。
2. 通过 **Zendesk REST API** 读取 Wave Link Release Notes 文章列表，取最高的 Windows 版本号。
3. 读取该文章正文，用正则抽取官方 CDN 上的 x64 MSIX 直链
   `https://edge.elgato.com/egc/windows/ewlw/{ver}/Stable/Elgato.WaveLink_{ver}.{build}_x64.msix`，
   下载并做完整性校验（ZIP 魔数）。
4. 克隆补丁仓库，`dotnet publish` 构建出自包含（无需 .NET 运行时）的安装器 exe。
5. 将「安装器 + 驱动 + 脚本 + MSIX」拼成完整包，压缩后发布到 Release（标签 `wavelink-{版本}`）。
6. 把已发布版本号写入 `wavelink-app-version.txt`；下次定时运行若版本相同则跳过，避免重复发版。

> 注：MSIX 为 Elgato 专有软件，本仓库仅在构建时从其官方 CDN 拉取并随完整包分发，不修改、不单独二次托管。

## 合规提示 / Compliance

完整包内包含的 MSIX 来自 Elgato 官方，使用者需遵守 Elgato 的许可条款。本自动化仅做技术打包，不拥有该 MSIX 的版权。

## 使用教程 / Usage Guide

### 使用教程（Windows 10）

1. **下载**：在本仓库 **Releases** 页面下载最新的 `wavelink-win10-complete.zip`（稳定版），或带 `[BETA]` 标记的预发行包（例如 `wavelink-win10-beta-3.3.0.4108.zip`）。
2. **解压**：把 zip 解压到任意目录，得到 `WaveLinkWin10Setup.exe` + `driver/` + `scripts/` + `input/`（内含官方 MSIX）。
3. **开启开发者模式**：`设置 → 更新和安全 → 开发者选项 → 开发人员模式` 打开。这是免签名安装 MSIX 的前提，未开启会导致安装失败。
4. **运行安装器**：右键 `WaveLinkWin10Setup.exe` → **以管理员身份运行**。
5. **一键安装**：在界面点「环境检查」确认开发者模式已开；再点「一键运行全部」，安装器会自动：打补丁绕过 Win11 限制 → 以开发者模式免签名安装应用 → 安装 Wave Link 驱动。
6. **验证**：打开 Wave Link，确认能正常识别麦克风/混音设备；或在安装器里点「验证」查看服务状态。
7. **更新**：稳定版由本仓库每日 **06:00 UTC** 自动检测 Elgato 新版本并发版，用户只需重新下载最新 Release。Beta 版需手动下载对应的预发行包。

> 提示：若你已有自己的官方 MSIX，可把它放进 `input/` 目录覆盖，重跑安装器即可改装该版本。

### 故障排查 / Troubleshooting

- 安装报错「需要开发者模式」 → 回到第 3 步开启开发者模式。
- 应用装完打不开 → 确认系统是 Win10 且已装好运行库；查看安装器日志（`txtLog`）。
- 驱动安装失败 → 以管理员身份运行，并确认 `driver/WaveLinkDriver_3.0.0.466_x64.msi` 存在。

### Usage Guide (Windows 10)

1. **Download**: From this repo's **Releases**, grab the latest `wavelink-win10-complete.zip` (stable), or a `[BETA]` pre-release (e.g. `wavelink-win10-beta-3.3.0.4108.zip`).
2. **Extract**: Unzip to any folder. You get `WaveLinkWin10Setup.exe` + `driver/` + `scripts/` + `input/` (containing the official MSIX).
3. **Enable Developer Mode**: `Settings → Update & Security → For developers → Developer mode` (on). Required for unsigned MSIX install.
4. **Run the installer**: Right-click `WaveLinkWin10Setup.exe` → **Run as administrator**.
5. **One-click install**: Click "环境检查 / Environment Check" to confirm Developer Mode is on; then click "一键运行全部 / Run All". The installer auto-patches the Win11 gate, installs the app unsigned via Developer Mode, and installs the Wave Link driver.
6. **Verify**: Open Wave Link and confirm your mic/mixer devices are detected; or click "验证 / Verify" in the installer to check service status.
7. **Updates**: Stable builds are auto-detected and published daily at **06:00 UTC** — just re-download the latest Release. Beta builds must be downloaded manually from their pre-release.

> Tip: If you have your own official MSIX, drop it into `input/` to override, then re-run the installer to install that version.

### Troubleshooting

- "Developer mode required" error → go back to step 3 and enable Developer Mode.
- App installed but won't launch → make sure you're on Win10 with the required runtime; check the installer log (`txtLog`).
- Driver install failed → run as administrator and confirm `driver/WaveLinkDriver_3.0.0.466_x64.msi` exists.

## 本地验证 / Local validation

```bash
# 克隆本仓库与补丁仓库到同级目录后：
dotnet publish ../wavelink-win10-driver/src/WaveLinkWin10Setup/WaveLinkWin10Setup.csproj \
  -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist
# 再把 driver/ scripts/ 与从官网下载的 MSIX 拷贝进 dist，压缩即可
```
