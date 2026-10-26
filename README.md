# Mây Album

Mây Album là một web app nhỏ gọn để studio tạo album ảnh riêng tư từ thư mục Google Drive, gửi liên kết cho khách hàng và nhận lại danh sách ảnh khách yêu thích.

- Giao diện tiếng Việt, tông màu kem – mực – terracotta.
- Tạo album từ thư mục Google Drive công khai (có thể đọc cả thư mục con).
- Mỗi album có một liên kết riêng dạng `/a/{token}`.
- Khách xem ảnh, thả tim, tìm nhanh bằng “mã hình”, ghi chú và gửi lựa chọn.
- Studio xem lựa chọn, ghi chú, thời điểm gửi, xuất CSV và quản lý trạng thái chia sẻ.
- Sao chép nhanh mã ảnh khách đã chọn và lọc đúng tệp RAW/JPG từ thư mục trên máy tính, xử lý hoàn toàn trong trình duyệt.
- Bảng công việc kiểu Trello: kéo thả thẻ giữa các danh sách, nhãn màu, checklist, hạn chót và tùy chỉnh nền bảng.
- Tài khoản riêng cho từng thành viên với hai vai trò quản trị viên/nhân viên; mỗi thẻ có nhật ký ghi lại người tạo, sửa hoặc kéo thẻ.
- Có sẵn hai album mẫu để thử ngay, không cần Google Drive.

## Chạy ứng dụng

Yêu cầu: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet run
```

Mở http://localhost:5080.

Lần chạy đầu tiên, ứng dụng sẽ tạo `data/store.json` và nạp hai album mẫu:

| Album | Liên kết |
| --- | --- |
| Ngày Mình Chung Đôi | http://localhost:5080/a/demo-ngay-chung-doi |
| Biển Nhớ | http://localhost:5080/a/demo-bien-nho |

Trang quản trị: http://localhost:5080/admin

Tài khoản quản trị mặc định: `admin` · mật khẩu `studio2026`.

> Hãy đổi mật khẩu này ngay trong **Cài đặt studio → Tài khoản studio** trước khi đưa ứng dụng lên mạng. Xem phần [Bảo mật](#bảo-mật).

## Thu chi, xuất Excel và tên studio

Trong **Tài chính & lương → Thu chi**, bạn có thể tạo, sửa hoặc xoá khoản
thu/chi theo tháng; lọc **Tất cả / Thu / Chi**; nhập ghi chú và gắn tối đa
6 ảnh chứng từ cho mỗi khoản. Mỗi ảnh JPG/PNG/WebP tối đa 5 MB và có chú
thích riêng. Có thể sửa chú thích hoặc bỏ ảnh trong biểu mẫu chỉnh sửa.
Nếu tải ảnh lỗi giữa chừng, giữ biểu mẫu và bấm lưu để thử lại các ảnh còn
lại; các khoản và ảnh đã lưu thành công không được gửi lại.

- **Thu chi → Xuất Excel**: xuất tháng và bộ lọc đang chọn, kèm ghi chú,
  tên ảnh/chú thích và tổng thu, tổng chi, chênh lệch. Ảnh không được nhúng
  vào tệp Excel.
- **Tài chính & lương → Tổng quan / Bảng lương → Xuất Excel**: tải workbook
  gồm ba sheet Tổng quan, Bảng lương và Thu chi của tháng đang chọn.
- **Hợp đồng → Xuất Excel**: xuất danh sách theo trạng thái và từ khoá hiện tại.
- **Cài đặt studio → Thông tin studio**: đổi tên studio và lưu. Tên mới có
  hiệu lực ngay trong phiên hiện tại và vẫn giữ khi tải lại trang.

Thu nhập ghi tay được tính là **Thu khác**, tách khỏi thanh toán đơn hàng
và hợp đồng; tránh ghi lại cùng một khoản thanh toán ở cả hai nơi. Các khoản
chi cũ được giữ nguyên là khoản chi khi nâng cấp dữ liệu lên schema 8.

Ảnh chứng từ nằm trong `data/finance-photos/` và chỉ người có quyền tài chính
mới truy cập được. Khi sao lưu hoặc chuyển máy chủ, giữ **cả thư mục dữ liệu**,
bao gồm `store.json` và `finance-photos/`.

Xem [kiểm thử tài chính](tests/README.md) để chạy lại kiểm thử API và trình duyệt
trên dữ liệu thử riêng.

## Kết nối Google Drive

Ứng dụng dùng Google Drive API v3 với một API key, chỉ đọc được các thư mục được chia sẻ công khai.

1. Mở [Google Cloud Console](https://console.cloud.google.com/) và tạo một project.
2. Bật **Google Drive API** cho project.
3. Vào **APIs & Services → Credentials**, chọn **Create credentials → API key**.
4. Nên giới hạn key theo IP hoặc website để tránh bị lạm dụng.
5. Dán key vào **Cài đặt studio → Google Drive API** trong trang quản trị, hoặc cấu hình trong `appsettings.json`:

```json
{
  "GoogleDrive": {
    "ApiKey": "AIza..."
  }
}
```

Có thể dùng biến môi trường thay vì sửa tệp:

```powershell
$env:GoogleDrive__ApiKey = "AIza..."
```

Thư mục ảnh phải được chia sẻ ở chế độ **“Bất kỳ ai có liên kết”**. Khi tạo album, dán liên kết dạng:

```text
https://drive.google.com/drive/folders/1AbCdEf...
```

Ứng dụng cũng chấp nhận riêng ID thư mục. Ảnh lấy trực tiếp từ Drive qua ảnh thu nhỏ của Google, không tải toàn bộ ảnh về máy chủ.

## Quy trình sử dụng

### Tạo album

1. Đăng nhập `/admin`.
2. Chọn **Tạo album mới**.
3. Dán liên kết thư mục Drive và bấm **Xem trước** để kiểm tra ảnh, số lượng và chọn ảnh bìa.
4. Điền tên album, khách hàng, ngày chụp, địa điểm và lời nhắn.
5. Chọn các tuỳ chọn: nhận lựa chọn, cho phép tải ảnh, chia sẻ ngay hay lưu bản nháp.
6. Bấm **Tạo album**, sau đó sao chép liên kết gửi cho khách.

Mã hình được tạo tự động từ tên tệp. Ví dụ `LM-001.jpg` → `LM-001`. Nếu tên tệp không có mã, ứng dụng sẽ tạo mã duy nhất từ tên tệp.

### Phía khách hàng

Khách mở liên kết riêng, xem album và:

- thả tim ảnh muốn chọn;
- tìm theo mã hình hoặc tên tệp;
- lọc **Tất cả** / **Đã chọn**;
- mở ảnh lớn, xem trước và tải ảnh nếu studio bật quyền tải;
- ghi chú và gửi lựa chọn;
- quay lại chỉnh sửa và gửi lại sau khi đã gửi.

Lựa chọn được lưu trên máy chủ theo từng album. Mỗi album nên dành cho một khách hoặc một nhóm khách dùng chung một liên kết; ứng dụng không tách lựa chọn theo từng người dùng.

### Quản lý album

Trong trang chi tiết album, studio có thể:

- xem ảnh khách đã chọn và ghi chú;
- bật/tắt nhận lựa chọn, cho phép tải ảnh, chế độ chia sẻ;
- sửa thông tin album;
- đổi ảnh bìa;
- đồng bộ lại thư mục Drive (giữ nguyên lựa chọn cũ);
- sao chép nhanh mã ảnh khách đã chọn;
- mở công cụ lọc ảnh trên máy tính với album đã chọn sẵn;
- tải CSV danh sách ảnh khách đã chọn;
- đổi liên kết chia sẻ (liên kết cũ mất hiệu lực ngay);
- xoá album.

### Bảng công việc

Trang **Bảng công việc** (`/admin/board`) giữ nhịp sản xuất của studio trên một bảng kiểu Trello. Lần chạy đầu tiên sẽ nạp sẵn một bảng mẫu với năm danh sách: Ý tưởng, Chuẩn bị, Đang chụp, Hậu kỳ và Đã giao khách.

- Kéo thả thẻ giữa các danh sách hoặc trong cùng một danh sách để đổi thứ tự. Trên điện thoại hoặc khi dùng bàn phím, mở thẻ và chọn danh sách trong mục **Danh sách**.
- Mỗi thẻ có tên, mô tả, hạn chót, trạng thái hoàn tất hạn, nhãn màu, checklist và album liên kết. Tiến độ checklist hiển thị ngay trên thẻ và trên thanh tiến độ của bảng.
- Tìm thẻ theo tên, nhãn hoặc album; lọc theo **Quá hạn**, **7 ngày tới**, **Chưa có hạn** và theo từng nhãn. Nút **Xoá bộ lọc** đưa bảng về trạng thái đầy đủ.
- Thêm thẻ nhanh ở cuối mỗi danh sách: nhập tên rồi nhấn **Enter** (Shift + Enter để xuống dòng). Nút **Thêm thẻ** trên đầu trang mở đầy đủ biểu mẫu.
- Menu của mỗi danh sách cho phép đổi tên, đổi màu, di chuyển sang trái/phải hoặc xoá. Danh sách mới thêm ở cuối bảng.
- **Tùy chỉnh bảng** đổi tên, mô tả, chọn một trong năm nền (Mực ấm, Đất nung, Vườn chiều, Chiều muộn, Giấy kraft), chỉnh **màu nền và màu chữ của thẻ** (có xem trước và nút đặt lại mặc định) và quản lý bộ nhãn dùng chung. Màu thẻ áp dụng cho toàn bảng; mặc định là nền sáng, chữ mực đậm để luôn dễ đọc.
- Mỗi thẻ có **Nhật ký hoạt động**: ai đã tạo, sửa hoặc kéo thẻ, thời điểm và các thay đổi chi tiết (tên, mô tả, hạn, nhãn, checklist, album, danh sách). Nhấn **Chi tiết** để mở danh sách thay đổi. Thẻ cũ bắt đầu với nhật ký trống; mỗi thẻ giữ tối đa 60 hoạt động gần nhất.
- Bảng được lưu trong `data/store.json` cùng album, tài khoản và cài đặt studio. Dữ liệu phiên bản 1 hoặc 2 được tự động nâng cấp lên phiên bản 3 khi khởi động, không làm mất album, thẻ hay cài đặt cũ.
- Khi xoá một album, liên kết album trên các thẻ liên quan cũng được gỡ bỏ; thẻ vẫn giữ nguyên.

### Tài khoản studio

Trang **Cài đặt studio** (`/admin/settings`) chỉ dành cho quản trị viên. Tại mục **Tài khoản studio**, quản trị viên có thể:

- tạo tài khoản mới với tên hiển thị, tên đăng nhập, vai trò và mật khẩu;
- sửa tên hiển thị, tên đăng nhập hoặc vai trò;
- đặt lại mật khẩu khi thành viên quên;
- tạm khoá hoặc kích hoạt lại tài khoản mà vẫn giữ lịch sử thẻ.

Tên đăng nhập gồm 3–32 ký tự thường không dấu (`a-z`, `0-9`, dấu chấm, gạch ngang, gạch dưới). Mật khẩu dài 8–128 ký tự và được băm bằng PBKDF2-HMAC-SHA256 trước khi lưu. Mỗi người nên dùng tài khoản riêng để nhật ký thẻ ghi đúng người thao tác.

Hai vai trò:

- **Quản trị viên** xem được mọi trang, quản lý tài khoản và cài đặt studio.
- **Nhân viên** dùng được tổng quan, bảng công việc, album và công cụ lọc ảnh, nhưng không thấy trang Cài đặt.

Không thể tự đổi vai trò, tự tạm khoá tài khoản đang đăng nhập hoặc làm mất quản trị viên hoạt động cuối cùng. Tài khoản bị tạm khoá sẽ bị đăng xuất ngay.

### Lọc ảnh theo mã khách chọn

Trang **Lọc ảnh theo mã** (`/admin/tools`) giúp studio lấy đúng các tệp ảnh gốc tương ứng với những ảnh khách đã thả tim.

1. Mở **Lọc ảnh theo mã** từ thanh bên, tổng quan hoặc trang chi tiết album.
2. Chọn album để tự điền mã, hoặc dán mã thủ công. Có thể sao chép nhanh theo định dạng xuống dòng, dấu phẩy, dấu cách hoặc kèm đuôi tệp.
3. Chọn chế độ so khớp **Chữ và số** (mặc định) hoặc **Chỉ lấy số**.
4. Chọn **thư mục ảnh gốc** trên máy tính, đặt tên thư mục con nhận ảnh (hoặc chọn thư mục lưu riêng), chọn quét thư mục con và định dạng **RAW & JPG / Chỉ RAW / Chỉ JPG-PNG**.
5. Bấm **Bắt đầu lọc ảnh từ máy tính**.

Ảnh gốc được giữ nguyên. Ảnh khớp được sao chép vào thư mục nhận; nếu tên đã tồn tại, ứng dụng thêm hậu tố `-2`, `-3`… để không ghi đè. Thư mục nhận được bỏ qua khi quét để tránh tự sao chép lại.

Công cụ dùng File System Access API nên cần **Chrome hoặc Edge trên máy tính** và một secure context (`localhost` hoặc HTTPS). Ảnh được đọc và ghi trực tiếp trên máy; Mây Album không tải ảnh lên máy chủ.

## Cấu hình

Các giá trị chính trong `appsettings.json`:

| Khoá | Mặc định | Ý nghĩa |
| --- | --- | --- |
| `AdminCode` | `studio2026` | Mật khẩu khởi tạo cho tài khoản quản trị `admin` khi dữ liệu chưa có tài khoản |
| `GoogleDrive:ApiKey` | rỗng | API key Google Drive |
| `Data:Directory` | `data` | Thư mục chứa `store.json` |

`AdminCode` chỉ được dùng để tạo tài khoản quản trị đầu tiên. Sau khi dữ liệu đã có tài khoản, đổi biến này không đổi mật khẩu đang dùng; hãy đặt lại mật khẩu trong **Cài đặt studio → Tài khoản studio**.

Ví dụ đặt mật khẩu quản trị ban đầu bằng biến môi trường:

```powershell
$env:AdminCode = "mat-khau-rieng-cua-ban"
dotnet run
```

Đổi cổng chạy:

```powershell
$env:ASPNETCORE_URLS = "http://localhost:5090"
dotnet run
```

## Dữ liệu

- Toàn bộ dữ liệu nằm trong `data/store.json`, gồm album, cài đặt studio, tài khoản và bảng công việc.
- Tệp dùng `schemaVersion` 3. Khi mở dữ liệu phiên bản 1 hoặc 2, ứng dụng tự nâng cấp, thêm bảng mẫu hoặc tài khoản quản trị ban đầu và giữ nguyên album, thẻ, lựa chọn cùng cài đặt hiện có. Nhật ký của thẻ cũ bắt đầu trống.
- Mật khẩu chỉ được lưu dưới dạng băm PBKDF2-HMAC-SHA256 (120.000 vòng, salt riêng cho từng tài khoản); không có mật khẩu dạng văn bản thuần trong `store.json`.
- Ghi tệp theo kiểu atomic: ghi tệp tạm rồi thay thế, tránh hỏng dữ liệu khi đang ghi.
- Để khôi phục dữ liệu mẫu, dừng ứng dụng, xoá `data/store.json` rồi chạy lại.
- Nên sao lưu `data/store.json` định kỳ.
- Google API key nhập trong trang Cài đặt được lưu trong `store.json` và ghi đè key trong `appsettings.json`. Tệp này chứa dữ liệu riêng tư, không nên commit hoặc chia sẻ công khai.

## Bảo mật

Đây là ứng dụng một studio, chạy trên một máy chủ, không phải hệ thống multi-tenant.

Trước khi triển khai công khai:

1. **Đổi mật khẩu tài khoản `admin`** trong **Cài đặt studio → Tài khoản studio** và tạo tài khoản riêng cho từng thành viên. Đừng dùng chung một tài khoản.
2. Chạy sau HTTPS (reverse proxy như Caddy, Nginx hoặc IIS). Cookie phiên chỉ được đặt `Secure` khi request là HTTPS.
3. Giới hạn Google API key theo IP/website và chỉ bật Drive API.
4. Bảo vệ thư mục `data/` và sao lưu thường xuyên. Tệp chứa hash mật khẩu và API key.
5. Cân nhắc thêm rate limit cho `/api/auth/login` ở reverse proxy. Ứng dụng chỉ có độ trễ 320 ms cho mỗi lần nhập sai, không có khoá tài khoản.
6. Liên kết album là bearer token: ai có liên kết đều xem được album. Dùng **Đổi liên kết chia sẻ** khi cần thu hồi quyền truy cập.
7. Phiên đăng nhập lưu trong bộ nhớ; khởi động lại máy chủ sẽ đăng xuất tất cả phiên.
8. Trang Cài đặt và các API quản lý tài khoản chỉ cho vai trò quản trị viên. Tài khoản bị tạm khoá mất hiệu lực ngay cả khi đang có phiên.

## Triển khai

```powershell
dotnet publish -c Release -o publish
```

Chạy bản publish:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Production"
$env:AdminCode = "ma-rieng-cua-ban"
$env:GoogleDrive__ApiKey = "AIza..."
dotnet .\publish\MayAlbum.dll
```

Nên đặt ứng dụng sau reverse proxy có HTTPS và giữ nguyên thư mục `data/` khi nâng cấp.

`AdminCode` chỉ dùng một lần để tạo tài khoản `admin` đầu tiên khi `data/store.json` chưa có người dùng. Khi đã có tài khoản, biến này không còn tác dụng; muốn đổi mật khẩu quản trị, hãy đặt lại trong **Cài đặt studio → Tài khoản studio**.

## Cấu trúc dự án

```text
Api/                 endpoint công khai, quản trị và đăng nhập
Models/              model dữ liệu, DTO, session store
Services/            JSON store, Google Drive, dữ liệu mẫu, tiện ích
wwwroot/
  assets/css/        styles.css, admin.css, album.css
  assets/js/         app.js (router), admin.js, board.js, tools.js, album.js, api.js, ui.js
  assets/img/demo/   ảnh mẫu và thumbnail
  assets/fonts/      font self-hosted
  index.html         điểm vào của SPA
data/store.json      dữ liệu studio (tự tạo)
Program.cs           cấu hình ASP.NET Core, CSP, static files, fallback
```

Frontend là HTML/CSS/ES module thuần, không cần Node.js hay bước build.

## Giới hạn hiện tại

- Không gửi email tự động; studio sao chép liên kết và gửi thủ công.
- Chưa có tải toàn bộ album dạng ZIP; chỉ tải từng ảnh hoặc xuất CSV lựa chọn.
- Công cụ lọc ảnh cần Chrome/Edge trên máy tính và quyền truy cập thư mục; Firefox và Safari chưa hỗ trợ File System Access API.
- Kết quả lọc ảnh chỉ được ghi vào thư mục do người dùng chọn trên máy, không đồng bộ ngược lên album.
- Drive API key chỉ đọc được thư mục công khai, không hỗ trợ thư mục riêng tư hoặc tài khoản Drive được uỷ quyền.
- Bố cục gallery dùng CSS multi-column nên thứ tự hiển thị theo cột; thứ tự này có thể khác thứ tự đọc từng hàng.
- Dữ liệu JSON phù hợp với một studio và số lượng album vừa phải; nếu mở rộng nhiều studio/nhiều người dùng đồng thời, nên chuyển sang cơ sở dữ liệu thật.
