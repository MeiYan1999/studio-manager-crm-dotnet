"""Authentication/permission regression checks; run via run_backend.py on temporary data."""
import http.cookiejar
import json
import os
import secrets
import urllib.error
import urllib.parse
import urllib.request


def run():
    base = os.environ["CRM_TEST_URL"].rstrip("/")
    if urllib.parse.urlparse(base).hostname not in ("127.0.0.1", "localhost"):
        raise RuntimeError("Use a disposable local server")

    def client():
        return urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))

    def request(opener, path, body=None, method=None, expected=200):
        payload = json.dumps(body).encode() if isinstance(body, dict) else body
        req = urllib.request.Request(base + path, data=payload, method=method,
                                     headers={"Content-Type": "multipart/form-data; boundary=employee-home-test" if path.endswith("/home-image") and method == "POST" else "application/json"})
        try:
            response = opener.open(req, timeout=20)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            raw = response.read()
            assert response.status == expected, (path, response.status, expected)
            return json.loads(raw) if raw else None

    admin, employee = client(), client()
    request(admin, "/api/admin/users", expected=401)
    request(admin, "/api/auth/change-password", {"currentPassword": "wrong", "newPassword": "unused-test-password"}, expected=401)
    request(admin, "/api/auth/login", {"username": "admin", "password": "wrong"}, expected=401)
    request(admin, "/api/auth/login", {"username": "admin", "password": os.environ["CRM_TEST_PASSWORD"]})
    username = "qa-" + secrets.token_hex(5)
    password, replacement = secrets.token_urlsafe(24), secrets.token_urlsafe(24)
    user = request(admin, "/api/admin/users", {
        "username": username, "displayName": "Backend test employee", "role": "employee",
        "password": password, "permissions": ["albums"], "mustChangePassword": True
    }, expected=201)
    try:
        request(employee, "/api/auth/login", {"username": username, "password": password})
        request(employee, "/api/admin/albums", expected=403)
        request(employee, "/api/auth/change-password", {"currentPassword": "wrong", "newPassword": replacement}, expected=400)
        request(employee, "/api/auth/change-password", {"currentPassword": password, "newPassword": replacement})
        request(employee, "/api/admin/albums")
        request(employee, "/api/admin/settings", expected=403)
        request(employee, "/api/admin/settings/home-image", method="DELETE", expected=403)
        request(employee, "/api/admin/settings/home-image", body=b"--employee-home-test--\r\n", method="POST", expected=403)
        request(employee, "/api/admin/finance/entries", expected=403)
        request(employee, "/api/admin/users", expected=403)
        request(employee, "/api/auth/logout", method="POST", expected=204)
        assert request(employee, "/api/auth/session")["authenticated"] is False
        request(employee, "/api/admin/albums", expected=401)
        request(employee, "/api/auth/login", {"username": username, "password": password}, expected=401)
        request(employee, "/api/auth/login", {"username": username, "password": replacement})
        request(admin, "/api/admin/users/" + user["id"], {"isActive": False}, method="PUT")
        request(employee, "/api/admin/albums", expected=401)
        request(employee, "/api/auth/login", {"username": username, "password": replacement}, expected=401)
    finally:
        request(admin, "/api/admin/users/" + user["id"], {"isActive": False}, method="PUT")
    print("Passed: login, required password change, module permissions, logout and disabled sessions.")


if __name__ == "__main__":
    run()
