# Kiểm thử thu chi, Excel và đổi tên studio

Yêu cầu .NET 8 SDK, Python 3; kiểm thử trình duyệt cần Node.js và Playwright.
Chạy trên database thử riêng: các bài kiểm thử tạo/xoá giao dịch, hợp đồng
và đổi tên studio. Nhật ký kiểm thử vẫn được giữ trong dữ liệu thử.

Từ thư mục gốc repository, mở terminal thứ nhất:

```bash
dotnet build -c Release
Data__Directory=/tmp/may-crm-finance-test ASPNETCORE_URLS=http://127.0.0.1:5080 \
  dotnet bin/Release/net8.0/MayAlbum.dll
```

Terminal thứ hai:

```bash
python3 tests/finance_smoke.py
npm install --prefix /tmp/may-crm-test-deps --no-audit --no-fund playwright
/tmp/may-crm-test-deps/node_modules/.bin/playwright install chromium
NODE_PATH=/tmp/may-crm-test-deps/node_modules node tests/finance_ui.cjs
```

Các lệnh shell ở trên dùng Bash. Test mặc định dùng tài khoản quản trị khởi tạo
được mô tả trong README. Nếu thay cổng hoặc mật khẩu của dữ liệu thử, cung cấp
`CRM_TEST_URL` và `CRM_TEST_PASSWORD` qua môi trường. Cả hai bài kiểm thử từ chối
chạy trên hostname ngoài localhost/127.0.0.1.

- API: tổng thu/chi/lợi nhuận, dữ liệu không bị thay đổi khi sửa không hợp lệ,
  lọc theo loại, ghi chú tiếng Việt, ảnh và chú thích, giới hạn 5 MB/6 ảnh,
  quyền truy cập, xoá ảnh, nội dung các sheet XLSX và bộ lọc hợp đồng, đổi tên.
- Trình duyệt: tạo khoản thu với hai ảnh, giả lập lỗi tải ảnh thứ hai và lưu
  lại không trùng khoản/ảnh; sửa chú thích, xoá ảnh; tải Excel; bố cục desktop
  và 420px; đổi tên rồi điều hướng/tải lại; không có lỗi JavaScript chưa xử lý.
  Đường dẫn ảnh chụp kiểm thử được in khi chạy thành công.

Ảnh thử là PNG nhỏ được tạo trong bài kiểm thử; không cần Google Drive.
Không chạy các bài kiểm thử này trên dữ liệu studio thật.
