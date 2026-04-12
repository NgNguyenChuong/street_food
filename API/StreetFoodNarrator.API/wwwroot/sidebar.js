// Sidebar injection script
(function() {
    let user = null;
    try {
        if (window.TokenManager && typeof window.TokenManager.getUser === 'function') {
            user = window.TokenManager.getUser();
        } else {
            const params = new URLSearchParams(window.location.search);
            const roleParam = params.get('role');
            const role = roleParam ? roleParam.toLowerCase() : (sessionStorage.getItem('activeRole') || '').toLowerCase();
            let key = null;
            if (role === 'admin') key = 'user_admin';
            if (role === 'vendor') key = 'user_vendor';
            if (key) {
                const userRaw = localStorage.getItem(key);
                user = userRaw ? JSON.parse(userRaw) : null;
            } else {
                const adminRaw = localStorage.getItem('user_admin');
                const vendorRaw = localStorage.getItem('user_vendor');
                user = adminRaw ? JSON.parse(adminRaw) : (vendorRaw ? JSON.parse(vendorRaw) : null);
            }
        }
    } catch {
        user = null;
    }
    const roles = user?.roles || [];
    const roleContext = (sessionStorage.getItem('activeRole') || localStorage.getItem('activeRole') || '').toLowerCase();
    const hasAdmin = roles.includes('Admin');
    const hasVendor = roles.includes('Vendor');
    const isAdmin = roleContext ? roleContext === 'admin' : hasAdmin;
    const isVendor = roleContext ? roleContext === 'vendor' : hasVendor;
    const poiListHref = isAdmin ? 'poi-list' : 'poi-list?scope=mine';
    const poiListLabel = isAdmin ? 'Danh sách POI' : 'POI của tôi';

    const displayName = user?.fullName || user?.email?.split('@')[0] || 'Người dùng';
    const displayEmail = user?.email || '';
    const roleLabel = isAdmin ? 'Quản trị viên' : (isVendor ? 'Vendor' : 'Người dùng');
    const roleBadgeClass = isAdmin ? 'role-badge-admin' : 'role-badge-vendor';
    const avatarLetters = displayName.split(' ').map(w => w[0]).slice(-2).join('').toUpperCase();
    const NOTIF_POLL_VISIBLE_MS = 10000;
    const NOTIF_POLL_HIDDEN_MS = 30000;
    const NOTIF_READ_MAX_KEYS = 500;
    const notifRoleScope = roleContext || (isAdmin ? 'admin' : (isVendor ? 'vendor' : 'guest'));
    const notifIdentityScope = encodeURIComponent((displayEmail || displayName || 'anonymous').trim().toLowerCase());
    const NOTIF_READ_STORAGE_KEY = `sidebar_notif_read_v1:${notifRoleScope}:${notifIdentityScope}`;
    let notifPollTimer = null;
    let notifRequestInFlight = false;
    let latestNotifications = [];
    let readNotifKeys = new Set();

        const sidebarHTML = `
        <aside class="sidebar ${isVendor ? 'sidebar-vendor' : ''}" id="sidebar">
            <div class="logo-section">
            <div class="logo">
                <div class="logo-icon">
                    <i class="fas fa-utensils"></i>
                </div>
                <span id="appNameLabel">Street Food</span>
            </div>
            </div>

            <div class="user-profile-card">
                <div class="user-avatar">${avatarLetters}</div>
                <div class="user-info">
                    <div class="user-name" title="${displayName}">${displayName}</div>
                    ${displayEmail ? `<div class="user-email" title="${displayEmail}">${displayEmail}</div>` : ''}
                </div>
                <span class="role-badge ${roleBadgeClass}">${roleLabel}</span>
            </div>

            <div id="vendorApprovalNotice" class="vendor-approval-notice" style="display:none"></div>

            <div class="sidebar-notification">
                <button type="button" class="notif-btn" id="sidebarNotifBtn" aria-label="Thông báo">
                    <i class="fas fa-bell"></i>
                    <span>Thông báo</span>
                    <span class="notif-badge hidden" id="sidebarNotifBadge">0</span>
                </button>
                <div class="notif-panel" id="sidebarNotifPanel">
                    <div class="notif-panel-head">
                        <strong>Thông báo</strong>
                        <button type="button" class="notif-refresh" id="sidebarNotifRefresh" title="Làm mới">
                            <i class="fas fa-rotate"></i>
                        </button>
                    </div>
                    <div class="notif-list" id="sidebarNotifList">
                        <div class="notif-empty">Đang tải thông báo...</div>
                    </div>
                </div>
            </div>

            <nav>
                <ul class="nav-menu">
                    ${isAdmin ? `
                    <li class="nav-item">
                        <a href="dashboard" class="nav-link">
                            <i class="nav-icon fas fa-home"></i>
                            <span>Trang chủ</span>
                        </a>
                    </li>
                    ` : `
                    <li class="nav-item">
                        <a href="dashboard" class="nav-link">
                            <i class="nav-icon fas fa-home"></i>
                            <span>Trang chủ</span>
                        </a>
                    </li>
                    `}

                    ${isAdmin ? `
                    <li class="nav-item">
                        <a href="poi-list" class="nav-link">
                            <i class="nav-icon fas fa-map-marker-alt"></i>
                            <span>Quản lý POI</span>
                        </a>
                    </li>
                    ` : `
                    <li class="nav-item has-submenu" id="poiNavGroup">
                        <button type="button" class="nav-link nav-toggle">
                            <i class="nav-icon fas fa-map-marker-alt"></i>
                            <span>POI của tôi</span>
                            <i class="nav-caret fas fa-chevron-down"></i>
                        </button>
                        <ul class="submenu">
                            <li><a href="${poiListHref}" class="nav-sublink">${poiListLabel}</a></li>
                            <li><a href="poi-create" class="nav-sublink" id="vendorCreatePoiLink">Tạo POI</a></li>
                        </ul>
                    </li>
                    `}

                    ${isVendor ? `
                    <li class="nav-item">
                        <a href="vendor-profile" class="nav-link">
                            <i class="nav-icon fas fa-id-card"></i>
                            <span>Thông tin tài khoản</span>
                        </a>
                    </li>
                    <li class="nav-item">
                        <a href="payment-management" class="nav-link">
                            <i class="nav-icon fas fa-credit-card"></i>
                            <span>Thanh toán gói dịch vụ</span>
                        </a>
                    </li>
                    ` : ''}

                    ${isAdmin ? `
                    <li class="nav-item">
                        <a href="tour" class="nav-link">
                            <i class="nav-icon fas fa-route"></i>
                            <span>Quản lý Tour</span>
                        </a>
                    </li>
                    <li class="nav-item">
                        <a href="vendors-list" class="nav-link">
                            <i class="nav-icon fas fa-store"></i>
                            <span>Quản lý Vendors</span>
                        </a>
                    </li>
                    <li class="nav-item has-submenu" id="paymentNavGroup">
                        <button type="button" class="nav-link nav-toggle">
                            <i class="nav-icon fas fa-wallet"></i>
                            <span>Quản lý thanh toán</span>
                            <i class="nav-caret fas fa-chevron-down"></i>
                        </button>
                        <ul class="submenu">
                            <li><a href="payment-management?mode=vendor" class="nav-sublink">Thanh toán vendor</a></li>
                            <li><a href="payment-management?mode=app" class="nav-sublink">Thanh toán user app</a></li>
                        </ul>
                    </li>
                    ` : ''}

                    ${isAdmin ? `
                    <li class="nav-item">
                        <a href="audio-list" class="nav-link">
                            <i class="nav-icon fas fa-microphone"></i>
                            <span>Quản lý âm thanh</span>
                        </a>
                    </li>
                    ` : `
                    <li class="nav-item has-submenu" id="audioNavGroup">
                        <button type="button" class="nav-link nav-toggle">
                            <i class="nav-icon fas fa-microphone"></i>
                            <span>Âm thanh</span>
                            <i class="nav-caret fas fa-chevron-down"></i>
                        </button>
                        <ul class="submenu">
                            <li><a href="audio-list" class="nav-sublink" id="vendorAudioListLink">Danh sách âm thanh</a></li>
                            <li><a href="audio-bulk-generate" class="nav-sublink" id="vendorAudioBulkLink">TTS hàng loạt</a></li>
                        </ul>
                    </li>
                    `}
                    ${isAdmin ? `
                    <li class="nav-item">
                        <a href="users" class="nav-link">
                            <i class="nav-icon fas fa-users"></i>
                            <span>Lịch sử sử dụng</span>
                        </a>
                    </li>
                    ` : ''}
                    <li class="nav-item">
                        <a href="/" class="nav-link" onclick="logout(); return false;">
                            <i class="nav-icon fas fa-sign-out-alt"></i>
                            <span>Đăng xuất</span>
                        </a>
                    </li>
                </ul>
            </nav>
        </aside>
    `;

    const sidebarCSS = `
        <style id="sidebar-styles">
            @import url('https://fonts.googleapis.com/css2?family=Be+Vietnam+Pro:wght@400;500;600;700;800&family=Noto+Serif:wght@700;900&display=swap');

            html, body, button, input, select, textarea {
                font-family: 'Be Vietnam Pro', 'DM Sans', 'Segoe UI', Tahoma, sans-serif;
            }

            .page-title, .card-title, .logo {
                font-family: 'Noto Serif', 'Playfair Display', serif;
            }

            .sidebar {
                position: fixed;
                left: 0;
                top: 0;
                width: 280px;
                height: 100vh;
                background: linear-gradient(180deg, #1A1A2E 0%, #2D2D44 100%);
                padding: 2rem 0;
                z-index: 1000;
                box-shadow: 4px 0 20px rgba(26, 26, 46, 0.08);
                transition: transform 0.3s cubic-bezier(0.4, 0, 0.2, 1);
            }

            .logo-section {
                padding: 0 2rem 1.5rem;
                border-bottom: 1px solid rgba(255, 255, 255, 0.1);
                margin-bottom: 0;
            }

            .user-profile-card {
                display: flex;
                align-items: center;
                gap: 0.75rem;
                margin: 0 1rem 1.5rem;
                padding: 0.9rem 1rem;
                background: rgba(255, 255, 255, 0.06);
                border: 1px solid rgba(255, 255, 255, 0.1);
                border-radius: 14px;
                margin-top: 1rem;
                overflow: hidden;
            }

            .user-avatar {
                width: 40px;
                height: 40px;
                border-radius: 10px;
                background: linear-gradient(135deg, #FF6B35, #E85A2A);
                display: flex;
                align-items: center;
                justify-content: center;
                font-size: 0.85rem;
                font-weight: 700;
                color: white;
                flex-shrink: 0;
                letter-spacing: 0.5px;
            }

            .user-info {
                flex: 1;
                overflow: hidden;
            }

            .user-name {
                color: #ffffff;
                font-size: 0.85rem;
                font-weight: 600;
                white-space: nowrap;
                overflow: hidden;
                text-overflow: ellipsis;
                line-height: 1.3;
            }

            .user-email {
                color: rgba(255, 255, 255, 0.45);
                font-size: 0.7rem;
                white-space: nowrap;
                overflow: hidden;
                text-overflow: ellipsis;
                margin-top: 0.1rem;
            }

            .role-badge {
                flex-shrink: 0;
                font-size: 0.65rem;
                font-weight: 700;
                padding: 0.25rem 0.55rem;
                border-radius: 20px;
                letter-spacing: 0.3px;
                text-transform: uppercase;
            }

            .role-badge-admin {
                background: rgba(255, 107, 53, 0.2);
                color: #FF8C5E;
                border: 1px solid rgba(255, 107, 53, 0.35);
            }

            .role-badge-vendor {
                background: rgba(34, 197, 94, 0.15);
                color: #4ade80;
                border: 1px solid rgba(34, 197, 94, 0.3);
            }

            .vendor-approval-notice {
                margin: 0 1rem 1rem;
                padding: 0.75rem 0.9rem;
                border-radius: 12px;
                font-size: 0.8rem;
                line-height: 1.45;
                border: 1px solid rgba(251, 191, 36, 0.35);
                background: rgba(251, 191, 36, 0.12);
                color: #fde68a;
            }

            .vendor-approval-notice.rejected {
                border-color: rgba(248, 113, 113, 0.35);
                background: rgba(248, 113, 113, 0.12);
                color: #fecaca;
            }

            .sidebar-notification {
                position: relative;
                margin: 0 1rem 1rem;
            }

            .notif-btn {
                width: 100%;
                display: flex;
                align-items: center;
                justify-content: center;
                gap: 0.5rem;
                padding: 0.75rem 0.9rem;
                border-radius: 12px;
                border: 1px solid rgba(255, 255, 255, 0.12);
                background: rgba(255, 255, 255, 0.06);
                color: #fff;
                cursor: pointer;
                font-weight: 600;
                transition: all 0.2s ease;
                position: relative;
            }

            .notif-btn:hover {
                background: rgba(34, 197, 94, 0.16);
                border-color: rgba(34, 197, 94, 0.35);
            }

            .notif-badge {
                min-width: 20px;
                height: 20px;
                border-radius: 999px;
                background: #EF4444;
                color: white;
                font-size: 0.7rem;
                font-weight: 700;
                display: inline-flex;
                align-items: center;
                justify-content: center;
                padding: 0 0.35rem;
                line-height: 1;
            }

            .notif-badge.hidden {
                display: none;
            }

            .notif-panel {
                display: none;
                position: absolute;
                left: 0;
                right: 0;
                top: calc(100% + 0.5rem);
                background: #FFFFFF;
                border-radius: 12px;
                border: 1px solid #E8ECF1;
                box-shadow: 0 12px 30px rgba(15, 23, 42, 0.16);
                z-index: 1200;
                overflow: hidden;
            }

            .notif-panel.show {
                display: block;
            }

            .notif-panel-head {
                display: flex;
                align-items: center;
                justify-content: space-between;
                padding: 0.75rem 0.85rem;
                border-bottom: 1px solid #EEF2F7;
                color: #111827;
                font-size: 0.86rem;
            }

            .notif-refresh {
                width: 28px;
                height: 28px;
                border-radius: 8px;
                border: 1px solid #E5E7EB;
                background: #fff;
                color: #374151;
                cursor: pointer;
            }

            .notif-list {
                max-height: 320px;
                overflow-y: auto;
            }

            .notif-empty {
                padding: 0.9rem;
                color: #6B7280;
                font-size: 0.82rem;
            }

            .notif-item {
                display: flex;
                gap: 0.65rem;
                text-decoration: none;
                padding: 0.75rem 0.85rem;
                border-bottom: 1px solid #F3F4F6;
                color: #111827;
                transition: background 0.15s ease;
            }

            .notif-item.unread {
                background: #F0F9FF;
            }

            .notif-item.unread .notif-item-title {
                color: #0F172A;
            }

            .notif-item.unread .notif-item-icon {
                box-shadow: inset 0 0 0 2px rgba(59, 130, 246, 0.2);
            }

            .notif-item:last-child {
                border-bottom: none;
            }

            .notif-item:hover {
                background: #F9FAFB;
            }

            .notif-item-icon {
                width: 28px;
                height: 28px;
                border-radius: 8px;
                background: #EEF2FF;
                color: #3730A3;
                display: inline-flex;
                align-items: center;
                justify-content: center;
                flex-shrink: 0;
            }

            .notif-item.warn .notif-item-icon {
                background: #FEF2F2;
                color: #B91C1C;
            }

            .notif-item-content {
                min-width: 0;
            }

            .notif-item-title {
                display: block;
                font-size: 0.82rem;
                font-weight: 700;
                color: #111827;
            }

            .notif-item-status {
                display: inline-flex;
                align-items: center;
                justify-content: center;
                margin-left: 0.35rem;
                color: #6B7280;
                font-size: 0.68rem;
                vertical-align: middle;
            }

            .notif-item-status i {
                line-height: 1;
            }

            .notif-item-message {
                display: block;
                margin-top: 0.15rem;
                font-size: 0.76rem;
                color: #6B7280;
                line-height: 1.35;
            }

            .notif-item-response {
                display: block;
                margin-top: 0.25rem;
                font-size: 0.75rem;
                color: #0F766E;
                line-height: 1.35;
                font-weight: 600;
            }

            .notif-item-meta {
                display: block;
                margin-top: 0.2rem;
                font-size: 0.7rem;
                color: #9CA3AF;
                line-height: 1.35;
            }

            .logo {
                font-family: 'Playfair Display', serif;
                font-size: 1.5rem;
                font-weight: 900;
                color: white;
                display: flex;
                align-items: center;
                gap: 0.75rem;
            }

            .logo-icon {
                width: 48px;
                height: 48px;
                background: linear-gradient(135deg, #22c55e 0%, #0f766e 100%);
                border-radius: 12px;
                display: flex;
                align-items: center;
                justify-content: center;
                font-size: 1.5rem;
            }

            .nav-menu {
                list-style: none;
                padding: 0 1rem;
            }

            .nav-item {
                margin-bottom: 0.5rem;
            }

            .nav-link {
                display: flex;
                align-items: center;
                gap: 1rem;
                padding: 1rem 1.5rem;
                color: rgba(255, 255, 255, 0.7);
                text-decoration: none;
                border-radius: 12px;
                transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
                font-weight: 500;
                position: relative;
                width: 100%;
                background: transparent;
                border: none;
                text-align: left;
                cursor: pointer;
            }

            .sidebar-vendor .nav-link {
                font-size: 0.96rem;
            }

            .nav-link::before {
                content: '';
                position: absolute;
                left: 0;
                top: 0;
                width: 4px;
                height: 100%;
                background: #22c55e;
                transform: scaleY(0);
                transition: transform 0.3s ease;
            }

            .nav-link:hover,
            .nav-link.active {
                background: rgba(34, 197, 94, 0.15);
                color: white;
                transform: translateX(4px);
            }

            .nav-link.active::before {
                transform: scaleY(1);
            }

            .nav-icon {
                font-size: 1.25rem;
                width: 24px;
                text-align: center;
            }

            .nav-caret {
                margin-left: auto;
                font-size: 0.85rem;
                transition: transform 0.2s ease;
            }

            .nav-item.has-submenu.open .nav-caret {
                transform: rotate(180deg);
            }

            .submenu {
                list-style: none;
                padding-left: 2.5rem;
                margin-top: 0.35rem;
                display: none;
            }

            .nav-item.has-submenu.open .submenu {
                display: block;
            }

            .nav-sublink {
                display: block;
                padding: 0.6rem 1rem;
                color: rgba(255, 255, 255, 0.65);
                text-decoration: none;
                border-radius: 10px;
                transition: all 0.2s ease;
                font-size: 0.9rem;
                font-weight: 500;
                line-height: 1.3;
            }

            .sidebar-vendor .nav-sublink {
                font-size: 0.95rem;
                font-weight: 500;
            }

            .nav-sublink:hover,
            .nav-sublink.active {
                background: rgba(34, 197, 94, 0.12);
                color: #fff;
            }

            /* Keep admin payment menu typography consistent with other admin items */
            #paymentNavGroup > .nav-link,
            #paymentNavGroup .nav-sublink {
                font-size: 0.96rem;
                font-weight: 500;
                line-height: 1.35;
            }

            .nav-link.disabled,
            .nav-sublink.disabled {
                opacity: 0.4;
                pointer-events: none;
                cursor: not-allowed;
                transform: none !important;
            }

            .nav-sublink.gated-link {
                opacity: 0.5;
                cursor: not-allowed;
            }

            body {
                 margin-left: 280px !important;
            }

            @media (max-width: 768px) {
                body {
                    margin-left: 0 !important;
                }
                .sidebar {
                    transform: translateX(-100%);
                }
            }
        </style>
    `;

    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', init);
    } else {
        init();
    }

    function applyAppSettings(settings) {
        const appName = settings?.appName;
        if (!appName) return;
        const label = document.getElementById('appNameLabel');
        if (label) label.textContent = appName;
    }

    function getApiClient() {
        if (window.api) return window.api;
        if (typeof api !== 'undefined') return api;
        return null;
    }

    function getTokenManager() {
        if (window.TokenManager) return window.TokenManager;
        if (typeof TokenManager !== 'undefined') return TokenManager;
        return null;
    }

    function loadReadNotificationKeys() {
        try {
            const raw = localStorage.getItem(NOTIF_READ_STORAGE_KEY);
            const parsed = raw ? JSON.parse(raw) : [];
            if (!Array.isArray(parsed)) return new Set();
            return new Set(parsed.filter(v => typeof v === 'string' && v.trim().length > 0));
        } catch {
            return new Set();
        }
    }

    function saveReadNotificationKeys() {
        try {
            const keys = Array.from(readNotifKeys);
            if (keys.length > NOTIF_READ_MAX_KEYS) {
                keys.splice(0, keys.length - NOTIF_READ_MAX_KEYS);
            }
            readNotifKeys = new Set(keys);
            localStorage.setItem(NOTIF_READ_STORAGE_KEY, JSON.stringify(keys));
        } catch {
            // ignore storage errors
        }
    }

    function getNotificationKey(item) {
        if (!item) return null;

        if (item.notificationId !== undefined && item.notificationId !== null && String(item.notificationId).trim() !== '') {
            return `id:${String(item.notificationId).trim()}`;
        }

        const title = String(item.title || '').trim();
        const message = String(item.message || '').trim();
        const createdAt = String(item.createdAt || '').trim();
        const href = String(item.href || '').trim();

        if (!title && !message && !createdAt && !href) return null;
        return `sig:${title}|${message}|${createdAt}|${href}`;
    }

    function isNotificationRead(item) {
        const key = getNotificationKey(item);
        return !!key && readNotifKeys.has(key);
    }

    function markNotificationKeyAsRead(key) {
        if (!key || readNotifKeys.has(key)) return false;
        readNotifKeys.add(key);
        saveReadNotificationKeys();
        return true;
    }

    async function hydrateAppSettings() {
        try {
            const cached = localStorage.getItem('app_settings_cache');
            if (cached) {
                applyAppSettings(JSON.parse(cached));
            }
        } catch {
            // ignore cache errors
        }

        try {
            const apiClient = getApiClient();
            const tokenMgr = getTokenManager();
            if (!apiClient || !tokenMgr || !tokenMgr.isAuthenticated()) return;
            const settings = await apiClient.getMySettings();
            if (settings) {
                localStorage.setItem('app_settings_cache', JSON.stringify(settings));
                applyAppSettings(settings);
            }
        } catch {
            // ignore remote errors
        }
    }

    function buildNotificationItem({
        notificationId = null,
        icon = 'fa-circle-info',
        title,
        message,
        href,
        count = 1,
        kind = 'info',
        createdAt = null,
        responseMessage = '',
        processedAt = null,
        processedBy = ''
    }) {
        return { notificationId, icon, title, message, href, count, kind, createdAt, responseMessage, processedAt, processedBy };
    }

    function escapeHtml(input) {
        return String(input || '')
            .replace(/&/g, '&amp;')
            .replace(/</g, '&lt;')
            .replace(/>/g, '&gt;')
            .replace(/"/g, '&quot;')
            .replace(/'/g, '&#39;');
    }

    function formatNotificationDate(value) {
        if (!value) return '';
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return '';
        return date.toLocaleString('vi-VN', {
            year: 'numeric',
            month: '2-digit',
            day: '2-digit',
            hour: '2-digit',
            minute: '2-digit'
        });
    }

    async function fetchSidebarNotifications() {
        const apiClient = getApiClient();
        if (!apiClient) return [];

        const payload = typeof apiClient.getSidebarNotifications === 'function'
            ? await apiClient.getSidebarNotifications(30)
            : await apiClient.request('/Notifications/sidebar?limit=30');

        const rows = Array.isArray(payload?.data)
            ? payload.data
            : (Array.isArray(payload) ? payload : []);

        return rows.map(row => buildNotificationItem({
            notificationId: row.notificationId,
            icon: row.icon || 'fa-circle-info',
            title: row.title || 'Thông báo',
            message: row.message || '',
            href: row.href || '#',
            count: 1,
            kind: row.kind || 'info',
            createdAt: row.createdAt,
            responseMessage: row.responseMessage || '',
            processedAt: row.processedAt,
            processedBy: row.processedBy || ''
        }));
    }

    function renderSidebarNotifications(items) {
        const list = document.getElementById('sidebarNotifList');
        const badge = document.getElementById('sidebarNotifBadge');
        if (!list || !badge) return;

        if (!items.length) {
            list.innerHTML = '<div class="notif-empty">Không có thông báo mới.</div>';
            badge.classList.add('hidden');
            return;
        }

        const total = items.reduce((sum, item) => {
            if (isNotificationRead(item)) return sum;
            return sum + Math.max(0, Number(item.count || 0));
        }, 0);
        if (total > 0) {
            badge.textContent = total > 99 ? '99+' : String(total);
            badge.classList.remove('hidden');
        } else {
            badge.classList.add('hidden');
        }

        list.innerHTML = items.map(item => {
            const notifKey = getNotificationKey(item) || '';
            const isRead = isNotificationRead(item);
            const unreadClass = isRead ? '' : ' unread';
            const createdText = formatNotificationDate(item.createdAt);
            const processedText = formatNotificationDate(item.processedAt);
            const infoBits = [];
            if (createdText) infoBits.push(`Ngày tạo: ${createdText}`);
            if (processedText) infoBits.push(`Ngày xử lý: ${processedText}`);
            if (item.processedBy) infoBits.push(`Xử lý bởi: ${item.processedBy}`);
            const meta = infoBits.join(' • ');
            const hasLink = item.href && item.href !== '#';
            const itemClass = `notif-item ${item.kind === 'warn' ? 'warn' : ''}${unreadClass}`.trim();
            const keyAttr = notifKey ? ` data-notif-key="${escapeHtml(notifKey)}"` : '';
            const openTag = hasLink
                ? `<a href="${escapeHtml(item.href)}" class="${itemClass}"${keyAttr}>`
                : `<div class="${itemClass}"${keyAttr}>`;
            const closeTag = hasLink ? '</a>' : '</div>';
            const viewedIcon = isRead
                ? '<span class="notif-item-status" title="Đã xem" aria-label="Đã xem"><i class="fas fa-eye"></i></span>'
                : '';

            return `${openTag}
                <span class="notif-item-icon"><i class="fas ${escapeHtml(item.icon || 'fa-circle-info')}"></i></span>
                <span class="notif-item-content">
                    <span class="notif-item-title">${escapeHtml(item.title || '')}${viewedIcon}</span>
                    <span class="notif-item-message">${escapeHtml(item.message || '')}</span>
                    ${item.responseMessage ? `<span class="notif-item-response">Phản hồi: ${escapeHtml(item.responseMessage)}</span>` : ''}
                    ${meta ? `<span class="notif-item-meta">${escapeHtml(meta)}</span>` : ''}
                </span>
            ${closeTag}`;
        }).join('');
    }

    async function loadSidebarNotifications(options = {}) {
        const { showLoading = true } = options;
        const list = document.getElementById('sidebarNotifList');
        if (showLoading && list) list.innerHTML = '<div class="notif-empty">Đang tải thông báo...</div>';

        if (notifRequestInFlight) return;
        notifRequestInFlight = true;

        try {
            const items = await fetchSidebarNotifications();
            latestNotifications = items;
            renderSidebarNotifications(items);
        } catch {
            if (showLoading && list) list.innerHTML = '<div class="notif-empty">Không tải được thông báo.</div>';
        } finally {
            notifRequestInFlight = false;
        }
    }

    function setupNotificationBell() {
        const btn = document.getElementById('sidebarNotifBtn');
        const panel = document.getElementById('sidebarNotifPanel');
        const refresh = document.getElementById('sidebarNotifRefresh');
        const list = document.getElementById('sidebarNotifList');
        if (!btn || !panel) return;

        const scheduleNextPoll = () => {
            if (notifPollTimer) {
                clearTimeout(notifPollTimer);
            }

            const delay = document.hidden ? NOTIF_POLL_HIDDEN_MS : NOTIF_POLL_VISIBLE_MS;
            notifPollTimer = window.setTimeout(async () => {
                await loadSidebarNotifications({ showLoading: false });
                scheduleNextPoll();
            }, delay);
        };

        btn.addEventListener('click', async (event) => {
            event.stopPropagation();
            panel.classList.toggle('show');
            if (panel.classList.contains('show')) {
                await loadSidebarNotifications({ showLoading: true });
            }
        });

        panel.addEventListener('click', (event) => event.stopPropagation());
        document.addEventListener('click', () => panel.classList.remove('show'));

        if (refresh) {
            refresh.addEventListener('click', async (event) => {
                event.preventDefault();
                event.stopPropagation();
                await loadSidebarNotifications({ showLoading: true });
            });
        }

        if (list) {
            list.addEventListener('click', (event) => {
                const itemEl = event.target.closest('.notif-item');
                if (!itemEl) return;
                const key = itemEl.getAttribute('data-notif-key');
                if (markNotificationKeyAsRead(key)) {
                    renderSidebarNotifications(latestNotifications);
                }
            });
        }

        loadSidebarNotifications({ showLoading: true });
        scheduleNextPoll();

        document.addEventListener('visibilitychange', () => {
            if (!document.hidden) {
                loadSidebarNotifications({ showLoading: false });
            }
            scheduleNextPoll();
        });

        window.addEventListener('focus', () => {
            loadSidebarNotifications({ showLoading: false });
        });

        window.addEventListener('online', () => {
            loadSidebarNotifications({ showLoading: false });
        });
    }

    function init() {
        const currentPage = window.location.pathname.split('/').pop()?.replace('.html', '') || 'index';
        if (currentPage === 'index' || currentPage === 'login' || currentPage === 'register') {
            return;
        }

        document.head.insertAdjacentHTML('beforeend', sidebarCSS);
        document.body.insertAdjacentHTML('afterbegin', sidebarHTML);

        const normalizeHref = (href) => (href || '').split('?')[0].split('#')[0];
        const currentSearchParams = new URLSearchParams(window.location.search || '');
        const getHrefQuery = (href) => {
            const queryPart = (href || '').split('?')[1] || '';
            return queryPart.split('#')[0];
        };

        const isHrefActive = (href) => {
            if (normalizeHref(href) !== currentPage) return false;

            const query = getHrefQuery(href);
            if (!query) {
                return !window.location.search;
            }

            const linkParams = new URLSearchParams(query);
            for (const [key, value] of linkParams.entries()) {
                const currentValue = currentSearchParams.get(key);
                if ((currentValue || '').toLowerCase() !== String(value).toLowerCase()) {
                    return false;
                }
            }

            return true;
        };

        const links = document.querySelectorAll('.nav-link');
        links.forEach(link => {
            const href = link.getAttribute('href') || '';
            if (href && isHrefActive(href)) {
                link.classList.add('active');
            }
        });

        const sublinks = document.querySelectorAll('.nav-sublink');
        sublinks.forEach(link => {
            const href = link.getAttribute('href') || '';
            if (href && isHrefActive(href)) {
                link.classList.add('active');
            }

            if (normalizeHref(href) === currentPage || link.classList.contains('active')) {
                const parent = link.closest('.nav-item.has-submenu');
                if (parent) parent.classList.add('open');
            }
        });

        if (currentPage === 'poi-edit') {
            const poiGroup = document.getElementById('poiNavGroup');
            if (poiGroup) poiGroup.classList.add('open');
        }

        const toggles = document.querySelectorAll('.nav-toggle');
        toggles.forEach(toggle => {
            toggle.addEventListener('click', () => {
                const parent = toggle.closest('.nav-item.has-submenu');
                if (parent) parent.classList.toggle('open');
            });
        });

        readNotifKeys = loadReadNotificationKeys();
        setupNotificationBell();

        hydrateAppSettings();
        applyVendorApprovalGate();
        applyVendorPremiumPoiGate();
    }

    async function applyVendorApprovalGate() {
        if (!isVendor) return;
        const apiClient = getApiClient();
        const tokenMgr = getTokenManager();
        if (!apiClient || !tokenMgr || !tokenMgr.isAuthenticated()) return;

        let status = 'pending';
        try {
            const profile = await apiClient.getVendorMe();
            status = String(profile?.verificationStatus || profile?.VerificationStatus || 'pending').toLowerCase();
        } catch {
            status = 'pending';
        }

        if (status === 'approved') return;

        const note = document.getElementById('vendorApprovalNotice');
        if (note) {
            note.style.display = 'block';
            note.classList.toggle('rejected', status === 'rejected');
            note.textContent = status === 'rejected'
                ? 'Tài khoản Vendor của bạn đang bị từ chối. Vui lòng liên hệ Admin để được hỗ trợ và duyệt lại.'
                : 'Tài khoản Vendor của bạn đang chờ Admin duyệt. Một số chức năng quản lý nội dung tạm thời bị khóa.';
        }

        const allowList = new Set(['/', 'index', 'dashboard']);
        const links = document.querySelectorAll('.nav-link, .nav-sublink');

        links.forEach(link => {
            const href = link.getAttribute('href') || '';
            const normalized = (href || '').split('?')[0].split('#')[0];
            if (!allowList.has(normalized)) {
                link.classList.add('disabled');
                link.setAttribute('aria-disabled', 'true');
                link.setAttribute('title', 'Tài khoản Vendor đang chờ duyệt');
            }
        });
    }

    async function getVendorPremiumStatus() {
        const apiClient = getApiClient();
        const tokenMgr = getTokenManager();
        if (!isVendor || !apiClient || !tokenMgr || !tokenMgr.isAuthenticated()) {
            return { isPremiumActive: false };
        }

        try {
            if (typeof apiClient.request === 'function') {
                return await apiClient.request('/Payments/me/premium-status');
            }
        } catch {
            // ignore and fallback to inactive state
        }

        return { isPremiumActive: false };
    }

    function showVendorPremiumRequiredMessage() {
        const message = 'Bạn cần đăng ký gói Premium để dùng chức năng này. Vui lòng vào mục "Thanh toán gói dịch vụ".';
        if (window.UiFeedback?.toast) {
            window.UiFeedback.toast(message, 'warning', 4200);
            return;
        }

        let wrap = document.getElementById('sidebarToastWrap');
        if (!wrap) {
            wrap = document.createElement('div');
            wrap.id = 'sidebarToastWrap';
            wrap.style.position = 'fixed';
            wrap.style.top = '1rem';
            wrap.style.right = '1rem';
            wrap.style.zIndex = '12000';
            wrap.style.display = 'flex';
            wrap.style.flexDirection = 'column';
            wrap.style.gap = '.5rem';
            document.body.appendChild(wrap);
        }

        const item = document.createElement('div');
        item.style.background = '#a16207';
        item.style.color = '#fff';
        item.style.padding = '.75rem .95rem';
        item.style.borderRadius = '10px';
        item.style.fontWeight = '600';
        item.style.maxWidth = '520px';
        item.style.boxShadow = '0 10px 26px rgba(0,0,0,.18)';
        item.textContent = message;
        wrap.appendChild(item);
        setTimeout(() => item.remove(), 4200);
    }

    async function applyVendorPremiumPoiGate() {
        if (!isVendor) return;

        const createPoiLink = document.getElementById('vendorCreatePoiLink');
        const audioListLink = document.getElementById('vendorAudioListLink');
        const audioBulkLink = document.getElementById('vendorAudioBulkLink');
        const currentPage = window.location.pathname.split('/').pop()?.replace('.html', '') || 'index';

        const premiumStatus = await getVendorPremiumStatus();
        const isPremiumActive = !!premiumStatus?.isPremiumActive;

        if (!isPremiumActive && createPoiLink) {
            createPoiLink.addEventListener('click', (event) => {
                event.preventDefault();
                showVendorPremiumRequiredMessage();
            });
            createPoiLink.classList.add('gated-link');
            createPoiLink.setAttribute('aria-disabled', 'true');
            createPoiLink.setAttribute('title', 'Chức năng yêu cầu gói Premium còn hạn. Vào Thanh toán gói dịch vụ để đăng ký.');
        }

        if (!isPremiumActive) {
            [audioListLink, audioBulkLink].forEach(link => {
                if (!link) return;
                link.addEventListener('click', (event) => {
                    event.preventDefault();
                    showVendorPremiumRequiredMessage();
                });
                link.classList.add('gated-link');
                link.setAttribute('aria-disabled', 'true');
                link.setAttribute('title', 'Chức năng âm thanh yêu cầu gói Premium còn hạn. Vào Thanh toán gói dịch vụ để đăng ký.');
            });
        }

        if (!isPremiumActive && (currentPage === 'poi-create' || currentPage === 'audio-list' || currentPage === 'audio-bulk-generate')) {
            showVendorPremiumRequiredMessage();
            window.setTimeout(() => {
                window.location.href = 'payment-management';
            }, 100);
        }
    }

    window.applyAppSettings = applyAppSettings;

    function ensureConfirmModal() {
        if (document.getElementById('appConfirmModal')) return;
        const style = document.createElement('style');
        style.textContent = `
        .app-confirm-overlay{position:fixed;inset:0;background:rgba(26,26,46,.5);backdrop-filter:blur(4px);display:none;align-items:center;justify-content:center;z-index:9999;padding:1rem}
        .app-confirm-overlay.active{display:flex}
        .app-confirm{background:#fff;border-radius:16px;box-shadow:0 20px 60px rgba(0,0,0,.2);max-width:420px;width:100%;overflow:hidden}
        .app-confirm-header{padding:1rem 1.25rem;border-bottom:1px solid #eee;font-weight:700}
        .app-confirm-body{padding:1.1rem 1.25rem;color:#333;line-height:1.5}
        .app-confirm-actions{display:flex;justify-content:flex-end;gap:.5rem;padding:1rem 1.25rem;border-top:1px solid #eee;background:#fafafa}
        .app-confirm-btn{padding:.55rem .9rem;border-radius:10px;border:1px solid #ddd;background:#fff;cursor:pointer;font-weight:600}
        .app-confirm-btn.primary{background:linear-gradient(135deg,#FF6B35 0%,#004E89 100%);border:none;color:#fff}
        `;
        document.head.appendChild(style);
        const overlay = document.createElement('div');
        overlay.id = 'appConfirmModal';
        overlay.className = 'app-confirm-overlay';
        overlay.innerHTML = `
            <div class="app-confirm" role="dialog" aria-modal="true" aria-labelledby="appConfirmTitle">
                <div class="app-confirm-header" id="appConfirmTitle">Xác nhận</div>
                <div class="app-confirm-body" id="appConfirmMessage"></div>
                <div class="app-confirm-actions">
                    <button class="app-confirm-btn" id="appConfirmCancel">Hủy</button>
                    <button class="app-confirm-btn primary" id="appConfirmOk">Đồng ý</button>
                </div>
            </div>
        `;
        document.body.appendChild(overlay);
    }

    function showConfirm(message, onConfirm) {
        if (window.UiFeedback?.confirm) {
            window.UiFeedback.confirm(message || '', {
                title: 'Xác nhận',
                confirmText: 'Đồng ý',
                cancelText: 'Hủy'
            }).then(ok => {
                if (ok) onConfirm && onConfirm();
            });
            return;
        }
        ensureConfirmModal();
        const overlay = document.getElementById('appConfirmModal');
        const msg = document.getElementById('appConfirmMessage');
        const okBtn = document.getElementById('appConfirmOk');
        const cancelBtn = document.getElementById('appConfirmCancel');
        msg.textContent = message || '';
        const close = () => overlay.classList.remove('active');
        const handleOk = () => { close(); onConfirm && onConfirm(); };
        const handleCancel = () => { close(); };
        okBtn.onclick = handleOk;
        cancelBtn.onclick = handleCancel;
        overlay.onclick = (e) => { if (e.target === overlay) handleCancel(); };
        overlay.classList.add('active');
    }

    window.logout = function() {
        showConfirm('Bạn có chắc muốn đăng xuất?', () => {
            if (window.TokenManager && typeof window.TokenManager.logout === 'function') {
                window.TokenManager.logout();
                return;
            }
            localStorage.removeItem('jwt_token_admin');
            localStorage.removeItem('user_admin');
            localStorage.removeItem('jwt_token_vendor');
            localStorage.removeItem('user_vendor');
            localStorage.removeItem('jwt_token');
            localStorage.removeItem('user');
            localStorage.removeItem('authToken');
            sessionStorage.removeItem('authToken');
            localStorage.removeItem('userSession');
            sessionStorage.removeItem('userSession');
            sessionStorage.removeItem('activeRole');
            window.location.href = '/';
        });
    };
})();

