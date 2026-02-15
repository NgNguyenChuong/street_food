// Navigation Component for Street Food Narrator
// Handles authentication, session management, and navigation

class Navigation {
    constructor() {
        this.user = null;
        this.session = null;
        this.init();
    }

    init() {
        // Check authentication
        this.checkAuth();
        
        // Load user info
        if (this.session) {
            this.user = JSON.parse(this.session);
            this.renderNav();
            this.setupLogout();
        }
    }

    checkAuth() {
        this.session = localStorage.getItem('userSession') || sessionStorage.getItem('userSession');
        
        // Allow access to login and register pages without auth
        const publicPages = ['login.html', 'register.html'];
        const currentPage = window.location.pathname.split('/').pop();
        
        if (!this.session && !publicPages.includes(currentPage)) {
            // Redirect silently without alert
            window.location.href = 'login.html';
            return false;
        }
        
        return true;
    }

    renderNav() {
        const navHtml = `
            <nav class="app-nav">
                <div class="nav-header">
                    <div class="logo">
                        <i class="fas fa-utensils"></i>
                        <span>Street Food</span>
                    </div>
                    <button class="nav-toggle" onclick="nav.toggleNav()">
                        <i class="fas fa-bars"></i>
                    </button>
                </div>
                
                <div class="nav-menu">
                    <a href="admin-dashboard.html" class="nav-item ${this.isActive('admin-dashboard')}">
                        <i class="fas fa-home"></i>
                        <span>Dashboard</span>
                    </a>
                    
                    <div class="nav-section">
                        <div class="nav-section-title">Quản lý</div>
                        
                        <a href="poi-list.html" class="nav-item ${this.isActive('poi-list')}">
                            <i class="fas fa-map-marker-alt"></i>
                            <span>POI</span>
                        </a>
                        
                        <a href="audio-list.html" class="nav-item ${this.isActive('audio-list')}">
                            <i class="fas fa-music"></i>
                            <span>Audio</span>
                        </a>
                        
                        ${this.user.role === 'Admin' ? `
                        <a href="vendors-list.html" class="nav-item ${this.isActive('vendors-list')}">
                            <i class="fas fa-store"></i>
                            <span>Vendors</span>
                        </a>
                        ` : ''}
                    </div>
                    
                    <div class="nav-section">
                        <div class="nav-section-title">Công cụ</div>
                        
                        <a href="poi-create.html" class="nav-item ${this.isActive('poi-create')}">
                            <i class="fas fa-plus-circle"></i>
                            <span>Tạo POI</span>
                        </a>
                        
                        <a href="audio-bulk-generate.html" class="nav-item ${this.isActive('audio-bulk-generate')}">
                            <i class="fas fa-microphone"></i>
                            <span>Tạo Audio</span>
                        </a>
                        
                        ${this.user.role === 'Admin' ? `
                        <a href="analytics.html" class="nav-item ${this.isActive('analytics')}">
                            <i class="fas fa-chart-line"></i>
                            <span>Thống kê</span>
                        </a>
                        ` : ''}
                    </div>
                    
                    <div class="nav-section">
                        <div class="nav-section-title">Cài đặt</div>
                        
                        <a href="settings.html" class="nav-item ${this.isActive('settings')}">
                            <i class="fas fa-cog"></i>
                            <span>Cài đặt</span>
                        </a>
                    </div>
                </div>
                
                <div class="nav-footer">
                    <div class="user-profile">
                        <div class="user-avatar">
                            ${this.getUserInitials()}
                        </div>
                        <div class="user-info">
                            <div class="user-name">${this.user.name || this.user.email}</div>
                            <div class="user-role">${this.user.role}</div>
                        </div>
                    </div>
                    <button class="btn-logout" id="logoutBtn">
                        <i class="fas fa-sign-out-alt"></i>
                        <span>Đăng xuất</span>
                    </button>
                </div>
            </nav>
        `;

        // Insert nav at the beginning of body
        document.body.insertAdjacentHTML('afterbegin', navHtml);
        
        // Add styles
        this.addStyles();
    }

    isActive(page) {
        const currentPage = window.location.pathname.split('/').pop();
        return currentPage === `${page}.html` ? 'active' : '';
    }

    getUserInitials() {
        if (this.user.name) {
            return this.user.name.split(' ').map(n => n[0]).join('').toUpperCase().substring(0, 2);
        }
        return this.user.email.substring(0, 2).toUpperCase();
    }

    toggleNav() {
        const nav = document.querySelector('.app-nav');
        nav.classList.toggle('collapsed');
    }

    setupLogout() {
        // Wait a bit for DOM to be ready
        setTimeout(() => {
            const logoutBtn = document.getElementById('logoutBtn');
            if (logoutBtn) {
                logoutBtn.addEventListener('click', () => this.logout());
            }
        }, 100);
    }

    logout() {
        if (confirm('Bạn có chắc muốn đăng xuất?')) {
            localStorage.removeItem('userSession');
            sessionStorage.removeItem('userSession');
            localStorage.removeItem('authToken');
            sessionStorage.removeItem('authToken');
            window.location.href = 'login.html';
        }
    }

    addStyles() {
        if (document.getElementById('nav-styles')) return;

        const style = document.createElement('style');
        style.id = 'nav-styles';
        style.textContent = `
            .app-nav {
                position: fixed;
                left: 0;
                top: 0;
                width: 280px;
                height: 100vh;
                background: linear-gradient(180deg, #1A1A2E 0%, #2D2D44 100%);
                padding: 1.5rem 0;
                z-index: 1000;
                box-shadow: 4px 0 20px rgba(26, 26, 46, 0.1);
                transition: transform 0.3s ease;
                display: flex;
                flex-direction: column;
                overflow-y: auto;
            }

            .app-nav.collapsed {
                transform: translateX(-100%);
            }

            .nav-header {
                padding: 0 1.5rem 1.5rem;
                border-bottom: 1px solid rgba(255, 255, 255, 0.1);
                display: flex;
                justify-content: space-between;
                align-items: center;
            }

            .logo {
                display: flex;
                align-items: center;
                gap: 0.75rem;
                font-family: 'Playfair Display', serif;
                font-size: 1.5rem;
                font-weight: 700;
                color: #FF6B35;
            }

            .logo i {
                font-size: 2rem;
            }

            .nav-toggle {
                display: none;
                background: none;
                border: none;
                color: white;
                font-size: 1.5rem;
                cursor: pointer;
                padding: 0.5rem;
            }

            .nav-menu {
                flex: 1;
                padding: 1.5rem 0;
                overflow-y: auto;
            }

            .nav-section {
                margin-bottom: 2rem;
            }

            .nav-section-title {
                padding: 0.5rem 1.5rem;
                font-size: 0.75rem;
                font-weight: 600;
                color: rgba(255, 255, 255, 0.5);
                text-transform: uppercase;
                letter-spacing: 1px;
                margin-bottom: 0.5rem;
            }

            .nav-item {
                display: flex;
                align-items: center;
                gap: 1rem;
                padding: 0.875rem 1.5rem;
                color: rgba(255, 255, 255, 0.8);
                text-decoration: none;
                transition: all 0.3s ease;
                position: relative;
            }

            .nav-item i {
                width: 20px;
                font-size: 1.1rem;
            }

            .nav-item:hover {
                background: rgba(255, 107, 53, 0.1);
                color: #FF6B35;
            }

            .nav-item.active {
                background: linear-gradient(to right, rgba(255, 107, 53, 0.2), transparent);
                color: #FF6B35;
                border-left: 3px solid #FF6B35;
                font-weight: 600;
            }

            .nav-footer {
                margin-top: auto;
                padding: 1.5rem;
                border-top: 1px solid rgba(255, 255, 255, 0.1);
            }

            .user-profile {
                display: flex;
                align-items: center;
                gap: 1rem;
                margin-bottom: 1rem;
                padding: 1rem;
                background: rgba(255, 255, 255, 0.05);
                border-radius: 12px;
            }

            .user-avatar {
                width: 48px;
                height: 48px;
                background: linear-gradient(135deg, #FF6B35 0%, #E85A2A 100%);
                border-radius: 50%;
                display: flex;
                align-items: center;
                justify-content: center;
                font-weight: 700;
                color: white;
                font-size: 1.1rem;
            }

            .user-info {
                flex: 1;
                min-width: 0;
            }

            .user-name {
                font-weight: 600;
                color: white;
                white-space: nowrap;
                overflow: hidden;
                text-overflow: ellipsis;
            }

            .user-role {
                font-size: 0.85rem;
                color: rgba(255, 255, 255, 0.6);
            }

            .btn-logout {
                width: 100%;
                padding: 0.875rem;
                background: rgba(239, 71, 111, 0.1);
                border: 1px solid rgba(239, 71, 111, 0.3);
                border-radius: 10px;
                color: #EF476F;
                font-weight: 600;
                cursor: pointer;
                transition: all 0.3s ease;
                display: flex;
                align-items: center;
                justify-content: center;
                gap: 0.75rem;
            }

            .btn-logout:hover {
                background: rgba(239, 71, 111, 0.2);
                border-color: #EF476F;
            }

            /* Adjust main content when nav is present */
            body.has-nav {
                margin-left: 280px;
            }

            body.has-nav .container {
                max-width: calc(100% - 2rem);
            }

            @media (max-width: 768px) {
                .app-nav {
                    transform: translateX(-100%);
                }

                .app-nav.open {
                    transform: translateX(0);
                }

                .nav-toggle {
                    display: block;
                }

                body.has-nav {
                    margin-left: 0;
                }

                /* Mobile overlay */
                .nav-overlay {
                    position: fixed;
                    top: 0;
                    left: 0;
                    right: 0;
                    bottom: 0;
                    background: rgba(0, 0, 0, 0.5);
                    z-index: 999;
                    display: none;
                }

                .nav-overlay.active {
                    display: block;
                }
            }
        `;
        document.head.appendChild(style);

        // Add body class
        document.body.classList.add('has-nav');
    }
}

// Initialize navigation
let nav;
document.addEventListener('DOMContentLoaded', function() {
    // Skip nav on login/register pages
    const publicPages = ['login.html', 'register.html'];
    const currentPage = window.location.pathname.split('/').pop();
    
    if (!publicPages.includes(currentPage)) {
        nav = new Navigation();
    }
});
