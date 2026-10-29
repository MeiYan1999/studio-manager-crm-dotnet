# Tiếp nhận và phát triển StudioCore

## Phạm vi

Tiếp quản source có sẵn, giữ kiến trúc ASP.NET Core 8 Minimal API và frontend JavaScript
ES modules thuần. Snapshot người dùng cung cấp tương ứng commit `9dad3b0`. Không xây dựng lại
ứng dụng. Dự án được đổi tên kỹ thuật thành StudioCore; repository và tài nguyên Render
hiện có vẫn giữ tên cũ để bảo toàn kết nối và dữ liệu.

## Các yêu cầu đã thực hiện

- Khảo sát cấu trúc, cấu hình, routing, API, session, phân quyền, dữ liệu và storage.
- Đồng bộ README với source: schema 8, PostgreSQL JSONB, quyền theo phân hệ, local/S3,
  startup/migration, backup và giới hạn đang có.
- Thêm project xUnit, smoke auth, runner API dữ liệu tạm và GitHub Actions build/test/publish.
- Thêm preview local có dữ liệu giả và tài khoản review; hướng dẫn review trong docs/LOCAL-REVIEW.md.
- Đổi csproj, assembly, namespaces, tên mặc định, Docker entrypoint, script và tài liệu sang StudioCore.
- Kết nối GitHub và đưa các thay đổi lên repository theo yêu cầu người dùng.

## Kiến trúc và tính năng

`Api/` chứa album công khai/quản trị, auth, board, CRM, sales và operations. `Models/` chứa
model, DTO và bộ lọc quyền; `Services/` chứa DataStore, Drive API, storage, Excel và tính toán.
`wwwroot/` là SPA, router nằm trong assets/js/app.js. Npgsql 8.0.6 là dependency backend.
Không có npm build cho ứng dụng.

Tính năng có source: album/chọn ảnh/CSV, công cụ lọc ảnh trên máy, bảng công việc, khách hàng,
gói dịch vụ, nhân sự, lịch chụp, đơn hàng/thanh toán/hóa đơn, hợp đồng/báo cáo, thu chi/chứng từ,
lương/chấm công/phân ca, trang phục/concept/thuê trả, thông báo, tài khoản và nhật ký.
Danh sách này mô tả implementation, không có nghĩa toàn bộ nghiệp vụ đã được test đầy đủ.

## Dữ liệu và vận hành

- Local: `<Data:Directory>/store.json`; PostgreSQL: toàn bộ trạng thái ở `app_state.data` JSONB.
- Ảnh local nằm trong finance-photos; S3 lưu nội dung object riêng, metadata trong trạng thái.
- Source GitHub không chứa dữ liệu đang chạy trên Render. Muốn local giống production cần
  bản sao lưu database và ảnh, không kết nối dữ liệu thật cho preview/test.
- Startup có thể tạo bảng, nhập JSON vào database trống, migrate và normalize dữ liệu.
- Các session nằm trong RAM, mất khi restart; ứng dụng hướng tới một studio/một instance.
- Tên studio/logo trong database được giữ nguyên khi đổi tên source.

## Giới hạn và thứ tự cải thiện

1. Tính nguyên tử khi ghi: callback sửa trực tiếp trạng thái RAM, chưa rollback khi callback/lưu lỗi.
2. Đồng bộ khi chạy nhiều instance: snapshot RAM độc lập có thể ghi đè JSONB của nhau.
3. Kiểm thử nghiệp vụ CRM/sales/payroll, migration nhiều phiên bản và PostgreSQL thật.
4. Chuẩn hóa backup/restore database và storage; kiểm tra HTTPS/proxy thực tế.
5. Tách nhỏ các file lớn theo nhiệm vụ cụ thể, giữ API và hành vi hiện có.

Cấu hình AdminCode và authentication được giữ nguyên theo yêu cầu người dùng. Không khẳng định
đã kiểm tra bí mật hoặc cấu hình production; không đưa credentials thực tế vào tài liệu.

## Kiểm tra và phát triển tiếp

Lệnh build/test trong README và tests/README.md. CI chạy xUnit, auth/finance/S3 smoke và
publish validation. Preview `--smoke` kiểm tra tạo dữ liệu qua API; cần review browser riêng.
Google Drive thật, PostgreSQL thật và hạ tầng production không nằm trong các bài kiểm thử local.

Ưu tiên thay đổi nhỏ, tái sử dụng thành phần, không ghi đè thay đổi của người dùng hoặc tự ý
đổi schema/auth. Cập nhật main có thể kích hoạt Render tự deploy theo cấu hình repository.
