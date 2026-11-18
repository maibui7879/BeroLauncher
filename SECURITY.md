# Bảo mật BeroLauncher

Không gửi khóa riêng, token GitHub hoặc báo cáo chưa che thông tin nhạy cảm vào issue công khai. Dùng mục **Security → Report a vulnerability** nếu kho đã bật private vulnerability reporting.

## Phạm vi bảo vệ

Gói ngôn ngữ phải có `channel.json` được ký bằng khóa tin cậy nhúng trong launcher. Launcher xác minh chữ ký, URL phát hành cố định, kích thước và SHA-256 khi tải, rồi xác minh lại **trước mỗi lần cài**, kể cả gói đã lưu trong cache. Manifest dùng để cài là đúng các byte vừa xác minh; payload được giữ mở chỉ đọc trong lúc kiểm tra và dựng. Gói không có chữ ký hoặc bị thay manifest/payload sẽ bị từ chối trước khi ghi vào game.

Gói cache cũ không có `channel.json` cần được tải lại bằng **Kiểm tra cập nhật**. Không tắt xác minh để tiếp tục dùng gói cache không xác thực. Gói chỉ chứa manifest và payload dữ liệu; không có bước chạy script hay chương trình lấy từ gói.

Hash chỉ kiểm tra toàn vẹn; chữ ký mới xác nhận nguồn. Nếu tài khoản phát hành, khóa riêng hoặc bản thân máy người chơi bị chiếm quyền thì các kiểm tra này không đủ bảo vệ. Các archive game có thể chứa Lua, nên khóa ký gói cũng là ranh giới tin cậy cho nội dung script. Không đưa khóa riêng lên repo hoặc GitHub Actions.

## Thiết lập GitHub cần bật bởi chủ kho

Các tệp trong repo không tự bật những thiết lập này:

1. Bật 2FA/passkey; rà soát collaborator, deploy key và ứng dụng có quyền ghi. Token phát hành chỉ cấp quyền cần thiết cho kho này.
2. Tạo ruleset cho `main`: bắt buộc pull request, CODEOWNER review, kiểm thử job `build` thành công; chặn force-push và xóa nhánh. Không bật bypass rộng cho token phát hành ngôn ngữ.
3. Bảo vệ tag `launcher-v*`, `launcher-stable`, `lang-v*` và `lang-stable`; chỉ cho tài khoản phát hành tin cậy tạo/cập nhật. Publisher cần đổi asset của hai kênh stable; cân nhắc quyền này khi cấu hình.
4. Tạo environment `launcher-release`, cấu hình người duyệt và chỉ cho tag launcher hợp lệ triển khai. Workflow đã tham chiếu environment này; **tên environment không tự tạo yêu cầu duyệt**.
5. Đặt quyền mặc định Actions là read-only, chỉ cho Actions được duyệt; bật secret scanning, push protection và private vulnerability reporting nếu khả dụng.
6. Giữ nguyên pin SHA của Actions; xem xét PR Dependabot trước khi cập nhật. Review đặc biệt cẩn thận mọi thay đổi workflow, khóa công khai, bộ cập nhật và bộ cài.
7. Ký kênh launcher bằng `tools/publish-launcher-channel.mjs` trên máy phát hành được bảo vệ. Giữ khóa riêng ngoài repo và GitHub Actions; dùng `GH_TOKEN` cục bộ chỉ với quyền Contents cần thiết.

## Kiểm tra trước khi phát hành

Chạy `dotnet run --project launcher-tests/Launcher.Tests.csproj -c Release`, dựng launcher từ commit đã review rồi phát hành phiên bản launcher mới. Thay đổi bảo mật trong mã nguồn không tự cập nhật các file EXE đã phát hành. Người dùng cần tải bản launcher mới để được bảo vệ; cập nhật gói ngôn ngữ không thay thế launcher.
