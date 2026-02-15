Thuyết minh tự động đa ngôn ngữ cho phố ẩm thực Vĩnh Khánh
+) Yêu cầu tổng quát         
   POI: trong phạm vi của quán sẽ phát audio để thuyết minh (vẫn chưa rõ cái này)
            Location: cấp quyền truy cập để có thể xác định được vị trí hiện tại
Dùng QR để khi đến app có thể quét ( có thời hạn 5 ngày ) 
Có thể dùng QR code cho trường hợp không đi đến trực tiếp luôn
•	Trường hợp nếu không đi đến trực tiếp thì thực hiện chức năng xem ảo
Sẽ dùng TextToSpeech để thuyết minh
Có thể chọn nhiều ngôn ngữ khác như Anh, Nhật,Hàn….
Dùng library  (GPS) -> Dùng để biết vị trí của người dùng sau đó thuyết minh
 POI 
TEXT – CHỌN  - Audio 1
-	Audio 2
-	Audio 3
-	….
Các actor chính :  Du khách -> App
		 Người bán hàng  -> làm web quản lý các thông tin 
 Admin -> sẽ làm web quản lý các thông tin (dashboard, thông tin của tất cả các gian hàng)
Hạ tầng: Server ( Web )
	    Client ( App )
Nội dung cần : 
-	Chức năng cần thiết (PoC)
-	Chức năng mở rộng (MVP) -> Nếu có thời gian (điểm cộng)
-	Kiến trúc gợi ý
Về data:
-	Data của ứng dụng << Dung lượng thiết bị
	Tải response nhanh hơn và không cần internet (offline)
-	Data của ứng dụng >> Dung lượng thiết bị
	Cần Internet
+) Các yêu cầu chi tiết
1.	GPS tracking theo real-time
-	Lấy vị trí người dùng chính xác (Foreground + background)
-	Giải pháp để tối ưu pin (Hỏi GPT và tìm hiểu thêm)
(Vd: Tracking location trong khoảng 10s)
2.	Kích hoạc điểm thuyết minh (Geofence)
-	Thiết lập điểm POI (POINT OF INTEREST) với tọa độ Lat/Long
-	Bán kính kích hoạt
-	Mức độ ưu tiên
-	Tự động phát nội dung khi người dùng:
o	Đi vào vùng
o	Đi đến gần điểm
-	Có thể chống spam = debounce & cooldown (tránh lặp đi lặp lại nhiều lần khi đứng tại vị trí đó lâu)
3.	Thuyết minh tự động
-	Text-to-speech(TTS)
o	Linh hoạt, đa ngôn ngữ
o	Dung lượng nhẹ
-	File audio thu sẵn
o	Giọng tự nhiên, chuyên nghiệp
o	Chất lượng cao nhưng không quá nặng dữ liệu’
-	App cần:
o	Quản lý hàng chờ audio
o	Không phát trùng lặp
o	Tự dừng khi có thông báo khác
4.	Quản lý dữ liệu POI
-	Danh sách điểm thuyết minh
-	Mô tả văn bản
-	Ảnh minh họa
-	Link đồ họa 
-	File Audio or Script TTS
5.	Map view
-	Hiển thị vị trí người dùng trên bản đồ 
-	Hiển thị tất cả các POI
-	Highlight POI đang gần nhất
-	Xem chi tiết POI
6.	Hệ thống quản trị nội dung (CMS)
• Tạo trang web quản lý:
• POI
• Audio
• Bản dịch
• Lịch sử sử dụng
• Quản lý tour
	Bổ sung thêm: Thêm thống kê tại sao đông? Có gì hot ở gian hàng ? thích món nào? 
7.	 Phân tích dữ liệu (Analytics)
• Lưu tuyến di chuyển (ẩn danh)
• Top địa điểm được nghe nhiều nhất
• Thời gian trung bình nghe 1 POI
• Heatmap vị trí người dùng
8.	QR code kích hoạt nội dung
-	Dùng tại điểm dừng xe buýt các phường Khánh Hội, Vĩnh Hội, Xóm Chiếu
-	Quét là ra luôn, không cần GPS.
9.	Luồng hoạt động mẫu
• App tải danh sách POI (lat/lng, bán kính, ưu tiên, nội dung thuyết minh).
• Khi người dùng di chuyển, background service cập nhật vị trí.
• Geofence Engine xác định POI gần nhất/ưu tiên cao nhất trong bán kính → gửi sự kiện.
• Narration Engine kiểm tra trạng thái (đang phát? đã phát trong X phút?) → quyết định phát TTS/Audio.
• Ghi log đã phát, tránh lặp.
10.	Framework .NET MAUI (Android/iOS)
- GPS & Background:
• Android: FusedLocationProviderClient (thông qua dependency service hoặc Essentials: Geolocation), Foreground Service de tracking nên.
• iOS: CLLocationManager với quyền always, region monitoring (geofence).
• Geofencing: Native API hoặc tự tính khoảng cách bằng Haversine rồi trigger theo ngưỡng.
• TTS/Audio:
• TTS bản địa (Android TextToSpeech, iOS AVSpeechSynthesizer) hoặc Azure Cognitive Services (nếu cần giọng tự nhiên, lưu ý offline vs online).
• Map: Microsoft.Maui.Controls.Maps (MAUI Maps) hoặc Google Maps SDK / MapKit binding nếu cần tính năng nâng cao.
• Offline: SQLite (EF Core/SQLite-net) + file âm thanh tải trước. Bản đồ offline có thể cần SDK bên thứ 3 (ví dụ Mapbox, Here) nếu muốn cache/offline mapping thật sự.
•	Yêu cầu thêm: Có thêm hình ảnh minh họa càng tốt, tạo folder làm việc khoa học và chuẩn cấu trúc, code bằng C# và làm theo kiểu OOP 
