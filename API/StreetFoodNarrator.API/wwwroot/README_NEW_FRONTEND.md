# 🍜 Street Food Narrator - Frontend CMS

## 📁 Cấu trúc mới (SPA - Single Page Application)

```
wwwroot/
├── index.html              # Trang đăng nhập
├── dashboard.html          # CMS Dashboard (SPA chính)
├── css/
│   ├── base.css           # Variables, reset, typography
│   ├── components.css     # Buttons, badges, tables, modals
│   └── layout.css         # Topbar, sidebar, pages
├── js/
│   ├── api.js             # API wrapper (POI, Audio, Auth, Vendors, Settings)
│   ├── auth.js            # JWT authentication & role management
│   ├── ui.js              # Modal, toast, pagination, badges
│   ├── poi.js             # POI management (list, create, edit, delete)
│   ├── audio.js           # Audio management (upload, generate, workflow)
│   └── modules.js         # Vendors & Settings
└── frontend_backup/        # ✅ Backup phiên bản cũ
```

## ✨ Tính năng chính

### 🔐 Authentication
- **JWT-based login** với role parsing từ token
- **Role-based UI**: Admin / Vendor permissions
- Session storage cho user data
- Auto-redirect khi chưa đăng nhập

### 📍 POI Management
- ✅ List, search, filter POI
- ✅ Create/Edit/Delete POI
- ✅ View chi tiết POI (full schema)
- ✅ Inline audio management
- ✅ Pagination & stats

### 🎵 Audio Management
- ✅ List, search, filter audio
- ✅ Upload audio (multipart form)
- ✅ **AI Generate** audio với TTS
- ✅ Workflow: Draft → Submit → Approve/Reject
- ✅ Audio player inline
- ✅ Edit audio metadata

### 🏪 Vendors (Admin only)
- ✅ List vendors với stats
- ✅ Filter by status
- ✅ View vendor details
- ✅ Approve/Reject workflow

### 👤 Vendor Profile (Vendor role)
- ✅ View vendor info
- ✅ POI count, rating, views
- ✅ Business description

### ⚙️ Settings
- ✅ App configuration
- ✅ Notifications preferences
- ✅ Security settings (2FA)
- ✅ TTS settings
- ✅ Geofence radius (Admin)

## 🎨 UI/UX Features

- **Modern dark theme** với accent color #f97316 (orange)
- **Responsive design** (desktop → tablet → mobile)
- **Smooth animations** (fadeIn, slideUp, pulse-glow)
- **Interactive feedback** (loading states, toasts, confirmations)
- **Role-based visibility** với `data-role` attributes
- **Accessibility**: keyboard navigation, focus states

## 🚀 Cách sử dụng

### 1. Chạy API Backend
```bash
cd API/StreetFoodNarrator.API
dotnet run
```
API sẽ chạy tại: `http://localhost:5004`

### 2. Truy cập CMS
Mở trình duyệt và vào:
- **Login**: `http://localhost:5004/index.html`
- **Dashboard**: `http://localhost:5004/dashboard.html` (sau khi login)

### 3. Đăng nhập
```
Email: admin@streetfood.vn
Password: Admin@123
```

## 📋 API Endpoints được sử dụng

### Auth
- `POST /api/Auth/login` - Đăng nhập
- `GET /api/Auth/me` - Lấy thông tin user
- `POST /api/Auth/refresh` - Refresh token

### POIs
- `GET /api/POIs?page&pageSize&search&category&isActive` - List POI
- `GET /api/POIs/{id}` - Chi tiết POI
- `POST /api/POIs` - Tạo POI
- `PUT /api/POIs/{id}` - Cập nhật POI
- `DELETE /api/POIs/{id}` - Xóa POI
- `GET /api/POIs/stats` - Thống kê POI

### Audio
- `GET /api/Audio?page&pageSize&language&poiId&status` - List audio
- `GET /api/Audio/{id}` - Chi tiết audio
- `POST /api/Audio` - Tạo audio record
- `PUT /api/Audio/{id}` - Cập nhật audio
- `DELETE /api/Audio/{id}` - Xóa audio
- `POST /api/Audio/upload` - Upload file audio (multipart)
- `POST /api/Audio/{id}/generate-file` - Generate TTS audio
- `POST /api/Audio/{id}/submit` - Gửi để duyệt
- `POST /api/Audio/{id}/approve` - Duyệt audio (Admin)
- `POST /api/Audio/{id}/reject` - Từ chối audio (Admin)
- `GET /api/Audio/pois-without-audio?language` - POI chưa có audio

### TTS
- `GET /api/TTS/test` - Test TTS service
- `POST /api/TTS/generate` - Generate TTS
- `GET /api/TTS/voices?language` - List voices

### Vendors
- `GET /api/Vendors/me` - Vendor profile (Vendor role)
- `PUT /api/Vendors/me` - Update vendor profile
- `GET /api/Vendors?page&pageSize&search&status` - List vendors (Admin)
- `GET /api/Vendors/{id}` - Chi tiết vendor (Admin)
- `POST /api/Vendors/{id}/verify` - Verify vendor (Admin)
- `POST /api/Vendors/{id}/reject` - Reject vendor (Admin)
- `GET /api/Vendors/stats` - Thống kê vendors (Admin)

### Settings
- `GET /api/Settings/me` - User settings
- `PUT /api/Settings/me` - Update settings

## 🔄 Workflow

### Audio Generation Workflow
1. **Vendor** chọn POI và tạo audio:
   - Option 1: Upload file audio có sẵn
   - Option 2: AI Generate với TTS
2. Generate script tự động từ POI data
3. Chọn voice và language
4. System tạo audio file và lưu vào database
5. **Vendor** submit audio để duyệt
6. **Admin** approve hoặc reject
7. Audio được active và sử dụng cho POI

### POI Management Workflow
1. **Admin/Vendor** tạo POI mới
2. Nhập thông tin: tên, mô tả, địa chỉ, location
3. POI được lưu với status active/inactive
4. Tạo audio cho POI (xem Audio Workflow)
5. POI hiển thị trên mobile app khi user đến gần

## 🎯 Điểm khác biệt so với phiên bản cũ

| Feature | Old (Multi-page) | New (SPA) |
|---------|------------------|-----------|
| Architecture | Multiple HTML files | Single dashboard.html |
| Navigation | Page reload | Client-side routing |
| State | Lost on navigation | Preserved in memory |
| Performance | Slower (full reload) | Faster (partial update) |
| API calls | Duplicate calls | Optimized with cache |
| UI consistency | Variable | Unified design system |
| Role management | Page-level | Component-level |
| Audio workflow | Basic | Complete workflow |
| TTS Integration | None | Full AI Generate |

## 🐛 Debug Tips

### Check API connection
```javascript
// Open browser console
console.log(API_BASE); // Should be http://localhost:5004
```

### Check authentication
```javascript
console.log(getToken());    // Should show JWT token
console.log(currentUser);   // Should show user object
console.log(isAdmin());     // true/false
console.log(isVendor());    // true/false
```

### Test API endpoints
```javascript
// Test POI list
POIApi.list({ page:1, pageSize:10 }).then(console.log);

// Test audio list
AudioApi.list({ language:'vi-VN' }).then(console.log);
```

## 📦 Dependencies

### Fonts
- **Syne** - Headings, brand
- **DM Sans** - Body text
- **JetBrains Mono** - Code, IDs

### No external JS libraries
- Vanilla JavaScript (ES6+)
- No jQuery, React, Vue
- Native Fetch API
- Native DOM manipulation

## 🔒 Security

- JWT token stored in `sessionStorage`
- Authorization header: `Bearer {token}`
- Role-based access control (RBAC)
- CORS enabled on API
- XSS protection với input sanitization

## 📱 Responsive Breakpoints

```css
Desktop:  > 1024px  (Full sidebar, all features)
Tablet:   768-1024px (Narrow sidebar)
Mobile:   < 768px   (Collapsed sidebar, stacked layout)
```

## 🎨 Color Palette

```css
--accent:    #f97316  (Orange - primary actions)
--green:     #22c55e  (Success)
--blue:      #3b82f6  (Info)
--red:       #ef4444  (Danger)
--yellow:    #f59e0b  (Warning)
--purple:    #a855f7  (Special)
```

## 📝 Notes

- **Backup cũ**: Toàn bộ frontend cũ đã được backup vào `frontend_backup/`
- **API Base URL**: Mặc định `http://localhost:5004`, có thể thay đổi trong `api.js`
- **Token expiry**: Check trong backend, frontend chưa có auto-refresh
- **File size limit**: Check backend configuration cho upload

## 🚧 TODO / Future enhancements

- [ ] Auto-refresh token khi hết hạn
- [ ] Bulk operations (delete multiple POIs, audio)
- [ ] Advanced search với filters
- [ ] Export data (CSV, JSON)
- [ ] Image upload cho POI
- [ ] Map integration (Google Maps / Mapbox)
- [ ] Real-time notifications (SignalR)
- [ ] Analytics dashboard với charts
- [ ] Multi-language support cho UI

---

**Created**: March 7, 2026  
**Version**: 1.0.0  
**Stack**: Vanilla JS + CSS3 + HTML5
