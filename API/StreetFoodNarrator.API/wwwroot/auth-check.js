// Simple authentication check - redirects to login if not authenticated
(function() {
    // List of public pages that don't require authentication
    const publicPages = ['index.html', 'login.html', 'register.html'];
    const currentPage = window.location.pathname.split('/').pop() || 'index.html';
    
    // Skip check for public pages
    if (publicPages.includes(currentPage)) {
        // Clear redirect tracking when on login page
        sessionStorage.removeItem('auth_last_redirect');
        return;
    }
    
    // Prevent infinite redirect loop - only track login redirects
    const lastRedirect = sessionStorage.getItem('auth_last_redirect');
    const now = Date.now();
    
    if (lastRedirect) {
        const timeSinceLastRedirect = now - parseInt(lastRedirect);
        if (timeSinceLastRedirect < 1000) { // Less than 1 second ago
            console.error('Auth redirect loop detected. Clearing session and stopping.');
            localStorage.removeItem('userSession');
            sessionStorage.removeItem('userSession');
            sessionStorage.removeItem('auth_last_redirect');
            return; // Stop redirecting
        }
    }
    
    // Check for session
    const session = localStorage.getItem('userSession') || sessionStorage.getItem('userSession');

    // Redirect to login if no session found
    if (!session) {
        console.log('⚠️ No session found, redirecting to login...');
        sessionStorage.setItem('auth_last_redirect', now.toString());
        window.location.href = '/index.html';
        return;
    }
    
    // Clear redirect tracking on successful auth
    sessionStorage.removeItem('auth_last_redirect');
    
    // Optional: Validate session and make user info available
    try {
        window.currentUser = JSON.parse(session);
        console.log('✅ Auth check passed for:', window.currentUser.email || window.currentUser.name);
    } catch (e) {
        console.error('❌ Invalid session data:', e);
        localStorage.removeItem('userSession');
        sessionStorage.removeItem('userSession');
        sessionStorage.setItem('auth_last_redirect', now.toString());
        window.location.href = '/index.html';
    }
})();
