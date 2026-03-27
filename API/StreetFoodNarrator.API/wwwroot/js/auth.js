/* ═══════════════════════════════════════════════════════
   AUTH.JS  
   POST /api/Auth/login  → LoginModel { email, password }
   GET  /api/Auth/me     → ApplicationUser
   ApplicationUser.roles = uuid[] (role IDs, NOT names)
   Role names parsed from JWT claims
═══════════════════════════════════════════════════════ */
let currentUser = null;

const getSession = () => { try { const r=sessionStorage.getItem('sfn_user'); return r?JSON.parse(r):null; } catch{return null;} };
const saveSession = u => { sessionStorage.setItem('sfn_user', JSON.stringify(u)); currentUser = u; };
const clearSession = () => { sessionStorage.removeItem('sfn_user'); removeToken(); currentUser = null; };

// Role helpers — check both role string and roleNames array
const isAdmin  = () => !!(currentUser?.role==='Admin'  || currentUser?.roleNames?.includes('Admin'));
const isVendor = () => !!(currentUser?.role==='Vendor' || currentUser?.roleNames?.includes('Vendor'));
// VendorProfile.vendorId (int32)
const getVendorId = () => currentUser?.vendorProfile?.vendorId ?? null;

// Parse role names from JWT payload
// ASP.NET Identity uses claim type: "http://schemas.microsoft.com/ws/2008/06/identity/claims/role"
function _parseRolesFromToken(token) {
  try {
    const payload = JSON.parse(atob(token.split('.')[1].replace(/-/g,'+').replace(/_/g,'/')));
    const MS_ROLE = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';
    const raw = payload[MS_ROLE] || payload['role'] || payload['roles'] || payload['Role'];
    if (!raw) return null;
    return Array.isArray(raw) ? raw : [raw];
  } catch { return null; }
}

async function doLogin(email, password) {
  try {
    // POST /api/Auth/login → expected to return { token } or { accessToken }
    const res = await AuthAPI.login(email, password);
    if (!res) return { ok:false, msg:'Server không trả về phản hồi' };
    const token = res.token || res.accessToken || res.access_token || res.jwt;
    if (!token) return { ok:false, msg:'Server không trả về JWT token. Kiểm tra response từ /api/Auth/login.' };
    setToken(token);

    // GET /api/Auth/me → ApplicationUser
    const me = await AuthAPI.me();
    if (!me) return { ok:false, msg:'Không lấy được thông tin user từ /api/Auth/me' };

    // ApplicationUser fields: id(uuid), email, userName, fullName, createdAt, lastLoginAt,
    // roles(uuid[]), vendorProfile: VendorProfile
    const roleNames = _parseRolesFromToken(token) || (me.vendorProfile ? ['Vendor'] : ['Admin']);

    const user = {
      id:            me.id,
      email:         me.email,
      userName:      me.userName,
      name:          me.fullName || me.userName || me.email,
      fullName:      me.fullName,
      createdAt:     me.createdAt,
      lastLoginAt:   me.lastLoginAt,
      roleNames,
      role:          roleNames[0] || 'Vendor',
      vendorProfile: me.vendorProfile ?? null,   // VendorProfile schema
      vendorId:      me.vendorProfile?.vendorId ?? null,
    };
    saveSession(user);
    return { ok:true, user };
  } catch(err) {
    return { ok:false, msg: err.message || 'Đăng nhập thất bại' };
  }
}

function requireAuth() {
  const user = getSession();
  if (!user || !getToken()) { window.location.href='index'; return null; }
  currentUser = user;
  return user;
}

function applyRoleUI() {
  if (!currentUser) return;
  document.querySelectorAll('[data-role="admin"]').forEach(el  => el.style.display = isAdmin()  ? '' : 'none');
  document.querySelectorAll('[data-role="vendor"]').forEach(el => el.style.display = isVendor() ? '' : 'none');
  const rb = document.getElementById('role-badge');
  if (rb) { rb.textContent = isAdmin()?'Admin':'Vendor'; rb.className='role-badge '+(isAdmin()?'role-admin':'role-vendor'); }
  const un = document.getElementById('user-name');   if(un) un.textContent = currentUser.name;
  const av = document.getElementById('user-avatar'); if(av) av.textContent = (currentUser.name||'U')[0].toUpperCase();
}

function doLogout() { clearSession(); window.location.href='index'; }
