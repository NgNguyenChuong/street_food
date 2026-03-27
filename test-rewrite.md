# URL Rewrite Test - Street Food Narrator

## ✅ ĐÃ SỬA XONG!

### Middleware Flow (Thứ tự quan trọng):
```
1. UseHttpsRedirection()
2. UseRewriter()           ← Thêm .html vào URL
3. Custom Auth Middleware  ← Check authentication (sau khi có .html)
4. UseDefaultFiles()
5. UseStaticFiles()        ← Serve file HTML
```

## Regex Pattern (Đã sửa):
```regex
^(?!api|swagger|uploads)([a-zA-Z0-9\-_/]+)(?<!\.(js|css|json|png|jpg|jpeg|gif|svg|ico|woff|woff2|ttf|eot|html))(\?.*)?$
```
**Replacement:** `$1.html$2`

### Giải thích:
- `^` - Bắt đầu string
- `(?!api|swagger|uploads)` - Negative lookahead: KHÔNG bắt đầu với api/swagger/uploads
- `([a-zA-Z0-9\-_/]+)` - **Capture Group 1**: Match đường dẫn (ít nhất 1 ký tự)
- `(?<!\.(js|css|...))` - Negative lookbehind: KHÔNG kết thúc bằng các extension
- `(\?.*)?` - **Capture Group 2**: Optional query string
- `$` - Kết thúc string
- **Replacement**: `$1.html$2` → Thêm .html giữa path và query string

## Test Cases:

### ✅ Nên match (thêm .html):
| Request URL | Rewrite thành |
|------------|---------------|
| `/dashboard` | `/dashboard.html` |
| `/poi-list` | `/poi-list.html` |
| `/audio-list` | `/audio-list.html` |
| `/users` | `/users.html` |
| `/dashboard?id=123` | `/dashboard.html?id=123` |
| `/poi-edit?id=5` | `/poi-edit.html?id=5` |

### ❌ Không match (giữ nguyên):
| Request URL | Kết quả |
|------------|---------|
| `/api/pois` | Không match (có `api`) |
| `/swagger/index.html` | Không match (có `swagger`) |
| `/js/app.js` | Không match (kết thúc `.js`) |
| `/css/style.css` | Không match (kết thúc `.css`) |
| `/uploads/file.png` | Không match (có `uploads`) |
| `/dashboard.html` | Không match (đã có `.html`) |
| `/` | Không match (empty path) |

## 🧪 Cách Test:

1. **Chạy server:**
   ```bash
   cd D:\street_food\API\StreetFoodNarrator.API
   dotnet run
   ```

2. **Test trong browser:**
   - ✅ `http://localhost:5004/dashboard` → Load dashboard.html
   - ✅ `http://localhost:5004/poi-list` → Load poi-list.html
   - ✅ `http://localhost:5004/users` → Load users.html
   - ✅ `http://localhost:5004/dashboard.html` → Vẫn hoạt động bình thường

3. **Test với curl:**
   ```bash
   # Test rewrite
   curl -I http://localhost:5004/dashboard
   
   # Test không rewrite API
   curl -I http://localhost:5004/api/pois
   ```

## 🔐 Authentication Flow:

- **Public pages** (không cần auth):
  - `/`, `/index.html`, `/login.html`, `/register.html`
  
- **Protected pages** (cần JWT token):
  - Tất cả các trang HTML khác
  - Redirect về `/index.html` nếu không có token
