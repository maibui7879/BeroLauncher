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

## Luồng dự kiến trên web

1. Người biên dịch bấm **Phát hành bản dịch**.
2. Chụp dữ liệu đã lưu, dựng cả UI và thoại từ bản gốc, kiểm tra khả năng tái dựng.
3. Tạo revision, manifest và chữ ký.
4. Tạo release draft, tải asset, kiểm tra lại dữ liệu đã tải.
5. Công bố release hoàn chỉnh, rồi cập nhật kênh stable sau cùng.

Lưu câu không tự động phát hành. Một revision đã phát hành không bị ghi đè bằng nội dung mới. Lỗi phát hành giữ kênh stable hiện tại.

## Luồng dự kiến trên launcher

1. Kiểm tra khi mở launcher và khi người dùng chọn kiểm tra cập nhật; có bộ nhớ đệm và thời gian chờ.
2. So sánh revision của pack đang cài, không so với phiên bản launcher.
3. Hiện **Có bản Việt hóa mới** và nút **Cập nhật**.
4. Tải về thư mục tạm, xác minh manifest, chữ ký, hash, phiên bản game và giới hạn kích thước.
5. Đóng game trước khi thay file; dựng đủ hai kho rồi mới cài.
6. Giữ bản đang cài nếu mạng lỗi; hoàn tác nếu bước cài thất bại.

## Đã có / chưa có

Đã có phần cài/gỡ cục bộ, kiểm tra hash, dựng delta, sao lưu và hoàn tác. Chưa có publisher GitHub trên web, khóa ký, manifest kênh stable hoặc bộ tải cập nhật của launcher. Tài liệu này mô tả giao thức dự kiến, không khẳng định tính năng đã hoạt động.
