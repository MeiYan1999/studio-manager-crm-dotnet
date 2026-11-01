"""Exercise uploads against a local S3 stub; no real storage credentials or data."""
import http.server
import threading
import subprocess
import tempfile
import os
import time
import json
import urllib.request
import urllib.error
import http.cookiejar
import base64
import secrets
import socket
import hashlib
import hmac
from pathlib import Path

root = Path(__file__).resolve().parents[1]
mode = 'ok'
received = []
png = base64.b64decode('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a9V0AAAAASUVORK5CYII=')

class S3(http.server.BaseHTTPRequestHandler):
    def log_message(self, *args): pass
    def do_PUT(self):
        data = self.rfile.read(int(self.headers.get('Content-Length', 0)))
        authorization = self.headers.get('Authorization', '')
        assert authorization.startswith('AWS4-HMAC-SHA256 Credential=test-access/')
        assert '/auto/s3/aws4_request' in authorization
        assert self.path.startswith('/test/system-master/images/')
        assert self.path.endswith('.png')
        received.append((dict(self.headers), data))
        if mode == 'ok':
            self.send_response(200)
            self.send_header('ETag', '"test"')
            self.end_headers()
        else:
            body = f'<Error><Code>{mode}</Code><Message>private provider detail</Message></Error>'.encode()
            self.send_response(404 if mode == 'NoSuchBucket' else 403)
            self.send_header('Content-Type', 'application/xml')
            self.send_header('Content-Length', str(len(body)))
            self.end_headers()
            self.wfile.write(body)

server = http.server.ThreadingHTTPServer(('127.0.0.1', 0), S3)
threading.Thread(target=server.serve_forever, daemon=True).start()
with socket.socket() as sock:
    sock.bind(('127.0.0.1', 0))
    port = sock.getsockname()[1]
base = f'http://127.0.0.1:{port}'
client = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
def request(path, data=None, method=None, content_type='application/json'):
    if isinstance(data, dict): data = json.dumps(data).encode()
    req = urllib.request.Request(base + path, data=data, method=method, headers={'Content-Type': content_type})
    try: res = client.open(req, timeout=15)
    except urllib.error.HTTPError as e: res = e
    with res: return res.status, json.loads(res.read())

with tempfile.TemporaryDirectory(prefix='crm-s3-test-') as folder:
    env = {key: value for key, value in os.environ.items()
           if not key.lower().startswith(('connectionstrings__', 'storage__', 'cloudflarer2__', 'data__', 'googledrive__', 'aspnetcore_', 'dotnet_', 'saas__', 'billing__', 'sepay__', 'email__', 'provisioning__'))
           and key.lower() not in ('database_url', 'admincode', 'render_external_hostname')}
    password = secrets.token_urlsafe(32)
    env.update(Data__Directory=folder, ASPNETCORE_URLS=base,
               AdminCode=password, ASPNETCORE_ENVIRONMENT='Production',
               ConnectionStrings__Postgres='', DATABASE_URL='',
               CloudflareR2__ServiceUrl=f'http://127.0.0.1:{server.server_port}',
               CloudflareR2__BucketName='test', CloudflareR2__AccessKey='test-access',
               CloudflareR2__SecretKey='test-secret')
    process = subprocess.Popen([os.environ.get('DOTNET_BIN', 'dotnet'), str(root/'bin/Release/net8.0/StudioCore.dll')], cwd=root, env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    try:
        for _ in range(100):
            try:
                if request('/api/health')[0] == 200: break
            except OSError: pass
            time.sleep(.1)
        assert request('/api/auth/login', {'username':'admin','password':password})[0] == 200
        boundary = 'testboundary'
        body = (f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="test.png"\r\nContent-Type: image/png\r\n\r\n'.encode() + png + f'\r\n--{boundary}--\r\n'.encode())
        for route in ['logo', 'login-image', 'home-image']:
            mode = 'ok'
            status, saved = request('/api/admin/settings/'+route, body, 'POST', 'multipart/form-data; boundary='+boundary)
            assert status == 200, saved
            headers, payload = received[-1]
            assert payload, 'AWS SDK must send a non-empty image payload'
            for mode, expected in [('NoSuchBucket','bucket'), ('AccessDenied','quyền'), ('SignatureDoesNotMatch','xác thực')]:
                status, error = request('/api/admin/settings/'+route, body, 'POST', 'multipart/form-data; boundary='+boundary)
                assert status == 502 and expected in error['error'], (status, error)
                assert 'private provider detail' not in error['error']
                current = request('/api/admin/settings')[1]
                assert current['logoUrl'] == saved['logoUrl']
                assert current['loginImageUrl'] == saved['loginImageUrl']
                assert current['homeImageUrl'] == saved['homeImageUrl']
        print('Passed: AWS SDK S3 uploads, tenant-prefixed keys, logo/login/home routes, safe provider errors, previous image preserved on failure.')
    finally:
        process.terminate()
        try:
            process.wait(timeout=10)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait()
        server.shutdown()
        server.server_close()
