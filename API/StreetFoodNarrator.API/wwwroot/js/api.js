/* ═══════════════════════════════════════════════════════
   API.JS  —  StreetFoodNarrator.API  localhost:5004
   Source: swagger.json OpenAPI 3.0.4  (full schema)
═══════════════════════════════════════════════════════ */
const API_BASE = 'http://localhost:5004';

// ── TOKEN ──
const getToken    = ()  => sessionStorage.getItem('sfn_token');
const setToken    = (t) => sessionStorage.setItem('sfn_token', t);
const removeToken = ()  => sessionStorage.removeItem('sfn_token');

// ── BASE FETCH ──
async function apiFetch(path, opts = {}) {
  const headers = { 'Content-Type': 'application/json', ...opts.headers };
  const token = getToken();
  if (token) headers['Authorization'] = `Bearer ${token}`;
  
  const response = await fetch(API_BASE + path, {
    ...opts,
    headers,
    credentials: 'include'
  });
  
  if (!response.ok) {
    const err = await response.text();
    throw new Error(err || `HTTP ${response.status}`);
  }
  
  const text = await response.text();
  if (!text) return null;
  try { return JSON.parse(text); } catch { return text; }
}

// ── MULTIPART (upload) ──
async function apiFetchForm(path, formData) {
  const token = getToken();
  const headers = {};
  if (token) headers['Authorization'] = `Bearer ${token}`;
  
  const response = await fetch(API_BASE + path, {
    method: 'POST',
    headers,
    body: formData,
    credentials: 'include'
  });
  
  if (!response.ok) throw new Error(`HTTP ${response.status}`);
  return response.json();
}

function qs(params = {}) {
  const p = Object.entries(params).filter(([,v]) => v !== undefined && v !== null && v !== '');
  if (!p.length) return '';
  return '?' + p.map(([k,v]) => `${k}=${encodeURIComponent(v)}`).join('&');
}

// ═══════════════════════════════════════════════════════
// AUTH  /api/Auth
// LoginModel: { email, password }
// RegisterModel: { email, password, fullName, phoneNumber }
// ═══════════════════════════════════════════════════════
const AuthAPI = {
  login: (email, password) =>
    apiFetch('/api/Auth/login', { method: 'POST', body: JSON.stringify({ email, password }) }),
  register: (data) =>
    apiFetch('/api/Auth/register', { method: 'POST', body: JSON.stringify(data) }),
  me: () => apiFetch('/api/Auth/me'),
  refresh: () => apiFetch('/api/Auth/refresh', { method: 'POST' }),
};

// ═══════════════════════════════════════════════════════
// POIs  /api/POIs
// GET list → POIListResponse { data: POIDto[], total, page, pageSize, totalPages }
// GET /{id} → POI (full, with audioContents[], vendorProfile, narrationLogs[])
// POST → CreatePOIModel → POI
// PUT /{id} → UpdatePOIModel
// DELETE /{id}
// GET /stats
// GET /sync?sinceVersion → POISyncResponse
// ═══════════════════════════════════════════════════════
const POIApi = {
  list: ({ page=1, pageSize=10, search, isActive, category }={}) =>
    apiFetch('/api/POIs' + qs({ page, pageSize, search, isActive, category })),
  get: (id) => apiFetch(`/api/POIs/${id}`),
  create: (data) => apiFetch('/api/POIs', { method:'POST', body:JSON.stringify(data) }),
  update: (id, data) => apiFetch(`/api/POIs/${id}`, { method:'PUT', body:JSON.stringify(data) }),
  delete: (id) => apiFetch(`/api/POIs/${id}`, { method:'DELETE' }),
  stats: () => apiFetch('/api/POIs/stats'),
  sync: (sinceVersion=0) => apiFetch('/api/POIs/sync' + qs({ sinceVersion })),
};

// ═══════════════════════════════════════════════════════
// AUDIO  /api/Audio
// GET list → AudioListResponse { data: AudioDto[], total, page, pageSize, totalPages }
// GET /{id} → AudioContent
// POST → CreateAudioModel → AudioContent
// PUT /{id} → UpdateAudioModel { title, description, isActive }
// DELETE /{id}
// POST /upload → multipart: AudioFile, Title, Description, Language, POI_ID(int32), TTSText
// GET /pois-without-audio?language → POIWithoutAudioDto[]
// POST /{id}/generate-file
// POST /{id}/submit
// POST /{id}/approve
// POST /{id}/reject  → RejectAudioRequest { reason }
// POST /bulk-generate → BulkGenerateAudioRequest { languages[], poiIds[], onlyMissing }
// ═══════════════════════════════════════════════════════
const AudioApi = {
  list: ({ page=1, pageSize=10, language, poiId, status, poiName }={}) =>
    apiFetch('/api/Audio' + qs({ page, pageSize, language, poiId, status, poiName })),
  get: (id) => apiFetch(`/api/Audio/${id}`),
  create: (data) => apiFetch('/api/Audio', { method:'POST', body:JSON.stringify(data) }),
  update: (id, data) => apiFetch(`/api/Audio/${id}`, { method:'PUT', body:JSON.stringify(data) }),
  delete: (id) => apiFetch(`/api/Audio/${id}`, { method:'DELETE' }),
  upload: ({ audioFile, title, description, language, poiId, ttsText }) => {
    const fd = new FormData();
    fd.append('AudioFile', audioFile);
    if (title) fd.append('Title', title);
    if (description) fd.append('Description', description);
    if (language) fd.append('Language', language);
    if (poiId) fd.append('POI_ID', poiId);
    if (ttsText) fd.append('TTSText', ttsText);
    return apiFetchForm('/api/Audio/upload', fd);
  },
  poisWithoutAudio: (language='vi-VN') =>
    apiFetch('/api/Audio/pois-without-audio' + qs({ language })),
  generateFile: (id) => apiFetch(`/api/Audio/${id}/generate-file`, { method:'POST' }),
  submit: (id)   => apiFetch(`/api/Audio/${id}/submit`,  { method:'POST' }),
  approve: (id)  => apiFetch(`/api/Audio/${id}/approve`, { method:'POST' }),
  reject: (id, reason) =>
    apiFetch(`/api/Audio/${id}/reject`, { method:'POST', body:JSON.stringify({ reason }) }),
  bulkGenerate: (data) =>
    apiFetch('/api/Audio/bulk-generate', { method:'POST', body:JSON.stringify(data) }),
};

// ═══════════════════════════════════════════════════════
// TTS  /api/TTS
// GET /test
// POST /generate → TTSRequest { text, language, voice }
// GET /voices?language
// ═══════════════════════════════════════════════════════
const TTSApi = {
  test: () => apiFetch('/api/TTS/test'),
  generate: ({ text, language, voice }) =>
    apiFetch('/api/TTS/generate', { method:'POST', body:JSON.stringify({ text, language, voice }) }),
  voices: (language) => apiFetch('/api/TTS/voices' + qs({ language })),
};

// ═══════════════════════════════════════════════════════
// VENDORS  /api/Vendors
// GET /me → VendorProfile
// PUT /me → UpdateVendorProfileRequest
// GET (admin) → VendorListResponse { data: VendorDto[], total, page, pageSize, totalPages }
// GET /{id} (admin) → VendorProfile
// POST /{id}/verify (admin)
// POST /{id}/reject (admin) → { reason }
// GET /stats (admin) → VendorStatsResponse
// ═══════════════════════════════════════════════════════
const VendorsApi = {
  me: () => apiFetch('/api/Vendors/me'),
  updateMe: (data) => apiFetch('/api/Vendors/me', { method:'PUT', body:JSON.stringify(data) }),
  list: ({ page=1, pageSize=10, search, status }={}) =>
    apiFetch('/api/Vendors' + qs({ page, pageSize, search, status })),
  get: (id) => apiFetch(`/api/Vendors/${id}`),
  verify: (id) => apiFetch(`/api/Vendors/${id}/verify`, { method:'POST' }),
  reject: (id, reason) =>
    apiFetch(`/api/Vendors/${id}/reject`, { method:'POST', body:JSON.stringify({ reason }) }),
  stats: () => apiFetch('/api/Vendors/stats'),
};

// ═══════════════════════════════════════════════════════
// SETTINGS  /api/Settings
// GET /me → UserSettings
// PUT /me → SettingsUpdateDto
// ═══════════════════════════════════════════════════════
const SettingsApi = {
  me: () => apiFetch('/api/Settings/me'),
  update: (data) => apiFetch('/api/Settings/me', { method:'PUT', body:JSON.stringify(data) }),
};

// ═══════════════════════════════════════════════════════
// GEOCODE  /api/Geocode
// GET /reverse?latitude={lat}&longitude={lng}
// ═══════════════════════════════════════════════════════
const GeocodeApi = {
  reverse: (lat, lng) => apiFetch(`/api/Geocode/reverse${qs({ latitude:lat, longitude:lng })}`),
};

// ═══════════════════════════════════════════════════════
// TOURS  /api/Tours
// GET list → TourListResponse { data: TourDto[], total, page, pageSize, totalPages }
// GET /{id} → Tour
// POST → CreateTourRequest → Tour
// PUT /{id} → UpdateTourRequest
// DELETE /{id}
// GET /stats
// ═══════════════════════════════════════════════════════
const ToursApi = {
  list: ({ page=1, pageSize=10, search, isActive }={}) =>
    apiFetch('/api/Tours' + qs({ page, pageSize, search, isActive })),
  get: (id) => apiFetch(`/api/Tours/${id}`),
  create: (data) => apiFetch('/api/Tours', { method:'POST', body:JSON.stringify(data) }),
  update: (id, data) => apiFetch(`/api/Tours/${id}`, { method:'PUT', body:JSON.stringify(data) }),
  delete: (id) => apiFetch(`/api/Tours/${id}`, { method:'DELETE' }),
  stats: () => apiFetch('/api/Tours/stats'),
};

// ═══════════════════════════════════════════════════════
// TRANSLATIONS  /api/Translations
// GET list → TranslationListResponse { data: TranslationDto[], total, page, pageSize, totalPages }
// GET /{id} → Translation
// POST → CreateTranslationRequest → Translation
// PUT /{id} → UpdateTranslationRequest
// DELETE /{id}
// GET /stats
// GET /export?language → Dictionary<string, string>
// ═══════════════════════════════════════════════════════
const TranslationsApi = {
  list: ({ page=1, pageSize=50, search, category, status }={}) =>
    apiFetch('/api/Translations' + qs({ page, pageSize, search, category, status })),
  get: (id) => apiFetch(`/api/Translations/${id}`),
  create: (data) => apiFetch('/api/Translations', { method:'POST', body:JSON.stringify(data) }),
  update: (id, data) => apiFetch(`/api/Translations/${id}`, { method:'PUT', body:JSON.stringify(data) }),
  delete: (id) => apiFetch(`/api/Translations/${id}`, { method:'DELETE' }),
  stats: () => apiFetch('/api/Translations/stats'),
  export: (language='vi') => apiFetch('/api/Translations/export' + qs({ language })),
};

// ═══════════════════════════════════════════════════════
// ANALYTICS  /api/Analytics
// GET /narration-logs → NarrationLogListResponse
// GET /devices → DeviceListResponse
// POST /devices/register → DeviceInfo
// GET /overview → AnalyticsOverviewResponse
// GET /top-pois → List<TopPOIDto>
// ═══════════════════════════════════════════════════════
const AnalyticsApi = {
  narrationLogs: ({ page=1, pageSize=20, poiId, userId, fromDate, toDate }={}) =>
    apiFetch('/api/Analytics/narration-logs' + qs({ page, pageSize, poiId, userId, fromDate, toDate })),
  devices: ({ page=1, pageSize=20, platform, search }={}) =>
    apiFetch('/api/Analytics/devices' + qs({ page, pageSize, platform, search })),
  registerDevice: (data) => 
    apiFetch('/api/Analytics/devices/register', { method:'POST', body:JSON.stringify(data) }),
  overview: () => apiFetch('/api/Analytics/overview'),
  topPOIs: (limit=10) => apiFetch('/api/Analytics/top-pois' + qs({ limit })),
};

// ═══════════════════════════════════════════════════════
// DeviceActivityApi
// GET /device-activity/online-summary → online counts
// GET /device-activity/online → online devices list
// GET /device-activity/all → all devices with status
// ═══════════════════════════════════════════════════════
const DeviceActivityApi = {
  onlineSummary: () => apiFetch('/api/device-activity/online-summary'),
  online: ({ role, clientType, page=1, pageSize=50 }={}) =>
    apiFetch('/api/device-activity/online' + qs({ role, clientType, page, pageSize })),
  all: ({ role, status, search, page=1, pageSize=50 }={}) =>
    apiFetch('/api/device-activity/all' + qs({ role, status, search, page, pageSize })),
};

