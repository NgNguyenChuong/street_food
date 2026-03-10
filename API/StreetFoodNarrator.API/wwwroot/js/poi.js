/* ═══════════════════════════════════════════════════════
   POI.JS 
   List : POIListResponse { data: POIDto[], total, page, pageSize, totalPages }
   Detail: POI { id(ObjectId), poI_ID, name_Vi/En/Zh, description_Vi/En/Zh,
     location(GeoJsonLocation), address, audioContents[], vendorProfile(VendorProfile),
     signatureDishes[], openingHours, openingHoursText, phoneNumber, averagePrice,
     priceLevel, rating, category, imageUrl, funFact, history, story,
     zoneType, zoneLevel, triggerRadius, cooldownMinutes, isActive, createdAt, updatedAt }
   Create: CreatePOIModel  |  Update: UpdatePOIModel
═══════════════════════════════════════════════════════ */
let _poiPg = {page:1,pageSize:10,total:0,totalPages:1};
let _poiQry = {};

async function initPOI() {
  _poiPg = {page:1,pageSize:10,total:0,totalPages:1}; _poiQry={};
  await _loadPOIs();
}
async function _loadPOIs() {
  showTableLoading('poi-tbody',9);
  try {
    const res = await POIApi.list({ ..._poiQry, ..._poiPg });
    if(!res || !res.data) throw new Error('Invalid response');
    _poiPg = { page:res.page, pageSize:res.pageSize, total:res.total, totalPages:res.totalPages };
    _renderPOITable(res.data);
    renderPagination('poi-pagi', _poiPg, (p)=>{ _poiPg.page=p; _loadPOIs(); });
  } catch(err) { showTableError('poi-tbody',9,err.message); showToast(err.message,'error'); }
}
async function _loadPOIStats() {
  try {
    const s = await POIApi.stats();
    document.getElementById('stat-poi-total').textContent = s.total||0;
    document.getElementById('stat-poi-active').textContent = s.active||0;
  } catch{}
}
function filterPOITable() {
  const s=document.getElementById('poi-search')?.value?.trim()||undefined;
  const cat=document.getElementById('poi-cat-filter')?.value||undefined;
  const sv=document.getElementById('poi-status-filter')?.value;
  _poiQry={search:s,category:cat};
  if(sv==='active') _poiQry.isActive=true;
  else if(sv==='inactive') _poiQry.isActive=false;
  _poiPg.page=1; _loadPOIs();
}

// ── RENDER TABLE (POIDto fields) ──
function _renderPOITable(data) {
  document.getElementById('poi-count').textContent=`${_poiPg.total} địa điểm`;
  const tb=document.getElementById('poi-tbody');
  if(!data.length){ tb.innerHTML=`<tr><td colspan="9"><div class="empty-state"><div class="icon">📍</div><p>Chưa có địa điểm nào</p></div></td></tr>`; return; }
  tb.innerHTML=data.map(p=>`
    <tr onclick="_openPOIDetail(${p.poI_ID})" style="cursor:pointer">
      <td><input type="checkbox" onclick="event.stopPropagation()" style="accent-color:var(--accent)"/></td>
      <td><div style="display:flex;align-items:center;gap:10px">
        ${p.imageUrl?`<img src="${p.imageUrl}" style="width:34px;height:34px;border-radius:8px;object-fit:cover" onerror="this.style.display='none'"/>`:`<span style="font-size:20px">🍜</span>`}
        <div><div style="font-weight:500;font-size:13.5px">${p.name_Vi||'—'}</div>
          <div style="font-size:11.5px;color:var(--muted)">${p.name_En||''}</div></div>
      </div></td>
      <td><span class="badge b-orange">${p.category||'—'}</span></td>
      <td style="font-size:12.5px">${p.vendorId?`<span class="mono">VND-${p.vendorId}</span>`:`<span style="color:var(--muted)">Admin</span>`}</td>
      <td><span class="mono" style="font-size:11.5px">${(p.latitude||0).toFixed(4)},${(p.longitude||0).toFixed(4)}</span></td>
      <td>${p.audioCount>0?`<span class="badge b-green">🎵 ${p.audioCount}</span>`:`<span class="badge b-gray">✗</span>`}</td>
      <td style="font-size:13px">${p.rating?`⭐ ${p.rating.toFixed(1)}`:'—'}</td>
      <td>${statusBadge(p.isActive?'active':'inactive')}</td>
      <td onclick="event.stopPropagation()"><div class="actions">
        <button class="a-btn a-view" onclick="_openPOIDetail(${p.poI_ID})">👁</button>
        <button class="a-btn a-edit" onclick="_openEditPOI(${p.poI_ID})">✎</button>
        ${isAdmin()?`<button class="a-btn a-del" onclick="_deletePOI(${p.poI_ID},this)">✕</button>`:''}
      </div></td>
    </tr>`).join('');
}

// ── DETAIL MODAL (full POI schema) ──
async function _openPOIDetail(poiId) {
  openModal('poi-detail');
  const box=document.getElementById('poi-detail-content');
  box.innerHTML=`<div class="empty-state"><div class="spinner"></div><p>Đang tải...</p></div>`;
  try {
    const p = await POIApi.get(poiId);
    if(!p) throw new Error('Không tìm thấy POI');
    const audio = (p.audioContents||[]).map(a=>`
      <div style="background:var(--s2);border:1px solid var(--border);border-radius:8px;padding:10px">
        <div style="display:flex;align-items:center;justify-content:space-between;gap:8px">
          <div style="flex:1"><div style="font-weight:500;font-size:13px">${a.title||'—'}</div>
          <div style="font-size:11px;color:var(--muted);margin-top:2px">${langBadge(a.language)} ${fmtDuration(a.duration)}</div></div>
          <div style="display:flex;gap:4px">
            ${a.audioUrl?`<button class="a-btn a-play" onclick="_playAudio('${a.audioUrl}',this)">▶</button>`:''}
            ${statusBadge(a.status||'draft')}
            ${a.status==='Submitted' && isAdmin()?`<button class="btn-sm btn-success" onclick="_approveAudioInline('${a.id}')">Duyệt</button><button class="btn-sm btn-danger" onclick="_rejectAudioInline('${a.id}')">Từ chối</button>`:''}
          </div>
        </div>
      </div>`).join('');
    box.innerHTML=`
      <div style="display:flex;flex-direction:column;gap:16px">
        ${p.imageUrl?`<img src="${p.imageUrl}" style="width:100%;max-height:240px;object-fit:cover;border-radius:var(--radius)" onerror="this.style.display='none'"/>`:''}
        <div><h2 style="font-size:20px;font-weight:700;margin-bottom:4px">${p.name_Vi||'—'}</h2>
        <p style="color:var(--muted);font-size:13px">${p.name_En||''}</p></div>
        <div style="display:grid;grid-template-columns:repeat(3,1fr);gap:10px">
          ${_ic('Danh mục',`<span class="badge b-orange">${p.category||'—'}</span>`)}
          ${_ic('Trạng thái',statusBadge(p.isActive?'active':'inactive'))}
          ${_ic('Đánh giá',p.rating?`⭐ ${p.rating.toFixed(1)}`:'—')}
        </div>
        ${_ic('Mô tả (VI)',p.description_Vi||'—')}
        ${p.description_En?_ic('Mô tả (EN)',p.description_En):''}
        <div style="display:grid;grid-template-columns:1fr 1fr;gap:10px">
          ${_ic('Địa chỉ',p.address||'—')}
          ${_ic('Vị trí',`${(p.location?.coordinates?.[1]||0).toFixed(5)}, ${(p.location?.coordinates?.[0]||0).toFixed(5)}`)}
        </div>
        ${p.signatureDishes?.length?_ic('Món đặc trưng',p.signatureDishes.join(', ')):''}
        ${p.openingHoursText?_ic('Giờ mở cửa',p.openingHoursText):''}
        ${p.phoneNumber?_ic('Điện thoại',p.phoneNumber):''}
        ${p.averagePrice?_ic('Giá TB',p.averagePrice):''}
        ${p.priceLevel?_ic('Mức giá',p.priceLevel):''}
        ${p.funFact?_ic('Fun Fact',p.funFact):''}
        ${p.history?_ic('Lịch sử',p.history):''}
        ${p.story?_ic('Câu chuyện',p.story):''}
        <div style="display:grid;grid-template-columns:repeat(4,1fr);gap:10px">
          ${_ic('Zone Type',p.zoneType||'—')}
          ${_ic('Zone Level',p.zoneLevel||'—')}
          ${_ic('Trigger Radius',`${p.triggerRadius||0}m`)}
          ${_ic('Cooldown',`${p.cooldownMinutes||0}m`)}
        </div>
        ${p.vendorProfile?_ic('Vendor',`<div><div style="font-weight:500">${p.vendorProfile.businessName||'—'}</div><div style="font-size:11px;color:var(--muted)">${p.vendorProfile.contactEmail||''}</div></div>`):''}
        <div><h3 style="font-size:15px;font-weight:600;margin-bottom:10px">Audio (${(p.audioContents||[]).length})</h3>
        ${audio||'<p style="color:var(--muted);font-size:13px">Chưa có audio</p>'}</div>
        <div style="display:grid;grid-template-columns:1fr 1fr;gap:10px;font-size:12px;color:var(--muted)">
          <div>Tạo: ${fmtDate(p.createdAt)}</div>
          <div>Cập nhật: ${fmtDate(p.updatedAt)}</div>
        </div>
        <button class="btn btn-ghost btn-full" onclick="closeModal()">Đóng</button>
      </div>`;
  } catch(err) { box.innerHTML=`<div class="empty-state"><div class="icon">❌</div><p style="color:var(--red)">${err.message}</p></div>`; showToast(err.message,'error'); }
}
function _ic(label,html){return `<div style="background:var(--s2);border:1px solid var(--border);border-radius:8px;padding:12px"><div style="font-size:10px;color:var(--muted);font-weight:700;text-transform:uppercase;letter-spacing:.8px;margin-bottom:5px">${label}</div><div style="font-size:13px">${html}</div></div>`;}

// ── EDIT MODAL ──
async function _openEditPOI(poiId) {
  openModal('poi-edit');
  try {
    const p = await POIApi.get(poiId);
    document.getElementById('edit-poi-id').value = p.poI_ID;
    document.getElementById('edit-name-vi').value = p.name_Vi||'';
    document.getElementById('edit-name-en').value = p.name_En||'';
    document.getElementById('edit-desc-vi').value = p.description_Vi||'';
    document.getElementById('edit-category').value = p.category||'';
    document.getElementById('edit-address').value = p.address||'';
    document.getElementById('edit-latitude').value = p.location?.coordinates?.[1]||'';
    document.getElementById('edit-longitude').value = p.location?.coordinates?.[0]||'';
  } catch(err) { showToast(err.message,'error'); }
}

// ── SAVE POI ──
async function savePOI(isEdit=false) {
  const f=document.getElementById(isEdit?'modal-poi-edit':'modal-poi-add');
  const g=n=>f?.querySelector(`[name="${n}"]`)?.value?.trim();
  const data = {
    name_Vi: g('name-vi'),
    name_En: g('name-en'),
    description_Vi: g('desc-vi'),
    category: g('category'),
    address: g('address'),
    latitude: parseFloat(g('latitude'))||0,
    longitude: parseFloat(g('longitude'))||0,
    isActive: true,
  };
  try {
    if(isEdit) {
      const id = f?.querySelector('[name="poi-id"]')?.value;
      await POIApi.update(id, data);
      showToast('Cập nhật POI thành công!','success');
    } else {
      await POIApi.create(data);
      showToast('Tạo POI mới thành công!','success');
    }
    closeModal();
    _loadPOIs();
  } catch(err) { showToast(err.message,'error'); }
}

// ── DELETE ──
function _deletePOI(poiId, btn) {
  confirmAction('Xóa POI này?', async ()=>{
    try { await POIApi.delete(poiId); showToast('Đã xóa POI','success'); _loadPOIs(); }
    catch(err){ showToast(err.message,'error'); }
  });
}

// ── AUDIO inline approve/reject ──
async function _approveAudioInline(id){ try{await AudioApi.approve(id); showToast('Đã duyệt','success'); _openPOIDetail(document.getElementById('edit-poi-id')?.value); }catch(e){showToast(e.message,'error');} }
function _rejectAudioInline(id){ const r=prompt('Lý do từ chối:'); if(r){ AudioApi.reject(id,r).then(()=>{showToast('Đã từ chối','success'); _openPOIDetail(document.getElementById('edit-poi-id')?.value);}).catch(e=>showToast(e.message,'error')); } }

// ── AUDIO PLAYER ──
let _currAudio=null;
function _playAudio(url, btn) {
  if(_currAudio){ _currAudio.pause(); _currAudio=null; btn.textContent='▶'; return; }
  _currAudio=new Audio(url); _currAudio.play(); btn.textContent='⏸';
  _currAudio.onended=()=>{ _currAudio=null; btn.textContent='▶'; };
}
