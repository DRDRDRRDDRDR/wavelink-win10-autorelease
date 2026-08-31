@echo off
echo ============================================================
echo  Wave Link MSIX 获取说明 (应用本体，本仓库不托管)
echo ============================================================
echo.
echo  本仓库不包含 Elgato 专有 MSIX 安装包（版权与体积原因）。
echo  请按以下步骤提供官方 Wave Link 3.x MSIX：
echo.
echo  1) 获取官方 Wave Link 3.x 的 MSIX 安装包
echo     途径：Elgato 官方下载器导出 / 应用商店导出 / 你已有的备份
echo     （如 Elgato.WaveLink_3.2.10.4073_x64_Win10.msix）
echo  2) 将文件放入本仓库的 input\ 目录：
echo       %~dp0..\input\
echo  3) 运行一键安装：
echo       PowerShell -ExecutionPolicy Bypass -File scripts\setup_wavelink_win10.ps1
echo.
echo  当前 input\ 目录内容：
echo ------------------------------------------------------------
dir "%~dp0..\input" 2>nul || echo   (input 目录为空或不存在)
echo.
pause
