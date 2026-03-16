/* ══════════════════════════════════════
   SHELL.JS — Highlight nav item dựa trên URL hiện tại
══════════════════════════════════════ */
(function() {
  const page = location.pathname.split('/').pop().replace('.html','') || 'dashboard';
  const nav  = document.querySelector(`.nav-item[data-page="${page}"]`);
  if (nav) nav.classList.add('active');
})();

/* Đồng hồ live cho tất cả trang */
function startClock(elId) {
  const el = document.getElementById(elId);
  if (!el) return;
  function tick() {
    const now = new Date();
    el.textContent =
      now.toLocaleDateString('vi-VN',{weekday:'short',day:'2-digit',month:'2-digit',year:'numeric'}) +
      '  ' + now.toLocaleTimeString('vi-VN');
  }
  tick();
  setInterval(tick, 1000);
}
