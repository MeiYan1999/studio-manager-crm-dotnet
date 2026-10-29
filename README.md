# StudioCore

StudioCore là ứng dụng quản lý một studio: album ảnh từ Google Drive và lựa chọn của khách hàng, CRM, lịch chụp, công việc, bán hàng, tài chính và vận hành. Backend là ASP.NET Core Minimal API (.NET 8); frontend dùng HTML/CSS/JavaScript ES modules thuần, không có bước build Node.js.

- Giao diện tiếng Việt, tông màu kem – mực – terracotta.
- Tạo album từ thư mục Google Drive công khai (có thể đọc cả thư mục con).
- Mỗi album có một liên kết riêng dạng `/a/{token}`.
- Khách xem ảnh, thả tim, tìm nhanh bằng “mã hình”, ghi chú và gửi lựa chọn.
- Studio xem lựa chọn, ghi chú, thời điểm gửi, xuất CSV và quản lý trạng thái chia sẻ.
- Sao chép nhanh mã ảnh khách đã chọn và lọc đúng tệp RAW/JPG từ thư mục trên máy tính, xử lý hoàn toàn trong trình duyệt.
- Bảng công việc kiểu Trello: kéo thả thẻ giữa các danh sách, nhãn màu, checklist, hạn chót và tùy chỉnh nền bảng.
- Tài khoản riêng cho từng thành viên với hai vai trò quản trị viên/nhân viên; mỗi thẻ có nhật ký ghi lại người tạo, sửa hoặc kéo thẻ.
- Có sẵn hai album mẫu để thử ngay, không cần Google Drive.

## Kiến trúc và các phân hệ

- `Api/`: Minimal API cho auth, album công khai, quản trị, CRM, bán hàng và vận hành.
- `Models/`: dữ liệu, request/DTO, session trong RAM và endpoint filters kiểm tra quyền.
- `Services/DataStore.cs`: singleton giữ trạng thái trong RAM, lưu JSON local hoặc một bản ghi JSONB trong PostgreSQL qua Npgsql 8.0.6; không dùng Entity Framework.
- `wwwroot/assets/js/app.js`: router phía trình duyệt. `api.js` gọi API cùng origin; các module còn lại dựng giao diện theo phân hệ.
- `ObjectStorage`: local hoặc S3 tương thích (ký AWS Signature V4); `GoogleDriveService`: Drive API v3 cho thư mục công khai.
- CRM: khách hàng, gói dịch vụ, nhân sự, lịch chụp và nhật ký studio.
- Bán hàng: đơn hàng, thanh toán, hóa đơn, hợp đồng, lịch thanh toán và báo cáo.
- Vận hành: thu chi, chứng từ, bảng lương, chấm công, phân ca tuần, trang phục, concept và phiếu thuê/trả.
- Quản trị: tài khoản admin/employee, quyền theo phân hệ, bắt buộc đổi mật khẩu, nội dung website, logo và ảnh đăng nhập.
- Nhập backup CRM có xem trước (`DryRun`) và tùy chọn xử lý bản ghi hiện có. Thông báo nằm trong ứng dụng; quét quá hạn được kích hoạt khi gọi API thông báo, không có background scheduler.

## Chạy ứng dụng

Yêu cầu: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```powershell
dotnet restore StudioCore.csproj
dotnet run --project StudioCore.csproj
```

Mở http://localhost:5080, quản trị tại `/admin`, kiểm tra server tại `/api/health`.
Profile trong `Properties/launchSettings.json` dùng môi trường Development và cổng 5080.
Để chọn cổng bằng `ASPNETCORE_URLS`, chạy với `--no-launch-profile`.
Không cần Node.js để chạy ứng dụng; Python/Node chỉ phục vụ các script kiểm thử.

Chạy local mặc định dùng `data/`. Nếu shell có `ConnectionStrings__Postgres` hoặc
`DATABASE_URL`, ứng dụng sẽ kết nối database đó ngay khi khởi động. Không dùng cấu hình
production khi thử nghiệm: startup có thể tạo bảng, nhập JSON và nâng cấp dữ liệu.
Xem [kiểm thử backend](tests/README.md) để chạy server với dữ liệu tạm tự động.

## Đưa lên Render

Repository có sẵn `Dockerfile` và `render.yaml`. Trên Render, chọn **New → Blueprint**,
kết nối repository này và nhập một mật khẩu mạnh cho biến bí mật `AdminCode`.
Blueprint tạo web service và PostgreSQL. Ảnh chứng từ được lưu trong object storage
tương thích S3 như Cloudflare R2; Render sẽ yêu cầu các thông tin bucket khi tạo Blueprint.

Với Cloudflare R2, tạo một bucket riêng tư và một API token có quyền **Object Read & Write**
cho đúng bucket đó. Điền endpoint dạng `https://<ACCOUNT_ID>.r2.cloudflarestorage.com`,
tên bucket, Access Key ID và Secret Access Key vào các biến Render tương ứng. Không bật
Public Development URL; ứng dụng đọc ảnh qua API đã xác thực.

Sau khi deploy xong, mở địa chỉ Render cấp và đăng nhập bằng tài khoản `admin` cùng
mật khẩu đã nhập ở `AdminCode`. Đổi mật khẩu trong **Cài đặt studio → Tài khoản studio**
ngay sau lần đăng nhập đầu tiên.

Lần chạy đầu tiên nếu chưa có dữ liệu và chưa cấu hình PostgreSQL, ứng dụng tạo `data/store.json` và nạp hai album mẫu:

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

Ảnh chứng từ chỉ truy cập qua API có quyền tài chính. Với storage local, ảnh nằm trong
`<Data:Directory>/finance-photos/`; với provider `s3`, nội dung ảnh nằm trong bucket riêng tư.
Sao lưu cần giữ cả dữ liệu nghiệp vụ và các object/file ảnh tương ứng; xem mục Dữ liệu.

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
- Bảng được lưu trong `data/store.json` cùng album, tài khoản và cài đặt studio. Dữ liệu cũ được tự động nâng cấp qua các bước migration đến phiên bản 8 khi khởi động, không làm mất album, thẻ hay cài đặt cũ.
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
- **Nhân viên** mặc định dùng tổng quan, bảng công việc, album và công cụ lọc ảnh. Quản trị viên có thể cấp quyền riêng cho CRM, bán hàng, tài chính, chấm công, trang phục, thông báo và nhật ký. Danh sách quyền đã cấu hình rỗng nghĩa là không có quyền phân hệ. Cài đặt studio và quản lý tài khoản vẫn yêu cầu vai trò admin ở backend.

Không thể tự đổi vai trò, tự tạm khoá tài khoản đang đăng nhập hoặc làm mất quản trị viên hoạt động cuối cùng. Tài khoản bị tạm khoá sẽ bị đăng xuất ngay.

### Lọc ảnh theo mã khách chọn

Trang **Lọc ảnh theo mã** (`/admin/tools`) giúp studio lấy đúng các tệp ảnh gốc tương ứng với những ảnh khách đã thả tim.

1. Mở **Lọc ảnh theo mã** từ thanh bên, tổng quan hoặc trang chi tiết album.
2. Chọn album để tự điền mã, hoặc dán mã thủ công. Có thể sao chép nhanh theo định dạng xuống dòng, dấu phẩy, dấu cách hoặc kèm đuôi tệp.
3. Chọn chế độ so khớp **Chữ và số** (mặc định) hoặc **Chỉ lấy số**.
4. Chọn **thư mục ảnh gốc** trên máy tính, đặt tên thư mục con nhận ảnh (hoặc chọn thư mục lưu riêng), chọn quét thư mục con và định dạng **RAW & JPG / Chỉ RAW / Chỉ JPG-PNG**.
5. Bấm **Bắt đầu lọc ảnh từ máy tính**.

Ảnh gốc được giữ nguyên. Ảnh khớp được sao chép vào thư mục nhận; nếu tên đã tồn tại, ứng dụng thêm hậu tố `-2`, `-3`… để không ghi đè. Thư mục nhận được bỏ qua khi quét để tránh tự sao chép lại.

Công cụ dùng File System Access API nên cần **Chrome hoặc Edge trên máy tính** và một secure context (`localhost` hoặc HTTPS). Ảnh được đọc và ghi trực tiếp trên máy; StudioCore không tải ảnh lên máy chủ.

## Cấu hình

Cấu hình được đọc qua ASP.NET Core configuration (JSON và biến môi trường). Dấu `:` trong tên khóa tương ứng `__` khi đặt biến môi trường. Không đưa credentials thật vào mã nguồn hoặc tài liệu.

| Khoá | Mặc định | Ý nghĩa |
| --- | --- | --- |
| `AdminCode` | mật khẩu phát triển mặc định | Mật khẩu bootstrap và mã đăng nhập tương thích cũ; phải đặt riêng khi triển khai |
| `GoogleDrive:ApiKey` | rỗng | API key Google Drive |
| `Data:Directory` | `data` | Thư mục chứa `store.json` |
| `ConnectionStrings:Postgres` | rỗng | Chuỗi kết nối PostgreSQL; khi có giá trị, PostgreSQL thay cho `store.json` |
| `Storage:Provider` | `local` | Đặt `s3` để lưu ảnh chứng từ, logo và ảnh đăng nhập trong object storage |
| `Storage:Endpoint` | rỗng | S3 endpoint, ví dụ endpoint bucket Cloudflare R2 |
| `Storage:Region` | xem cấu hình triển khai | Bắt buộc khi dùng S3; phải phù hợp nhà cung cấp |
| `Storage:Bucket` | rỗng | Tên bucket riêng tư |
| `Storage:AccessKey` | rỗng | Access key của bucket |
| `Storage:SecretKey` | rỗng | Secret key của bucket |

`AdminCode` tạo mật khẩu tài khoản admin đầu tiên. Đổi biến này không đổi password hash
của tài khoản đã có, nhưng **có đổi mã đăng nhập legacy sau khi khởi động lại**.
`POST /api/auth/login` hiện vẫn chấp nhận body có `code` khi không có `username` và đăng
nhập quản trị viên hoạt động đầu tiên. Đổi mật khẩu tài khoản không thu hồi mã legacy này.
Luồng này được ghi nhận để sửa riêng; bản cập nhật tài liệu/test không thay đổi auth.

`ConnectionStrings:Postgres` được ưu tiên trước `DATABASE_URL`. Khi biến thứ nhất được
đặt thành chuỗi rỗng, fallback `DATABASE_URL` không được dùng (code dùng toán tử `??`).
Các ví dụ chỉ dùng placeholder; cấu hình bí mật thật qua môi trường/secret manager.

Ví dụ đặt mật khẩu quản trị ban đầu bằng biến môi trường:

```powershell
$env:AdminCode = "mat-khau-rieng-cua-ban"
dotnet run
```

Đổi cổng chạy:

```powershell
$env:ASPNETCORE_URLS = "http://localhost:5090"
dotnet run --no-launch-profile
```

## Dữ liệu

- Không cấu hình PostgreSQL: toàn bộ trạng thái lưu trong `<Data:Directory>/store.json`.
- Có `ConnectionStrings:Postgres` hoặc fallback `DATABASE_URL`: ứng dụng tạo bảng
  `app_state` nếu chưa có, đọc/ghi toàn bộ trạng thái vào cột `data` kiểu JSONB tại `id = 1`.
  Đây chưa phải mô hình bảng quan hệ riêng cho từng thực thể.
- Nếu bản ghi PostgreSQL chưa tồn tại nhưng JSON local có sẵn, startup nhập JSON đó.
  Khi database đã có dữ liệu, PostgreSQL là nguồn chính; JSON local không được cập nhật như bản sao lưu.
- Migration hiện nâng cấp dữ liệu cũ đến schema **8** (model `AppData` còn khởi tạo giá trị 7;
  `DataStore` áp dụng migration khi mở). Các khoản chi cũ giữ loại `expense`.
- Startup còn normalize dữ liệu; sao lưu trước khi chạy phiên bản mới trên dữ liệu thật.
- Ghi local dùng file tạm rồi thay thế. Khóa trong RAM chỉ bảo vệ một tiến trình;
  callback ghi hoặc thao tác lưu lỗi không rollback trạng thái RAM đã thay đổi.
- Mỗi lần ghi serialize toàn bộ trạng thái. Không chạy nhiều instance ghi cùng database/file:
  các snapshot RAM độc lập có thể ghi đè cập nhật của nhau.
- Mật khẩu tài khoản lưu dưới dạng PBKDF2-HMAC-SHA256 với salt riêng. Google API key nhập
  qua Cài đặt được lưu trong trạng thái và ưu tiên hơn key cấu hình môi trường.
- Với local: sao lưu `store.json` và cả thư mục `finance-photos/` (chứa cả object branding).
  Với PostgreSQL/S3: sao lưu database và toàn bộ object tương ứng trong bucket; giữ cấu hình
  kết nối an toàn. Không coi import CRM là thay thế cho backup/restore toàn bộ hệ thống.
- Không commit `data/` hoặc bản sao lưu chứa thông tin khách hàng, hash mật khẩu hay API key.

## Bảo mật

Đây là ứng dụng một studio, chạy trên một máy chủ, không phải hệ thống multi-tenant.

Trước khi triển khai công khai:

1. **Đổi mật khẩu tài khoản `admin`** trong **Cài đặt studio → Tài khoản studio** và tạo tài khoản riêng cho từng thành viên. Đừng dùng chung một tài khoản.
2. Chạy sau HTTPS (reverse proxy như Caddy, Nginx hoặc IIS). Cookie phiên chỉ được đặt `Secure` khi request là HTTPS.
3. Giới hạn Google API key theo IP/website và chỉ bật Drive API.
4. Bảo vệ thư mục `data/` và sao lưu thường xuyên. Tệp chứa hash mật khẩu và API key.
5. Cân nhắc thêm rate limit cho `/api/auth/login` ở reverse proxy. Ứng dụng chỉ có độ trễ 320 ms cho mỗi lần nhập sai, không có khoá tài khoản.
6. Liên kết album là bearer token: ai có liên kết đều xem được album. Dùng **Đổi liên kết chia sẻ** khi cần thu hồi quyền truy cập.
7. Phiên có thời hạn 14 ngày, lưu trong RAM; restart đăng xuất tất cả. Đổi/reset mật khẩu hiện chưa thu hồi toàn bộ phiên cũ. Cookie dùng `HttpOnly`, `SameSite=Lax`; `Secure` phụ thuộc request HTTPS. Cần kiểm tra forwarded headers thực tế sau reverse proxy.
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
dotnet .\publish\StudioCore.dll
```

Nên đặt ứng dụng sau reverse proxy có HTTPS và giữ nguyên thư mục `data/` khi nâng cấp.

`AdminCode` vẫn được luồng đăng nhập legacy sử dụng; xem mục Cấu hình. Docker dùng .NET 8 và cổng 10000. `render.yaml` cấu hình Docker web service, PostgreSQL, S3 và health check `/api/health`, với auto deploy theo commit. README này không xác nhận trạng thái production thực tế.

## Cấu trúc dự án

```text
Api/                 endpoint công khai, quản trị và đăng nhập
Models/              model dữ liệu, DTO, session store
Services/            JSON store, Google Drive, dữ liệu mẫu, tiện ích
wwwroot/
  assets/css/        styles.css, admin.css, album.css
  assets/js/         app.js (router), admin.js, crm.js, sales.js, operations.js,
                     board.js, tools.js, album.js, api.js, ui.js
  assets/img/demo/   ảnh mẫu và thumbnail
  assets/fonts/      font self-hosted
  index.html         điểm vào của SPA
tests/               xUnit, runner API dữ liệu tạm, smoke API/S3 và test trình duyệt
.github/workflows/   CI backend (build, test, smoke và publish)
data/store.json      dữ liệu studio local (tự tạo khi không dùng PostgreSQL)
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
- Cả JSON local và PostgreSQL JSONB hiện phù hợp triển khai một studio/một instance. Mở rộng nhiều instance cần xử lý đồng bộ ghi; thêm PostgreSQL hiện tại chưa tự giải quyết vấn đề này.

## Build, test và CI

```bash
dotnet restore tests/StudioCore.Tests/StudioCore.Tests.csproj
dotnet build tests/StudioCore.Tests/StudioCore.Tests.csproj -c Release --no-restore
dotnet test tests/StudioCore.Tests/StudioCore.Tests.csproj -c Release --no-build
python3 tests/run_backend.py
dotnet publish StudioCore.csproj -c Release --no-restore -o publish
```

GitHub Actions tại `.github/workflows/backend.yml` chạy các bước trên khi push, pull request
hoặc chạy thủ công; không deploy. Unit test dùng xUnit, API smoke dùng thư viện chuẩn Python,
S3 dùng server giả lập local; không cần PostgreSQL, Google Drive hay credentials bên ngoài.
Xem [tests/README.md](tests/README.md) để biết phạm vi và cách chạy test trình duyệt riêng.

## Tên dự án StudioCore

Project/assembly/namespace hiện dùng `StudioCore`; project test dùng `StudioCore.Tests`.
Tên mặc định cho dữ liệu mới và fallback giao diện là StudioCore. Tên/logo đã lưu trong
database vẫn ưu tiên hiển thị; đổi tên dự án không ghi đè thông tin studio hiện có.
Tên service/database trong `render.yaml`, tên repository GitHub, URL Render và cookie/session
được giữ nguyên để không tạo lại tài nguyên hoặc thay đổi cấu hình đang triển khai. Dockerfile
đã trỏ đến `StudioCore.csproj` và `StudioCore.dll`.

## Gallery ảnh theo concept

Trang chủ thay hai thẻ album mẫu bằng 5 nhóm: Gia đình, Trẻ em, Sơ sinh, Mẹ bầu và Beauty.
Bấm vào thẻ để xem các bộ ảnh trong nhóm; bấm ảnh để mở ảnh lớn, dùng nút hoặc phím
mũi tên chuyển ảnh, Escape để đóng. Nhóm chưa có ảnh hiển thị trạng thái đang cập nhật.

Cấu hình bằng tài khoản quản trị viên:

1. Tạo hoặc nhập album ảnh trong **Album**, chọn ảnh bìa và bật chia sẻ album.
2. Mở **Tủ trang phục → Concept → Thêm concept/Sửa**.
3. Chọn **Nhóm concept trên trang chủ** và **Album ảnh của concept**.
4. Bật **Hiển thị bộ ảnh trên website** rồi lưu. Tên, mô tả concept và ảnh trong album
   đã chọn sẽ công khai; dữ liệu khách hàng, ghi chú, lựa chọn ảnh và mã chia sẻ không được trả trong API gallery.
5. Bấm **Xem trên website** để kiểm tra. Bỏ chọn hiển thị để gỡ bộ ảnh khỏi gallery.

Mỗi nhóm có thể có nhiều concept. Ảnh bìa nhóm lấy từ album của concept đang hiển thị đầu tiên.
Album bị ngừng chia sẻ, không còn tồn tại hoặc concept đang bảo trì sẽ không xuất hiện công khai.
Xóa album gỡ liên kết và tắt hiển thị các concept liên quan; không xóa bản thân concept.

Các concept cũ mặc định chưa công khai. Chỉ admin được cấu hình nhóm/album/công khai;
quyền quản lý concept nội bộ của nhân viên giữ nguyên. Các trường JSON bổ sung không đổi
schemaVersion. Hai album demo và đường dẫn cũ vẫn tồn tại, chỉ gỡ thẻ giới thiệu khỏi trang chủ.
`/#album-mau` tiếp tục dẫn đến khu vực concept để giữ tương thích liên kết cũ.

## Đổi ảnh banner trang chủ

Quản trị viên mở **Cài đặt studio → Trang chủ → Ảnh banner trang chủ**, chọn ảnh PNG,
JPG hoặc WebP tối đa 5 MB. Nên dùng ảnh ngang rộng từ 1600 px, đặt chủ thể ở giữa để
hiển thị tốt trên desktop và điện thoại. Ảnh được lưu ngay khi tải lên; không cần bấm
Lưu cài đặt. Có khung xem trước, liên kết Xem trang chủ và nút Dùng lại ảnh trang chủ mặc định.

Ảnh lưu qua storage local/S3 đang cấu hình, độc lập với logo và ảnh đăng nhập. Chỉ admin
được đổi/xóa ảnh; khách truy cập có thể đọc ảnh để hiển thị trang chủ. Metadata được bổ sung
vào settings JSON hiện có, không đổi schemaVersion hoặc tạo bảng database mới. Dữ liệu cũ
không có ảnh tùy chỉnh tiếp tục dùng ảnh demo mặc định.

## Preview local để review tính năng

Clone repository rồi chạy từ thư mục gốc:

```bash
python3 tools/start-local-review.py
```

Script build source, mở trình duyệt tại localhost, tạo dữ liệu giả cho CRM, lịch chụp,
đơn hàng, hợp đồng, thu chi, chấm công và trang phục. Tài khoản `admin`/`review-staff`
cùng mật khẩu ngẫu nhiên được hiển thị trong Terminal. Nhấn Ctrl+C để dừng và xóa dữ liệu thử.
Có thể nhấp đúp `tools/Open-Local-Review.command` trên macOS.

Xem [hướng dẫn và checklist review](docs/LOCAL-REVIEW.md) và
[báo cáo tiếp nhận dự án](docs/PROJECT-HANDOVER.md).

Dữ liệu production không nằm trong GitHub: clone source không tải theo tên studio,
logo, khách hàng hoặc đơn hàng trên Render. Preview luôn dùng dữ liệu riêng; tên mặc định
StudioCore không ghi đè tên studio đã lưu trong database.

### Bảng giá trên website

Trang chủ có bảng giá năm nhóm từ nội dung mẫu. Vào **Gói Chụp → Bảng giá trên website**, chọn **Thêm nhóm**, **Thêm gói chụp**, hoặc mở nhóm và chọn **Sửa** để đổi tên, mô tả, giá, quyền lợi hoặc ẩn gói. Bảng giá website lưu riêng với gói dùng trong lịch chụp; sửa bảng giá không tự đổi giá lịch hay đơn hàng.
