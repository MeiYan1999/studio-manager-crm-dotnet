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
        payload_hash = hashlib.sha256(data).hexdigest()
        assert self.headers['x-amz-content-sha256'] == payload_hash
        amz_date = self.headers['x-amz-date']
        date = amz_date[:8]
        scope = f'{date}/ap-southeast-1/s3/aws4_request'
        signed = 'host;x-amz-content-sha256;x-amz-date'
        canonical_headers = (f'host:{self.headers["Host"]}\n'
                             f'x-amz-content-sha256:{payload_hash}\n'
                             f'x-amz-date:{amz_date}\n')
        canonical = f'PUT\n{self.path}\n\n{canonical_headers}\n{signed}\n{payload_hash}'
        string_to_sign = ('AWS4-HMAC-SHA256\n' + amz_date + '\n' + scope + '\n' +
                          hashlib.sha256(canonical.encode()).hexdigest())
        sign = lambda key, value: hmac.new(key, value.encode(), hashlib.sha256).digest()
        signing_key = sign(sign(sign(sign(b'AWS4test-secret', date), 'ap-southeast-1'), 's3'), 'aws4_request')
        signature = hmac.new(signing_key, string_to_sign.encode(), hashlib.sha256).hexdigest()
        assert self.headers['Authorization'] == (
            f'AWS4-HMAC-SHA256 Credential=test-access/{scope}, '
            f'SignedHeaders={signed}, Signature={signature}')
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
base = 'http://127.0.0.1:5097'
client = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
def request(path, data=None, method=None, content_type='application/json'):
    if isinstance(data, dict): data = json.dumps(data).encode()
    req = urllib.request.Request(base + path, data=data, method=method, headers={'Content-Type': content_type})
    try: res = client.open(req, timeout=15)
    except urllib.error.HTTPError as e: res = e
    with res: return res.status, json.loads(res.read())

with tempfile.TemporaryDirectory(prefix='crm-s3-test-') as folder:
    env = dict(os.environ, Data__Directory=folder, ASPNETCORE_URLS=base,
               Storage__Provider='s3', Storage__Endpoint=f'http://127.0.0.1:{server.server_port}',
               Storage__Region='ap-southeast-1', Storage__Bucket='test',
               Storage__AccessKey='test-access', Storage__SecretKey='test-secret')
    env.pop('ConnectionStrings__Postgres', None)
    process = subprocess.Popen([os.environ.get('DOTNET_BIN', 'dotnet'), str(root/'bin/Release/net8.0/MayAlbum.dll')], cwd=root, env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    try:
        for _ in range(100):
            try:
                if request('/api/health')[0] == 200: break
            except OSError: pass
            time.sleep(.1)
        assert request('/api/auth/login', {'username':'admin','password':'studio2026'})[0] == 200
        boundary = 'testboundary'
        body = (f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="test.png"\r\nContent-Type: image/png\r\n\r\n'.encode() + png + f'\r\n--{boundary}--\r\n'.encode())
        for route in ['logo', 'login-image']:
            mode = 'ok'
            status, saved = request('/api/admin/settings/'+route, body, 'POST', 'multipart/form-data; boundary='+boundary)
            assert status == 200, saved
            headers, payload = received[-1]
            assert payload == png, 'Upload must send raw image, not signed AWS chunks'
            assert 'aws-chunked' not in headers.get('Content-Encoding', '')
            assert 'STREAMING' not in headers.get('X-Amz-Content-SHA256', '')
            for mode, expected in [('NoSuchBucket','bucket'), ('AccessDenied','quyền'), ('SignatureDoesNotMatch','xác thực')]:
                status, error = request('/api/admin/settings/'+route, body, 'POST', 'multipart/form-data; boundary='+boundary)
                assert status == 502 and expected in error['error'], (status, error)
                assert 'private provider detail' not in error['error']
                current = request('/api/admin/settings')[1]
                assert current['logoUrl'] == saved['logoUrl']
                assert current['loginImageUrl'] == saved['loginImageUrl']
        print('Passed: signed non-chunked S3 uploads, logo/login routes, safe provider errors, previous image preserved on failure.')
    finally:
        process.terminate()
        process.wait(timeout=10)
        server.shutdown()
