/* ══════════════════════════════════════
   TRANSLATION.JS — Bản dịch module
══════════════════════════════════════ */
let _transFiltered = [...TRANS_DATA];

function renderTrans(data) {
  _transFiltered = data || TRANS_DATA;
  document.getElementById('trans-tbody').innerHTML = _transFiltered.length
    ? _transFiltered.map(t => `
      <tr>
        <td onclick="event.stopPropagation()"><input type="checkbox" style="accent-color:var(--accent)"/></td>
        <td><span class="mono">${t.key}</span></td>
        <td><span class="badge b-purple">${t.ns}</span></td>
        <td style="max-width:200px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">
          ${t.vi || '<em style="color:var(--muted)">Chưa có</em>'}
        </td>
        <td style="max-width:200px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap">
          ${t.en || '<em style="color:var(--muted)">Chưa dịch</em>'}
        </td>
        <td><span class="badge ${t.status==='done'?'b-green':'b-yellow'}">${t.status==='done'?'✔ Đã dịch':'⏳ Chờ'}</span></td>
        <td>
          <div class="actions">
            <button class="a-btn a-edit" onclick="editTrans(${t.id})">✎</button>
            <button class="a-btn a-del"  onclick="deleteTrans(${t.id},this)">✕</button>
          </div>
        </td>
      </tr>`).join('')
    : `<tr><td colspan="7"><div class="empty-state"><div class="icon">🌐</div><p>Không tìm thấy chuỗi dịch nào</p></div></td></tr>`;

  // Cập nhật stats
  const total = TRANS_DATA.length;
  const done  = TRANS_DATA.filter(t=>t.status==='done').length;
  const pend  = total - done;
  const pct   = total ? Math.round((done/total)*100) : 0;
  document.getElementById('trans-stat-total').textContent = total;
  document.getElementById('trans-stat-done').textContent = done;
  document.getElementById('trans-stat-pending').textContent = pend;
  document.getElementById('trans-stat-percent').textContent = pct+'%';
}

function filterTrans() {
  const q = document.getElementById('trans-search').value.toLowerCase();
  const ns = document.getElementById('trans-ns-filter').value;
  const st = document.getElementById('trans-status-filter').value;
  const filtered = TRANS_DATA.filter(t => {
    if(q && !t.key.toLowerCase().includes(q) && !(t.vi||'').toLowerCase().includes(q) && !(t.en||'').toLowerCase().includes(q)) return false;
    if(ns && t.ns !== ns) return false;
    if(st && t.status !== st) return false;
    return true;
  });
  renderTrans(filtered);
}
function editTrans(id) { toast('Chỉnh sửa bản dịch (đang phát triển)','info'); }
function deleteTrans(id, btn) {
  if(!confirm('Xác nhận xóa chuỗi dịch này?')) return;
  btn.closest('tr').remove();
  toast('Đã xóa chuỗi dịch','success');
}
function saveTrans() {
  closeModal();
  toast('Đã thêm chuỗi dịch mới','success');
}
function exportTransJSON() {
  const json = JSON.stringify(TRANS_DATA, null, 2);
  const blob = new Blob([json], {type: 'application/json'});
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = 'translations.json';
  a.click();
  URL.revokeObjectURL(url);
  toast('Đã xuất file JSON', 'success');
}

/* ══════════════════════════════════════
   TOUR.JS — Quản lý Tour module
══════════════════════════════════════ */
function renderTours(data) {
  const grid = document.getElementById('tours-grid');
  if(!grid) return;
  grid.innerHTML = data.length
    ? data.map(t => `
      <div class="tour-card" onclick="openTourDetail(${t.id})">
        <div class="tour-icon">${t.img}</div>
        <div class="tour-info">
          <h3 class="tour-name">${t.name}</h3>
          <p class="tour-desc">${t.desc}</p>
          <div class="tour-meta">
            <span>📍 ${t.pois} POI</span>
            <span>⏱️ ${t.dur}</span>
            <span>🚶 ${t.dist}</span>
          </div>
          <div class="tour-foot">
            <span class="badge ${t.status==='active'?'b-green':'b-yellow'}">${t.status==='active'?'✅ Hoạt động':'📝 Bản nháp'}</span>
            <div class="actions">
              <button class="a-btn a-edit" onclick="event.stopPropagation();editTour(${t.id})">✎</button>
              <button class="a-btn a-del"  onclick="event.stopPropagation();delRow(this,'tour này')">✕</button>
            </div>
          </div>
        </div>
      </div>`).join('')
    : `<div class="empty-state"><div class="icon">🚶</div><p>Chưa có tour nào</p></div>`;
}

function filterTours() {
  const status = prompt('Filter by status (active/draft):', 'active');
  if(!status) return;
  const filtered = TOUR_DATA.filter(t => t.status === status);
  renderTours(filtered);
}

function openTourDetail(id) {
  const t = TOUR_DATA.find(x => x.id === id);
  if(!t) return;
  const content = document.getElementById('tour-detail-content');
  if(!content) return;
  content.innerHTML = `
    <div style="display:flex;align-items:center;gap:16px;margin-bottom:20px">
      <div style="width:60px;height:60px;border-radius:12px;background:linear-gradient(135deg,var(--accent),var(--purple));display:flex;align-items:center;justify-content:center;font-size:32px">${t.img}</div>
      <div style="flex:1">
        <h3 style="font-size:18px;font-weight:700;margin-bottom:4px">${t.name}</h3>
        <p style="font-size:13px;color:var(--muted)">${t.desc}</p>
      </div>
      <span class="badge ${t.status==='active'?'b-green':'b-yellow'}">${t.status==='active'?'✅ Hoạt động':'📝 Bản nháp'}</span>
    </div>
    <div style="display:grid;grid-template-columns:repeat(3,1fr);gap:12px;margin-bottom:20px">
      <div style="background:var(--s2);border:1px solid var(--border);border-radius:8px;padding:12px">
        <div style="font-size:11px;color:var(--muted);margin-bottom:4px">Số POI</div>
        <div style="font-size:24px;font-weight:700">${t.pois}</div>
      </div>
      <div style="background:var(--s2);border:1px solid var(--border);border-radius:8px;padding:12px">
        <div style="font-size:11px;color:var(--muted);margin-bottom:4px">Thời lượng</div>
        <div style="font-size:24px;font-weight:700">${t.dur}</div>
      </div>
      <div style="background:var(--s2);border:1px solid var(--border);border-radius:8px;padding:12px">
        <div style="font-size:11px;color:var(--muted);margin-bottom:4px">Khoảng cách</div>
        <div style="font-size:24px;font-weight:700">${t.dist}</div>
      </div>
    </div>
    <h4 style="font-size:14px;font-weight:600;margin-bottom:10px">Danh sách POI trong Tour</h4>
    <div style="display:flex;flex-direction:column;gap:8px">
      ${POI_DATA.slice(0, t.pois).map((p,i) => `
        <div style="display:flex;align-items:center;gap:12px;padding:10px;background:var(--s2);border:1px solid var(--border);border-radius:8px">
          <div style="width:28px;height:28px;border-radius:50%;background:var(--accent);color:#fff;display:flex;align-items:center;justify-content:center;font-size:12px;font-weight:700">${i+1}</div>
          <div style="flex:1">
            <div style="font-weight:500;font-size:13px">${p.name}</div>
            <div style="font-size:11.5px;color:var(--muted);margin-top:2px">${p.addr}</div>
          </div>
          <span class="badge ${p.audio?'b-green':'b-gray'}">${p.audio?'🎵 Audio':'—'}</span>
        </div>
      `).join('')}
    </div>
  `;
  openModal('tour-detail');
}

function editTour(id) { toast('Chỉnh sửa tour (đang phát triển)','info'); }
function saveTour() {
  closeModal();
  toast('Đã lưu tour','success');
}

/* ══════════════════════════════════════
   HISTORY.JS — Lịch sử sử dụng module
══════════════════════════════════════ */
function renderHistChart() {
  const el = document.getElementById('history-chart');
  if(!el) return;
  const days = ['T2','T3','T4','T5','T6','T7','CN'];
  const vals = [120,145,98,175,210,189,234];
  el.innerHTML = days.map((d,i) => `
    <div style="flex:1;display:flex;flex-direction:column;align-items:center;gap:8px">
      <div style="width:100%;height:${vals[i]}px;background:linear-gradient(180deg,var(--accent),rgba(249,115,22,.3));border-radius:6px 6px 0 0"></div>
      <div style="font-size:11.5px;color:var(--muted);font-weight:600">${d}</div>
    </div>
  `).join('');
}

function renderTopPOI() {
  const el = document.getElementById('top-poi-hist');
  if(!el) return;
  const top = [...POI_DATA].sort((a,b) => b.views - a.views).slice(0,5);
  el.innerHTML = top.map((p,i) => `
    <div style="display:flex;align-items:center;gap:10px;padding:10px 0;border-bottom:1px solid var(--border)">
      <div style="font-size:16px;font-weight:700;color:var(--muted);width:24px">${i+1}</div>
      <div style="flex:1">
        <div style="font-weight:500;font-size:13px">${p.name}</div>
        <div style="font-size:11.5px;color:var(--muted);margin-top:2px">${p.addr}</div>
      </div>
      <div style="font-weight:600;color:var(--accent)">${p.views}</div>
    </div>
  `).join('');
}

function renderHistTimeline() {
  const el = document.getElementById('hist-timeline');
  if(!el) return;
  el.innerHTML = HISTORY_DATA.slice(0,5).map((h,i,arr) => `
    <div class="tl-row">
      <div class="tl-l">
        <div class="tl-dot"></div>
        ${i < arr.length-1 ? '<div class="tl-line"></div>' : ''}
      </div>
      <div class="tl-c">
        <div class="tl-title">${h.action==='view'?'👁️':'🎵'} ${h.poi}</div>
        <div class="tl-time">${h.device} · ${h.time}</div>
      </div>
    </div>
  `).join('');
}

function renderHistLogs(data) {
  const tb = document.getElementById('hist-tbody');
  if(!tb) return;
  tb.innerHTML = data.map(h => `
    <tr>
      <td><span class="mono">${h.userId}</span></td>
      <td>${h.device}</td>
      <td><span class="badge ${h.os.includes('iOS')?'b-blue':'b-green'}">${h.os}</span></td>
      <td style="font-size:13px">${h.poi}</td>
      <td><span class="badge ${h.action==='audio'?'b-purple':'b-gray'}">${h.action==='audio'?'🎵 Audio':'👁️ View'}</span></td>
      <td style="font-size:12px;color:var(--muted2)">${h.time}</td>
    </tr>
  `).join('');
}

function filterHistory() {
  const range = document.getElementById('hist-timerange')?.value || '7 ngày qua';
  toast(`Lọc theo: ${range}`, 'info');
}

/* ══════════════════════════════════════
   USERS.JS — Thiết bị module
══════════════════════════════════════ */
function renderUsers(data) {
  const tb = document.getElementById('user-tbody');
  if(!tb) return;
  tb.innerHTML = data.map(u => `
    <tr onclick="viewDevice(${u.id})">
      <td onclick="event.stopPropagation()"><input type="checkbox" style="accent-color:var(--accent)"/></td>
      <td><span class="mono">${u.deviceId}</span></td>
      <td>${u.model}</td>
      <td><span class="badge ${u.os.includes('iOS')?'b-blue':'b-green'}">${u.os}</span></td>
      <td style="font-size:12px;color:var(--muted2)">${u.firstSeen}</td>
      <td style="font-size:12px;color:var(--muted2)">${u.lastSeen}</td>
      <td><strong>${u.sessions}</strong></td>
      <td>${deviceStatusBadge(u.status)}</td>
      <td onclick="event.stopPropagation()">
        <div class="actions">
          <button class="a-btn a-edit" onclick="viewDevice(${u.id})">👁️</button>
          ${u.status==='active' ? `<button class="a-btn a-del" onclick="blockDevice(${u.id},this)">🚫</button>` : `<button class="a-btn a-approve" onclick="toast('Mở chặn thiết bị (đang phát triển)','info')">✅</button>`}
        </div>
      </td>
    </tr>
  `).join('');
}

function filterUsers() {
  const q = document.getElementById('user-search')?.value.toLowerCase() || '';
  const os = document.getElementById('user-os-filter')?.value || '';
  const st = document.getElementById('user-status-filter')?.value || '';
  const filtered = USER_DEVICES.filter(u => {
    if(q && !u.deviceId.toLowerCase().includes(q)) return false;
    if(os && !u.os.includes(os)) return false;
    if(st && u.status !== st) return false;
    return true;
  });
  renderUsers(filtered);
}

function viewDevice(id) {
  const u = USER_DEVICES.find(x => x.id === id);
  if(!u) return;
  const content = document.getElementById('device-detail-content');
  if(!content) return;
  content.innerHTML = `
    <div style="background:var(--s2);border:1px solid var(--border);border-radius:8px;padding:16px;margin-bottom:16px">
      <div style="display:grid;grid-template-columns:120px 1fr;gap:12px;font-size:13px">
        <div style="color:var(--muted)">Device ID</div><div><span class="mono">${u.deviceId}</span></div>
        <div style="color:var(--muted)">Model</div><div>${u.model}</div>
        <div style="color:var(--muted)">OS</div><div><span class="badge ${u.os.includes('iOS')?'b-blue':'b-green'}">${u.os}</span></div>
        <div style="color:var(--muted)">First Seen</div><div>${u.firstSeen}</div>
        <div style="color:var(--muted)">Last Seen</div><div>${u.lastSeen}</div>
        <div style="color:var(--muted)">Sessions</div><div><strong>${u.sessions}</strong></div>
        <div style="color:var(--muted)">Status</div><div>${deviceStatusBadge(u.status)}</div>
      </div>
    </div>
    <h4 style="font-size:14px;font-weight:600;margin-bottom:10px">Hoạt động gần đây</h4>
    <div class="tl">
      ${HISTORY_DATA.filter(h => h.userId === u.deviceId).slice(0,5).map((h,i,arr) => `
        <div class="tl-row">
          <div class="tl-l">
            <div class="tl-dot"></div>
            ${i < arr.length-1 ? '<div class="tl-line"></div>' : ''}
          </div>
          <div class="tl-c">
            <div class="tl-title">${h.action==='view'?'👁️':'🎵'} ${h.poi}</div>
            <div class="tl-time">${h.time}</div>
          </div>
        </div>
      `).join('')}
    </div>
  `;
  openModal('device-detail');
}
function blockDevice(id, btn) {
  if(!confirm('Chặn thiết bị này?')) return;
  toast('Đã chặn thiết bị','success');
}
