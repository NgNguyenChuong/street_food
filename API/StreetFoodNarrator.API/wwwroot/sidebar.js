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
    const normalizeRoleName = (role) => {
        const r = String(role || '').trim().toLowerCase();
        if (r === 'admin') return 'Admin';
        if (r === 'vendor') return 'Vendor';
        return null;
    };

    const userRoles = (Array.isArray(user?.roles) ? user.roles : [])
        .map(normalizeRoleName)
        .filter(Boolean);
    const tokenRoles = (() => {
        try {
            const token = window.TokenManager?.getToken?.();
            if (!token) return [];
            const payloadRaw = token.split('.')[1];
            if (!payloadRaw) return [];
            const payload = JSON.parse(atob(payloadRaw.replace(/-/g, '+').replace(/_/g, '/')));
            const roleClaim = payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || payload.role || payload.roles || payload.Role;
            if (!roleClaim) return [];
            return (Array.isArray(roleClaim) ? roleClaim : [roleClaim])
                .flatMap(r => String(r).split(','))
                .map(normalizeRoleName)
                .filter(Boolean);
        } catch {
            return [];
        }
    })();

    const roles = [...new Set([...userRoles, ...tokenRoles])];
    const roleContext = (sessionStorage.getItem('activeRole') || localStorage.getItem('activeRole') || '').toLowerCase();
    const hasAdmin = roles.includes('Admin');
    const hasVendor = roles.includes('Vendor');

    // Respect explicit context only when that role actually exists in token/profile.
    const isAdmin = roleContext === 'admin' ? hasAdmin : (roleContext === 'vendor' ? false : hasAdmin);
    const isVendor = roleContext === 'vendor' ? hasVendor : (roleContext === 'admin' ? false : hasVendor);
    const poiListHref = isAdmin ? 'poi-list' : 'poi-list?scope=mine';
    const poiListLabel = isAdmin ? 'Danh sách POI' : 'POI của tôi';

    const displayName = user?.fullName || user?.email?.split('@')[0] || 'Người dùng';
    const displayEmail = user?.email || '';
    const roleLabel = isAdmin ? 'Quản trị viên' : (isVendor ? 'Vendor' : 'Người dùng');
    const roleBadgeClass = isAdmin ? 'role-badge-admin' : 'role-badge-vendor';
    const avatarLetters = displayName.split(' ').map(w => w[0]).slice(-2).join('').toUpperCase();
    const notifSeenStorageKey = isAdmin ? 'sidebar_notif_seen_admin' : 'sidebar_notif_seen_vendor';
    const signalRClientUrl = 'https://cdnjs.cloudflare.com/ajax/libs/microsoft-signalr/8.0.7/signalr.min.js';
    let liveNotificationItems = [];
    let notifHubConnection = null;

        const sidebarHTML = `
        <aside class="sidebar" id="sidebar">
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
                    <li class="nav-item">
                        <a href="dashboard" class="nav-link">
                            <i class="nav-icon fas fa-home"></i>
                            <span>Trang chủ</span>
                        </a>
                    </li>

                    ${isAdmin ? `
                    <li class="nav-item" data-nav-group="poi">
                        <a href="${poiListHref}" class="nav-link">
                            <i class="nav-icon fas fa-map-marker-alt"></i>
                            <span>Quản lý POI</span>
                        </a>
                    </li>
                    ` : `
                    <li class="nav-item has-submenu" data-nav-group="poi">
                        <button type="button" class="nav-link nav-toggle">
                            <i class="nav-icon fas fa-map-marker-alt"></i>
                            <span>POI của tôi</span>
                            <i class="nav-caret fas fa-chevron-down"></i>
                        </button>
                        <ul class="submenu">
                            <li><a href="${poiListHref}" class="nav-sublink">${poiListLabel}</a></li>
                            <li><a href="poi-create" class="nav-sublink">Tạo POI</a></li>
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
                    ` : ''}

                    ${isAdmin ? `
                    <li class="nav-item">
                        <a href="translation" class="nav-link">
                            <i class="nav-icon fas fa-language"></i>
                            <span>Bản dịch</span>
                        </a>
                    </li>
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
                    ` : ''}

                    ${isAdmin ? `
                    <li class="nav-item" data-nav-group="audio">
                        <a href="audio-list" class="nav-link">
                            <i class="nav-icon fas fa-microphone"></i>
                            <span>Âm thanh</span>
                        </a>
                    </li>
                    ` : `
                    <li class="nav-item has-submenu" data-nav-group="audio">
                        <button type="button" class="nav-link nav-toggle">
                            <i class="nav-icon fas fa-microphone"></i>
                            <span>Âm thanh</span>
                            <i class="nav-caret fas fa-chevron-down"></i>
                        </button>
                        <ul class="submenu">
                            <li><a href="audio-list" class="nav-sublink">Danh sách âm thanh</a></li>
                            <li><a href="audio-bulk-generate" class="nav-sublink">TTS hàng loạt</a></li>
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

            .notif-item.success .notif-item-icon {
                background: #ECFDF3;
                color: #047857;
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

            .notif-item-message {
                display: block;
                margin-top: 0.15rem;
                font-size: 0.76rem;
                color: #6B7280;
                line-height: 1.35;
            }

            .notif-item-time {
                display: block;
                margin-top: 0.2rem;
                font-size: 0.7rem;
                color: #9CA3AF;
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
            }

            .nav-sublink:hover,
            .nav-sublink.active {
                background: rgba(34, 197, 94, 0.12);
                color: #fff;
            }

            .nav-link.disabled,
            .nav-sublink.disabled {
                opacity: 0.4;
                pointer-events: none;
                cursor: not-allowed;
                transform: none !important;
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
            if (!window.api || !window.TokenManager || !TokenManager.isAuthenticated()) return;
            const settings = await api.getMySettings();
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
        kind = 'info',
        createdAt = null
    }) {
        return { notificationId, icon, title, message, href, kind, createdAt };
    }

    function toMillis(input) {
        if (!input) return 0;
        const d = new Date(input);
        const t = d.getTime();
        return Number.isFinite(t) ? t : 0;
    }

    function getSeenMillis() {
        const raw = localStorage.getItem(notifSeenStorageKey);
        if (!raw) return 0;
        const parsed = Number(raw);
        return Number.isFinite(parsed) ? parsed : 0;
    }

    function setSeenMillis(value) {
        if (!Number.isFinite(value) || value <= 0) return;
        localStorage.setItem(notifSeenStorageKey, String(Math.floor(value)));
    }

    function markSeenByItems(items) {
        const maxCreatedAt = items.reduce((max, item) => Math.max(max, toMillis(item.createdAt)), 0);
        if (maxCreatedAt > 0) setSeenMillis(maxCreatedAt);
    }

    function formatNotificationTime(createdAt) {
        const millis = toMillis(createdAt);
        if (!millis) return '';
        const diffSec = Math.max(0, Math.floor((Date.now() - millis) / 1000));
        if (diffSec < 60) return 'Vừa xong';
        const diffMin = Math.floor(diffSec / 60);
        if (diffMin < 60) return `${diffMin} phút trước`;
        const diffHour = Math.floor(diffMin / 60);
        if (diffHour < 24) return `${diffHour} giờ trước`;
        const diffDay = Math.floor(diffHour / 24);
        if (diffDay <= 7) return `${diffDay} ngày trước`;
        return new Date(millis).toLocaleString('vi-VN');
    }

    function mapServerNotificationToItem(raw) {
        if (!raw) return null;
        return buildNotificationItem({
            notificationId: raw.notificationId ?? raw.NotificationId ?? null,
            icon: raw.icon ?? raw.Icon ?? 'fa-circle-info',
            title: raw.title ?? raw.Title ?? 'Thông báo',
            message: raw.message ?? raw.Message ?? '',
            href: raw.href ?? raw.Href ?? '#',
            kind: String(raw.kind ?? raw.Kind ?? 'info').toLowerCase(),
            createdAt: raw.createdAt ?? raw.CreatedAt ?? null
        });
    }

    function mergeRealtimeNotification(item) {
        if (!item) return;
        const next = [item, ...liveNotificationItems.filter(x => String(x.notificationId ?? '') !== String(item.notificationId ?? ''))];
        liveNotificationItems = next.slice(0, 80);
    }

    function getUnreadCount(items) {
        const seen = getSeenMillis();
        if (!seen) return items.length;
        return items.filter(item => toMillis(item.createdAt) > seen).length;
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

        const unread = getUnreadCount(items);
        if (unread > 0) {
            badge.textContent = unread > 99 ? '99+' : String(unread);
            badge.classList.remove('hidden');
        } else {
            badge.classList.add('hidden');
        }

        list.innerHTML = items.map(item => `
            <a href="${item.href || '#'}" class="notif-item ${item.kind === 'warn' ? 'warn' : ''} ${item.kind === 'success' ? 'success' : ''}">
                <span class="notif-item-icon"><i class="fas ${item.icon || 'fa-circle-info'}"></i></span>
                <span class="notif-item-content">
                    <span class="notif-item-title">${item.title || ''}</span>
                    <span class="notif-item-message">${item.message || ''}</span>
                    <span class="notif-item-time">${formatNotificationTime(item.createdAt)}</span>
                </span>
            </a>
        `).join('');
    }

    async function loadFallbackNotificationsFromPois() {
        if (!window.api || typeof api.getPOIs !== 'function') return [];

        try {
            const reviewFilter = isAdmin ? 'pending' : null;
            const res = await api.getPOIs(1, 40, '', null, null, reviewFilter);
            const rows = Array.isArray(res?.data) ? res.data : [];
            if (!rows.length) return [];

            const items = rows
                .map((poi) => {
                    const review = String(poi.reviewStatus || '').toLowerCase();
                    const hasPending = !!(poi.pendingChanges && Object.keys(poi.pendingChanges).length);
                    const id = poi.poI_ID ?? poi.poi_ID ?? poi.id;
                    const name = poi.name_Vi || poi.name || `POI #${id ?? ''}`;
                    const createdAt = poi.updatedAt || poi.createdAt || null;

                    if (isAdmin) {
                        if (!(hasPending || review === 'pending')) return null;
                        return buildNotificationItem({
                            notificationId: `fallback-admin-poi-${id}-${createdAt || ''}`,
                            icon: 'fa-pen-to-square',
                            title: 'POI chờ duyệt',
                            message: `POI "${name}" đang chờ Admin duyệt chỉnh sửa.`,
                            href: id ? `poi-edit?id=${id}` : 'poi-list?reviewStatus=pending',
                            kind: 'info',
                            createdAt
                        });
                    }

                    if (hasPending || review === 'pending') {
                        return buildNotificationItem({
                            notificationId: `fallback-vendor-poi-pending-${id}-${createdAt || ''}`,
                            icon: 'fa-hourglass-half',
                            title: 'POI đang chờ duyệt',
                            message: `POI "${name}" đang chờ Admin duyệt.`,
                            href: id ? `poi-edit?id=${id}` : 'poi-list?reviewStatus=pending',
                            kind: 'info',
                            createdAt
                        });
                    }
                    if (review === 'rejected') {
                        return buildNotificationItem({
                            notificationId: `fallback-vendor-poi-rejected-${id}-${createdAt || ''}`,
                            icon: 'fa-circle-xmark',
                            title: 'POI bị từ chối',
                            message: `Yêu cầu chỉnh sửa của POI "${name}" đã bị từ chối.`,
                            href: id ? `poi-edit?id=${id}` : 'poi-list?reviewStatus=rejected',
                            kind: 'warn',
                            createdAt
                        });
                    }
                    return null;
                })
                .filter(Boolean)
                .sort((a, b) => toMillis(b.createdAt) - toMillis(a.createdAt));

            return items.slice(0, 40);
        } catch {
            return [];
        }
    }
    async function loadSidebarNotifications({ markSeen = false } = {}) {
        const list = document.getElementById('sidebarNotifList');
        if (list) list.innerHTML = '<div class="notif-empty">Đang tải thông báo...</div>';

        try {
            if (!window.api || typeof api.getNotifications !== 'function') {
                throw new Error('Notifications API is unavailable');
            }

            const role = isAdmin ? 'admin' : 'vendor';
            let res = await api.getNotifications(40, role);
            let rows = Array.isArray(res?.data) ? res.data : [];

            // Fallback for stale/misaligned role context in browser storage.
            if (rows.length === 0) {
                res = await api.getNotifications(40, null);
                rows = Array.isArray(res?.data) ? res.data : [];
            }
            if (rows.length > 0) {
                liveNotificationItems = rows
                    .map(mapServerNotificationToItem)
                    .filter(Boolean)
                    .sort((a, b) => toMillis(b.createdAt) - toMillis(a.createdAt));
            } else {
                liveNotificationItems = await loadFallbackNotificationsFromPois();
            }

            if (markSeen) {
                markSeenByItems(liveNotificationItems);
            }

            renderSidebarNotifications(liveNotificationItems);
        } catch {
            liveNotificationItems = await loadFallbackNotificationsFromPois();
            if (markSeen) {
                markSeenByItems(liveNotificationItems);
            }
            renderSidebarNotifications(liveNotificationItems);
        }
    }

    async function ensureSignalRLoaded() {
        if (window.signalR) return;
        const existing = document.getElementById('signalr-client-script');
        if (existing) {
            await new Promise((resolve, reject) => {
                existing.addEventListener('load', resolve, { once: true });
                existing.addEventListener('error', reject, { once: true });
            });
            return;
        }

        await new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.id = 'signalr-client-script';
            script.src = signalRClientUrl;
            script.async = true;
            script.onload = resolve;
            script.onerror = reject;
            document.head.appendChild(script);
        });
    }

    async function startRealtimeNotifications(panel) {
        if (!window.TokenManager || typeof TokenManager.getToken !== 'function') return;
        const token = TokenManager.getToken();
        if (!token) return;

        try {
            await ensureSignalRLoaded();
            if (!window.signalR) return;

            if (notifHubConnection) {
                try {
                    await notifHubConnection.stop();
                } catch {
                    // ignore reconnect cleanup
                }
            }

            let hubUrl = '/hubs/notifications';
            if (window.api && typeof api.baseURL === 'string' && api.baseURL.length > 0) {
                hubUrl = api.baseURL.replace(/\/api\/?$/i, '') + '/hubs/notifications';
            }

            notifHubConnection = new signalR.HubConnectionBuilder()
                .withUrl(hubUrl, {
                    accessTokenFactory: () => TokenManager.getToken() || ''
                })
                .withAutomaticReconnect()
                .build();

            notifHubConnection.on('notification', (payload) => {
                const item = mapServerNotificationToItem(payload);
                if (!item) return;

                mergeRealtimeNotification(item);
                if (panel?.classList.contains('show')) {
                    markSeenByItems([item]);
                }
                renderSidebarNotifications(liveNotificationItems);
            });

            await notifHubConnection.start();
        } catch {
            // keep polling fallback if realtime hub fails
        }
    }

    function setupNotificationBell() {
        const btn = document.getElementById('sidebarNotifBtn');
        const panel = document.getElementById('sidebarNotifPanel');
        const refresh = document.getElementById('sidebarNotifRefresh');
        if (!btn || !panel) return;

        btn.addEventListener('click', async (event) => {
            event.stopPropagation();
            panel.classList.toggle('show');
            if (panel.classList.contains('show')) {
                await loadSidebarNotifications({ markSeen: true });
            }
        });

        panel.addEventListener('click', (event) => event.stopPropagation());
        document.addEventListener('click', () => panel.classList.remove('show'));

        if (refresh) {
            refresh.addEventListener('click', async (event) => {
                event.preventDefault();
                event.stopPropagation();
                await loadSidebarNotifications({ markSeen: panel.classList.contains('show') });
            });
        }

        loadSidebarNotifications({ markSeen: false });
        // Reliability-first: polling only, no hard dependency on realtime transport.
        setInterval(() => loadSidebarNotifications({ markSeen: panel.classList.contains('show') }), 15000);
    }

    function init() {
        const currentPage = window.location.pathname.split('/').pop()?.replace('.html', '') || 'index';
        if (currentPage === 'index' || currentPage === 'login' || currentPage === 'register') {
            return;
        }

        document.head.insertAdjacentHTML('beforeend', sidebarCSS);
        document.body.insertAdjacentHTML('afterbegin', sidebarHTML);

        const normalizeHref = (href) => (href || '').split('?')[0].split('#')[0];

        const links = document.querySelectorAll('.nav-link');
        links.forEach(link => {
            const href = link.getAttribute('href') || '';
            if (normalizeHref(href) === currentPage && !/[?#]/.test(href)) {
                link.classList.add('active');
            }
        });

        const sublinks = document.querySelectorAll('.nav-sublink');
        sublinks.forEach(link => {
            const href = link.getAttribute('href') || '';
            if (normalizeHref(href) === currentPage && !/[?#]/.test(href)) {
                link.classList.add('active');
            }
            if (normalizeHref(href) === currentPage) {
                const parent = link.closest('.nav-item.has-submenu');
                if (parent) parent.classList.add('open');
            }
        });

        if (currentPage === 'poi-edit') {
            const poiLink = document.querySelector('.nav-item[data-nav-group="poi"] > .nav-link');
            if (poiLink) poiLink.classList.add('active');
            const poiGroup = document.querySelector('.nav-item.has-submenu[data-nav-group="poi"]');
            if (poiGroup) poiGroup.classList.add('open');
        }

        const toggles = document.querySelectorAll('.nav-toggle');
        toggles.forEach(toggle => {
            toggle.addEventListener('click', () => {
                const parent = toggle.closest('.nav-item.has-submenu');
                if (parent) parent.classList.toggle('open');
            });
        });

        setupNotificationBell();

        hydrateAppSettings();
        applyVendorApprovalGate();
    }

    async function applyVendorApprovalGate() {
        if (!isVendor) return;
        if (!window.api || !window.TokenManager || !TokenManager.isAuthenticated()) return;

        let status = 'pending';
        try {
            const profile = await api.getVendorMe();
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


