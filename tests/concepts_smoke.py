"""Public concept galleries: only explicitly published, shared album photos are public."""
import http.cookiejar
import json
import os
import secrets
import urllib.error
import urllib.parse
import urllib.request


def run():
    base = os.environ['CRM_TEST_URL'].rstrip('/')
    if urllib.parse.urlparse(base).hostname not in ('localhost', '127.0.0.1'):
        raise RuntimeError('Use a disposable local server')
    admin = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
    guest = urllib.request.build_opener()
    staff = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))

    def request(client, path, body=None, method=None, expected=200):
        payload = json.dumps(body).encode() if body is not None else None
        req = urllib.request.Request(base + path, data=payload, method=method, headers={'Content-Type': 'application/json'})
        try:
            response = client.open(req, timeout=20)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            raw = response.read()
            assert response.status == expected, (path, response.status, expected)
            return json.loads(raw) if raw else None

    categories = request(guest, '/api/public/concepts')
    assert len(categories) == 5
    assert all(item['photoCount'] == 0 for item in categories)
    request(guest, '/api/public/concepts/unknown', expected=404)
    request(guest, '/api/admin/concepts', {'name': 'Denied'}, expected=401)
    request(admin, '/api/auth/login', {'username': 'admin', 'password': os.environ['CRM_TEST_PASSWORD']})
    album = request(admin, '/api/admin/albums')[0]
    internal = request(admin, '/api/admin/concepts', {'name': 'Internal only'}, expected=201)
    assert not internal['isPublished']
    request(admin, '/api/admin/concepts', {'name': 'Invalid', 'category': 'family', 'isPublished': True}, expected=400)
    concept = request(admin, '/api/admin/concepts', {
        'name': 'Public family gallery', 'category': 'family', 'albumId': album['id'],
        'isPublished': True, 'description': 'Public description', 'fee': 987654
    }, expected=201)
    path = '/api/admin/concepts/' + concept['id']
    gallery = request(guest, '/api/public/concepts/family')
    assert set(gallery) == {'id', 'title', 'description', 'concepts'}
    visible = gallery['concepts'][0]
    assert len(visible['photos']) == album['photoCount']
    assert set(visible) == {'id', 'name', 'description', 'photos'}
    assert set(visible['photos'][0]) == {'thumbnailUrl', 'url', 'alt'}
    assert album['shareToken'] not in json.dumps(gallery)
    assert '987654' not in json.dumps(gallery)
    request(admin, path, {'name': 'Must not persist', 'category': 'unknown'}, method='PUT', expected=400)
    assert next(item for item in request(admin, '/api/admin/concepts') if item['id'] == concept['id'])['name'] == concept['name']
    username, password = 'qa-' + secrets.token_hex(5), secrets.token_urlsafe(24)
    request(admin, '/api/admin/users', {'username': username, 'password': password,
        'displayName': 'Wardrobe test', 'role': 'employee', 'permissions': ['wardrobe'], 'mustChangePassword': False}, expected=201)
    request(staff, '/api/auth/login', {'username': username, 'password': password})
    request(staff, path, {'isPublished': False}, method='PUT', expected=403)
    request(staff, '/api/admin/concepts', {'name': 'Staff publication denied', 'category': 'children'}, expected=403)
    request(staff, path, {'fee': 123000}, method='PUT')
    assert request(guest, '/api/public/concepts/family')['concepts']
    request(admin, path, {'isPublished': False}, method='PUT')
    assert not request(guest, '/api/public/concepts/family')['concepts']
    request(admin, path, {'isPublished': True}, method='PUT')
    request(admin, '/api/admin/albums/' + album['id'], {'isPublished': False}, method='PATCH')
    assert not request(guest, '/api/public/concepts/family')['concepts']
    request(staff, path, {'fee': 234000}, method='PUT')
    request(admin, '/api/admin/albums/' + album['id'], {'isPublished': True}, method='PATCH')
    assert request(guest, '/api/public/concepts/family')['concepts']
    request(admin, '/api/admin/albums/' + album['id'], method='DELETE', expected=204)
    current = next(item for item in request(admin, '/api/admin/concepts') if item['id'] == concept['id'])
    assert current['albumId'] == '' and current['isPublished'] is False
    assert not request(guest, '/api/public/concepts/family')['concepts']
    print('Passed: concept publication, category grouping, privacy, staff permissions, invalid-edit isolation and album lifecycle.')


if __name__ == '__main__':
    run()
