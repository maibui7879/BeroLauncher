# BeroLauncher

Launcher cộng đồng cho Stella Sora trên Windows x64, với giao diện tiếng Việt và font Lexend.

## Chức năng hiện có

- Chọn vị trí game, mở game.
- Bật/tắt Việt hóa từ gói phát hành đi kèm.
- Kiểm tra phiên bản dữ liệu game trước khi cài.
- Sao lưu bản gốc và hoàn tác khi thay thế dữ liệu thất bại.

Màn hình chính dành cho người chơi; cài đặt vị trí game và ngôn ngữ nằm trong **Cài đặt**. Đây không phải launcher chính thức của Yostar.

## Tình trạng cập nhật online

**Chưa tích hợp tải pack trực tuyến trong bản 0.2.0.** Hiện launcher dùng thư mục `release` đi kèm. Luồng phát hành từ web local và đồng bộ pack đang được chuẩn bị; xem [kế hoạch phát hành](docs/UPDATES.md).

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

Gói Việt hóa tương thích gồm `release.json`, `ui.bin` và `story.bin`, đặt trong thư mục `release` cạnh `BeroLauncher.exe`. Mã nguồn và bản sao lưu game không cần gửi cho người chơi.

Nếu đã bật Việt hóa bằng công cụ khác, hãy tắt bằng công cụ đó trước lần đầu cài qua BeroLauncher. Không ghi đè bản game chưa được hỗ trợ.

## Phát hành launcher

Tag `launcher-v*` kích hoạt workflow dựng, kiểm thử và tạo bản phát hành GitHub. Tag launcher và tag pack ngôn ngữ được tách riêng để hai luồng cập nhật không lẫn nhau.

## Giấy phép

Mã nguồn theo [GPL-3.0](LICENSE). Font Lexend theo [SIL Open Font License](ui/fonts/OFL-Lexend.txt). Tên và tài sản game thuộc chủ sở hữu tương ứng.
