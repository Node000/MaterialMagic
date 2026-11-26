// The watchdog remains referenced so an unanswered DevTools request cannot strand the test.
// These scripts only connect to Chrome; they never own or stop an existing browser or web server.
export function smokeDeadline() {
  const watchdog = setTimeout(() => {
    console.error('浏览器冒烟测试超过 28 秒，已终止。');
    process.exit(124);
  }, 28_000);
  return {
    fetch: (url) => fetch(url, { signal: AbortSignal.timeout(4_000) }),
    close(socket) {
      try { socket?.close(); } finally {
        // Keep the deadline armed until Node exits even if the peer never acknowledges close.
        watchdog.unref();
      }
    },
  };
}
