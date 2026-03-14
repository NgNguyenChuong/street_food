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
    const poiListHref = isAdmin ? 'poi-list.html' : 'poi-list.html?scope=mine';
    const poiListLabel = isAdmin ? 'Danh sách POI' : 'POI của tôi';

    const displayName = user?.fullName || user?.email?.split('@')[0] || 'Người dùng';
    const displayEmail = user?.email || '';
    const roleLabel = isAdmin ? 'Quản trị viên' : (isVendor ? 'Vendor' : 'Người dùng');
    const roleBadgeClass = isAdmin ? 'role-badge-admin' : 'role-badge-vendor';
    const avatarLetters = displayName.split(' ').map(w => w[0]).slice(-2).join('').toUpperCase();

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

            <nav>
                <ul class="nav-menu">
                    ${isAdmin ? `
                    <li class="nav-item">
                        <a href="dashboard.html" class="nav-link">
                            <i class="nav-icon fas fa-home"></i>
                            <span>Bảng điều khiển</span>
                        </a>
                    </li>
                    ` : `
                    <li class="nav-item">
                        <a href="dashboard.html" class="nav-link">
                            <i class="nav-icon fas fa-home"></i>
                            <span>Bảng điều khiển</span>
                        </a>
                    </li>
                    `}

                    <li class="nav-item has-submenu">
                        <button type="button" class="nav-link nav-toggle">
                            <i class="nav-icon fas fa-map-marker-alt"></i>
                            <span>${isAdmin ? 'Quản lý POI' : 'POI của tôi'}</span>
                            <i class="nav-caret fas fa-chevron-down"></i>
                        </button>
                        <ul class="submenu">
                            <li><a href="${poiListHref}" class="nav-sublink">${poiListLabel}</a></li>
                            <li><a href="poi-create.html" class="nav-sublink">Tạo POI</a></li>
                        </ul>
                    </li>

                    ${isAdmin ? `
                    <li class="nav-item">
                        <a href="translation.html" class="nav-link">
                            <i class="nav-icon fas fa-language"></i>
                            <span>Bản dịch</span>
                        </a>
                    </li>
                    <li class="nav-item">
                        <a href="tour.html" class="nav-link">
                            <i class="nav-icon fas fa-route"></i>
                            <span>Quản lý Tour</span>
                        </a>
                    </li>
                    <li class="nav-item">
                        <a href="vendors-list.html" class="nav-link">
                            <i class="nav-icon fas fa-store"></i>
                            <span>Quản lý Vendors</span>
                        </a>
                    </li>
                    ` : ''}

                    <li class="nav-item has-submenu">
                        <button type="button" class="nav-link nav-toggle">
                            <i class="nav-icon fas fa-microphone"></i>
                            <span>Âm thanh</span>
                            <i class="nav-caret fas fa-chevron-down"></i>
                        </button>
                        <ul class="submenu">
                            <li><a href="audio-list.html" class="nav-sublink">Danh sách âm thanh</a></li>
                            <li><a href="audio-bulk-generate.html" class="nav-sublink">TTS hàng loạt</a></li>
                            <li><a href="audio-list.html#upload" class="nav-sublink">Upload audio</a></li>
                        </ul>
                    </li>
                    ${isAdmin ? `
                    <li class="nav-item">
                        <a href="history.html" class="nav-link">
                            <i class="nav-icon fas fa-users"></i>
                            <span>Người dùng</span>
                        </a>
                    </li>
                    ` : ''}
                    <li class="nav-item">
                        <a href="/index.html" class="nav-link" onclick="logout(); return false;">
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

    function init() {
        const currentPage = window.location.pathname.split('/').pop() || 'index.html';
        if (currentPage === 'index.html' || currentPage === 'login.html' || currentPage === 'register.html') {
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

        if (currentPage === 'poi-edit.html') {
            const poiGroup = document.querySelector('.nav-item.has-submenu');
            if (poiGroup) poiGroup.classList.add('open');
        }

        const toggles = document.querySelectorAll('.nav-toggle');
        toggles.forEach(toggle => {
            toggle.addEventListener('click', () => {
                const parent = toggle.closest('.nav-item.has-submenu');
                if (parent) parent.classList.toggle('open');
            });
        });

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

        const allowList = new Set(['/index.html', 'dashboard.html']);
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
            window.location.href = '/index.html';
        });
    };
})();

