# Luồng cập nhật ngôn ngữ

## Phân chia trách nhiệm

- Web dịch chạy local: biên tập, lưu dữ liệu, dựng và kiểm thử pack.
- GitHub Releases: phân phối pack đã phát hành và các bản launcher.
- BeroLauncher: kiểm tra bản mới, thông báo, tải và cài khi người chơi đồng ý và game đã đóng.

Web local không cần mở liên tục. Người chơi tải pack từ GitHub, không kết nối với máy biên dịch. Không đưa khóa GitHub hoặc khóa ký vào launcher.

## Quy ước phát hành

- Launcher: tag `launcher-v0.2.0`, asset ZIP chạy độc lập.
- Ngôn ngữ: tag `lang-v<revision>`, asset pack và manifest của riêng revision đó.
- Kênh `stable` chỉ chuyển sang revision mới sau khi tải xong và kiểm tra toàn bộ asset.
- Không dùng `/releases/latest` chung để suy ra bản ngôn ngữ: bản launcher và pack có thể được phát hành xen kẽ.
- Launcher và pack có phiên bản riêng; manifest pack chỉ định phiên bản launcher tối thiểu, fingerprint dữ liệu game, kích thước và SHA-256 của payload.
- Manifest phát hành cần ký bằng khóa phát hành riêng; launcher giữ khóa công khai và kiểm tra trước khi dùng URL tải hoặc áp dụng pack. SHA-256 đơn lẻ không xác minh danh tính người phát hành.

## Luồng trên web local

1. Người biên dịch bấm **Phát hành bản dịch**.
2. Chụp dữ liệu đã lưu, dựng cả UI và thoại từ bản gốc, kiểm tra khả năng tái dựng.
3. Tạo revision, manifest và chữ ký.
4. Tạo release draft, tải asset, kiểm tra lại dữ liệu đã tải.
5. Công bố release hoàn chỉnh, rồi cập nhật kênh stable sau cùng.

Lưu câu không tự động phát hành. Một revision đã phát hành không bị ghi đè bằng nội dung mới. Lỗi phát hành giữ kênh stable hiện tại.

## Luồng trên launcher

1. Kiểm tra khi mở launcher và khi người dùng chọn kiểm tra cập nhật; có bộ nhớ đệm và thời gian chờ.
2. So sánh revision của pack đang cài, không so với phiên bản launcher.
3. Hiện **Có bản Việt hóa mới** và nút **Cập nhật**.
4. Tải về thư mục tạm, xác minh manifest, chữ ký, hash, phiên bản game và giới hạn kích thước.
5. Đóng game trước khi thay file; dựng đủ hai kho rồi mới cài.
6. Giữ bản đang cài nếu mạng lỗi; hoàn tác nếu bước cài thất bại.

## Đã có / chưa có

Đã tích hợp publisher trên web local và bộ cập nhật của launcher 0.3.0. Web giữ khóa RSA riêng, repo chỉ chứa khóa công khai. Kênh ổn định là asset `channel.json` của release `lang-stable`; payload ký trỏ đến các asset bất biến của revision `lang-v…`. Khi thay asset kênh có thể có một khoảng 404 ngắn; launcher giữ bản hiện tại và cho kiểm tra lại.

Launcher kiểm tra khi mở ứng dụng hoặc bấm **Kiểm tra cập nhật**, không nhận push trực tiếp khi đang mở suốt nhiều giờ. Không cần máy biên dịch luôn bật. Chưa tích hợp tự cập nhật chính file launcher.

Khóa công khai cố định trong bản launcher. Không thay khóa riêng trên máy phát hành nếu chưa có kế hoạch cập nhật khóa trong launcher. Lưu bản sao khóa riêng ở nơi riêng tư; không đưa khóa riêng hay thông tin đăng nhập GitHub vào repo.
