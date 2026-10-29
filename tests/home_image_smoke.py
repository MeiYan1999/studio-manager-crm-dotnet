"""Home banner regression checks. Run via run_backend.py on disposable local data."""
import base64
import http.cookiejar
import json
import os
from pathlib import Path
import secrets
import urllib.error
import urllib.parse
import urllib.request


def run():
    base = os.environ["CRM_TEST_URL"].rstrip("/")
    if urllib.parse.urlparse(base).hostname not in ("127.0.0.1", "localhost"):
        raise RuntimeError("Use a disposable local server")
    admin = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
    guest = urllib.request.build_opener()

    def request(client, path, body=None, method=None, expected=200, content_type="application/json"):
        if isinstance(body, dict):
            body = json.dumps(body).encode()
        req = urllib.request.Request(base + path, data=body, method=method,
                                     headers={"Content-Type": content_type})
        try:
            response = client.open(req, timeout=20)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            raw = response.read()
            assert response.status == expected, (path, response.status, expected)
            return (json.loads(raw) if "application/json" in response.headers.get("Content-Type", "") else raw), response.headers

    def upload(client, payload, expected=200, mime="image/png"):
        boundary = "home-test-" + secrets.token_hex(10)
        body = (f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="banner.png"\r\n'
                f'Content-Type: {mime}\r\n\r\n').encode() + payload + f"\r\n--{boundary}--\r\n".encode()
        return request(client, "/api/admin/settings/home-image", body,
                       expected=expected, content_type="multipart/form-data; boundary=" + boundary)[0]

    png = base64.b64decode("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a9V0AAAAASUVORK5CYII=")
    initial, _ = request(guest, "/api/public/website")
    assert initial["homeImageUrl"] == ""
    request(guest, "/api/public/home-image", expected=404)
    upload(guest, png, expected=401)
    request(guest, "/api/admin/settings/home-image", method="DELETE", expected=401)
    request(admin, "/api/auth/login", {"username": "admin", "password": os.environ["CRM_TEST_PASSWORD"]})
    saved = upload(admin, png)
    first_url = saved["homeImageUrl"]
    assert first_url.startswith("/api/public/home-image?v=home-image-")
    image, headers = request(guest, first_url)
    assert image == png
    assert headers.get_content_type() == "image/png"
    assert headers.get("X-Content-Type-Options") == "nosniff"
    public, _ = request(guest, "/api/public/website")
    assert public["homeImageUrl"] == first_url
    assert "homeImageKey" not in public and "homeImageContentType" not in public
    upload(admin, b"this is not an image", expected=400)
    upload(admin, b"", expected=400)
    upload(admin, png + b"x" * (5 * 1024 * 1024), expected=400)
    current, _ = request(admin, "/api/admin/settings")
    assert current["homeImageUrl"] == first_url, "Rejected upload must preserve the saved image"
    unchanged, _ = request(admin, "/api/admin/settings", current, method="PUT")
    assert unchanged["homeImageUrl"] == first_url, "Saving other settings must preserve the image"
    jpg = (Path(__file__).resolve().parents[1] / "wwwroot/assets/img/demo/01.jpg").read_bytes()
    replacement = upload(admin, jpg, mime="image/jpeg")
    assert replacement["homeImageUrl"] != first_url
    assert replacement["logoUrl"] == saved["logoUrl"] and replacement["loginImageUrl"] == saved["loginImageUrl"]
    image, headers = request(guest, replacement["homeImageUrl"])
    assert image == jpg and headers.get_content_type() == "image/jpeg"
    reset, _ = request(admin, "/api/admin/settings/home-image", method="DELETE")
    assert reset["homeImageUrl"] == ""
    public, _ = request(guest, "/api/public/website")
    assert public["homeImageUrl"] == ""
    request(guest, "/api/public/home-image", expected=404)
    print("Passed: home banner upload/replace/reset, validation, public image, cache version and independent settings.")


if __name__ == "__main__":
    run()
