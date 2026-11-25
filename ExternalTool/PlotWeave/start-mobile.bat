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
echo   启动 PlotWeave 手机 / 局域网模式 ...
echo   保持本窗口开启，手机与电脑连接同一个 Wi-Fi。
echo   用手机浏览器打开下面列出的局域网地址。
echo.
node tools\serve.mjs --host 0.0.0.0
if errorlevel 1 pause
