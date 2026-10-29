// Run against a disposable local database, following tests/README.md.
const { chromium } = require('playwright');
const assert = require('node:assert/strict');
const base = process.env.CRM_TEST_URL || 'http://127.0.0.1:5092';
assert.ok(['localhost', '127.0.0.1'].includes(new URL(base).hostname));
(async () => {
  const browser = await chromium.launch({ args: ['--no-sandbox'] });
  const page = await browser.newPage();
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  await page.goto(base + '/admin/login');
  await page.locator('[name=username]').fill('admin');
  await page.locator('[name=password]').fill(process.env.CRM_TEST_PASSWORD || 'studio2026');
  await page.getByRole('button', { name: 'Vào studio' }).click();
  await page.waitForURL('**/admin');
  const original = await (await page.request.get(base + '/api/admin/settings')).json();
  try {
    await page.goto(base + '/admin/settings');
    await page.locator('[name=studioName]').fill('Nắng Studio');
    await page.locator('[name=brandName]').fill('nắng');
    await page.locator('[name=brandLabel]').fill('studio');
    await page.locator('[name=heroTitle]').fill('Giữ những ngày đẹp');
    await page.locator('[name=heroDescription]').fill('Lời giới thiệu của Nắng');
    await page.locator('[name=footerDescription]').fill('<img src=x onerror=alert(1)>');
    await page.locator('[name=footerPrivacy]').fill('Album riêng của bạn');
    await page.locator('[name=howItWorksLabel]').fill('Hướng dẫn');
    const saved = page.waitForResponse(r => r.url().endsWith('/api/admin/settings') && r.request().method() === 'PUT');
    await page.locator('[data-settings-submit]').click();
    assert.equal((await saved).status(), 200);
    const settings = await (await page.request.get(base + '/api/admin/settings')).json();
    assert.equal(settings.website.brandName, 'nắng');
    const invalid = await page.request.put(base + '/api/admin/settings', {data: {...settings, website: {...settings.website, brandName: ''}}});
    assert.equal(invalid.status(), 400);
    const long = await page.request.put(base + '/api/admin/settings', {data: {...settings, website: {...settings.website, footerPrivacy: 'a'.repeat(601)}}});
    assert.equal(long.status(), 400);
    const publicSettings = await (await page.request.get(base + '/api/public/website')).json();
    assert.deepEqual(Object.keys(publicSettings).sort(), ['studioName', 'website']);
    await page.evaluate(() => window.dispatchEvent(new CustomEvent('may:navigate', {detail: {path: '/'}})));
    await page.locator('.site-nav').waitFor();
    assert.match(await page.locator('.site-nav .wordmark').innerText(), /nắng/);
    assert.equal(await page.locator('.landing-hero h1').innerText(), 'Giữ những ngày đẹp');
    assert.match(await page.locator('.site-footer').innerText(), /Nắng Studio/);
    assert.equal(await page.locator('.site-footer img').count(), 0);
    assert.match(await page.locator('.site-footer').innerText(), /<img src=x onerror=alert\(1\)>/);
    await page.reload();
    await page.locator('.site-nav').waitFor();
    assert.match(await page.locator('.site-nav .wordmark').innerText(), /nắng/);
    await page.setViewportSize({width: 390, height: 844});
    assert.ok(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
    await page.goto(base + '/admin/settings');
    assert.equal(await page.locator('[name=footerPrivacy]').inputValue(), 'Album riêng của bạn');
    assert.deepEqual(errors, []);
    console.log('Passed: settings save/reload, same-tab homepage, public branding, safe text, validation, mobile layout.');
  } finally {
    await page.request.put(base + '/api/admin/settings', { data: original });
    await browser.close();
  }
})().catch(error => {console.error(error); process.exit(1);});
