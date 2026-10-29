#!/usr/bin/env python3
"""Local feature preview. Creates only disposable data, never uses a production database."""
import argparse
from datetime import datetime, timedelta, timezone
import http.cookiejar
import json
import os
from pathlib import Path
import secrets
import shutil
import socket
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request
import webbrowser

SOURCE = Path(__file__).resolve().parents[1]
DLL = SOURCE / 'bin/Release/net8.0/StudioCore.dll'


def seed(base, admin_password, employee_password):
    client = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))

    def post(path, body=None):
        request = urllib.request.Request(base + path, data=json.dumps(body).encode() if body is not None else None,
                                         headers={'Content-Type': 'application/json'})
        try:
            with client.open(request, timeout=20) as response:
                return json.loads(response.read())
        except urllib.error.HTTPError as error:
            # Do not echo request bodies, cookies, credentials or arbitrary server responses.
            raise RuntimeError('Không tạo được dữ liệu mẫu: ' + path + ' (HTTP ' + str(error.code) + ')') from None

    post('/api/auth/login', {'username': 'admin', 'password': admin_password})
    today = datetime.now(timezone(timedelta(hours=7))).date()
    customers = [post('/api/admin/customers', {'name': name, 'source': 'Local review',
                 'notes': 'Dữ liệu giả để review, không phải khách hàng thật.'})
                 for name in ('Khách mẫu — Linh & Minh', 'Khách mẫu — Gia đình An')]
    package = post('/api/admin/packages', {'name': 'Review — Gói chụp ngoại cảnh', 'category': 'Ngoại cảnh',
                   'price': 5000000, 'hours': 3, 'includes': 'Chụp 3 giờ, album online và ảnh chỉnh sửa'})
    staff = post('/api/admin/staff', {'name': 'Nhân sự mẫu — Nhiếp ảnh', 'role': 'photographer',
                 'baseSalary': 10000000, 'commissionRate': 5, 'shift': 'normal'})
    for index, customer in enumerate(customers, 1):
        post('/api/admin/bookings', {'customerId': customer['id'], 'packageId': package['id'],
             'date': (today + timedelta(days=index)).isoformat(), 'startTime': '09:00',
             'photographerId': staff['id'], 'location': 'Địa điểm mẫu', 'notes': 'Lịch review local'})
    post('/api/admin/orders', {'customerId': customers[0]['id'],
         'items': [{'name': 'Gói chụp review', 'quantity': 1, 'unitPrice': 5000000}], 'notes': 'Đơn hàng mẫu'})
    post('/api/admin/contracts', {'customerId': customers[0]['id'], 'service': 'Hợp đồng chụp mẫu',
         'total': 5000000, 'terms': 'Nội dung minh họa cho bản review local.'})
    for kind, amount, note in (('income', 500000, 'Thu khác mẫu'), ('expense', 200000, 'Chi vận hành mẫu')):
        post('/api/admin/finance/entries', {'kind': kind, 'date': today.isoformat(),
             'category': 'Khác', 'amount': amount, 'note': note})
    post('/api/admin/attendance', {'staffId': staff['id'], 'date': today.isoformat(),
         'in': '08:35', 'out': '18:20', 'note': 'Chấm công mẫu: đi muộn, về sớm'})
    post('/api/admin/outfits', {'name': 'Review — Trang phục trắng', 'size': 'M', 'color': 'Trắng', 'quantity': 2})
    albums = post('/api/admin/albums')
    source_album = next(album for album in albums if album['isPublished'] and album['photoCount'] > 0)
    post('/api/admin/concepts', {'name': 'Review — Bộ ảnh gia đình', 'description': 'Ảnh minh họa trên dữ liệu thử riêng',
         'fee': 300000, 'category': 'family', 'albumId': source_album['id'], 'isPublished': True})
    post('/api/admin/users', {'username': 'review-staff', 'displayName': 'Nhân viên review',
         'password': employee_password, 'role': 'employee', 'mustChangePassword': False,
         'permissions': ['dashboard', 'albums', 'board', 'tools']})


def main():
    parser = argparse.ArgumentParser(description='Mở preview CRM bằng dữ liệu giả trên localhost.')
    parser.add_argument('--port', type=int, default=5080)
    parser.add_argument('--check', action='store_true', help='Chỉ kiểm tra file/công cụ, không mở server.')
    parser.add_argument('--smoke', action='store_true', help='Build, tạo dữ liệu mẫu rồi dừng; không mở trình duyệt.')
    args = parser.parse_args()
    if args.port != 0 and not 1024 <= args.port <= 65535:
        parser.error('Port phải là 0 (tự chọn) hoặc nằm trong khoảng 1024–65535.')
    if not (SOURCE / 'StudioCore.csproj').is_file():
        raise RuntimeError('Không tìm thấy StudioCore.csproj ở thư mục gốc repository.')
    dotnet = shutil.which('dotnet')
    if not dotnet:
        raise RuntimeError('Cần cài .NET 8 SDK trước khi chạy preview.')
    if args.check:
        print('Source: OK | dotnet: OK | Release DLL: ' + ('có sẵn' if DLL.exists() else 'sẽ build khi mở preview'))
        print('Chưa khởi động server hoặc tạo dữ liệu.')
        return
    with socket.socket() as probe:
        try:
            probe.bind(('127.0.0.1', args.port))
            args.port = probe.getsockname()[1]
        except OSError as error:
            raise RuntimeError('Không mở được cổng local (' + str(error) + '). Thử --port 0 để tự chọn cổng trống.') from None
    prefixes = ('connectionstrings__', 'storage__', 'data__', 'googledrive__', 'aspnetcore_', 'dotnet_', 'crm_test_')
    env = {key: value for key, value in os.environ.items()
           if not key.lower().startswith(prefixes) and key.lower() not in ('database_url', 'admincode')}
    # Always build the current source, so the preview cannot silently show an older binary.
    subprocess.run([dotnet, 'build', 'StudioCore.csproj', '-c', 'Release', '--disable-build-servers'],
                   cwd=SOURCE, env=env, check=True)
    base = 'http://127.0.0.1:' + str(args.port)
    admin_password = secrets.token_urlsafe(16)
    employee_password = secrets.token_urlsafe(16)
    with tempfile.TemporaryDirectory(prefix='studiocore-local-review-') as folder:
        env.update(ConnectionStrings__Postgres='', DATABASE_URL='', Storage__Provider='local',
                   Data__Directory=folder, AdminCode=admin_password, ASPNETCORE_ENVIRONMENT='Production',
                   ASPNETCORE_URLS=base)
        with tempfile.TemporaryFile() as log:
            process = subprocess.Popen([dotnet, str(DLL)], cwd=SOURCE, env=env, stdout=log, stderr=log)
            try:
                deadline = time.monotonic() + 30
                while True:
                    if process.poll() is not None:
                        raise RuntimeError('Server preview dừng trước khi sẵn sàng.')
                    try:
                        with urllib.request.urlopen(base + '/api/health', timeout=1) as response:
                            if response.status == 200:
                                break
                    except (OSError, urllib.error.URLError):
                        pass
                    if time.monotonic() >= deadline:
                        raise RuntimeError('Server preview chưa sẵn sàng sau 30 giây.')
                    time.sleep(.2)
                try:
                    seed(base, admin_password, employee_password)
                    sample_status = 'Đã tạo dữ liệu mẫu CRM, bán hàng, thu chi, chấm công và trang phục.'
                except (RuntimeError, OSError, ValueError) as error:
                    if args.smoke:
                        raise
                    sample_status = str(error) + '\nBạn vẫn có thể review; một số phân hệ có thể chưa có dữ liệu mẫu.'
                if args.smoke:
                    print('Preview seed smoke: PASS (isolated local data; server stopped after check).')
                    return
                print('\n' + '=' * 64)
                print('LOCAL REVIEW — dữ liệu giả, tự xóa khi dừng')
                print(base + '/admin')
                print('Quản trị viên: admin | Mật khẩu tạm: ' + admin_password)
                if 'Đã tạo' in sample_status:
                    print('Nhân viên: review-staff | Mật khẩu tạm: ' + employee_password)
                print(sample_status)
                print('Album mẫu: ' + base + '/a/demo-ngay-chung-doi')
                print('Giữ Terminal mở. Nhấn Ctrl+C để dừng và xóa dữ liệu thử.')
                print('=' * 64, flush=True)
                webbrowser.open(base + '/admin')
                try:
                    code = process.wait()
                    if code:
                        raise RuntimeError('Server preview kết thúc với mã ' + str(code))
                except KeyboardInterrupt:
                    print('\nĐang dừng preview và dọn dữ liệu thử...')
            finally:
                if process.poll() is None:
                    process.terminate()
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait()


if __name__ == '__main__':
    try:
        main()
    except (RuntimeError, OSError, subprocess.CalledProcessError) as error:
        print('Không mở được preview: ' + str(error), file=sys.stderr)
        sys.exit(1)
