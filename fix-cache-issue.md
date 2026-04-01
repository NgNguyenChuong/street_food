# Fix: Database Not Refreshing in audio-bulk-generate

## Vấn đề:
Trang `audio-bulk-generate.html` không load được database mới cập nhật lên do **HTTP caching**.

## Nguyên nhân:
1. Browser cache các GET requests
2. Không có cơ chế force refresh khi cần
3. API fetch không disable cache

## Các thay đổi đã thực hiện:

### 1. **api.js** - Tắt HTTP Cache
- Thêm `cache: 'no-store'` vào tất cả fetch requests
- Đảm bảo luôn lấy dữ liệu mới nhất từ server

```javascript
const response = await fetch(url, {
    ...fetchOptions,
    headers,
    cache: 'no-store'  // ← Thêm dòng này
});
```

### 2. **audio-bulk-generate.html** - Thêm nút Refresh
- Thêm nút "Làm mới" ở BƯỚC 2
- Thêm cache-buster timestamp khi force refresh

**UI Changes:**
```html
<button class="btn btn-outline" onclick="refreshPOIs()">
    <i class="fas fa-sync-alt"></i> Làm mới
</button>
```

**Code Changes:**
```javascript
// Thêm parameter forceRefresh
async function loadPOIs(forceRefresh = false) {
    // Add timestamp to bypass cache
    const cacheBuster = forceRefresh ? `&_t=${Date.now()}` : '';
    const perLang = await Promise.all(langs.map(async l => {
        try { 
            return { 
                lang: l, 
                pois: await api.request(`/Audio/pois-without-audio?language=${l}${cacheBuster}`) 
            }; 
        }
        catch { return { lang: l, pois: [] }; }
    }));
}

// Hàm refresh mới
async function refreshPOIs() {
    console.log('🔄 Đang làm mới danh sách POI...');
    await loadPOIs(true);
    console.log('✅ Đã làm mới!');
}
```

### 3. **Auto-refresh khi quay lại Step 2**
- Tự động refresh POI list khi người dùng quay lại BƯỚC 2

```javascript
function goStep(step) {
    // ... existing code ...
    
    // Auto-refresh POI list when entering step 2
    if (step === 2) {
        refreshPOIs();
    }
}
```

## Cách sử dụng:

### Tự động:
- Mỗi lần vào BƯỚC 2, danh sách POI sẽ tự động làm mới
- `cache: 'no-store'` đảm bảo không bị cache cũ

### Thủ công:
- Click nút **"Làm mới"** bất kỳ lúc nào để force refresh
- Console sẽ log: `🔄 Đang làm mới...` → `✅ Đã làm mới!`

## Test:
1. Upload POI mới vào database
2. Vào trang `audio-bulk-generate`
3. Chọn ngôn ngữ ở BƯỚC 1
4. Vào BƯỚC 2 → POI list sẽ tự động refresh
5. Hoặc click nút "Làm mới" để refresh thủ công

## Kết quả:
✅ Luôn hiển thị dữ liệu mới nhất từ database
✅ Không bị cache cũ
✅ Có thể force refresh bất kỳ lúc nào
