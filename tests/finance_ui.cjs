const { chromium } = require("playwright");
const assert = require("node:assert/strict");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const base = process.env.CRM_TEST_URL || "http://127.0.0.1:5080";
assert.ok(
  ["127.0.0.1", "localhost"].includes(new URL(base).hostname),
  "Use a disposable local server",
);
const artifacts = fs.mkdtempSync(path.join(os.tmpdir(), "crm-finance-ui-"));
const marker = "Finance UI " + Date.now();
(async () => {
  const browser = await chromium.launch({
    headless: true,
    args: ["--no-sandbox"],
  });
  const page = await browser.newPage({
    viewport: { width: 1296, height: 1000 },
  });
  const errors = [];
  page.on("pageerror", (e) => errors.push(e.message));
  await page.goto(base + "/admin/login");
  await page.locator("[name=username]").fill("admin");
  await page
    .locator("[name=password]")
    .fill(process.env.CRM_TEST_PASSWORD || "studio2026");
  await page.getByRole("button", { name: "Vào studio" }).click();
  await page.waitForURL("**/admin");
  const settings = await (
    await page.request.get(base + "/api/admin/settings")
  ).json();
  let entryId;
  try {
    await page.goto(base + "/admin/finance");
    await page.locator("[data-fin-tab=entries]").click();
    await page.locator("[data-finance-create]").first().click();
    await page.locator("[data-kind-choice=income]").click();
    await page.locator("[name=amount]").fill("250000");
    await page.locator("[name=note]").fill(marker + "\nDòng thứ hai");
    const png = Buffer.from(
      "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a9V0AAAAASUVORK5CYII=",
      "base64",
    );
    await page.locator("#finance-photo-input").setInputFiles([
      { name: "bien-nhan.png", mimeType: "image/png", buffer: png },
      { name: "chung-tu.png", mimeType: "image/png", buffer: png },
    ]);
    await page.locator("[data-pending-caption]").nth(0).fill("Ảnh biên nhận");
    await page.locator("[data-pending-caption]").nth(1).fill("Ảnh thứ hai");
    let uploads = 0;
    await page.route("**/api/admin/finance/entries/*/photos", async (route) => {
      if (route.request().method() === "POST" && ++uploads === 2)
        await route.fulfill({
          status: 503,
          contentType: "application/json",
          body: JSON.stringify({ error: "Giả lập gián đoạn tải ảnh" }),
        });
      else await route.continue();
    });
    await page.locator("[data-submit]").click();
    await page
      .getByRole("alert")
      .filter({ hasText: "Giả lập gián đoạn" })
      .waitFor();
    assert.equal(await page.locator("[data-existing-photo]").count(), 1);
    assert.equal(await page.locator("[data-pending-photo]").count(), 1);
    await page.locator("[data-submit]").click();
    await page.locator("dialog").waitFor({ state: "detached" });
    let response = await page.request.get(base + "/api/admin/finance/entries");
    const entries = (await response.json()).filter((e) =>
      e.note.startsWith(marker),
    );
    entryId = entries[0]?.id;
    assert.equal(entries.length, 1, "Retry must not duplicate entries");
    assert.equal(
      entries[0].photos.length,
      2,
      "Retry must not duplicate successful photos",
    );
    assert.deepEqual(
      entries[0].photos.map((p) => p.caption),
      ["Ảnh biên nhận", "Ảnh thứ hai"],
    );
    await page.locator(`[data-finance-row="${entryId}"]`).waitFor();
    await page.waitForTimeout(350);
    await page.screenshot({
      path: path.join(artifacts, "finance-desktop.png"),
      fullPage: true,
    });
    const imageWidth = await page
      .locator(".fin-photo img")
      .first()
      .evaluate((e) => e.getBoundingClientRect().width);
    assert.equal(imageWidth, 90);
    await page.locator(`[data-finance-edit="${entryId}"]`).click();
    await page.locator("[data-photo-caption]").first().fill("Chú thích đã sửa");
    await page.locator("[data-photo-remove]").last().click();
    await page.locator("[data-submit]").click();
    await page.locator("dialog").waitFor({ state: "detached" });
    const dl = page.waitForEvent("download");
    await page.locator("[data-fin-export]").first().click();
    const download = await dl;
    await download.saveAs(path.join(artifacts, "thu-chi.xlsx"));
    assert.match(download.suggestedFilename(), /\.xlsx$/);
    await page.setViewportSize({ width: 420, height: 900 });
    await page.waitForTimeout(350);
    await page.screenshot({
      path: path.join(artifacts, "finance-mobile.png"),
      fullPage: true,
    });
    assert.ok(
      await page.evaluate(
        () => document.documentElement.scrollWidth <= innerWidth,
      ),
      "Mobile page must not overflow",
    );
    await page.locator(`[data-finance-edit="${entryId}"]`).click();
    assert.ok(
      await page
        .locator("dialog")
        .evaluate((e) => e.scrollWidth <= e.clientWidth),
      "Modal must fit on mobile",
    );
    await page.waitForTimeout(350);
    await page.screenshot({
      path: path.join(artifacts, "finance-modal-mobile.png"),
      fullPage: true,
    });
    await page.locator("dialog [data-close-dialog]").first().click();
    await page.locator("dialog").waitFor({ state: "detached" });
    await page.goto(base + "/admin/settings");
    await page.locator("[name=studioName]").fill("Studio kiểm thử mới");
    await page.locator("[data-settings-submit]").click();
    await page
      .locator("[data-studio-name-preview]")
      .filter({ hasText: "Studio kiểm thử mới" })
      .waitFor();
    await page.locator("[data-sidebar-open]").click();
    await page
      .getByRole("link", { name: "Tổng quan", exact: true })
      .first()
      .click();
    await page
      .getByText("Studio kiểm thử mới", { exact: false })
      .first()
      .waitFor();
    await page.reload();
    await page
      .getByText("Studio kiểm thử mới", { exact: false })
      .first()
      .waitFor();
    assert.deepEqual(errors, [], "No uncaught browser errors");
    console.log("Artifacts:", artifacts);
    console.log(
      "PASS: create, photos/captions, partial-upload retry, edit/remove, XLSX download, 420px/desktop, studio rename/navigation/reload; no JS exceptions",
    );
  } finally {
    const list = await (
      await page.request.get(base + "/api/admin/finance/entries")
    ).json();
    for (const entry of list.filter((e) => e.note.startsWith(marker)))
      await page.request.delete(
        base + "/api/admin/finance/entries/" + entry.id,
      );
    await page.request.put(base + "/api/admin/settings", {
      data: {
        studioName: settings.studioName,
        contactEmail: settings.contactEmail,
      },
    });
    await browser.close();
  }
})().catch((e) => {
  console.error(e);
  process.exit(1);
});
