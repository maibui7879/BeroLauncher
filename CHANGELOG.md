# Thay đổi

## 0.4.2

- Thêm nút cập nhật gói ngôn ngữ và dùng tiếng Anh ngay trên màn hình chính.
- Hiển thị trạng thái khởi tạo, khóa nút chơi khi game đang chạy và bật lại sau khi game thoát.
- Hiển thị tiến trình kiểm tra, tải, cài đặt và khôi phục ngôn ngữ trên các nút.
- Đổi nhãn Thư viện thành Launcher và bỏ nhãn Windows.

## 0.4.1

- Xác minh lại chữ ký và hash trước mỗi lần cài, kể cả gói đã lưu trên máy.
- Chặn gói cache bị sửa manifest/payload hoặc thiếu chữ ký. Gói cache cũ cần tải lại qua Kiểm tra cập nhật.
- Tách model và dịch vụ, chuẩn hóa định dạng mã nguồn và bổ sung lint/build/test trong CI.
- Khóa GitHub Actions bằng SHA, thêm CODEOWNERS và kiểm tra cập nhật phụ thuộc.

## 0.4.0

- Icon vector đồng bộ cho thư viện, chơi game, cài đặt và ngôn ngữ.
- Thông báo bản dịch mới có khu vực riêng, không đẩy chật nút Chơi ngay.
- Cài đặt dạng panel bên phải, có cuộn khi cửa sổ thấp; Esc để đóng và hỗ trợ điều hướng bàn phím.
- Hiển thị trạng thái Việt hóa theo dữ liệu đang cài. Không bật lại khi bản dịch đã được bật và chưa có cập nhật mới.
- Thông báo kiểm tra cập nhật, mất kết nối và vị trí game rõ ràng hơn.
- Điều chỉnh cửa sổ theo vùng làm việc của màn hình; tôn trọng tùy chọn giảm chuyển động của Windows.

## 0.3.0

- Tải và cập nhật pack ngôn ngữ từ GitHub, xác minh chữ ký phát hành.

## 0.2.0

- Giao diện BeroLauncher dành cho người chơi; tách cài đặt khỏi màn hình game.
