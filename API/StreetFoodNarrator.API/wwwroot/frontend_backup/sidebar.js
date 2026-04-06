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

            <nav>
                <ul class="nav-menu">
                    ${isAdmin ? `
                    <li class="nav-item">
                        <a href="admin-dashboard" class="nav-link">
                            <i class="nav-icon fas fa-home"></i>
                            <span>Trang chủ</span>
                        </a>
                    </li>
                    ` : `
                    <li class="nav-item">
                        <a href="vendor-dashboard" class="nav-link">
                            <i class="nav-icon fas fa-home"></i>
                            <span>Trang chủ</span>
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
                        <a href="vendors-list.html" class="nav-link">
                            <i class="nav-icon fas fa-store"></i>
                            <span>Nhà cung cấp</span>
                        </a>
                    </li>
                    <li class="nav-item">
                        <a href="analytics.html" class="nav-link">
                            <i class="nav-icon fas fa-chart-line"></i>
                            <span>Phân tích</span>
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
                    <li class="nav-item">
                        <a href="settings.html" class="nav-link">
                            <i class="nav-icon fas fa-cog"></i>
                            <span>Cài đặt</span>
                        </a>
                    </li>
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
                padding: 0 2rem 2rem;
                border-bottom: 1px solid rgba(255, 255, 255, 0.1);
                margin-bottom: 2rem;
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

            body {
                 margin-left: 200px !important;
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
    }

    window.applyAppSettings = applyAppSettings;

    window.logout = function() {
        if (!confirm('Báº¡n cÃ³ cháº¯c muá»‘n Ä‘Äƒng xuáº¥t?')) {
            return;
        }
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
    };
})();

