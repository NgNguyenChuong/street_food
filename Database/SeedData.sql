-- Dữ liệu mẫu cho Street Food Narrator
-- Vĩnh Khánh Food Street, Quận 1, TPHCM

-- Dữ liệu thực từ Phố Ẩm Thực Vĩnh Khánh, Quận 4, TPHCM
-- Source: FlavorQuest project (verified real POIs)
-- 12 điểm ẩm thực từ đầu đến cuối phố

-- Sample POIs (Real data from Vĩnh Khánh Food Street)
INSERT INTO POIs (
    Latitude, Longitude, Radius, Priority,
    Name_Vi, Name_En,
    Description_Vi, Description_En,
    SignatureDish, FunFact, EstimatedHours,
    Type, IsActive
)
VALUES 
    -- 1. Cổng vào (Điểm khởi đầu tour)
    (10.761905898335831, 106.70222716527056, 80, 10,
     N'Cổng vào Phố Ẩm thực Vĩnh Khánh', 'Vinh Khanh Food Street Entrance',
     N'Biển chào "Phố Ẩm thực Vĩnh Khánh" – nơi bắt đầu hành trình khám phá thiên đường hải sản sầm uất nhất Quận 4. Được Time Out bình chọn là một trong 10 đường phố thú vị nhất thế giới năm 2025.', 
     'The welcoming gate of "Vinh Khanh Food Street" - the start of your journey to explore District 4''s busiest seafood paradise. Ranked as one of the 10 coolest streets in the world for 2025 by Time Out.',
     N'Hải sản tươi sống (Seafood)', 
     N'Đây là điểm quét QR code để bắt đầu tour thuyết minh tự động.',
     '17:00-23:00',
     'LandMark', 1),
     
    -- 2. Ốc Vũ
    (10.761518431027818, 106.70271542519974, 50, 7,
     N'Ốc Vũ', 'Vu Snails',
     N'Nằm tại 37 Vĩnh Khánh, quán thu hút đông đảo giới trẻ bởi thực đơn đa dạng và giá cả cực kỳ bình dân. Các món nướng tại đây luôn nóng hổi và đậm đà.', 
     'Located at 37 Vinh Khanh, this spot attracts many youngsters with its diverse menu and very affordable prices. The grilled dishes here are always hot and flavorful.',
     N'Ốc xào bơ tỏi, Hải sản nướng',
     N'Giá cực mềm, phù hợp cho học sinh sinh viên.',
     '15:00-00:00',
     'Restaurant', 1),
     
    -- 3. Ốc Thảo
    (10.761795162597451, 106.70239298897182, 50, 7,
     N'Ốc Thảo', 'Thao Snails',
     N'Địa chỉ 383 Vĩnh Khánh. Quán nổi tiếng với không gian rộng rãi, sạch sẽ và cách chế biến món ăn cầu kỳ, giữ được độ ngọt tự nhiên của hải sản.', 
     'Located at 383 Vinh Khanh. Famous for its spacious and clean area. The cooking techniques are meticulous, preserving the natural sweetness of the seafood.',
     N'Ốc len xào dừa, Sò điệp nướng mỡ hành',
     N'Thực đơn phong phú với hơn 30 loại ốc khác nhau.',
     '14:00-23:00',
     'Restaurant', 1),
     
    -- 4. Ốc Sáu Nở
    (10.761038078500885, 106.70290444809687, 50, 8,
     N'Ốc Sáu Nở', 'Sau No Snails',
     N'Tọa lạc tại khu vực 121-128 Vĩnh Khánh, đây là một trong những quán ốc vỉa hè đời đầu với không gian sôi động, đậm chất đường phố Sài Gòn.', 
     'Situated at 121-128 Vinh Khanh, this is one of the original sidewalk snail stalls with a vibrant atmosphere, quintessential of Saigon street style.',
     N'Nghêu hấp sả, Ốc hương rang muối',
     N'Tươi ngon và náo nhiệt là những từ khóa khi nhắc đến Sáu Nở.',
     '16:00-23:30',
     'Stall', 1),
     
    -- 5. Ốc Oanh (quán huyền thoại - Michelin Bib Gourmand)
    (10.760848629826567, 106.7032957744219, 60, 9,
     N'Ốc Oanh', 'Oanh Snails',
     N'Quán lão làng với tuổi đời hơn 20 năm tại 534 Vĩnh Khánh. Nổi tiếng nhất với các món ốc sốt bơ tỏi và đặc biệt được vinh danh trong danh mục Bib Gourmand của Michelin.', 
     'A legendary spot with over 20 years of history at 534 Vinh Khanh. Most famous for its butter garlic sauce and specially honored in the Michelin Bib Gourmand list.',
     N'Ốc hương xào bơ tỏi, Ốc bươu nướng phô mai',
     N'Đứng đầu danh sách những quán phải thử khi đến Vĩnh Khánh.',
     '15:00-23:00',
     'Restaurant', 1),
     
    -- 6. A Fat Hot Pot
    (10.760806933075282, 106.70347875218654, 50, 6,
     N'A Fat Hot Pot', 'A Fat Hot Pot',
     N'Nằm tại 668 Vĩnh Khánh, quán chuyên về lẩu và nướng tự chọn. Không gian rộng thoáng, rất phù hợp cho những buổi tụ tập nhóm đông người.', 
     'Located at 668 Vinh Khanh, specialized in self-selected hot pot and BBQ. The spacious and airy environment makes it ideal for large group gatherings.',
     N'Lẩu hải sản, Hào sữa nướng',
     N'Phong cách buffet tại bàn rất được ưa chuộng.',
     '16:00-23:00',
     'Restaurant', 1),
     
    -- 7. Chilli Lẩu Nướng Tự Chọn
    (10.760794431975599, 106.7036590681073, 50, 6,
     N'Chilli Lẩu Nướng Tự Chọn', 'Chilli BBQ & Hot Pot',
     N'Địa chỉ 232 Vĩnh Khánh. Đây là điểm đến quen thuộc của giới trẻ với các món lẩu nướng đa dạng, từ hải sản đến thịt bò Mỹ cao cấp.', 
     'Address: 232 Vinh Khanh. A popular destination for youngsters, offering a variety of BBQ and hot pot dishes from seafood to premium US beef.',
     N'Lẩu nướng hải sản, Bò Mỹ nướng',
     N'Gia vị ướp đậm đà là đặc trưng riêng của Chilli.',
     '16:00-00:00',
     'Restaurant', 1),
     
    -- 8. Alo Quán – Seafood & Beer
    (10.761127163188009, 106.70475425408135, 50, 7,
     N'Alo Quán – Seafood & Beer', 'Alo Quan - Seafood & Beer',
     N'Tọa lạc tại 333 Vĩnh Khánh, Alo Quán mang phong cách hiện đại, chill, phù hợp cho những buổi nhậu đêm thoải mái bên gia đình và bạn bè.', 
     'Located at 333 Vinh Khanh, Alo Quan offers a modern, chill style, perfect for late-night drinking and relaxing with family and friends.',
     N'Tôm sốt Thái, Nghêu hấp sả',
     N'Không gian mở rất thoáng và view ngắm phố cực đẹp.',
     '15:00-01:00',
     'Restaurant', 1),
     
    -- 9. Ốc Đào 2
    (10.761347965170131, 106.70496784739889, 50, 8,
     N'Ốc Đào 2', 'Dao Snails 2',
     N'Chi nhánh nổi tiếng tại 232/123 Vĩnh Khánh. Ốc Đào nổi danh nhờ nước chấm đặc trưng và thực đơn phong phú với hơn 30 cách chế biến khác nhau.', 
     'A famous branch at 232/123 Vinh Khanh. Dao Snails is renowned for its signature dipping sauce and a rich menu with over 30 different cooking styles.',
     N'Ốc sốt trứng muối, Ốc me',
     N'Thương hiệu ốc top đầu Sài Gòn nay đã có mặt tại Vĩnh Khánh.',
     '12:00-22:00',
     'Restaurant', 1),
     
    -- 10. Lãng Quán
    (10.761149988188182, 106.70538401196282, 50, 6,
     N'Lãng Quán', 'Lang Quan',
     N'Nằm ở đoạn giữa phố (khu vực 500-600), Lãng Quán là lựa chọn tuyệt vời cho các nhóm nhậu với menu lẩu nướng và hải sản vô cùng đa dạng.', 
     'Located in the middle of the street (area 500-600), Lang Quan is a great choice for drinking groups with an extremely diverse BBQ and seafood menu.',
     N'Lẩu hải sản nướng, Bạch tuộc nướng',
     N'Giá cả ổn định và phục vụ nhanh nhẹn.',
     '16:00-00:00',
     'Restaurant', 1),
     
    -- 11. Ớt Xiêm Quán
    (10.761185236052697, 106.70570361039157, 50, 7,
     N'Ớt Xiêm Quán', 'Ot Xiem Quan',
     N'Nằm tại 568 Vĩnh Khánh, quán nổi bật với các món hải sản được chế biến cầu kỳ theo phong cách riêng, không chỉ giới hạn ở các món ốc.', 
     'Located at 568 Vinh Khanh, this place stands out with elaborately prepared seafood dishes in its own style, not just limited to snail dishes.',
     N'Cá diêu hồng rang muối Hồng Kông, Tôm sú mù tạt',
     N'Điểm đến cho những ai thích khám phá hương vị hải sản mới lạ.',
     '16:00-23:00',
     'Restaurant', 1),
     
    -- 12. Bún Cá Châu Đốc Dì Tư
    (10.761123552506971, 106.70660690985743, 40, 7,
     N'Bún Cá Châu Đốc Dì Tư', 'Di Tu Chau Doc Fish Noodles',
     N'Địa chỉ 320/79 Vĩnh Khánh. Quán mang hương vị bún cá miền Tây đặc trưng với nước dùng thanh ngọt từ cá và nghệ. Một sự thay đổi khẩu vị tuyệt vời sau các món nướng.', 
     'Address: 320/79 Vinh Khanh. The stall brings the typical Western fish noodle flavor with a clear, sweet broth made from fish and turmeric. A great change of pace after grilled dishes.',
     N'Bún cá đặc biệt, Bún mực',
     N'Có phục vụ cả buổi sáng sớm cho khách ghé phố.',
     '06:00-21:00',
     'Restaurant', 1);

-- Sample Audio Contents (TTS scripts cho POIs quan trọng)
INSERT INTO AudioContents (POIId, Language, TTSScript, Type, Duration)
VALUES 
    -- Cổng vào (POI #1) - Tiếng Việt
    (1, 'vi', 
     N'Chào mừng bạn đến với Phố Ẩm thực Vĩnh Khánh! Đây là thiên đường hải sản sầm uất nhất Quận 4, được Time Out bình chọn là một trong 10 đường phố thú vị nhất thế giới. Hãy chuẩn bị khám phá hành trình ẩm thực đặc sắc với chúng tôi!', 
     'TTS', 20),
     
    -- Cổng vào (POI #1) - English
    (1, 'en', 
     'Welcome to Vinh Khanh Food Street! This is District 4''s busiest seafood paradise, ranked as one of the 10 coolest streets in the world by Time Out. Get ready to explore an amazing culinary journey with us!', 
     'TTS', 18),
     
    -- Ốc Vũ (POI #2) - Tiếng Việt
    (2, 'vi', 
     N'Bạn đang đến gần Ốc Vũ, quán ốc bình dân được giới trẻ yêu thích. Với thực đơn đa dạng và giá cả phải chăng, đây là điểm dừng chân lý tưởng cho học sinh sinh viên. Các món nướng ở đây luôn nóng hổi và đậm đà.', 
     'TTS', 15),
     
    -- Ốc Oanh (POI #5) - Tiếng Việt - MICHELIN!
    (5, 'vi', 
     N'Chào mừng bạn đến với Ốc Oanh - quán ốc huyền thoại với hơn 20 năm tuổi đời! Quán được vinh danh trong danh mục Bib Gourmand của Michelin. Món ốc hương xào bơ tỏi và ốc bươu nướng phô mai là đặc sản không thể bỏ qua.', 
     'TTS', 22),
     
    -- Ốc Oanh (POI #5) - English
    (5, 'en', 
     'Welcome to Oanh Snails - a legendary restaurant with over 20 years of history! Honored in the Michelin Bib Gourmand list. The butter garlic snails and grilled cheese escargot are must-try specialties.', 
     'TTS', 20),
     
    -- Ốc Đào 2 (POI #9) - Tiếng Việt
    (9, 'vi', 
     N'Bạn đang đến gần Ốc Đào 2. Quán nổi tiếng với nước chấm đặc trưng và hơn 30 cách chế biến ốc khác nhau. Món ốc sốt trứng muối và ốc me là những món đặc sản phải thử!', 
     'TTS', 15),
     
    -- Bún Cá Dì Tư (POI #12) - Tiếng Việt
    (12, 'vi', 
     N'Đây là Bún Cá Châu Đốc Dì Tư, mang hương vị miền Tây đích thực với nước dùng thanh ngọt từ cá và nghệ. Một sự thay đổi khẩu vị tuyệt vời sau những món hải sản nướng!', 
     'TTS', 14);

-- Sample Tour
INSERT INTO Tours (Name, Description, Duration, IsActive)
VALUES 
    (N'Tour Ẩm Thực Vỉa Hè Sài Gòn', 
     N'Khám phá các món ăn vỉa hè đặc trưng của Sài Gòn tại khu vực Vĩnh Khánh', 
     60, 1);

-- Link POIs to Tour
INSERT INTO POI_Tours (POIId, TourId, [Order])
VALUES 
    (1, 1, 1),
    (2, 1, 2),
    (3, 1, 3),
    (4, 1, 4);
