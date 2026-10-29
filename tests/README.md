# Kiểm thử backend và giao diện

## Backend: lệnh dùng chung cho local và CI

Yêu cầu .NET 8 SDK, Python 3.9+. Chạy từ thư mục gốc repository:

```bash
dotnet restore tests/StudioCore.Tests/StudioCore.Tests.csproj
dotnet build tests/StudioCore.Tests/StudioCore.Tests.csproj -c Release --no-restore
dotnet test tests/StudioCore.Tests/StudioCore.Tests.csproj -c Release --no-build
python3 tests/run_backend.py
```

- xUnit: hash mật khẩu/malformed hashes; quyền mặc định/quyền tường minh; tính giờ công,
  làm tròn 15 phút, đi muộn/về sớm theo ca; phân tích link Drive; XLSX tiếng Việt và text
  giống công thức; lưu/mở lại JSON, cô lập ReadClone, tương thích dữ liệu cũ khi thêm ảnh trang chủ và migration khoản chi schema 7 → 8.
- `auth_smoke.py`: chưa đăng nhập, mật khẩu sai, bắt buộc đổi mật khẩu, quyền theo phân hệ,
  logout, mật khẩu cũ sau đổi, tài khoản bị khóa và session đang tồn tại.
- `finance_smoke.py`: tổng thu/chi/lợi nhuận; sửa không hợp lệ không đổi dữ liệu;
  bộ lọc, ảnh/chú thích, giới hạn 5 MB/6 ảnh; API ảnh có xác thực; nội dung XLSX,
  xuất hợp đồng và đổi tên studio.
- `home_image_smoke.py`: upload/thay/reset banner, ảnh công khai và Content-Type,
  URL có version mới khi thay ảnh, giữ ảnh sau khi lưu cài đặt khác, từ chối file trống/sai
  định dạng/quá 5 MB và giữ độc lập logo/ảnh đăng nhập. Auth smoke kiểm tra employee bị chặn.
- `concepts_smoke.py`: 5 nhóm công khai, concept mặc định ẩn, admin chọn album và bật hiển thị,
  employee không được cấu hình công khai, payload không chứa thông tin khách hàng/token/giá,
  sửa không hợp lệ không đổi tên, album bị ngừng chia sẻ/xóa gỡ ảnh khỏi gallery.
- xUnit `ConceptShowcaseTests`: tương thích concept cũ, dữ liệu public tối thiểu, ẩn ảnh khi
  nguồn bị khóa/xóa/bảo trì và kiểm tra điều kiện công khai.
- `storage_s3_smoke.py`: upload logo/ảnh đăng nhập/ảnh trang chủ đến S3 giả lập; kiểm tra chữ ký SigV4,
  payload không chunked, lỗi nhà cung cấp được chuyển thông báo an toàn và giữ ảnh cũ khi lỗi.

`run_backend.py` khởi động bản Release trên cổng loopback tạm, dùng thư mục dữ liệu tạm,
password sinh ngẫu nhiên và loại bỏ cấu hình database/storage/.NET liên quan khỏi môi trường
kế thừa. Runner dừng process và dọn dữ liệu khi kết thúc, kể cả test lỗi.
Test S3 khởi động server và stub riêng, cũng dùng dữ liệu tạm/cổng động.
Không chạy các smoke script trực tiếp với dữ liệu studio thật: chúng tạo/sửa/khóa bản ghi.

CI `.github/workflows/backend.yml` chạy restore, build, xUnit, runner API/S3, preview seed smoke và publish;
lưu kết quả xUnit dạng TRX. CI không deploy và không dùng secrets bên ngoài.
Chưa bao phủ PostgreSQL thật, Drive thật, browser, toàn bộ CRM/sales/payroll và lỗi ghi/rollback.
Các rủi ro authentication legacy và session đã nêu ở README chưa được sửa trong nhiệm vụ này.

## Giao diện: chạy riêng, chưa nằm trong CI backend

Cần Node.js và Playwright. Dùng terminal Bash riêng với dữ liệu thử hoàn toàn mới;
xóa các biến database kế thừa bằng cách đặt rỗng:

```bash
dotnet build StudioCore.csproj -c Release
TEST_DATA_DIR="$(mktemp -d)"
ConnectionStrings__Postgres='' DATABASE_URL='' Storage__Provider=local \
  Data__Directory="$TEST_DATA_DIR" ASPNETCORE_ENVIRONMENT=Production \
  AdminCode='local-test-password-only' ASPNETCORE_URLS=http://127.0.0.1:5080 \
  dotnet bin/Release/net8.0/StudioCore.dll
```

Terminal thứ hai:

```bash
export CRM_TEST_URL=http://127.0.0.1:5080
export CRM_TEST_PASSWORD='local-test-password-only'
npm install --prefix /tmp/may-crm-test-deps --no-audit --no-fund playwright
/tmp/may-crm-test-deps/node_modules/.bin/playwright install chromium
NODE_PATH=/tmp/may-crm-test-deps/node_modules node tests/finance_ui.cjs
NODE_PATH=/tmp/may-crm-test-deps/node_modules node tests/branding_ui.cjs
```

Sau kiểm thử, dừng server bằng Ctrl+C. Thư mục `$TEST_DATA_DIR` chỉ chứa dữ liệu thử;
xóa riêng thư mục này khi không cần giữ kết quả. Không tái dùng thư mục dữ liệu thật.
Test giao diện tài chính kiểm tra retry upload không trùng khoản/ảnh, chú thích/xóa ảnh,
xuất Excel, đổi tên sau reload, lỗi JavaScript và bố cục desktop/420px.

Kiểm thử thương hiệu và nội dung trang chủ (dùng máy chủ thử riêng như trên;
đặt `CRM_TEST_URL` nếu dùng cổng khác):

```bash
CRM_TEST_URL=http://127.0.0.1:5080 NODE_PATH=/tmp/may-crm-test-deps/node_modules node tests/branding_ui.cjs
```

Bài kiểm thử đổi và khôi phục cài đặt: logo, tên studio, lời giới thiệu,
điều hướng về trang chủ sau khi lưu, tải lại trang, nội dung dạng văn bản,
kiểm tra độ dài/tên trống và bố cục điện thoại.
