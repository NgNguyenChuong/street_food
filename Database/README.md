# 🗄️ DATABASE

## Cách sử dụng (MongoDB)

### 1. Tạo collections + indexes
```bash
mongosh < d:/project/street_food/Database/CreateSchema.sql
```

### 2. Import dữ liệu mẫu
```bash
mongosh < d:/project/street_food/Database/SeedData.mongodb.js
```

### 3. Dữ liệu mẫu
Thư mục **SampleData/** chứa:
- **Audio/** - File audio mẫu (.mp3, .wav)
- **Images/** - Hình ảnh POI mẫu

## Lưu ý
- Dữ liệu seed dựa trên Vĩnh Khánh Food Street
- Nếu muốn chỉnh sửa dữ liệu mẫu, cập nhật `SeedData.mongodb.js`
