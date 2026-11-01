"""SaaS regression checks; run_backend.py provisions a separate disposable studio."""
import http.cookiejar
import json
import os
import urllib.error
import urllib.parse
import urllib.request


def run():
    base = os.environ['CRM_TEST_URL'].rstrip('/')
    assert urllib.parse.urlparse(base).hostname in ('127.0.0.1', 'localhost')
    def client():
        return urllib.request.build_opener(urllib.request.HTTPCookieProcessor(http.cookiejar.CookieJar()))

    def request(opener, path, body=None, method=None, expected=200, headers=None):
        req = urllib.request.Request(base + path, data=json.dumps(body).encode() if body is not None else None,
                                     method=method, headers={'Content-Type': 'application/json', **(headers or {})})
        try:
            response = opener.open(req, timeout=30)
        except urllib.error.HTTPError as error:
            response = error
        with response:
            raw = response.read()
            assert response.status == expected, (path, response.status, expected, raw[:250])
            return json.loads(raw) if raw else None

    master, studio, anonymous = client(), client(), client()
    assert request(anonymous, '/api/onboarding/status')['registrationEnabled'] is True
    request(anonymous, '/s/studio-b/api/onboarding/status', expected=404)
    registration = {'studioName': 'Studio mới', 'slug': 'studio-moi', 'username': 'owner', 'email': 'owner@example.test', 'password': 'test-registration-password'}
    created = request(anonymous, '/api/onboarding/register', registration, expected=201)
    assert created['loginUrl'] == '/s/studio-moi/admin/login'
    new_studio = client()
    login_path = created['loginUrl'].replace('/admin/login', '/api/auth/login')
    request(new_studio, login_path, {'username': 'owner', 'password': registration['password']}, expected=403)
    resent = request(anonymous, '/s/studio-moi/api/auth/verification-email', {'identifier': 'owner'}, expected=202)
    request(anonymous, '/s/studio-moi/api/auth/verify-email', {'token': created['verificationToken']}, expected=400)
    request(anonymous, '/s/studio-moi/api/auth/verify-email', {'token': resent['token']})
    request(anonymous, '/s/studio-moi/api/auth/verify-email', {'token': resent['token']}, expected=400)
    request(new_studio, login_path, {'username': 'owner', 'password': registration['password']})
    second_owner_session = client()
    request(second_owner_session, login_path, {'username': 'owner', 'password': registration['password']})
    assert request(anonymous, '/s/studio-moi/api/auth/forgot-password', {'identifier': 'missing@example.test'}, expected=202)['token'] is None
    reset = request(anonymous, '/s/studio-moi/api/auth/forgot-password', {'identifier': 'owner@example.test'}, expected=202)
    replacement_password = 'replacement-registration-password'
    request(anonymous, '/s/studio-moi/api/auth/reset-password', {'token': reset['token'], 'newPassword': replacement_password})
    assert request(new_studio, '/s/studio-moi/api/auth/session')['authenticated'] is False
    assert request(second_owner_session, '/s/studio-moi/api/auth/session')['authenticated'] is False
    request(anonymous, '/s/studio-moi/api/auth/reset-password', {'token': reset['token'], 'newPassword': replacement_password}, expected=400)
    request(new_studio, login_path, {'username': 'owner', 'password': registration['password']}, expected=401)
    request(new_studio, login_path, {'username': 'owner', 'password': replacement_password})
    new_status = request(new_studio, '/s/studio-moi/api/saas/status')
    assert new_status['currentPlan'] == 'Free' and new_status['maxUsers'] == 1
    request(new_studio, '/s/studio-b/api/saas/status', expected=401)
    request(anonymous, '/api/onboarding/register', registration, expected=400)
    request(anonymous, '/api/onboarding/register', {**registration, 'slug': 'x', 'password': '123'}, expected=400)
    request(anonymous, '/s/studio-moi/api/onboarding/register', {**registration, 'slug': 'other-studio'}, expected=404)
    request(master, '/api/auth/login', {'username': 'admin', 'password': os.environ['CRM_TEST_PASSWORD']})
    root_status = request(master, '/api/saas/status')
    assert root_status['currentPlan'] == 'ErpStudio' and root_status['isTrialGift']
    prefix = '/s/studio-b'
    request(master, prefix + '/api/saas/status', expected=401)  # Master cookie cannot access another tenant.
    request(studio, prefix + '/api/auth/login', {'username': 'owner', 'password': os.environ['CRM_TEST_TENANT_PASSWORD']})
    status = request(studio, prefix + '/api/saas/status')
    assert status['currentPlan'] == 'Free' and status['maxUsers'] == 1 and status['studioCode'] != root_status['studioCode']
    assert request(studio, prefix + '/api/admin/albums') == []
    locked = request(studio, prefix + '/api/admin/customers', expected=403)
    assert locked['code'] == 'subscription_feature_locked' and locked['feature'] == 'Crm' and locked['requiredPlan'] == 'ErpSolo'
    catalog = request(anonymous, '/api/public/plans')
    assert [(p['key'], p['monthlyPrice']) for p in catalog] == [('Free', 0), ('AlbumPro', 75000), ('ErpSolo', 125000), ('ErpStudio', 225000)]
    request(studio, prefix + '/api/admin/finance/entries', expected=403)
    request(studio, prefix + '/api/saas/backup/albums', expected=403)
    request(studio, prefix + '/api/admin/users', {'username': 'second', 'password': 'test-only-password', 'role': 'employee'}, expected=403)
    request(studio, prefix + '/api/saas/domains', {'slug': 'studio-b', 'customDomain': 'gallery.example.net'}, method='PUT', expected=403)
    request(studio, prefix + '/api/saas/domains', {'slug': '', 'customDomain': None}, method='PUT', expected=400)
    request(studio, prefix + '/api/saas/preferences', {'serviceTypes': [{'name': 'Gia đình', 'color': '#42665b'}], 'greeting': 'Xin chào studio B'}, method='PUT')
    assert request(studio, prefix + '/api/saas/preferences')['greeting'] == 'Xin chào studio B'
    assert request(master, '/api/saas/preferences')['greeting'] != 'Xin chào studio B'
    request(studio, prefix + '/api/saas/locations', {'locations': ['Phòng riêng B']}, method='PUT')
    assert request(studio, prefix + '/api/saas/status')['quickLocations'] == ['Phòng riêng B']
    for i in range(5):
        result = request(studio, prefix + '/api/saas/pc-filter/consume', {'albumKey': 'album-' + str(i)})
        assert result['currentMonthPcFilterUsed'] == i + 1
    assert request(studio, prefix + '/api/saas/pc-filter/consume', {'albumKey': 'album-0'})['currentMonthPcFilterUsed'] == 5
    request(studio, prefix + '/api/saas/pc-filter/consume', {'albumKey': 'sixth'}, expected=403)
    request(studio, prefix + '/api/saas/pc-filter/consume', {'albumKey': 'forged'}, expected=403, headers={'Origin': 'https://evil.example'})
    subdomain = request(anonymous, '/api/public/website', headers={'Host': 'studio-b.crm.test'})
    assert subdomain['studioName'] == 'Isolated free studio'
    request(anonymous, '/api/public/website', expected=404, headers={'Host': 'unknown.crm.test'})
    request(anonymous, '/api/public/website', expected=404, headers={'Host': 'gallery.example.net'})
    request(anonymous, '/s/unknown/api/public/website', expected=404)
    request(anonymous, '/api/payments/sepay-webhook', {}, expected=401)
    for index, plan, amount in ((1, 'AlbumPro', 75000), (2, 'ErpSolo', 125000)):
        order = request(studio, prefix + '/api/saas/billing/checkout', {'plan': plan, 'billingCycle': 'Monthly'})
        assert order['amount'] == amount
        message = {'id': index, 'transferType': 'in', 'accountNumber': 'test-account', 'content': order['description'], 'transferAmount': amount - 1}
        headers = {'Authorization': 'Apikey integration-test-key'}
        request(anonymous, '/api/payments/sepay-webhook', message, expected=422, headers=headers)
        message['transferAmount'] = amount
        assert request(anonymous, '/api/payments/sepay-webhook', message, headers=headers)['success']
        end = request(studio, prefix + '/api/saas/status')['subscriptionExpiresAt']
        request(anonymous, '/api/payments/sepay-webhook', message, headers=headers)
        status = request(studio, prefix + '/api/saas/status')
        assert status['subscriptionExpiresAt'] == end and status['currentPlan'] == plan
        if index == 1:
            request(studio, prefix + '/api/admin/customers', expected=403)
            request(studio, prefix + '/api/saas/backup/albums')
            request(studio, prefix + '/api/saas/pc-filter/consume', {'albumKey': 'sixth'})
    assert request(studio, prefix + '/api/admin/customers') == []
    locked_staff = request(studio, prefix + '/api/admin/staff', {}, expected=403)
    assert locked_staff['requiredPlan'] == 'ErpStudio' and locked_staff['feature'] == 'StaffRbac'
    assert len(request(studio, prefix + '/api/saas/billing/history')['transactions']) == 2
    order = request(studio, prefix + '/api/saas/billing/checkout', {'plan': 'ErpStudio', 'billingCycle': 'Monthly'})
    request(anonymous, '/api/payments/sepay-webhook', {'id': 3, 'transferType': 'in', 'accountNumber': 'test-account', 'content': order['description'], 'transferAmount': 225000}, headers={'Authorization': 'Apikey integration-test-key'})
    assert request(studio, prefix + '/api/saas/status')['currentPlan'] == 'ErpStudio'
    assert request(studio, prefix + '/api/admin/staff') == []
    assert request(master, '/api/saas/billing/history')['transactions'] == []
    request(studio, prefix + '/api/saas/invitations/accept', {'token': None, 'username': None, 'password': None, 'displayName': ''}, expected=400)
    print('Passed: tenant path/subdomain, session & data isolation, gates, quota/idempotency, preferences, webhook auth/amount/replay and upgrades.')


if __name__ == '__main__':
    run()
