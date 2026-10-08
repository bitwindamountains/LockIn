import { test, expect } from '@playwright/test';
import { readFile } from 'node:fs/promises';
import path from 'node:path';

// Presentation fixtures only: no native host or actual restriction is claimed here.
test.beforeEach(async ({ page }) => {
  await page.route('https://lockin.test/**', async route => {
    const file = path.basename(new URL(route.request().url()).pathname);
    const mime = file.endsWith('.css') ? 'text/css' : file.endsWith('.js') ? 'text/javascript' : 'text/html';
    await route.fulfill({ contentType: mime, body: await readFile(path.resolve('dist', file)) });
  });
  await page.addInitScript(() => {
    const fixture = (window as any).fixture = {
      fail: false, deny: false, calls: [] as any[],
      state: { paired: false, message: 'Desktop connected. Paste your setup code.', remaining: 1200, requiredOrigins: ['https://www.youtube.com/*'], needsPermissions: false, policy: null as any }
    };
    (window as any).chrome = {
      storage: { local: { get: async () => ({}), set: async () => {} }, onChanged: { addListener: () => {} } },
      permissions: { request: async (request: any) => { fixture.calls.push(request); if (!fixture.deny) fixture.state.needsPermissions = false; return !fixture.deny; } },
      runtime: { sendMessage: async (request: any) => {
        if (fixture.fail) throw new Error('Fixture transport unavailable');
        if (request.type === 'Pair') { fixture.state.paired = true; fixture.state.needsPermissions = true; fixture.state.message = 'Review selected site access.'; }
        return request.type === 'PopupStatus' ? structuredClone(fixture.state) : {};
      } }
    };
  });
});

test('popup pairs, discloses permissions and retains a usable focus target', async ({ page }, info) => {
  await page.setViewportSize({ width: 320, height: 760 });
  await page.goto('https://lockin.test/popup.html');
  await expect(page.locator('#pair')).toBeEnabled();
  await page.getByLabel('Setup code').fill('123456');
  await expect(page.locator('#pairing')).toBeHidden();
  await expect(page.locator('#heading')).toBeFocused();
  await expect(page.getByRole('button', { name: 'Allow site access' })).toBeVisible();
  await page.evaluate(() => { (window as any).fixture.deny = true; });
  await page.getByRole('button', { name: 'Allow site access' }).click();
  await expect(page.getByRole('alert')).toHaveText('Site access was not granted.');
  await page.evaluate(() => { (window as any).fixture.deny = false; });
  await page.getByRole('button', { name: 'Allow site access' }).click();
  await expect(page.locator('#permissions')).toBeHidden();
  await expect(page.locator('#heading')).toBeFocused();
  expect(await page.evaluate(() => (window as any).fixture.calls)).toEqual([
    { origins: ['https://www.youtube.com/*'] }, { origins: ['https://www.youtube.com/*'] }
  ]);
  for (const width of [320, 375]) {
    await page.setViewportSize({ width, height: 760 });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    await page.screenshot({ path: info.outputPath(`popup-${width}.png`), fullPage: true });
  }
});

test('study pages fit six widths and polling preserves focused resource links', async ({ page }, info) => {
  await page.goto('https://lockin.test/focus.html');
  await page.evaluate(() => {
    (window as any).fixture.state.policy = { endsAtUtc: new Date(Date.now() + 1200000).toISOString(), studyResources: ['https://study.example.org/course/a-long-readable-lesson-name'] };
    (window as any).fixture.state.message = 'Browser session active.';
  });
  await page.getByRole('button', { name: 'Check session' }).click();
  const link = page.getByRole('link', { name: /study.example.org/ });
  await expect(link).toBeVisible();
  await link.focus();
  await expect(link).toBeFocused();
  await page.waitForTimeout(5200); // Exercise one actual polling interval.
  await expect(link).toBeFocused();
  for (const width of [320, 375, 768, 1024, 1280, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    await expect(link).toBeVisible();
    expect((await link.boundingBox())!.height).toBeGreaterThanOrEqual(44);
    await page.screenshot({ path: info.outputPath(`focus-${width}.png`), fullPage: true });
  }
  await page.evaluate(() => { (window as any).fixture.fail = true; });
  await page.getByRole('button', { name: 'Check session' }).click();
  await expect(page.locator('#timer')).toHaveText('Status unavailable');
  await expect(page.getByRole('alert')).toContainText('Could not check');
  await page.evaluate(() => { (window as any).fixture.fail = false; (window as any).fixture.state.policy = null; });
  await page.getByRole('button', { name: 'Check session' }).click();
  await expect(page.getByRole('link')).toHaveCount(0);
  await expect(page.locator('#resources-empty')).toContainText('when a browser session starts');
  await expect(page.locator('#error')).toBeEmpty();
  await page.evaluate(() => { (window as any).fixture.state.coverageIssue = 'cleanupFailed'; });
  await page.getByRole('button', { name: 'Check session' }).click();
  await expect(page.locator('#timer')).toHaveText('Recovery needed');
  await expect(page.getByRole('status')).toContainText('Cleanup is unconfirmed');
  await page.goto('https://lockin.test/blocked.html');
  await expect(page.getByRole('heading', { name: 'This can wait.' })).toBeVisible();
  for (const width of [320, 375, 768, 1024, 1280, 1440]) {
    await page.setViewportSize({ width, height: 900 });
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
  }
});
