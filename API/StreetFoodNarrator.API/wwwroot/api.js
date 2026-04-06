// API Base URL (auto-detect; allow override)
function resolveApiBaseUrl() {
    const override = window.__API_BASE_URL || localStorage.getItem('API_BASE_URL');
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

        const { suppressNotFound, ...fetchOptions } = options;

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

            

            if (response.status === 401) {

                TokenManager.logout();

                throw new Error('Unauthorized - Please login again');

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
                        if (data.error) errorMsg += '\n\nChi tiáº¿t: ' + data.error;

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

            body: JSON.stringify({ email, password, fullName, phoneNumber })

        });

    }

    

    async login(email, password) {

        const data = await this.request('/Auth/login', {

            method: 'POST',

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
    async getMenuItems(poiId, page = 1, pageSize = 50) {
        let query = `page=${page}&pageSize=${pageSize}`;
        if (poiId) query += `&poiId=${poiId}`;
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
    register: (data) => api.request('/Auth/register', { method: 'POST', body: JSON.stringify(data) }),
    me: () => api.getCurrentUser(),
    refresh: () => api.refreshToken()
};

const POIApi = {
    list: ({ page = 1, pageSize = 10, search, isActive, category } = {}) =>
        api.request('/POIs' + qs({ page, pageSize, search, isActive, category })),
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



// Show toast notification

function showToast(message, type = 'info') {

    const toast = document.createElement('div');

    toast.className = `toast toast-${type}`;

    toast.textContent = message;

    toast.style.cssText = `

        position: fixed;

        top: 20px;

        right: 20px;

        padding: 1rem 1.5rem;

        border-radius: 8px;

        color: white;

        font-weight: 600;

        z-index: 10000;

        animation: slideIn 0.3s ease-out;

        box-shadow: 0 4px 12px rgba(0,0,0,0.15);

    `;

    

    const colors = {

        success: '#06D6A0',

        error: '#EF476F',

        warning: '#FFD23F',

        info: '#004E89'

    };

    

    toast.style.background = colors[type] || colors.info;

    

    document.body.appendChild(toast);

    

    setTimeout(() => {

        toast.style.animation = 'slideOut 0.3s ease-in';

        setTimeout(() => toast.remove(), 300);

    }, 3000);

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


