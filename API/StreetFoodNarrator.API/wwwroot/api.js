// API Base URL (auto-detect; allow override)
function normalizeApiBaseUrl(raw) {
    if (!raw) return '';

    let value = String(raw).trim();
    if (!value) return '';

    const ensureApiSuffix = (base) => /\/api$/i.test(base) ? base : `${base}/api`;

    if (/^https?:\/\//i.test(value)) {
        return ensureApiSuffix(value.replace(/\/+$/, ''));
    }

    if (value.startsWith('//')) {
        const absolute = `${window.location.protocol}${value}`.replace(/\/+$/, '');
        return ensureApiSuffix(absolute);
    }

    if (!value.startsWith('/')) {
        value = `/${value}`;
    }

    value = value.replace(/\/+$/, '');
    value = ensureApiSuffix(value);
    return `${window.location.origin}${value}`;
}

function resolveApiBaseUrl() {
    const override = normalizeApiBaseUrl(window.__API_BASE_URL || localStorage.getItem('API_BASE_URL'));
    if (override) return override;

    const { protocol, hostname, port } = window.location;

    // If served from the API host itself, use same-origin
    if ((hostname === 'localhost' || hostname === '127.0.0.1') && (port === '7110' || port === '5004')) {
        return `${protocol}//${hostname}:${port}/api`;
    }

    // Live Server defaults
    if (hostname === '127.0.0.1' && port === '5500') {
        return 'http://localhost:5004/api';
    }
    if (hostname === 'localhost' && port === '5500') {
        return 'http://localhost:5004/api';
    }

    // Production: assume same origin
    return `${window.location.origin}/api`;
}

const API_BASE_URL = resolveApiBaseUrl();

// JWT helpers
function parseJwtPayload(token) {
    if (!token) return null;
    try {
        const payload = token.split('.')[1];
        if (!payload) return null;
        const json = atob(payload.replace(/-/g, '+').replace(/_/g, '/'));
        return JSON.parse(json);
    } catch {
        return null;
    }
}

function parseJwtRoles(token) {
    const payload = parseJwtPayload(token);
    if (!payload) return [];
    const MS_ROLE = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';
    const raw = payload[MS_ROLE] || payload.role || payload.roles || payload.Role;
    if (!raw) return [];
    const list = Array.isArray(raw) ? raw : [raw];
    return list.flatMap(r => String(r).split(',')).map(r => r.trim()).filter(Boolean);
}

function getCookieValue(name) {
    if (!name || !document.cookie) return null;
    const pairs = document.cookie.split(';');
    for (const pair of pairs) {
        const idx = pair.indexOf('=');
        if (idx < 0) continue;
        const key = pair.slice(0, idx).trim();
        if (key !== name) continue;
        const value = pair.slice(idx + 1).trim();
        try {
            return decodeURIComponent(value);
        } catch {
            return value;
        }
    }
    return null;
}




// Token Management

function setAuthCookie(token) {
    if (!token) return;
    const maxAge = 60 * 60 * 24 * 7; // 7 days
    document.cookie = `auth_token=${encodeURIComponent(token)}; path=/; max-age=${maxAge}; SameSite=Lax`;
}

function clearAuthCookie() {
    document.cookie = 'auth_token=; path=/; Max-Age=0; SameSite=Lax';
}

function ensureAuthCookie() {
    const token = TokenManager.getToken();
    if (token) setAuthCookie(token);
}

const TokenManager = {
    roleKeys: {
        Admin: { token: 'jwt_token_admin', user: 'user_admin' },
        Vendor: { token: 'jwt_token_vendor', user: 'user_vendor' }
    },

    normalizeRole(role) {
        if (!role) return null;
        const r = String(role).toLowerCase();
        if (r === 'admin') return 'Admin';
        if (r === 'vendor') return 'Vendor';
        return null;
    },

    getRoleContext() {
        const stored = sessionStorage.getItem('activeRole') || localStorage.getItem('activeRole');
        return this.normalizeRole(stored);
    },

    setRoleContext(role) {
        const normalized = this.normalizeRole(role);
        if (normalized) {
            sessionStorage.setItem('activeRole', normalized);
            localStorage.setItem('activeRole', normalized);
        }
        return normalized;
    },

    loginPath: '/index',

    getLoginUrl(role) {
        const normalized = this.normalizeRole(role) || this.getRoleContext();
        const base = this.loginPath;
        return normalized ? `${base}?role=${normalized.toLowerCase()}` : base;
    },

    initializeRoleContext() {
        try {
            const params = new URLSearchParams(window.location.search);
            const roleParam = params.get('role');
            if (roleParam) {
                this.setRoleContext(roleParam);
            }
        } catch {
            // ignore
        }
    },

    ensureRoleContext() {
        const existing = this.getRoleContext();
        if (existing) return existing;

        const adminUser = localStorage.getItem(this.roleKeys.Admin.user);
        const vendorUser = localStorage.getItem(this.roleKeys.Vendor.user);
        const adminToken = localStorage.getItem(this.roleKeys.Admin.token);
        const vendorToken = localStorage.getItem(this.roleKeys.Vendor.token);

        if (adminUser && !vendorUser) return this.setRoleContext('Admin');
        if (vendorUser && !adminUser) return this.setRoleContext('Vendor');
        if (adminToken && !vendorToken) return this.setRoleContext('Admin');
        if (vendorToken && !adminToken) return this.setRoleContext('Vendor');

        // Infer role from current auth cookie to avoid stale/mixed localStorage contexts.
        const cookieToken = getCookieValue('auth_token');
        if (cookieToken) {
            const inferredRole = this.pickRoleForUser(null, cookieToken);
            if (inferredRole) {
                this.setRoleContext(inferredRole);
                const key = this.roleKeys[inferredRole].token;
                if (!localStorage.getItem(key)) {
                    localStorage.setItem(key, cookieToken);
                }
                return inferredRole;
            }
        }

        const legacyUserRaw = localStorage.getItem('user');
        if (legacyUserRaw) {
            try {
                const legacyUser = JSON.parse(legacyUserRaw);
                const inferred = this.pickRoleForUser(legacyUser);
                if (inferred) return this.setRoleContext(inferred);
            } catch {
                // ignore
            }
        }

        return null;
    },

    pickRoleForUser(user, token) {
        const rawRoles = user?.roles || user?.roleNames || [];
        const roles = Array.isArray(rawRoles) ? rawRoles.slice() : [rawRoles];
        const tokenRoles = token ? parseJwtRoles(token) : [];
        const merged = [...new Set([...roles, ...tokenRoles].filter(Boolean))];
        const ctx = this.getRoleContext();
        if (ctx && merged.includes(ctx)) return ctx;
        if (merged.includes('Admin')) return 'Admin';
        if (merged.includes('Vendor')) return 'Vendor';
        return null;
    },


    getToken(role) {
        const normalized = this.normalizeRole(role) || this.ensureRoleContext();
        if (!normalized) return null;
        const key = this.roleKeys[normalized].token;
        let token = localStorage.getItem(key);
        if (!token) {
            const legacy = localStorage.getItem('jwt_token');
            if (legacy) {
                localStorage.setItem(key, legacy);
                token = legacy;
            }
        }

        if (!token) {
            const cookieToken = getCookieValue('auth_token');
            if (cookieToken) {
                const cookieRole = this.pickRoleForUser(null, cookieToken);
                if (!cookieRole || cookieRole === normalized) {
                    token = cookieToken;
                    localStorage.setItem(key, token);
                }
            }
        }
        return token;
    },

    setToken(token, role) {
        const normalized = this.normalizeRole(role) || this.getRoleContext();
        if (!normalized) return;
        localStorage.setItem(this.roleKeys[normalized].token, token);
        setAuthCookie(token);
    },

    removeToken(role) {
        const normalized = this.normalizeRole(role) || this.getRoleContext();
        if (!normalized) return;
        localStorage.removeItem(this.roleKeys[normalized].token);
        clearAuthCookie();
    },

    isAuthenticated(role) {
        return this.getToken(role) !== null;
    },

    getUser(role) {
        const normalized = this.normalizeRole(role) || this.ensureRoleContext();
        if (!normalized) return null;
        const key = this.roleKeys[normalized].user;
        let user = localStorage.getItem(key);
        if (!user) {
            const legacy = localStorage.getItem('user');
            if (legacy) {
                localStorage.setItem(key, legacy);
                user = legacy;
            }
        }
        return user ? JSON.parse(user) : null;
    },

    setUser(user, role) {
        const normalized = this.normalizeRole(role) || this.getRoleContext();
        if (!normalized) return;
        localStorage.setItem(this.roleKeys[normalized].user, JSON.stringify(user));
    },

    removeUser(role) {
        const normalized = this.normalizeRole(role) || this.getRoleContext();
        if (!normalized) return;
        localStorage.removeItem(this.roleKeys[normalized].user);
    },

    logout(role) {
        const normalized = this.normalizeRole(role) || this.getRoleContext();
        const loginUrl = this.getLoginUrl(normalized);

        if (normalized) {
            this.removeToken(normalized);
            this.removeUser(normalized);
        }

        // Clear any other stored sessions/tokens to ensure full logout
        Object.keys(this.roleKeys).forEach(key => {
            const roleKey = this.roleKeys[key];
            if (roleKey?.token) localStorage.removeItem(roleKey.token);
            if (roleKey?.user) localStorage.removeItem(roleKey.user);
        });

        localStorage.removeItem('jwt_token');
        localStorage.removeItem('user');
        localStorage.removeItem('authToken');
        sessionStorage.removeItem('authToken');
        localStorage.removeItem('userSession');
        sessionStorage.removeItem('userSession');
        localStorage.removeItem('activeRole');
        sessionStorage.removeItem('activeRole');
        clearAuthCookie();

        window.location.href = loginUrl;
    }
};

TokenManager.initializeRoleContext();
ensureAuthCookie();



// API Client

class API {

    constructor() {

        this.baseURL = API_BASE_URL;

    }

    

    async request(endpoint, options = {}) {

        const { suppressNotFound, skipAutoLogoutOn401 = false, ...fetchOptions } = options;

        const url = `${this.baseURL}${endpoint}`;

        const headers = {

            'Content-Type': 'application/json',

            ...fetchOptions.headers

        };

        

        const token = TokenManager.getToken();

        if (token) {

            headers['Authorization'] = `Bearer ${token}`;

        }

        

        try {

            const response = await fetch(url, {

                ...fetchOptions,

                headers,
                
                // Disable cache for all requests to ensure fresh data
                cache: 'no-store'

            });

            

            if (response.status === 401 && !skipAutoLogoutOn401) {

                TokenManager.logout();

                throw new Error('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.');

            }

            

            const data = await response.json().catch(() => null);

            

            if (!response.ok) {
                if (response.status === 404 && suppressNotFound) {
                    return null;
                }

                // Try to get detailed error message

                let errorMsg = `HTTP error! status: ${response.status}`;

                

                if (data) {

                    if (data.errors && Array.isArray(data.errors)) {

                        // Identity errors format

                        errorMsg = data.errors.join(', ');

                    } else if (data.message) {

                        errorMsg = data.message;
                        // Append underlying detail (e.g. edge-tts stderr) when available
                        if (data.error) errorMsg += '\n\nChi tiết: ' + data.error;

                    } else if (data.title) {

                        errorMsg = data.title;

                    }

                }

                

                throw new Error(errorMsg);

            }

            

            return data;

        } catch (error) {

            if (!(error?.message && suppressNotFound)) {
                console.error('API Error:', error);
            }

            throw error;

        }

    }

    

    // Authentication APIs

    async register(email, password, fullName, phoneNumber = null) {

        return this.request('/Auth/register', {

            method: 'POST',

            skipAutoLogoutOn401: true,

            body: JSON.stringify({ email, password, fullName, phoneNumber })

        });

    }

    

    async login(email, password) {

        const data = await this.request('/Auth/login', {

            method: 'POST',

            skipAutoLogoutOn401: true,

            body: JSON.stringify({ email, password })

        });

        

        if (data.token) {
            const role = TokenManager.pickRoleForUser(data.user, data.token);
            if (role) TokenManager.setRoleContext(role);
            TokenManager.setToken(data.token, role);
            if (data.user) TokenManager.setUser(data.user, role);
        }

        

        return data;

    }

    

    async getCurrentUser() {

        return this.request('/Auth/me');

    }

    

    async refreshToken() {

        const data = await this.request('/Auth/refresh', { method: 'POST' });

        if (data.token) {
            const role = TokenManager.getRoleContext() || TokenManager.pickRoleForUser(TokenManager.getUser(), data.token);
            TokenManager.setToken(data.token, role);
        }

        return data;

    }

    



    // Settings APIs

    async getMySettings() {

        const role = TokenManager.getRoleContext() || TokenManager.pickRoleForUser(TokenManager.getUser());
        const roleParam = role ? `?role=${encodeURIComponent(role.toLowerCase())}` : '';
        return this.request(`/Settings/me${roleParam}`);

    }

    async updateMySettings(payload) {

        const role = TokenManager.getRoleContext() || TokenManager.pickRoleForUser(TokenManager.getUser());
        const roleParam = role ? `?role=${encodeURIComponent(role.toLowerCase())}` : '';
        return this.request(`/Settings/me${roleParam}`, {

            method: 'PUT',

            body: JSON.stringify(payload)

        });

    }

    // Notifications APIs
    async getSidebarNotifications(limit = 20) {
        const safeLimit = Math.max(1, Number(limit || 20));
        return this.request(`/Notifications/sidebar?limit=${safeLimit}`);
    }

    // POI APIs

    async getPOIs(page = 1, pageSize = 10, search = '', isActive = null, category = null, reviewStatus = null) {

        let query = `page=${page}&pageSize=${pageSize}`;

        if (search) query += `&search=${encodeURIComponent(search)}`;

        if (isActive !== null) query += `&isActive=${isActive}`;
        if (category) query += `&category=${encodeURIComponent(category)}`;
        if (reviewStatus) query += `&reviewStatus=${encodeURIComponent(reviewStatus)}`;
        return this.request(`/POIs?${query}`);

    }

    // Geocoding (via backend to avoid CORS)
    async geocode(query, limit = 3) {
        const q = encodeURIComponent(query || '');
        return this.request(`/Geocode?q=${q}&limit=${limit}`);
    }

    

    async getPOI(id) {

        return this.request(`/POIs/${id}`);

    }

    

    async createPOI(poiData) {

        return this.request('/POIs', {

            method: 'POST',

            body: JSON.stringify(poiData)

        });

    }

    

    async updatePOI(id, poiData) {

        return this.request(`/POIs/${id}`, {

            method: 'PUT',

            body: JSON.stringify(poiData)

        });

    }

    async reviewPOI(id, body) {
        return this.request(`/POIs/${id}/review`, {
            method: 'POST',
            body: JSON.stringify(body)
        });
    }

    

    async deletePOI(id) {

        return this.request(`/POIs/${id}`, {

            method: 'DELETE'

        });

    }

    

    async getPOIStats() {
        return this.request('/POIs/stats');
    }

    // Menu Items APIs
    async getMenuItems(poiId, page = 1, pageSize = 50, includeDeleted = false) {
        let query = `page=${page}&pageSize=${pageSize}`;
        if (poiId) query += `&poiId=${poiId}`;
        if (includeDeleted) query += '&includeDeleted=true';
        return this.request(`/MenuItems?${query}`);
    }

    async getMenuItem(id) {
        return this.request(`/MenuItems/${id}`);
    }

    async createMenuItem(data) {
        return this.request('/MenuItems', { method: 'POST', body: JSON.stringify(data) });
    }

    async updateMenuItem(id, data) {
        return this.request(`/MenuItems/${id}`, { method: 'PUT', body: JSON.stringify(data) });
    }

    async deleteMenuItem(id) {
        return this.request(`/MenuItems/${id}`, { method: 'DELETE' });
    }


    // Vendor APIs
    async getVendors(search = '', status = '', sort = '') {
        let query = '';
        if (search) query += `search=${encodeURIComponent(search)}&`;
        if (status) query += `status=${encodeURIComponent(status)}&`;
        if (sort) query += `sort=${encodeURIComponent(sort)}&`;
        query = query ? `?${query.slice(0, -1)}` : '';
        return this.request(`/Vendors${query}`);
    }

    async getVendorMe() {
        return this.request('/Vendors/me', { suppressNotFound: true });
    }

    async updateVendorMe(data) {
        return this.request('/Vendors/me', { method: 'PUT', body: JSON.stringify(data) });
    }

    async getVendorStats() {
        return this.request('/Vendors/stats');
    }
    

    // Auto Translate API
    async autoTranslate(text, targetLangs = ['en', 'zh-CN']) {
        return this.request('/AutoTranslate', { 
            method: 'POST', 
            body: JSON.stringify({ text, targetLangs })
        });
    }

    // Audio APIs

    async getAudioList(page = 1, pageSize = 10, language = null, poiId = null, status = null, poiName = null) {

        let query = `page=${page}&pageSize=${pageSize}`;

        if (language) query += `&language=${language}`;

        if (poiId) query += `&poiId=${poiId}`;

        if (status) query += `&status=${status}`;

        if (poiName) query += `&poiName=${encodeURIComponent(poiName)}`;

        return this.request(`/Audio?${query}`);

    }

    

    async getAudio(id) {

        return this.request(`/Audio/${id}`);

    }

    

    async uploadAudio(formData) {

        const token = TokenManager.getToken();

        const response = await fetch(`${this.baseURL}/Audio/upload`, {

            method: 'POST',

            headers: {

                'Authorization': `Bearer ${token}`

            },

            body: formData // Don't set Content-Type for FormData

        });

        

        if (response.status === 401) {

            TokenManager.logout();

            throw new Error('Unauthorized');

        }

        

        const data = await response.json();

        if (!response.ok) {

            throw new Error(data.message || 'Upload failed');

        }

        

        return data;

    }

    async replaceAudioFile(id, formData) {

        const token = TokenManager.getToken();

        const response = await fetch(`${this.baseURL}/Audio/${id}/replace-file`, {

            method: 'POST',

            headers: {

                'Authorization': `Bearer ${token}`

            },

            body: formData // Don't set Content-Type for FormData

        });

        

        if (response.status === 401) {

            TokenManager.logout();

            throw new Error('Unauthorized');

        }

        

        const data = await response.json();

        if (!response.ok) {

            throw new Error(data.message || 'Replace file failed');

        }

        

        return data;

    }

    async uploadTourImage(formData) {

        const token = TokenManager.getToken();

        const response = await fetch(`${this.baseURL}/Tours/upload-image`, {

            method: 'POST',

            headers: {

                'Authorization': `Bearer ${token}`

            },

            body: formData

        });

        if (response.status === 401) {

            TokenManager.logout();

            throw new Error('Unauthorized');

        }

        const data = await response.json().catch(() => null);

        if (!response.ok) {

            throw new Error(data?.message || 'Upload failed');

        }

        return data;

    }

    

    async getPOIsWithoutAudio(language = 'vi') {

        return this.request(`/Audio/pois-without-audio?language=${language}`);

    }

    

    async updateAudio(id, audioData) {

        return this.request(`/Audio/${id}`, {

            method: 'PUT',

            body: JSON.stringify(audioData)

        });

    }

    

    async deleteAudio(id) {

        return this.request(`/Audio/${id}`, {

            method: 'DELETE'

        });

    }

    async submitAudio(id) {
        return this.request(`/Audio/${id}/submit`, { method: 'POST' });
    }

    async approveAudio(id) {
        return this.request(`/Audio/${id}/approve`, { method: 'POST' });
    }

    async rejectAudio(id, reason = '') {
        return this.request(`/Audio/${id}/reject`, {
            method: 'POST',
            body: JSON.stringify({ reason })
        });
    }

}



// Export API instance

const api = new API();
if (typeof window !== 'undefined') {
    window.api = api;
    window.TokenManager = TokenManager;
}

// Schema-style helpers (compatible with provided CMS files)
const getToken = () => TokenManager.getToken();
const setToken = (token) => {
    const role = TokenManager.getRoleContext() || TokenManager.pickRoleForUser(TokenManager.getUser(), token) || (parseJwtRoles(token)[0] ?? null);
    if (role) TokenManager.setRoleContext(role);
    TokenManager.setToken(token, role);
};
const removeToken = () => {
    TokenManager.removeToken('Admin');
    TokenManager.removeToken('Vendor');
    localStorage.removeItem('jwt_token');
    sessionStorage.removeItem('jwt_token');
};

function qs(params = {}) {
    const q = new URLSearchParams();
    for (const [k, v] of Object.entries(params)) {
        if (v !== null && v !== undefined && v !== '') q.set(k, v);
    }
    const s = q.toString();
    return s ? '?' + s : '';
}

const AuthAPI = {
    login: (email, password) => api.login(email, password),
    register: (data) => api.request('/Auth/register', { method: 'POST', skipAutoLogoutOn401: true, body: JSON.stringify(data) }),
    me: () => api.getCurrentUser(),
    refresh: () => api.refreshToken()
};

const POIApi = {
    list: ({ page = 1, pageSize = 10, search, isActive, category, tourEligible } = {}) =>
        api.request('/POIs' + qs({ page, pageSize, search, isActive, category, tourEligible })),
    get: (id) => api.request(`/POIs/${id}`),
    create: (data) => api.request('/POIs', { method: 'POST', body: JSON.stringify(data) }),
    update: (id, data) => api.request(`/POIs/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
    delete: (id) => api.request(`/POIs/${id}`, { method: 'DELETE' }),
    stats: () => api.request('/POIs/stats'),
    sync: (sinceVersion = 0) => api.request('/POIs/sync' + qs({ sinceVersion }))
};

const AudioApi = {
    list: ({ page = 1, pageSize = 10, language, poiId, status, poiName } = {}) =>
        api.request('/Audio' + qs({ page, pageSize, language, poiId, status, poiName })),
    get: (id) => api.request(`/Audio/${id}`),
    create: (data) => api.request('/Audio', { method: 'POST', body: JSON.stringify(data) }),
    update: (id, data) => api.request(`/Audio/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
    delete: (id) => api.request(`/Audio/${id}`, { method: 'DELETE' }),
    upload: ({ audioFile, title, description, language, poiId, ttsText }) => {
        const fd = new FormData();
        if (audioFile) fd.append('AudioFile', audioFile);
        if (title) fd.append('Title', title);
        if (description) fd.append('Description', description);
        if (language) fd.append('Language', language);
        if (poiId != null) fd.append('POI_ID', String(poiId));
        if (ttsText) fd.append('TTSText', ttsText);
        return api.uploadAudio(fd);
    },
    poisWithoutAudio: (language = 'vi') => api.request('/Audio/pois-without-audio' + qs({ language })),
    generateFile: (id) => api.request(`/Audio/${id}/generate-file`, { method: 'POST' }),
    submit: (id) => api.request(`/Audio/${id}/submit`, { method: 'POST' }),
    approve: (id) => api.request(`/Audio/${id}/approve`, { method: 'POST' }),
    reject: (id, reason) => api.request(`/Audio/${id}/reject`, { method: 'POST', body: JSON.stringify({ reason }) }),
    bulkGenerate: (data) => api.request('/Audio/bulk-generate', { method: 'POST', body: JSON.stringify(data) })
};

const TTSApi = {
    test: () => api.request('/TTS/test'),
    generate: ({ text, language, voice }) => api.request('/TTS/generate', { method: 'POST', body: JSON.stringify({ text, language, voice }) }),
    voices: (language) => api.request('/TTS/voices' + qs({ language }))
};

const VendorApi = {
    me: () => api.request('/Vendors/me', { suppressNotFound: true }),
    updateMe: (data) => api.request('/Vendors/me', { method: 'PUT', body: JSON.stringify(data) }),
    list: ({ search, status, sort } = {}) => api.request('/Vendors' + qs({ search, status, sort })),
    stats: () => api.request('/Vendors/stats'),
    get: (vendorId) => api.request(`/Vendors/${vendorId}`),
    create: (data) => api.request('/Vendors', { method: 'POST', body: JSON.stringify(data) }),
    update: (vendorId, data) => api.request(`/Vendors/${vendorId}`, { method: 'PUT', body: JSON.stringify(data) }),
    updateStatus: (vendorId, status) => api.request(`/Vendors/${vendorId}/status`, { method: 'PUT', body: JSON.stringify({ status }) }),
    delete: (vendorId) => api.request(`/Vendors/${vendorId}`, { method: 'DELETE' })
};

const SettingsApi = {
    get: (role) => {
        const r = role || TokenManager.getRoleContext();
        return api.request('/Settings/me' + qs({ role: r ? String(r).toLowerCase() : null }));
    },
    update: (data, role) => {
        const r = role || TokenManager.getRoleContext();
        return api.request('/Settings/me' + qs({ role: r ? String(r).toLowerCase() : null }), { method: 'PUT', body: JSON.stringify(data) });
    }
};

const QrApi = {
    getAdminStatus: () => api.request('/Qr/admin/status'),
    rotateTest3Minutes: () => api.request('/Qr/admin/test-rotate', { method: 'POST' })
};

const TranslationsApi = {
    list: ({ page = 1, pageSize = 50, search, category, status } = {}) => 
        api.request('/Translations' + qs({ page, pageSize, search, category, status })),
    get: (id) => api.request(`/Translations/${id}`),
    create: (data) => api.request('/Translations', { method: 'POST', body: JSON.stringify(data) }),
    update: (id, data) => api.request(`/Translations/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
    delete: (id) => api.request(`/Translations/${id}`, { method: 'DELETE' }),
    stats: () => api.request('/Translations/stats')
};

const AnalyticsApi = {
    overview: () => api.request('/Analytics/overview'),
    narrationLogs: ({ page = 1, pageSize = 20, fromDate, toDate } = {}) => 
        api.request('/Analytics/narration-logs' + qs({ page, pageSize, fromDate, toDate })),
    topPOIs: (limit = 10) => api.request('/Analytics/top-pois' + qs({ limit })),
    devices: ({ page = 1, pageSize = 20, platform } = {}) => 
        api.request('/Analytics/devices' + qs({ page, pageSize, platform }))
};

const ToursApi = {
    list: ({ page = 1, pageSize = 20, search } = {}) => 
        api.request('/Tours' + qs({ page, pageSize, search })),
    get: (id) => api.request(`/Tours/${id}`),
    create: (data) => api.request('/Tours', { method: 'POST', body: JSON.stringify(data) }),
    update: (id, data) => api.request(`/Tours/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
    delete: (id) => api.request(`/Tours/${id}`, { method: 'DELETE' }),
    stats: () => api.request('/Tours/stats'),
    uploadImage: (file) => {
        const fd = new FormData();
        fd.append('file', file);
        return api.uploadTourImage(fd);
    }
};

const PaymentApi = {
    simulatePremium: (data = {}) =>
        api.request('/Payments/simulate-premium', { method: 'POST', body: JSON.stringify(data) }),
    simulatePremiumExpiring: (remainingMinutes = 3) =>
        api.request('/Payments/simulate-premium-expiring', {
            method: 'POST',
            body: JSON.stringify({ remainingMinutes })
        }),
    mySubmissions: () => api.request('/Payments/me'),
    myPremiumStatus: () => api.request('/Payments/me/premium-status'),
    listSubmissions: ({ status, search } = {}) =>
        api.request('/Payments/admin/submissions' + qs({ status, search })),
    listAppSubscriptions: ({ status, platform, search } = {}) =>
        api.request('/Subscriptions/admin/subscriptions' + qs({ status, platform, search })),
    reviewSubmission: (submissionId, status, note) =>
        api.request(`/Payments/admin/submissions/${submissionId}/review`, {
            method: 'POST',
            body: JSON.stringify({ status, note })
        })
};

const UsersApi = {
    list: ({ page = 1, pageSize = 20, search, role, includeDeleted } = {}) =>
        api.request('/Users' + qs({ page, pageSize, search, role, includeDeleted })),
    get: (id) => api.request(`/Users/${id}`),
    create: (data) => api.request('/Users', { method: 'POST', body: JSON.stringify(data) }),
    update: (id, data) => api.request(`/Users/${id}`, { method: 'PUT', body: JSON.stringify(data) }),
    delete: (id) => api.request(`/Users/${id}`, { method: 'DELETE' }),
    lock: (id) => api.request(`/Users/${id}/lock`, { method: 'POST' }),
    unlock: (id) => api.request(`/Users/${id}/unlock`, { method: 'POST' })
};




// Check auth on protected pages

function requireAuth() {

    if (!TokenManager.isAuthenticated()) {
        window.location.href = TokenManager.getLoginUrl();
        return false;
    }

    return true;

}



// Show loading indicator

function showLoading(elementId = 'loading') {

    const loader = document.getElementById(elementId);

    if (loader) loader.style.display = 'flex';

}



function hideLoading(elementId = 'loading') {

    const loader = document.getElementById(elementId);

    if (loader) loader.style.display = 'none';

}



// Unified feedback UI: toast (top-right), confirm popup, prompt popup.
(function initUnifiedFeedbackUi() {
    if (window.UiFeedback) return;

    function normalizeType(type) {
        const key = String(type || 'info').toLowerCase();
        if (key === 'ok') return 'success';
        if (key === 'err') return 'error';
        if (key === 'warn') return 'warning';
        return key;
    }

    function ensureStyles() {
        if (document.getElementById('unified-feedback-style')) return;
        const style = document.createElement('style');
        style.id = 'unified-feedback-style';
        style.textContent = `
            #uf-toast-wrap {
                position: fixed;
                top: 1rem;
                right: 1rem;
                z-index: 12000;
                display: flex;
                flex-direction: column;
                gap: .5rem;
                pointer-events: none;
                max-width: min(90vw, 420px);
            }
            .uf-toast {
                pointer-events: auto;
                display: flex;
                align-items: center;
                gap: .55rem;
                color: #fff;
                font-weight: 600;
                line-height: 1.35;
                border-radius: 11px;
                padding: .72rem .9rem;
                box-shadow: 0 12px 28px rgba(0,0,0,.2);
                animation: uf-toast-in .2s ease-out;
            }
            .uf-toast.success { background: #0f766e; }
            .uf-toast.error { background: #b42318; }
            .uf-toast.warning { background: #b45309; }
            .uf-toast.info { background: #1f2937; }
            @keyframes uf-toast-in {
                from { opacity: 0; transform: translateY(-8px); }
                to { opacity: 1; transform: translateY(0); }
            }
            .uf-overlay {
                position: fixed;
                inset: 0;
                background: rgba(15, 23, 42, .46);
                display: flex;
                align-items: center;
                justify-content: center;
                z-index: 12001;
                padding: 1rem;
                backdrop-filter: blur(2px);
            }
            .uf-dialog {
                width: min(460px, 100%);
                background: #fff;
                color: #111827;
                border-radius: 14px;
                box-shadow: 0 20px 42px rgba(0,0,0,.24);
                padding: 1rem;
            }
            .uf-title {
                font-size: 1.02rem;
                font-weight: 700;
                margin-bottom: .45rem;
            }
            .uf-msg {
                line-height: 1.46;
                color: #374151;
                margin-bottom: .8rem;
                white-space: pre-wrap;
            }
            .uf-input {
                width: 100%;
                border: 1px solid #d1d5db;
                border-radius: 10px;
                padding: .62rem .7rem;
                margin-bottom: .85rem;
                font-size: .95rem;
                outline: none;
            }
            .uf-input:focus {
                border-color: #2563eb;
                box-shadow: 0 0 0 3px rgba(37,99,235,.17);
            }
            .uf-actions {
                display: flex;
                justify-content: flex-end;
                gap: .5rem;
            }
            .uf-btn {
                border: 0;
                border-radius: 10px;
                padding: .52rem .82rem;
                font-weight: 600;
                cursor: pointer;
            }
            .uf-btn-cancel { background: #e5e7eb; color: #111827; }
            .uf-btn-ok { background: #2563eb; color: #fff; }
            .uf-btn-ok.danger { background: #b91c1c; }
        `;
        document.head.appendChild(style);
    }

    function ensureToastWrap() {
        ensureStyles();
        let wrap = document.getElementById('uf-toast-wrap');
        if (!wrap) {
            wrap = document.createElement('div');
            wrap.id = 'uf-toast-wrap';
            document.body.appendChild(wrap);
        }
        return wrap;
    }

    function toast(message, type = 'info', timeout = 3200) {
        if (!message) return;
        const kind = normalizeType(type);
        const icon = kind === 'success' ? 'fa-check-circle'
            : kind === 'error' ? 'fa-circle-exclamation'
            : kind === 'warning' ? 'fa-triangle-exclamation'
            : 'fa-circle-info';

        const item = document.createElement('div');
        item.className = `uf-toast ${kind}`;
        item.innerHTML = `<i class="fas ${icon}"></i><span>${String(message)}</span>`;
        ensureToastWrap().appendChild(item);
        setTimeout(() => item.remove(), timeout || 3200);
    }

    function confirm(message, options = {}) {
        ensureStyles();
        return new Promise(resolve => {
            const title = options.title || 'Xác nhận';
            const confirmText = options.confirmText || 'Đồng ý';
            const cancelText = options.cancelText || 'Hủy';
            const danger = !!options.danger;

            const overlay = document.createElement('div');
            overlay.className = 'uf-overlay';
            overlay.innerHTML = `
                <div class="uf-dialog" role="dialog" aria-modal="true">
                    <div class="uf-title">${title}</div>
                    <div class="uf-msg">${String(message || '')}</div>
                    <div class="uf-actions">
                        <button type="button" class="uf-btn uf-btn-cancel" data-uf-cancel>${cancelText}</button>
                        <button type="button" class="uf-btn uf-btn-ok ${danger ? 'danger' : ''}" data-uf-ok>${confirmText}</button>
                    </div>
                </div>
            `;
            const done = (ok) => { overlay.remove(); resolve(ok); };
            overlay.addEventListener('click', (e) => { if (e.target === overlay) done(false); });
            overlay.querySelector('[data-uf-cancel]')?.addEventListener('click', () => done(false));
            overlay.querySelector('[data-uf-ok]')?.addEventListener('click', () => done(true));
            document.body.appendChild(overlay);
        });
    }

    function prompt(message, defaultValue = '', options = {}) {
        ensureStyles();
        return new Promise(resolve => {
            const title = options.title || 'Nhập thông tin';
            const confirmText = options.confirmText || 'Xác nhận';
            const cancelText = options.cancelText || 'Hủy';
            const placeholder = options.placeholder || '';

            const overlay = document.createElement('div');
            overlay.className = 'uf-overlay';
            overlay.innerHTML = `
                <div class="uf-dialog" role="dialog" aria-modal="true">
                    <div class="uf-title">${title}</div>
                    <div class="uf-msg">${String(message || '')}</div>
                    <input class="uf-input" value="${String(defaultValue || '').replace(/"/g, '&quot;')}" placeholder="${String(placeholder || '').replace(/"/g, '&quot;')}" />
                    <div class="uf-actions">
                        <button type="button" class="uf-btn uf-btn-cancel" data-uf-cancel>${cancelText}</button>
                        <button type="button" class="uf-btn uf-btn-ok" data-uf-ok>${confirmText}</button>
                    </div>
                </div>
            `;
            const input = overlay.querySelector('.uf-input');
            const done = (val) => { overlay.remove(); resolve(val); };
            overlay.addEventListener('click', (e) => { if (e.target === overlay) done(null); });
            overlay.querySelector('[data-uf-cancel]')?.addEventListener('click', () => done(null));
            overlay.querySelector('[data-uf-ok]')?.addEventListener('click', () => done((input?.value || '').trim()));
            input?.addEventListener('keydown', (e) => {
                if (e.key === 'Enter') done((input.value || '').trim());
                if (e.key === 'Escape') done(null);
            });
            document.body.appendChild(overlay);
            setTimeout(() => input?.focus(), 0);
        });
    }

    window.UiFeedback = { toast, confirm, prompt, normalizeType };
})();

// Keep backward-compatible API for existing pages.
function showToast(message, type = 'info', timeout = 3200) {
    return window.UiFeedback.toast(message, type, timeout);
}



// Format date

function formatDate(dateString) {

    const date = new Date(dateString);

    return date.toLocaleString('vi-VN', {

        year: 'numeric',

        month: '2-digit',

        day: '2-digit',

        hour: '2-digit',

        minute: '2-digit'

    });

}



// Format file size

function formatFileSize(bytes) {

    if (bytes === null || bytes === undefined || Number.isNaN(Number(bytes))) return '0 KB';
    if (Number(bytes) === 0) return '0 KB';

    const sizes = ['Bytes', 'KB', 'MB', 'GB'];

    const i = Math.floor(Math.log(bytes) / Math.log(1024));

    return Math.round(bytes / Math.pow(1024, i) * 100) / 100 + ' ' + sizes[i];

}


