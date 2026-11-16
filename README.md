# BeroLauncher

Launcher cộng đồng cho Stella Sora trên Windows x64, với giao diện tiếng Việt và font Lexend.

## Chức năng hiện có

- Chọn vị trí game, mở game.
- Tự kiểm tra bản Việt hóa mới từ GitHub khi mở launcher.
- Tải pack và xác minh chữ ký phát hành, kích thước và SHA-256 trước khi cài.
- Bật/tắt Việt hóa; bản đã tải được giữ lại để dùng khi mất mạng.
- Kiểm tra phiên bản dữ liệu game trước khi cài.
- Sao lưu bản gốc và hoàn tác khi thay thế dữ liệu thất bại.

Màn hình chính dành cho người chơi; cài đặt vị trí game và ngôn ngữ nằm trong **Cài đặt**. Đây không phải launcher chính thức của Yostar.

## Tình trạng cập nhật online

Từ bản **0.3.0**, launcher nhận pack từ kênh GitHub `lang-stable`. Khi có bản mới, chọn **Cập nhật Việt hóa**; các nút **Bật Việt hóa**, **Tắt Việt hóa** và **Kiểm tra cập nhật** nằm trong Cài đặt. Xem [luồng phát hành](docs/UPDATES.md).

Repo này chỉ chứa launcher và các kiểm thử. Web dịch, database, dữ liệu game gốc và thông tin xác thực không nằm trong repo. Pack ngôn ngữ được phân phối riêng qua GitHub Releases, không commit vào lịch sử Git.

## Chạy từ mã nguồn

Cần Windows và .NET SDK 9 có hỗ trợ Windows Desktop.

```powershell
dotnet run --project launcher/StellaViet.Launcher.csproj
dotnet run --project launcher-tests/Launcher.Tests.csproj
```

Để đóng bản chạy độc lập, người nhận không cần cài .NET:

```powershell
dotnet publish launcher/StellaViet.Launcher.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o artifacts/BeroLauncher
```

Launcher tải gói tương thích gồm `release.json`, `ui.bin` và `story.bin` từ địa chỉ GitHub được ký xác minh. Có thể cung cấp gói local trong thư mục `release` cạnh `BeroLauncher.exe` cho chế độ offline. Mã nguồn và bản sao lưu game không cần gửi cho người chơi.

Nếu đã bật Việt hóa bằng công cụ khác, hãy tắt bằng công cụ đó trước lần đầu cài qua BeroLauncher. Không ghi đè bản game chưa được hỗ trợ.

## Phát hành launcher

Tag `launcher-v*` kích hoạt workflow dựng, kiểm thử và tạo bản phát hành GitHub. Tag launcher và tag pack ngôn ngữ được tách riêng để hai luồng cập nhật không lẫn nhau.

## Giấy phép

Mã nguồn theo [GPL-3.0](LICENSE). Font Lexend theo [SIL Open Font License](ui/fonts/OFL-Lexend.txt). Tên và tài sản game thuộc chủ sở hữu tương ứng.
