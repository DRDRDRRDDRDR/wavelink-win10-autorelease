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

## 本地验证 / Local validation

```bash
# 克隆本仓库与补丁仓库到同级目录后：
dotnet publish ../wavelink-win10-driver/src/WaveLinkWin10Setup/WaveLinkWin10Setup.csproj \
  -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o dist
# 再把 driver/ scripts/ 与从官网下载的 MSIX 拷贝进 dist，压缩即可
```
