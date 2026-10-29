"""Checks the public price catalogue and its authenticated editor on disposable data."""
import http.cookiejar, json, os, urllib.error, urllib.parse, urllib.request

def run():
    base = os.environ['CRM_TEST_URL'].rstrip('/')
    if urllib.parse.urlparse(base).hostname not in ('localhost', '127.0.0.1'):
        raise RuntimeError('Use a disposable local server')
    guest = urllib.request.build_opener()
    admin = urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))
    def req(client, path, body=None, method=None, status=200):
        request = urllib.request.Request(base + path, data=json.dumps(body).encode() if body is not None else None, method=method, headers={'Content-Type':'application/json'})
        try: response = client.open(request, timeout=20)
        except urllib.error.HTTPError as error: response = error
        with response:
            data = response.read()
            assert response.status == status, (path, response.status, status)
            return json.loads(data) if data else None
    groups = req(guest, '/api/public/prices')
    assert [g['id'] for g in groups] == ['family','children','newborn','maternity','beauty']
    assert sum(len(g['items']) for g in groups) == 27
    assert groups[0]['items'][0]['priceText'].startswith('Thứ 2 - Thứ 6: 1.800.000đ')
    path = '/api/admin/price-catalog/family/family-1'
    req(guest, path, {'name':'forbidden'}, 'PUT', 401)
    req(admin, '/api/auth/login', {'username':'admin','password':os.environ['CRM_TEST_PASSWORD']})
    packages_before = req(admin, '/api/admin/packages')
    item = req(admin, '/api/admin/price-catalog')[0]['items'][0]
    item['priceText'] = 'Giá kiểm tra'; item['visible'] = False
    req(admin, path, item, 'PUT')
    assert all(i['id'] != item['id'] for i in req(guest, '/api/public/prices')[0]['items'])
    item['visible'] = True
    req(admin, path, item, 'PUT')
    assert req(guest, '/api/public/prices')[0]['items'][0]['priceText'] == 'Giá kiểm tra'
    assert req(admin, '/api/admin/packages') == packages_before
    req(guest, '/api/admin/price-catalog/groups', {'title':'Denied'}, 'POST', 401)
    group = req(admin, '/api/admin/price-catalog/groups', {'title':'Nhóm thử', 'description':'Mô tả thử'}, 'POST', 201)
    item_path = '/api/admin/price-catalog/' + group['id'] + '/items'
    req(admin, item_path, {'name':'', 'visible':True}, 'POST', 400)
    created = req(admin, item_path, {'name':'Gói mới', 'priceText':'2.000đ', 'benefits':'Ảnh\nTrang phục', 'visible':True}, 'POST', 201)
    visible_group = next(g for g in req(guest, '/api/public/prices') if g['id'] == group['id'])
    assert visible_group['items'][0]['id'] == created['id']
    assert visible_group['items'][0]['benefits'] == 'Ảnh\nTrang phục'
    req(admin, '/api/admin/price-catalog/missing/items', {'name':'Gói', 'visible':True}, 'POST', 404)
    assert req(admin, '/api/admin/packages') == packages_before
    item['name'] = ''
    req(admin, path, item, 'PUT', 400)
    assert req(guest, '/api/public/prices')[0]['items'][0]['priceText'] == 'Giá kiểm tra'
    print('Passed: default pricing, authorization, edit, visibility and existing packages.')

if __name__ == '__main__': run()
