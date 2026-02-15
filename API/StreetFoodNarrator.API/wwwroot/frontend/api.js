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
        return 'https://localhost:7110/api';
    }
    if (hostname === 'localhost' && port === '5500') {
        return 'https://localhost:7110/api';
    }

    // Production: assume same origin
    return `${window.location.origin}/api`;
}

const API_BASE_URL = resolveApiBaseUrl();



// Token Management

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

    getLoginUrl(role) {
        const normalized = this.normalizeRole(role) || this.getRoleContext();
        return normalized ? `login.html?role=${normalized.toLowerCase()}` : 'login.html';
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

    pickRoleForUser(user) {
        const roles = user?.roles || [];
        const ctx = this.getRoleContext();
        if (ctx && roles.includes(ctx)) return ctx;
        if (roles.includes('Admin')) return 'Admin';
        if (roles.includes('Vendor')) return 'Vendor';
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
        return token;
    },

    setToken(token, role) {
        const normalized = this.normalizeRole(role) || this.getRoleContext();
        if (!normalized) return;
        localStorage.setItem(this.roleKeys[normalized].token, token);
    },

    removeToken(role) {
        const normalized = this.normalizeRole(role) || this.getRoleContext();
        if (!normalized) return;
        localStorage.removeItem(this.roleKeys[normalized].token);
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

        window.location.href = loginUrl;
    }
};

TokenManager.initializeRoleContext();



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

                headers

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
            const role = TokenManager.pickRoleForUser(data.user);
            if (role) TokenManager.setRoleContext(role);
            TokenManager.setToken(data.token, role);
            TokenManager.setUser(data.user, role);
        }

        

        return data;

    }

    

    async getCurrentUser() {

        return this.request('/Auth/me');

    }

    

    async refreshToken() {

        const data = await this.request('/Auth/refresh', { method: 'POST' });

        if (data.token) {
            const role = TokenManager.getRoleContext();
            TokenManager.setToken(data.token, role);
        }

        return data;

    }

    



    // Settings APIs

    async getMySettings() {

        const role = TokenManager.getRoleContext();
        const roleParam = role ? `?role=${encodeURIComponent(role.toLowerCase())}` : '';
        return this.request(`/Settings/me${roleParam}`);

    }

    async updateMySettings(payload) {

        const role = TokenManager.getRoleContext();
        const roleParam = role ? `?role=${encodeURIComponent(role.toLowerCase())}` : '';
        return this.request(`/Settings/me${roleParam}`, {

            method: 'PUT',

            body: JSON.stringify(payload)

        });

    }

    // POI APIs

    async getPOIs(page = 1, pageSize = 10, search = '', isActive = null, category = null) {

        let query = `page=${page}&pageSize=${pageSize}`;

        if (search) query += `&search=${encodeURIComponent(search)}`;

        if (isActive !== null) query += `&isActive=${isActive}`;
        if (category) query += `&category=${encodeURIComponent(category)}`;

        

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

    

    async deletePOI(id) {

        return this.request(`/POIs/${id}`, {

            method: 'DELETE'

        });

    }

    

    async getPOIStats() {
        return this.request('/POIs/stats');
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

    

    async getPOIsWithoutAudio(language = 'vi-VN') {

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

    if (!bytes) return 'N/A';

    const sizes = ['Bytes', 'KB', 'MB', 'GB'];

    const i = Math.floor(Math.log(bytes) / Math.log(1024));

    return Math.round(bytes / Math.pow(1024, i) * 100) / 100 + ' ' + sizes[i];

}

