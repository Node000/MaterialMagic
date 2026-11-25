@echo off
chcp 65001 >nul
cd /d "%~dp0"

where node >nul 2>nul
if errorlevel 1 (
  echo.
  echo 未找到 Node.js。请先安装 Node.js 18 或更高版本：
  echo https://nodejs.org/
  echo.
  pause
  exit /b 1
)

echo.
echo   启动 PlotWeave ...
echo   浏览器会自动打开；关闭本窗口或按 Ctrl+C 停止服务。
echo.
node tools\serve.mjs --open
if errorlevel 1 pause
