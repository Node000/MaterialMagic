#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")"

if ! command -v node >/dev/null 2>&1; then
  echo "未找到 Node.js。请先安装 Node.js 18 或更高版本：https://nodejs.org/"
  exit 1
fi

echo
echo "  启动 PlotWeave ..."
echo "  浏览器会自动打开；按 Ctrl+C 停止服务。"
echo
exec node tools/serve.mjs --open
