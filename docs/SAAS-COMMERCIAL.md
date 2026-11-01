# SaaS: kiến trúc, vận hành và cấu hình

## Kiến trúc hiện tại

ASP.NET Core 8 Minimal API + ES modules/CSS thuần. `DataStore` giữ nguyên giao diện
đọc/ghi để các phân hệ CRM, album, lịch, hợp đồng và tài chính tiếp tục dùng logic cũ.
Runtime chuyển sang `SaasDatabase` + EF Core 8: SQLite local hoặc PostgreSQL dùng
chung database/schema. Mỗi collection nghiệp vụ có bảng với khóa ghép `TenantId/Id`,
FK tới `Studios`, global query filter và kiểm tra tenant khi SaveChanges/SaveChangesAsync.
Ảnh/lựa chọn/dòng đơn hàng lồng nhau giữ trong cột JSON và được gắn TenantId khi ghi.
Thứ tự collection cũ được giữ trong metadata workspace, không phụ thuộc thứ tự khóa SQL.

`TenantWorkspace` giữ Settings, các bộ đếm, thứ tự collection và concurrency revision.
Mỗi lần ghi đọc lại workspace trong transaction; PostgreSQL khóa workspace bằng
`FOR UPDATE`. Cấp gói/quota/thư mời dùng transaction riêng khóa dòng Studio.
Xung đột trả 409 để client thử lại, không bỏ qua lỗi hay ghi đè snapshot cũ.

Các thành phần chính:

| Thành phần | Trách nhiệm |
| --- | --- |
| `Persistence/StudioDbContext.cs`, `Migrations/` | Mapping, filter, FK, migration hai provider |
| `Persistence/SaasDatabase.cs` | Import, transaction, adapter nghiệp vụ cũ |
| `Persistence/TenantResolverMiddleware.cs` | Tenant từ path, subdomain, custom domain |
| `Services/SubscriptionGuard.cs` | Ma trận quyền, quota, hết hạn, background job |
| `Services/SubscriptionBilling.cs` | Giá server, QR, xác thực webhook, chống ghi trùng |
| `Api/Saas*Endpoints.cs`, `InvitationEndpoints.cs` | API Settings/billing/backup/email |
| `wwwroot/assets/js/tenant.js`, `saas.js` | Điều hướng tenant, Settings và modal thanh toán |

## Migration và dữ liệu cũ

Startup tự chạy EF migrations. Migration tạo bảng mới với `TenantId` mặc định
`system-master`, sau đó nhập dữ liệu cũ **một lần** trong transaction. Nguồn nhập
là PostgreSQL `app_state`, JSON local hoặc dữ liệu mẫu theo cơ chế DataStore cũ.
Không xóa nguồn cũ, không đổi ID hoặc liên kết album. Khi workspace đã có,
startup không nhập lại. `store.json`/`app_state` cũ không nhận các thay đổi tiếp theo.

Studio hệ thống giữ quyền nghiệp vụ đang dùng bằng gói **ERP STUDIO quà tặng vô hạn**
(`IsTrialGift=true`). Studio tạo mới luôn Free, 1 user và 5 album lọc PC/tháng.
Đây là ngoại lệ tương thích dữ liệu cũ, không phải giá thương mại của studio mới.

Trước triển khai production: tạo backup DB và media nhất quán, phục hồi vào staging,
chạy bản mới trên bản sao rồi đối chiếu số lượng và nghiệp vụ. Chưa chạy migration
trên PostgreSQL production trong nhiệm vụ này. Không dùng `database update 0` để
rollback dữ liệu thật: migration Down xóa bảng mới. Quay lại binary cũ cũng không
tự nhận các thay đổi đã ghi vào bảng mới; cần kế hoạch phục hồi/export dữ liệu.

SQLite dùng `<Data:Directory>/saas.db`; backup bằng SQLite backup API hoặc dừng
ghi trước khi sao chép, xử lý cả WAL. PostgreSQL dùng cùng kết nối hiện có.
Media vẫn ở local/S3 hoặc Drive; phải sao lưu riêng. Backup trên UI chỉ là metadata
album, lựa chọn, concept và bảng giá, không phải backup toàn bộ CRM hoặc ảnh gốc.

## Chạy local và tạo studio mới

```bash
dotnet restore tests/StudioCore.Tests/StudioCore.Tests.csproj
dotnet build StudioCore.csproj -c Release
dotnet run --project StudioCore.csproj --no-launch-profile
```

Đặt `ASPNETCORE_URLS` nếu cần cổng riêng. Dùng dữ liệu tạm và xóa cấu hình DB/storage
production khỏi môi trường trước khi thử. `python3 tools/start-local-review.py`
tự tạo môi trường thử riêng, seed dữ liệu giả và dọn khi dừng.

Trang bán hàng mở đăng ký công khai qua `POST /api/onboarding/register`. Production chỉ
cho phép đăng ký khi đã cấu hình SMTP và `Saas__PublicOrigin`: owner nhận liên kết xác
thực dùng một lần, hiệu lực 24 giờ, và chưa thể đăng nhập trước khi xác thực. Trang đăng
nhập hỗ trợ gửi lại xác thực, quên mật khẩu và token reset dùng một lần trong 30 phút.
Thông báo quên mật khẩu không tiết lộ tài khoản có tồn tại hay không.

CLI vẫn dành cho operator, tạo owner đã xác thực để seed/recovery có kiểm soát:

1. Cấu hình `Provisioning__Name`, `Provisioning__Slug`, `Provisioning__Username`,
   `Provisioning__Password`, `Provisioning__Email` trong môi trường của lệnh.
2. Chạy `dotnet bin/Release/net8.0/StudioCore.dll --create-studio` với đúng kết nối DB.
3. Lệnh tạo Studio + workspace + owner trong một transaction, in mã/slug/gói,
   không in mật khẩu. Xóa biến provisioning khỏi môi trường sau khi dùng.
4. Mở `/s/{slug}/admin/login`. Mật khẩu owner tối thiểu 8 ký tự; slug 3–63 ký tự.

## Routing và domain

Đặt `Saas__BaseDomain` thành hostname nền tảng, không có `https://`, cổng hoặc `www`.
Nếu bỏ trống, fallback `RENDER_EXTERNAL_HOSTNAME`, rồi `localhost`.

- Path: `https://www.<base>/s/{slug}`. Middleware giữ PathBase và chuyển API/navigation
  sang cùng prefix; session có TenantId và cookie riêng từng studio.
- Subdomain: `https://{slug}.<base>`, dùng được ở Free.
- Custom: hostname khớp Studio, đã xác minh và gói còn hiệu lực từ AlbumPro.
- Host/slug không xác định trả 404; không fallback sang studio hệ thống.
- `/api/health`, `/assets/*` và webhook nền tảng được xử lý riêng.

Settings kiểm tra slug còn trống theo thời gian thực; DB unique index xử lý race.
Sau khi có slug phải đổi sang giá trị mới, không để trống. Đổi slug làm thay đổi URL
path/subdomain và các liên kết mời chưa dùng. Link album không chứa slug trước đây
vẫn phục vụ studio hệ thống.

Custom domain cần CNAME tới `cname.<base>` và TXT `_studiocore.<custom-domain>` bằng
token hiển thị trong Settings. API xác minh qua Cloudflare DNS-over-HTTPS. Chưa đủ
hai record thì không phục vụ tenant ở host đó. Hết hạn gói thì custom domain bị khóa,
nhưng cấu hình được giữ để kích hoạt lại sau khi gia hạn.

DNS xác minh **không tự cấu hình ingress hoặc TLS**. Operator phải cấu hình wildcard
DNS, hostname nền tảng, reverse proxy/hosting và chứng chỉ cho custom domains.
Render không tự nhận wildcard/custom domains chỉ nhờ record DNS. URL localhost ở
Settings là minh họa cấu trúc HTTPS; khi chạy local dùng địa chỉ/cổng HTTP thực tế.

## Quyền theo gói và quota

| Gói | Tháng | Năm (12 tháng) | User | Lọc PC |
| --- | ---: | ---: | ---: | --- |
| Free | 0 đ | 0 đ | 1 | 5 album/tháng |
| AlbumPro | 75.000 đ | 900.000 đ | 1 | Không giới hạn |
| ErpSolo | 125.000 đ | 1.500.000 đ | 1 | Không giới hạn |
| ErpStudio | 225.000 đ | 2.700.000 đ | 5 | Không giới hạn |

Giá năm hiện bằng 12 tháng vì chưa có chính sách giảm giá được cung cấp.
AlbumPro mở domain/backup; Solo mở CRM, lịch, hợp đồng, sổ quỹ; Studio mở quản trị
thành viên, phân ca/chấm công và lương tháng. Server chặn API thiếu quyền bằng 403
`upgrade_required`, không chỉ ẩn nút. Quyền theo vai trò/phân hệ vẫn áp dụng bên trong gói.

Background service mỗi phút kiểm tra hết hạn và reset quota khi sang tháng theo
múi giờ Việt Nam. Quyền được kiểm tra theo thời gian hiện tại ngay cả trước tick.
Lượt PC tính theo album hoặc hash danh sách mã thủ công, chỉ một lần/album/tháng;
lặp lại không trừ thêm. Lượt ghi nhận trước khi sao chép, kể cả sao chép thất bại.
Giới hạn được kiểm tra atomically. Đây là công cụ chạy trên máy khách: không thể
ngăn người dùng tự chạy thuật toán offline hoặc sửa JavaScript bằng DevTools.

Thư mời email dự trữ slot trong 3 ngày; chỉ STUDIO được mời/thêm user, không vượt
5 tài khoản đang hoạt động cộng thư mời đang chờ. Token ngẫu nhiên chỉ lưu hash,
chỉ dùng một lần, gắn studio; có thể thu hồi. Hạ/hết hạn gói không tự xóa user/dữ liệu
cũ, nhưng chặn thêm user và khóa tính năng trả phí. Cần chính sách truy cập user
đang vượt slot sau hết hạn trước khi mở bán rộng rãi.

## VietQR và SePay

Cấu hình qua secret manager/biến môi trường, không ghi giá trị thật vào repository:

| Biến môi trường | Mục đích |
| --- | --- |
| `Billing__BankCode` | Mã ngân hàng SePay QR hỗ trợ |
| `Billing__AccountNumber`, `Billing__AccountName` | Tài khoản nhận và tên hiển thị |
| `SePay__WebhookSecret` | HMAC SHA256, được ưu tiên khi đã cấu hình |
| `SePay__ApiKey` | API key webhook khi không dùng HMAC |
| `Saas__BaseDomain`, `Saas__PublicOrigin` | Host nền tảng và origin HTTPS gửi link mời |
| `Email__SmtpHost`, `Email__SmtpPort`, `Email__EnableSsl` | SMTP, mặc định cổng 587/TLS |
| `Email__From`, `Email__Username`, `Email__Password` | Người gửi và xác thực SMTP |
| `Email__DeliveryMode` | Chỉ đặt `test` trong kiểm thử tự động; production phải dùng SMTP thật |

Webhook: `POST https://<platform>/api/payments/sepay-webhook`.
Với HMAC: `X-SePay-Timestamp` Unix giây, `X-SePay-Signature: sha256=<hex>` ký
`timestamp.rawBody`; sai chữ ký hoặc lệch quá 5 phút trả 401. Khi chỉ dùng API key:
`Authorization: Apikey <key>`. Có HMAC thì không fallback sang API key yếu hơn.

Checkout lấy số tiền từ server, tạo PaymentCode riêng và nội dung
`UPGRADE {StudioCode} {PLAN} {PaymentCode}`, hiển thị QR từ `qr.sepay.vn`.
Webhook kiểm tra tài khoản nhận, chiều tiền vào, mã studio/gói/thanh toán và số tiền
khớp tuyệt đối. Reference giao dịch unique và transaction DB ngăn retry cộng hạn
hai lần. Chuyển khoản không liên quan được bỏ qua; sai tiền/nội dung cần xử lý
đối soát, không kích hoạt. QR hiệu lực 30 phút để tái sử dụng checkout; tiền tới
muộn vẫn được ghi nhận. Nếu đã lên gói cao hơn trước khi đơn cũ được thanh toán,
giao dịch chuyển `ManualReview`, không tự hạ gói. Chưa có UI hoàn tiền/xử lý ManualReview.

Gia hạn cùng gói cộng tiếp thời hạn còn lại; lên gói cao hơn bắt đầu chu kỳ mới,
không có bù trừ phần thời hạn cũ. Không cho chuyển xuống khi gói cao hơn còn hạn.
SSE báo thay đổi và UI polling 2 giây dự phòng; thời gian nhận tiền phụ thuộc ngân
hàng/SePay/mạng, không cam kết kích hoạt trong đúng 2 giây.

Tài liệu giao thức: [SePay webhook](https://developer.sepay.vn/en/sepay-webhooks/tich-hop-webhook),
[xác thực SePay](https://developer.sepay.vn/en/sepay-webhooks/xac-thuc),
[EF query filters](https://learn.microsoft.com/en-us/ef/core/querying/filters).

## Giới hạn cần hoàn thiện

- AI Selfie Face Search, upload/review video 5 phiên bản, watermark mặc định/chữ ký,
  Google Calendar hai chiều, xuất Google Sheets, bàn giao két sắt: mới có ma trận
  quyền; chưa có module hoạt động tương ứng. Không quảng cáo là dịch vụ đã hoàn thiện.
- Backup UI lưu metadata, chưa có tác vụ cloud backup ảnh gốc; chưa có hỗ trợ 1–1 vận hành.
- Chưa có PostgreSQL integration test thực tế, kiểm thử tải/race nhiều replica,
  DNS/custom TLS thật, email SMTP thật hoặc thanh toán ngân hàng thật.
- Session vẫn trong RAM; cần distributed session và media dùng chung trước khi scale.
- Các request còn tải cả workspace; cần repository/query theo phân hệ khi dữ liệu lớn.
- Cần chính sách khóa user vượt slot khi hết hạn, tác vụ đối soát/hoàn tiền và quản trị
  nền tảng trước khi mở bán. Onboarding/account lifecycle đã có rate limit riêng; cần
  kiểm thử SMTP thật và chính sách gửi mail production trước khi phát hành.

## Kiểm thử

Chạy các lệnh trong [tests/README.md](../tests/README.md). Test mới gồm SQLite import,
rollback, thứ tự collection, tenant filters/SaveChangesAsync, ba mode resolver,
HMAC, quota qua HTTP, giá/upgrade, replay webhook và cách ly session/preferences.
Test sinh SQL PostgreSQL kiểm tra kiểu dữ liệu/provider nhưng không thay thế việc
chạy migration trên PostgreSQL staging thật.
