"""Run against a disposable local server, never a production studio.

Start with a separate Data__Directory, then: python3 tests/finance_smoke.py
No third-party Python packages required. Set CRM_TEST_URL/CRM_TEST_PASSWORD
for a local server with a different port or initial admin password.
"""
import base64
import http.cookiejar
import io
import json
import os
import secrets
import urllib.error
import urllib.parse
import urllib.request
import zipfile
import xml.etree.ElementTree as ET
from datetime import date

BASE = os.environ.get("CRM_TEST_URL", "http://127.0.0.1:5080").rstrip("/")
assert urllib.parse.urlparse(BASE).hostname in ("127.0.0.1", "localhost"), "Use a disposable local server"
client = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
NS = {"s": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}


def request(path, body=None, method=None, expected=200, headers=None, authenticated=True):
    headers = dict(headers or {})
    if isinstance(body, dict):
        body = json.dumps(body).encode()
        headers["Content-Type"] = "application/json"
    req = urllib.request.Request(BASE + path, data=body, method=method, headers=headers)
    try:
        res = (client.open if authenticated else urllib.request.urlopen)(req, timeout=20)
    except urllib.error.HTTPError as error:
        res = error
    with res:
        raw = res.read()
        assert res.status == expected, (path, res.status, raw[:300])
        return json.loads(raw) if "application/json" in res.headers.get("Content-Type", "") else raw


def workbook(path):
    raw = request(path)
    with zipfile.ZipFile(io.BytesIO(raw)) as archive:
        assert archive.testzip() is None
        for name in archive.namelist():
            if name.endswith((".xml", ".rels")):
                ET.fromstring(archive.read(name))
        book = ET.fromstring(archive.read("xl/workbook.xml"))
        names = [sheet.attrib["name"] for sheet in book.findall("s:sheets/s:sheet", NS)]
        sheets = []
        for index in range(len(names)):
            root = ET.fromstring(archive.read(f"xl/worksheets/sheet{index + 1}.xml"))
            assert root.find("s:sheetViews/s:sheetView/s:pane", NS).attrib["state"] == "frozen"
            rows = []
            for row in root.findall("s:sheetData/s:row", NS):
                cells = {}
                for cell in row.findall("s:c", NS):
                    value = cell.find("s:is/s:t", NS) if cell.attrib.get("t") == "inlineStr" else cell.find("s:v", NS)
                    cells[cell.attrib["r"]] = value.text if value is not None else ""
                rows.append(cells)
            sheets.append(rows)
        return names, sheets


def upload(entry_id, data, caption="", expected=201):
    boundary = "test-" + secrets.token_hex(12)
    body = (
        f'--{boundary}\r\nContent-Disposition: form-data; name="caption"\r\n\r\n{caption}\r\n'
        f'--{boundary}\r\nContent-Disposition: form-data; name="file"; filename="receipt.png"\r\n'
        'Content-Type: image/png\r\n\r\n'
    ).encode() + data + f"\r\n--{boundary}--\r\n".encode()
    return request(f"/api/admin/finance/entries/{entry_id}/photos", body,
                   headers={"Content-Type": f"multipart/form-data; boundary={boundary}"}, expected=expected)


def run():
    request("/api/auth/login", {"username": "admin", "password": os.environ.get("CRM_TEST_PASSWORD", "studio2026")})
    settings = request("/api/admin/settings")
    month = date.today().strftime("%Y-%m")
    today = date.today().isoformat()
    ledger = "/api/admin/finance/entries"
    baseline = request(f"/api/admin/finance/overview?month={month}")
    entry_ids = []
    customer_id = contract_id = None
    marker = "QA-" + secrets.token_hex(5)
    try:
        for kind, amount in (("income", 700000), ("expense", 123000)):
            entry = request(ledger, {"kind": kind, "date": today, "amount": amount,
                                    "category": "Khác", "note": marker + "\nGhi chú tiếng Việt"}, expected=201)
            entry_ids.append(entry["id"])
        income_id = entry_ids[0]
        request(ledger + "/" + income_id, {"kind": "expense", "amount": -1}, method="PUT", expected=400)
        unchanged = next(e for e in request(ledger) if e["id"] == income_id)
        assert unchanged["kind"] == "income" and unchanged["amount"] == 700000
        png = base64.b64decode("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a9V0AAAAASUVORK5CYII=")
        photo = upload(income_id, png, "Biên nhận tiếng Việt")
        assert request(photo["url"]) == png
        request(photo["url"], authenticated=False, expected=401)
        upload(income_id, b"not a real image file", expected=400)
        upload(income_id, b"x" * (5 * 1024 * 1024 + 1), expected=400)
        updated = request(ledger + "/" + income_id, {"photos": [{"id": photo["id"], "caption": "Ảnh đã sửa"}]}, method="PUT")
        assert updated["photos"][0]["caption"] == "Ảnh đã sửa"
        for _ in range(5):
            upload(income_id, png)
        upload(income_id, png, expected=400)
        assert len(next(e for e in request(ledger) if e["id"] == income_id)["photos"]) == 6
        after = request(f"/api/admin/finance/overview?month={month}")
        assert after["otherIncome"] - baseline["otherIncome"] == 700000
        assert after["expenseTotal"] - baseline["expenseTotal"] == 123000
        assert after["profit"] - baseline["profit"] == 577000
        for kind in ("income", "expense"):
            rows = request(f"{ledger}?month={month}&kind={kind}")
            assert all(e["kind"] == kind for e in rows)
            _, sheets = workbook(f"{ledger}/export?month={month}&kind={kind}")
            text = str(sheets)
            assert marker in text and "Ghi chú tiếng Việt" in text
            assert ("Ảnh đã sửa" in text) == (kind == "income")
        names, sheets = workbook(f"/api/admin/finance/export?month={month}")
        assert names == ["Tổng quan", "Bảng lương", "Thu chi"], names
        summary = [list(row.values()) for row in sheets[0]]
        assert any("Thu khác" in row and str(after["otherIncome"]) in row for row in summary)
        request(f"{ledger}/export", authenticated=False, expected=401)
        request("/api/admin/contracts/export", authenticated=False, expected=401)
        customer_id = request("/api/admin/customers", {"name": marker}, expected=201)["id"]
        contract = request("/api/admin/contracts", {"customerId": customer_id, "service": marker,
                           "total": 900000, "status": "pending"}, expected=201)
        contract_id = contract["id"]
        _, sheets = workbook("/api/admin/contracts/export?q=" + marker)
        assert contract["code"] in str(sheets) and "900000" in str(sheets)
        _, sheets = workbook("/api/admin/contracts/export?status=signed&q=" + marker)
        assert contract["code"] not in str(sheets), "Export must respect current filters"
        request("/api/admin/settings", {"studioName": " "}, method="PUT", expected=400)
        request("/api/admin/settings", {"studioName": marker + " Studio", "contactEmail": settings["contactEmail"]}, method="PUT")
        assert request("/api/auth/session")["studioName"] == marker + " Studio"
        request(ledger + "/" + income_id + "/photos/" + photo["id"], method="DELETE", expected=204)
        request(photo["url"], expected=404)
        print("PASS: income/expense totals, filters, notes, photo validation/limits/auth/captions/delete, XLSX content, contracts export, studio rename")
    finally:
        request("/api/admin/settings", {"studioName": settings["studioName"], "contactEmail": settings["contactEmail"]}, method="PUT")
        if contract_id:
            request("/api/admin/contracts/" + contract_id, method="DELETE", expected=204)
        if customer_id:
            request("/api/admin/customers/" + customer_id, method="DELETE", expected=204)
        for entry_id in entry_ids:
            request(ledger + "/" + entry_id, method="DELETE", expected=204)


if __name__ == "__main__":
    run()
