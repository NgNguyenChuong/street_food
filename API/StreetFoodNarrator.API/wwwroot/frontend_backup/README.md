# 🍜 Street Food Narrator - Frontend Guide

## 🚀 Quick Start

### 1. API đang chạy tại
- **Swagger UI**: http://localhost:5004/swagger
- **API Base URL**: http://localhost:5004/api
- **Trang chủ quản lý**: http://localhost:5004/ (index.html)

### 2. Tạo Tài Khoản Admin

**Có 2 cách để có tài khoản:**

#### Cách 1: Đăng ký trực tiếp qua frontend
1. Mở http://localhost:5004/frontend/register.html
2. Nhập thông tin:
   - Họ và Tên: `Admin User`
   - Email: `admin@example.com`
   - Mật khẩu: `Admin@123`
3. Click **Đăng Ký**
4. Sau khi đăng ký thành công, vào Swagger UI để gán role Admin

#### Cách 2: Dùng Swagger UI
1. Mở http://localhost:5004
2. Tìm endpoint **POST /api/Auth/register**
3. Click **Try it out**
4. Nhập JSON:
```json
{
  "email": "admin@streetfood.vn",
  "password": "Admin@123",
  "fullName": "Administrator",
  "phoneNumber": "0912345678"
}
```
5. Click **Execute**
6. User sẽ có role "Vendor" mặc định

### 3. Gán Role Admin (nếu cần)

Sau khi đăng ký, để gán role Admin, dùng SQL:

```sql
-- Get User ID
SELECT Id, Email, FullName FROM AspNetUsers WHERE Email = 'admin@example.com';

-- Get Admin Role ID
SELECT Id FROM AspNetRoles WHERE Name = 'Admin';

-- Add user to Admin role
INSERT INTO AspNetUserRoles (UserId, RoleId)
VALUES ('user-id-here', 'admin-role-id-here');
```

Hoặc dùng sqlcmd:
```powershell
sqlcmd -S "localhost\SQLEXPRESS" -d StreetFoodNarratorDB -Q "DECLARE @UserId NVARCHAR(450) = (SELECT TOP 1 Id FROM AspNetUsers WHERE Email = 'admin@example.com'); DECLARE @RoleId NVARCHAR(450) = (SELECT Id FROM AspNetRoles WHERE Name = 'Admin'); INSERT INTO AspNetUserRoles (UserId, RoleId) VALUES (@UserId, @RoleId);"
```

### 4. Đăng Nhập

1. **Qua Frontend**:
   - Mở http://localhost:5004/ (index.html)
   - Email: tài khoản bạn vừa tạo
   - Password: mật khẩu bạn đã đặt

2. **Qua Swagger UI**:
   - Endpoint: **POST /api/Auth/login**
   - Nhập email và password
   - Copy JWT token từ response

## 📁 Cấu Trúc Frontend

```
wwwroot/
├── index.html               # Trang chủ đăng nhập (ngoài thư mục frontend)
└── frontend/
    ├── api.js               # JavaScript API Client
    ├── nav.js               # Navigation & Session Manager (NEW!)
    ├── login.html           # Redirect sang /index.html (giữ liên kết cũ)
    ├── register.html        # Trang đăng ký
    ├── admin-dashboard.html # Dashboard Admin
    ├── vendor-dashboard.html # Dashboard Vendor
    ├── poi-create.html      # Tạo POI mới
    ├── poi-list.html        # Danh sách POI
    ├── poi-edit.html        # Chỉnh sửa POI
    ├── audio-bulk-generate.html # Tạo audio tự động
    ├── audio-list.html      # Quản lý Audio
    ├── vendors-list.html    # Quản lý Vendors (Admin)
    ├── analytics.html       # Thống kê & Phân tích
    └── settings.html        # Cài đặt hệ thống
```

### 🆕 Navigation Component (nav.js)

Tất cả các trang hiện đã sử dụng **nav.js** - một component tự động xử lý:

✅ **Session Management**
- Tự động kiểm tra authentication khi load trang
- Hỗ trợ cả localStorage và sessionStorage
- Redirect về login nếu chưa đăng nhập

✅ **Sidebar Navigation**
- Hiển thị menu điều hướng thống nhất
- Highlight trang hiện tại
- Phân quyền menu theo role (Admin/Vendor)
- User profile display
- Logout button

✅ **Seamless Navigation**
- Chuyển trang không cần đăng nhập lại
- Session được giữ nguyên khi navigate
- Responsive cho mobile

**Cách sử dụng**: Chỉ cần thêm `<script src="nav.js"></script>` vào đầu trang (trước CSS), component sẽ tự động xử lý!

## 🎯 Các Tính Năng Đã Hoàn Thành

### ✅ Authentication
- ✅ Đăng ký tài khoản
- ✅ Đăng nhập với JWT
- ✅ Lưu token trong localStorage
- ✅ Auto-redirect theo role

### ✅ POI Management
- ✅ View statistics (Total POIs, Active, Audio count)
- ✅ Create POI với multi-language support
- ✅ List POIs với status badges, filters & pagination
- ✅ Edit POI với map preview
- ✅ Delete POI (admin only)
- ✅ Bulk operations (edit, delete)

### ✅ Audio Management
- ✅ View POIs thiếu audio
- ✅ Generate audio bằng Web Speech API
- ✅ Upload audio tự động
- ✅ Progress tracking
- ✅ Audio list với player controls
- ✅ Download & delete audio

### ✅ Vendor Management (Admin)
- ✅ List vendors với filters
- ✅ Vendor profiles với statistics
- ✅ Approve/Reject vendors
- ✅ View vendor details

### ✅ Analytics & Reports
- ✅ Dashboard thống kê
- ✅ Chart hiển thị visits theo thời gian
- ✅ Top POIs phổ biến
- ✅ User growth statistics
- ✅ Export reports

### ✅ Settings
- ✅ General settings (site name, timezone, language)
- ✅ Notification preferences
- ✅ Security settings (2FA, sessions)
- ✅ Advanced settings (cache, logs)

## 🎨 Thiết Kế

- **Primary Color**: #FF6B35 (Cam)
- **Secondary Color**: #004E89 (Xanh đậm)
- **Fonts**: 
  - Playfair Display (headings)
  - DM Sans (body)
- **Style**: Modern gradient, card-based layout

## 🔧 Testing

### Test Registration
1. Mở http://localhost:5004/frontend/register.html
2. Nhập thông tin và đăng ký
3. Kiểm tra console nếu có lỗi

### Test Login
1. Mở http://localhost:5004/ (index.html)
2. Đăng nhập bằng tài khoản vừa tạo
3. Sẽ redirect đến admin-dashboard.html

### Test POI Creation
1. Từ dashboard, click "Tạo POI Mới"
2. Nhập thông tin (ít nhất Name_Vi và Description_Vi)
3. Submit form

### Test Audio Generation
1. Click "Tạo Audio Tự Động"
2. Chọn ngôn ngữ
3. Select POIs và generate

## 🐛 Troubleshooting

### Lỗi 500 khi đăng ký
**Nguyên nhân**: Roles chưa tồn tại trong database

**Giải pháp**: API đã tự động tạo roles khi khởi động. Nếu vẫn lỗi, restart API:
```powershell
cd "d:\project\street_food\API\StreetFoodNarrator.API"
dotnet run
```

### Link "Đăng ký ngay" mở tab mới
**Giải pháp**: Đã fix - link không có `target="_blank"`

### CORS errors
**Giải pháp**: Đảm bảo API đang chạy và frontend được serve từ:
- http://localhost:5500 (Live Server)
- http://127.0.0.1:5500
- http://localhost:3000

## 📱 Sử Dụng với Live Server

Để test frontend với CORS đúng cách:

1. Cài extension **Live Server** trong VS Code
2. Right-click vào `index.html` (trong wwwroot)
3. Chọn **Open with Live Server**
4. Frontend sẽ chạy tại http://localhost:5500

## 🔑 Roles và Permissions

| Role | Permissions |
|------|------------|
| **Admin** | Tất cả quyền - CRUD POIs, Audio, Users, View stats |
| **Vendor** | Create/Update POIs riêng, Upload audio cho POIs của mình |
| **User** | View public POIs, Trigger audio playback |

## 📞 API Endpoints

### Auth
- `POST /api/Auth/register` - Đăng ký
- `POST /api/Auth/login` - Đăng nhập
- `GET /api/Auth/me` - User hiện tại
- `POST /api/Auth/refresh` - Refresh token

### POIs
- `GET /api/POIs` - List POIs (pagination, search, filter)
- `GET /api/POIs/{id}` - Get POI detail
- `POST /api/POIs` - Create POI [Admin, Vendor]
- `PUT /api/POIs/{id}` - Update POI [Admin, Vendor]
- `DELETE /api/POIs/{id}` - Delete POI [Admin]
- `GET /api/POIs/stats` - Statistics [Admin]

### Audio
- `GET /api/Audio` - List audio files
- `GET /api/Audio/{id}` - Get audio detail
- `POST /api/Audio/upload` - Upload audio [Admin, Vendor]
- `GET /api/Audio/pois-without-audio` - POIs thiếu audio
- `PUT /api/Audio/{id}` - Update audio [Admin, Vendor]
- `DELETE /api/Audio/{id}` - Delete audio [Admin]

---

## ✨ Next Steps

### Đã hoàn thành
1. ✅ Test dashboard statistics
2. ✅ Tạo POI đầu tiên
3. ✅ Generate audio cho POI
4. ✅ Tạo vendor-dashboard.html
5. ✅ POI List & Edit pages
6. ✅ Audio Management page
7. ✅ Vendors Management page
8. ✅ Analytics & Reports page
9. ✅ Settings page

### Các trang mới
- **poi-list.html**: Quản lý danh sách POI với filters, search, bulk operations
- **poi-edit.html**: Chỉnh sửa POI với map preview, image upload
- **audio-list.html**: Quản lý audio với player controls, download/delete
- **vendors-list.html**: Quản lý vendors, approve/reject, view profiles
- **analytics.html**: Thống kê chi tiết với charts, top POIs, export
- **settings.html**: Cài đặt hệ thống, notifications, security

### Cần làm tiếp
- ⏳ Tích hợp API thực tế cho các trang mới
- ⏳ Implement map functionality (Leaflet/Google Maps)
- ⏳ Real-time notifications
- ⏳ Mobile responsive optimization

## 🎯 Test Navigation

**File test**: [test-nav.html](test-nav.html)

Để test navigation system:
1. Đăng nhập vào hệ thống (index.html)
2. Mở http://localhost:5004/frontend/test-nav.html
3. Xem session info và user info
4. Click vào các quick links để test navigation
5. ✅ Bạn sẽ **KHÔNG** cần đăng nhập lại khi chuyển trang!

### Session Persistence
- Session được lưu trong localStorage/sessionStorage
- Token được gửi tự động với mỗi API request (qua api.js)
- Navigation component tự động redirect nếu session hết hạn
- Logout sẽ clear tất cả session data
5. ⏳ Tạo poi-list.html và poi-edit.html
6. ⏳ Tạo audio-list.html

---

