/* ═══════════════════════════════════════════════════════
   AUDIO.JS  
   AudioDto: audioContent_ID, title, language, audioUrl,
     duration(double), fileSize(int64), isActive, poI_ID,
     poiName, vendorId, status, rejectedReason, createdByRole, createdAt
   AudioContent: full schema (id ObjectId, audioContent_ID, poI_ID,
     title*, language*, audioUrl, format, bitrate, status,
     ttsVoice, ttsProvider, ttsSpeed, ttsPitch, ttsConfig(TTSConfig),
     duration(int32), fileSize(int64), ttsText, playCount, isActive, ...)
   CreateAudioModel: poiId, language, title, description,
     ttsText, ttsVoice, ttsProvider, ttsSpeed, ttsPitch, ttsConfig,
     fileName, filePath, fileSize, duration, type, format, bitrate,
     templateId, templateName, templateVariables
   UpdateAudioModel: { title, description, isActive(bool?) }
═══════════════════════════════════════════════════════ */
let _audioPg = {page:1,pageSize:10,total:0,totalPages:1};
let _audioQry = {};
let _aiGenPOI = null;

async function initAudio() {
  _audioPg={page:1,pageSize:10,total:0,totalPages:1}; _audioQry={};
  await _loadAudios();
  _loadTTSVoices();
}
async function _loadAudios() {
  showTableLoading('audio-tbody',9);
  try {
    const res = await AudioApi.list({ ..._audioQry, ..._audioPg });
    if(!res || !res.data) throw new Error('Invalid response');
    _audioPg = { page:res.page, pageSize:res.pageSize, total:res.total, totalPages:res.totalPages };
    _renderAudioTable(res.data);
    renderPagination('audio-pagi', _audioPg, (p)=>{ _audioPg.page=p; _loadAudios(); });
  } catch(err){ showTableError('audio-tbody',9,err.message); showToast(err.message,'error'); }
}

function filterAudioTable() {
  const q=document.getElementById('audio-search')?.value?.trim()||undefined;
  const lang=document.getElementById('audio-lang-filter')?.value||undefined;
  const stat=document.getElementById('audio-status-filter')?.value||undefined;
  _audioQry={poiName:q,language:lang,status:stat};
  _audioPg.page=1; _loadAudios();
}

// ── RENDER (AudioDto schema) ──
function _renderAudioTable(data) {
  const tb=document.getElementById('audio-tbody');
  if(!data.length){ tb.innerHTML=`<tr><td colspan="9"><div class="empty-state"><div class="icon">🎵</div><p>Chưa có audio nào</p></div></td></tr>`; return; }
  tb.innerHTML=data.map(a=>`
    <tr>
      <td><input type="checkbox" onclick="event.stopPropagation()" style="accent-color:var(--accent)"/></td>
      <td><div style="font-weight:500;font-size:13px">${a.title||'—'}</div>
        <div style="font-size:11px;color:var(--muted);margin-top:2px">${a.poiName||'—'}</div></td>
      <td>${langBadge(a.language)}</td>
      <td style="font-size:12px">${fmtDuration(a.duration)}</td>
      <td style="font-size:12px">${fmtSize(a.fileSize)}</td>
      <td>${statusBadge(a.status||'draft')}</td>
      <td style="font-size:12px;color:var(--muted2)">${a.createdByRole||'—'}</td>
      <td style="font-size:12px;color:var(--muted2)">${fmtDate(a.createdAt)}</td>
      <td><div class="actions">
        ${a.audioUrl?`<button class="a-btn a-play" onclick="_playAudio('${a.audioUrl}',this)">▶</button>`:''}
        <button class="a-btn a-edit" onclick="_openEditAudio('${a.id||a.audioContent_ID}')">✎</button>
        ${a.status==='Draft' && isVendor()?`<button class="btn-sm btn-primary" onclick="_submitAudio('${a.id||a.audioContent_ID}')">Gửi</button>`:''}
        ${a.status==='Submitted' && isAdmin()?`<button class="btn-sm btn-success" onclick="_approveAudio('${a.id||a.audioContent_ID}')">Duyệt</button>`:''}
        ${a.status==='Submitted' && isAdmin()?`<button class="btn-sm btn-danger" onclick="_rejectAudioPrompt('${a.id||a.audioContent_ID}')">Từ chối</button>`:''}
        ${isAdmin()?`<button class="a-btn a-del" onclick="_deleteAudio('${a.id||a.audioContent_ID}',this)">✕</button>`:''}
      </div></td>
    </tr>`).join('');
}

// ── WORKFLOW ──
async function _submitAudio(id) { try{await AudioApi.submit(id); showToast('Đã gửi audio','success'); _loadAudios();}catch(e){showToast(e.message,'error');} }
async function _approveAudio(id) { try{await AudioApi.approve(id); showToast('Đã duyệt audio','success'); _loadAudios();}catch(e){showToast(e.message,'error');} }
function _rejectAudioPrompt(id) { const r=prompt('Lý do từ chối:'); if(r){ AudioApi.reject(id,r).then(()=>{showToast('Đã từ chối','success'); _loadAudios();}).catch(e=>showToast(e.message,'error')); } }
function _deleteAudio(id,btn) { confirmAction('Xóa audio này?', async ()=>{ try{await AudioApi.delete(id); showToast('Đã xóa','success'); _loadAudios();}catch(e){showToast(e.message,'error');} }); }

// ── EDIT (UpdateAudioModel: title, description, isActive) ──
async function _openEditAudio(id) {
  openModal('audio-edit');
  try {
    const a = await AudioApi.get(id);
    document.getElementById('edit-audio-id').value = a.id||a.audioContent_ID;
    document.getElementById('edit-audio-title').value = a.title||'';
    document.getElementById('edit-audio-desc').value = a.description||'';
  } catch(err) { showToast(err.message,'error'); }
}

async function saveEditAudio() {
  const id = document.getElementById('edit-audio-id').value;
  const data = { title: document.getElementById('edit-audio-title').value, description: document.getElementById('edit-audio-desc').value };
  try {
    await AudioApi.update(id, data);
    showToast('Cập nhật audio thành công!','success');
    closeModal(); _loadAudios();
  } catch(err) { showToast(err.message,'error'); }
}

// ── UPLOAD (multipart: AudioFile,Title,Description,Language,POI_ID,TTSText) ──
async function doUploadAudio() {
  const file = document.getElementById('upload-audio-file').files[0];
  if(!file){ showToast('Chọn file audio','warning'); return; }
  const data = {
    audioFile: file,
    title: document.getElementById('upload-audio-title').value,
    description: document.getElementById('upload-audio-desc').value,
    language: document.getElementById('upload-audio-lang').value,
    poiId: parseInt(document.getElementById('upload-audio-poi').value)||0,
    ttsText: document.getElementById('upload-audio-tts').value,
  };
  try {
    await AudioApi.upload(data);
    showToast('Upload audio thành công!','success');
    closeModal(); _loadAudios();
  } catch(err) { showToast(err.message,'error'); }
}

// ═══════════════════════════════════════════════════════
// AI GENERATE  (TTS flow)
// 1. CreateAudioModel with ttsText → POST /api/Audio
// 2. POST /api/Audio/{id}/generate-file
// ═══════════════════════════════════════════════════════
let _ttsVoices=[];
async function _loadTTSVoices() {
  try {
    const vi = await TTSApi.voices('vi-VN');
    const en = await TTSApi.voices('en-US');
    _ttsVoices = [...(vi||[]), ...(en||[])];
    const sel = document.getElementById('aigen-voice');
    if(sel) sel.innerHTML = _ttsVoices.map(v=>`<option value="${v.voiceId||v.name}">${v.name||v.voiceId} (${v.language||'vi-VN'})</option>`).join('');
  } catch{}
}

async function openAIGenModal(poiId, lang) {
  openModal('ai-generate');
  _aiGenPOI = null;
  document.getElementById('aigen-poi-selector').style.display = poiId ? 'none' : 'block';
  document.getElementById('aigen-step1').style.display = 'block';
  document.getElementById('aigen-step2').style.display = 'none';
  
  if(poiId) {
    try {
      const p = await POIApi.get(poiId);
      _aiGenPOI = p;
      document.getElementById('aigen-poi-name').textContent = p.name_Vi||'—';
      document.getElementById('aigen-lang').value = lang||'vi-VN';
      const script = lang==='en-US' ? _scriptEN(p) : _scriptVI(p);
      document.getElementById('aigen-text').value = script;
    } catch(err) { showToast(err.message,'error'); }
  } else {
    try {
      const list = await POIApi.list({ pageSize:100 });
      const sel = document.getElementById('aigen-poi-select');
      sel.innerHTML = '<option value="">-- Chọn POI --</option>' + (list.data||[]).map(p=>`<option value="${p.poI_ID}">${p.name_Vi||p.name_En||'POI '+p.poI_ID}</option>`).join('');
    } catch{}
  }
}

function aiGenSelectPOI() {
  const id = document.getElementById('aigen-poi-select').value;
  if(!id) return;
  POIApi.get(id).then(p=>{
    _aiGenPOI = p;
    document.getElementById('aigen-poi-name').textContent = p.name_Vi||'—';
    const lang = document.getElementById('aigen-lang').value;
    const script = lang==='en-US' ? _scriptEN(p) : _scriptVI(p);
    document.getElementById('aigen-text').value = script;
  }).catch(err=>showToast(err.message,'error'));
}

function _getMonDacSan(p, lang) {
  const pickList = (val) => Array.isArray(val) ? val.filter(Boolean) : [];
  if (lang === 'en-US') {
    if (p.signatureDishes_En) return pickList(p.signatureDishes_En).join(', ');
    if (p.specialties_En) return pickList(p.specialties_En).join(', ');
  }
  if (lang === 'zh-CN') {
    if (p.signatureDishes_Zh) return pickList(p.signatureDishes_Zh).join(', ');
    if (p.specialties_Zh) return pickList(p.specialties_Zh).join(', ');
  }
  const sig = pickList(p.signatureDishes?.length ? p.signatureDishes : p.signatureDish);
  if (sig.length) return sig.join(', ');
  const spec = pickList(p.specialties);
  return spec.join(', ');
}

function _getAddress(p, lang) {
  if (lang === 'en-US' && p.address_En) return p.address_En;
  if (lang === 'zh-CN' && p.address_Zh) return p.address_Zh;
  return p.address || '';
}

function _scriptVI(p) {
  const name = p.name_Vi || 'địa điểm này';
  const mon  = _getMonDacSan(p, 'vi-VN') || 'đặc sản địa phương';
  const addr = _getAddress(p, 'vi-VN');
  return `Chào mừng bạn đến với ${name}.\n\n${name} nổi tiếng với món ${mon}.\n\nĐịa chỉ: ${addr}\nHãy đến và thưởng thức nhé!`;
}

function _scriptEN(p) {
  const name = p.name_En || p.name_Vi || 'this location';
  const mon  = _getMonDacSan(p, 'en-US') || 'local specialties';
  const addr = _getAddress(p, 'en-US');
  return `Welcome to ${name}.\n\n${name} is famous for ${mon}.\n\nAddress: ${addr}\n\nCome and enjoy!`;
}

async function runAIGenerate() {
  if(!_aiGenPOI){ showToast('Chọn POI trước','warning'); return; }
  const text = document.getElementById('aigen-text').value.trim();
  if(!text){ showToast('Nhập nội dung narration','warning'); return; }
  
  const lang = document.getElementById('aigen-lang').value;
  const voice = document.getElementById('aigen-voice').value;
  const title = `${_aiGenPOI.name_Vi||_aiGenPOI.name_En} - ${lang==='vi-VN'?'Tiếng Việt':'English'}`;
  
  const btn = event.target;
  btn.disabled = true;
  btn.innerHTML = '<span class="spinner" style="width:14px;height:14px;border-width:2px;vertical-align:middle;margin-right:6px"></span>Đang tạo...';
  
  try {
    // Step 1: Create audio record with ttsText
    const createData = {
      poiId: _aiGenPOI.poI_ID,
      language: lang,
      title,
      ttsText: text,
      ttsVoice: voice,
      ttsProvider: 'GoogleTTS',
      ttsSpeed: 1.0,
    };
    const audio = await AudioApi.create(createData);
    if(!audio || !audio.id) throw new Error('Failed to create audio record');
    
    // Step 2: Generate audio file
    await AudioApi.generateFile(audio.id);
    
    // Step 3: Show success
    document.getElementById('aigen-step1').style.display = 'none';
    document.getElementById('aigen-step2').style.display = 'block';
    document.getElementById('aigen-audio-id').value = audio.id;
    
    showToast('Tạo audio thành công! 🎉','success');
  } catch(err) {
    showToast(err.message,'error');
    btn.disabled = false;
    btn.textContent = 'Tạo Audio';
  }
}

async function submitGeneratedAudio() {
  const id = document.getElementById('aigen-audio-id').value;
  try {
    await AudioApi.submit(id);
    showToast('Đã gửi audio để duyệt','success');
    closeModal();
    _loadAudios();
  } catch(err) { showToast(err.message,'error'); }
}
