/* ══════════════════════════════════════
   UI.JS — Toast · Modal · Helpers · Pagination
══════════════════════════════════════ */

/* ── TOAST ── */
function toast(msg, type='info', duration=3000) {
  const icons = { success:'✅', error:'❌', info:'ℹ️', warn:'⚠️' };
  const el = document.createElement('div');
  el.className = `toast toast-${type}`;
  el.innerHTML = `<span>${icons[type]||'ℹ️'}</span><span>${msg}</span>`;
  document.getElementById('toast-container').appendChild(el);
  setTimeout(() => el.remove(), duration);
}

/* ── MODAL ── */
function openModal(id) {
  document.querySelectorAll('.modal').forEach(m => m.style.display = 'none');
  const m = document.getElementById('modal-' + id);
  if (m) m.style.display = 'block';
  document.getElementById('overlay').classList.add('open');
}
function closeModal() {
  document.getElementById('overlay').classList.remove('open');
  setTimeout(() => document.querySelectorAll('.modal').forEach(m => m.style.display = 'none'), 200);
}
// Close on overlay click (not on modal itself)
document.addEventListener('DOMContentLoaded', () => {
  const overlay = document.getElementById('overlay');
  if (overlay) overlay.addEventListener('click', closeModal);
});

/* ── NAV ── */
function go(page) {
  document.querySelectorAll('.page').forEach(p => p.classList.remove('active'));
  document.querySelectorAll('.nav-item[data-page]').forEach(n => n.classList.remove('active'));
  const pg = document.getElementById('page-' + page);
  const nv = document.querySelector(`.nav-item[data-page="${page}"]`);
  if (pg) pg.classList.add('active');
  if (nv) nv.classList.add('active');
}

/* ── CONFIRM DELETE ── */
function delRow(btn, label='mục này') {
  if (confirm(`Xác nhận xóa ${label}?`)) {
    btn.closest('tr').remove();
    toast('Đã xóa thành công', 'success');
  }
}

/* ── WAVEFORM GENERATOR ── */
function makeWaveform(count=28) {
  const heights = [4,6,8,12,16,20,18,14,10,8,12,18,24,20,16,12,8,14,20,18,12,8,10,16,14,10,6,4];
  return Array.from({length: count}, (_,i) =>
    `<div class="wv-bar" style="height:${heights[i % heights.length]}px"></div>`
  ).join('');
}

/* ── MAP PREVIEW EMBED ── */
function makeMapEmbed(lat, lng, name='') {
  if (!lat || !lng) return '<div style="display:flex;align-items:center;justify-content:center;height:200px;background:var(--s2);border-radius:8px;color:var(--muted)">📍 Map preview</div>';
  const bbox = `${lng-.003},${lat-.002},${lng+.003},${lat+.002}`;
  return `<div style="background:var(--s2);border-radius:8px;overflow:hidden;border:1px solid var(--border)">
    <iframe width="100%" height="200" frameborder="0" scrolling="no" marginheight="0" marginwidth="0"
      src="https://www.openstreetmap.org/export/embed.html?bbox=${bbox}&layer=mapnik&marker=${lat},${lng}"
      style="border:none;display:block"></iframe>
  </div>`;
}

/* ── PAGINATION ── */
function makePagination(total, perPage, current, onPage) {
  const pages = Math.ceil(total / perPage);
  if (pages <= 1) return '';
  let html = `<div class="pagination">`;
  if (current > 1) html += `<button onclick="${onPage}(${current-1})" class="pagi-btn">‹</button>`;
  for (let i=1; i<=pages; i++) {
    if (i===current) html += `<button class="pagi-btn active">${i}</button>`;
    else html += `<button onclick="${onPage}(${i})" class="pagi-btn">${i}</button>`;
  }
  if (current < pages) html += `<button onclick="${onPage}(${current+1})" class="pagi-btn">›</button>`;
  html += `</div>`;
  return html;
}

/* ── BADGE HELPERS ── */
function audioStatusBadge(s) {
  if (s==='ok') return '<span class="badge b-green">✔ OK</span>';
  if (s==='pending') return '<span class="badge b-yellow">⏳ Chờ duyệt</span>';
  if (s==='error') return '<span class="badge b-red">✗ Lỗi</span>';
  return '<span class="badge b-gray">—</span>';
}

function poiStatusBadge(s) {
  if (s==='active') return '<span class="badge b-green">✔ Hiển thị</span>';
  if (s==='draft') return '<span class="badge b-yellow">📝 Nháp</span>';
  return '<span class="badge b-gray">—</span>';
}

function deviceStatusBadge(s) {
  if (s==='active') return '<span class="badge b-green">● Online</span>';
  if (s==='idle') return '<span class="badge b-yellow">○ Idle</span>';
  return '<span class="badge b-gray">○ Offline</span>';
}

function platformBadge(p) {
  if (p==='android') return '<span class="badge b-green">🤖 Android</span>';
  if (p==='ios') return '<span class="badge b-blue">🍎 iOS</span>';
  return '<span class="badge b-gray">—</span>';
}

function langFlag(l) {
  return l==='vi' ? '🇻🇳 Tiếng Việt' : '🇺🇸 English';
}

/* ── EXPORT CSV STUB ── */
function exportCSV(type) {
  toast(`Xuất CSV ${type} — Sẽ triển khai trong phiên bản production`, 'info');
}

/* ── TABLE LOADING ── */
function showTableLoading(tbodyId, cols) {
  const el = document.getElementById(tbodyId); if(!el) return;
  const cell = `<td><div style="height:13px;background:var(--s3);border-radius:4px;animation:pulse-glow 1.5s infinite"></div></td>`;
  el.innerHTML = Array(6).fill(`<tr>${Array(cols).fill(cell).join('')}</tr>`).join('');
}

function showTableError(tbodyId, cols, msg) {
  const el = document.getElementById(tbodyId); if(!el) return;
  el.innerHTML = `<tr><td colspan="${cols}"><div class="empty-state"><div class="icon">❌</div><p style="color:var(--red)">${msg}</p></div></td></tr>`;
}

/* ── TOGGLE ALL CHECKBOX ── */
function toggleAll(cb, tbodyId) {
  document.getElementById(tbodyId)?.querySelectorAll('input[type=checkbox]').forEach(c => c.checked = cb.checked);
}

// ── PAGINATION ──
function renderPagination(containerId, {page,totalPages,total,pageSize}, onPageChange) {
  const c=document.getElementById(containerId); if(!c) return;
  const from=Math.min((page-1)*pageSize+1, total), to=Math.min(page*pageSize, total);
  const info=c.querySelector('.pagi-info'); if(info) info.textContent=`${from}–${to} / ${total}`;
  const btns=c.querySelector('.pagi-btns'); if(!btns) return;
  if(totalPages<=1){ btns.innerHTML=''; return; }
  const pages=[]; for(let i=1;i<=totalPages;i++){ if(i===1||i===totalPages||Math.abs(i-page)<=1) pages.push(i); else if(pages[pages.length-1]!=='…') pages.push('…'); }
  const fn=onPageChange.toString();
  let h=`<div class="pg" ${page>1?`onclick="(${fn})(${page-1})"`:''}>‹</div>`;
  pages.forEach(p=>{
    if(p==='…') h+=`<div class="pg" style="cursor:default;border:none">…</div>`;
    else h+=`<div class="pg ${p===page?'on':''}" onclick="(${fn})(${p})">${p}</div>`;
  });
  h+=`<div class="pg" ${page<totalPages?`onclick="(${fn})(${page+1})"`:''}>›</div>`;
  btns.innerHTML=h;
}

// ── BADGES ──
function statusBadge(s) {
  const m={
    active:['b-green','● Hoạt động'], inactive:['b-gray','○ Ngừng'],
    draft:['b-yellow','○ Nháp'],      pending:['b-yellow','⏳ Chờ'],
    submitted:['b-blue','📤 Chờ duyệt'], approved:['b-green','✔ Đã duyệt'],
    rejected:['b-red','✗ Từ chối'],   verified:['b-green','✔ Xác thực'],
    unverified:['b-yellow','○ Chưa XN'],
  };
  const [cls,lbl]=m[(s||'').toLowerCase()]||['b-gray',s||'—'];
  return `<span class="badge ${cls}">${lbl}</span>`;
}
function langBadge(l) {
  const m={'vi':'🇻🇳 VI','vi-vn':'🇻🇳 VI','en':'🇺🇸 EN','en-us':'🇺🇸 EN','ja':'🇯🇵 JA','ja-jp':'🇯🇵 JA','ko':'🇰🇷 KO','fr':'🇫🇷 FR','zh':'🇨🇳 ZH','zh-cn':'🇨🇳 ZH'};
  return `<span class="badge b-orange">${m[(l||'').toLowerCase()]||l||'—'}</span>`;
}

// ── FORMATTERS ──
function fmtDate(iso) {
  if(!iso) return '—';
  try{ return new Date(iso).toLocaleDateString('vi-VN',{day:'2-digit',month:'2-digit',year:'numeric'}); } catch{ return iso; }
}
function fmtDuration(sec) {
  if(sec==null) return '—';
  const s=Math.round(sec); return `${Math.floor(s/60)}:${String(s%60).padStart(2,'0')}`;
}
function fmtSize(bytes) {
  if(!bytes) return '—';
  if(bytes<1024) return bytes+' B';
  if(bytes<1048576) return (bytes/1024).toFixed(1)+' KB';
  return (bytes/1048576).toFixed(1)+' MB';
}

// ── MINI CHART ──
function renderMiniChart(id, values, labels) {
  const c=document.getElementById(id); if(!c) return;
  const mx=Math.max(...values,1);
  c.innerHTML=values.map((v,i)=>`<div style="flex:1;display:flex;flex-direction:column;align-items:center;gap:3px"><div title="${v}" style="width:100%;height:${Math.round(v/mx*58)}px;background:linear-gradient(180deg,var(--accent),rgba(249,115,22,.15));border-radius:3px 3px 0 0"></div><span style="font-size:9px;color:var(--muted)">${labels[i]}</span></div>`).join('');
}

// ── EXPORT CSV ──
function exportCSV(data, filename, cols) {
  const h=cols.map(c=>`"${c.label}"`).join(',');
  const rows=data.map(row=>cols.map(c=>{
    const v=c.field.split('.').reduce((o,k)=>o?.[k],row);
    return `"${String(v||'').replace(/"/g,'""')}"`;
  }).join(','));
  const csv=[h,...rows].join('\n');
  const blob=new Blob([csv],{type:'text/csv;charset=utf-8;'});
  const link=document.createElement('a'); link.href=URL.createObjectURL(blob);
  link.download=filename||'export.csv'; link.click();
}
