# Review local — StudioCore

Bản preview dùng source đã cập nhật README, backend tests và CI. Giao diện/nghiệp vụ CRM
hiện có được giữ nguyên; nhiệm vụ trước chưa thêm tính năng sản phẩm mới.

## Mở preview

Từ thư mục gốc repository, nhấp đúp `tools/Open-Local-Review.command`, hoặc mở Terminal:

```bash
python3 tools/start-local-review.py
```

Script build source hiện tại bằng .NET 8, mở server `http://127.0.0.1:5080`, tạo dữ liệu giả
qua API rồi mở trình duyệt ở `/admin`. Tài khoản `admin` và `review-staff` có mật khẩu ngẫu
nhiên, được hiển thị trong Terminal sau khi tạo dữ liệu. Nếu tạo dữ liệu mẫu lỗi, script báo
rõ và vẫn cho xem những phần đã khởi tạo. Cần .NET 8 SDK, Python 3.9+; lần build đầu có thể
cần mạng để restore Npgsql. Không cần tài khoản Google, S3 hoặc PostgreSQL.

Nếu cổng 5080 đang được dùng:

```bash
python3 tools/start-local-review.py --port 0
```

Giữ Terminal mở trong lúc review. Nhấn Ctrl+C để dừng; thư mục dữ liệu thử được tự xóa.
Mỗi lần chạy tạo phiên preview mới. Preview chỉ bind loopback và dùng storage local;
không kế thừa các biến môi trường database/storage liên quan. Không commit, push hoặc deploy.
Ảnh gốc có sẵn trong source; thử Drive thật cần cấu hình riêng, không cần cho album demo.

## Danh sách xem thử

| Thứ tự | Trang | Thao tác review |
|---|---|---|
| 1 | `/` | Trang chủ, tên studio, điều hướng, bố cục desktop/điện thoại |
| 2 | `/#concepts`, `/concepts/family` | Xem 5 nhóm, mở gallery thử, xem ảnh lớn và chuyển ảnh |
| 3 | `/a/demo-ngay-chung-doi` | Xem album mẫu, chọn ảnh, tìm mã ảnh, ghi chú và gửi lựa chọn |
| 4 | `/admin/albums` | Xem album, lựa chọn của khách, xuất CSV và tùy chọn chia sẻ |
| 5 | `/admin/board` | Kéo thả thẻ, checklist, nhãn, hạn chót và nhật ký |
| 6 | `/admin/customers`, `/admin/packages`, `/admin/staff` | Xem dữ liệu mẫu; thử thêm/sửa bản ghi |
| 7 | `/admin/bookings` | Lịch chụp mẫu ngày mai/ngày kia, liên kết khách/gói/nhân sự |
| 8 | `/admin/orders`, `/admin/contracts`, `/admin/reports` | Xem đơn/hợp đồng mẫu; thử ghi thanh toán và xuất báo cáo |
| 9 | `/admin/finance` | Thu/chi mẫu trong tháng, thêm ảnh chứng từ, lọc và xuất Excel |
| 10 | `/admin/attendance`, `/admin/shifts` | Chấm công mẫu muộn/về sớm; tự thử xếp ca tuần |
| 11 | `/admin/wardrobe` | Trang phục/concept mẫu; thử lập phiếu thuê rồi trả |
| 12 | `/admin/settings`, `/admin/audit` | Cài đặt tên/logo, tài khoản và nhật ký thay đổi |
| 13 | Đăng xuất, đăng nhập `review-staff` | Chỉ có quyền tổng quan, album, board và công cụ lọc ảnh |

Công cụ lọc ảnh dùng File System Access API: chỉ thử với thư mục ảnh mẫu do bạn tự chọn;
nó có thể sao chép file trên máy theo thao tác của bạn. Phần này cần Chrome/Edge hỗ trợ.

## Kiểm tra bộ preview

```bash
python3 tools/start-local-review.py --check
python3 tools/start-local-review.py --smoke --port 0
```

`--check` chỉ kiểm tra file/công cụ. `--smoke` build source, mở server loopback, tạo dữ liệu
qua API rồi dừng/dọn dữ liệu; lỗi nạp dữ liệu sẽ trả exit code khác 0. Chế độ này không mở
trình duyệt và không in mật khẩu tạm. Đây chưa phải kiểm thử bố cục hoặc thao tác UI.
Backend tests và CI được mô tả trong [tests/README.md](../tests/README.md).

Để phản hồi, ghi trang/chức năng, thao tác thực hiện, kết quả mong muốn và kết quả thực tế.

## Review gallery concept

Preview nạp một concept gia đình công khai từ album demo, trên dữ liệu thử riêng. Vào
Tủ trang phục → Concept để thử gán album, đổi nhóm và bật/tắt Hiển thị bộ ảnh trên website.
Các nhóm chưa gán ảnh có trạng thái đang cập nhật; ảnh thật cần do studio chọn từ album của mình.
