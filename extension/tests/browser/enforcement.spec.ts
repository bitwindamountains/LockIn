import { test, expect, chromium } from '@playwright/test';
import { mkdtemp, cp, readFile, writeFile, statfs } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';

// These tests seed a host-derived cache in a disposable extension copy. Native transport
// is not simulated as a successful connection, and production builds have no test hooks.
async function browser(action = 'replaceCurrent', simulatedTransport = false) {
  // Check before creating a disposable browser profile. Never delete owner data
  // or prior evidence automatically to make a test fit on the volume.
  for (const location of [tmpdir(), process.cwd()]) {
    const volume = await statfs(location);
    if (volume.bavail * volume.bsize < 1024 ** 3)
      throw new Error(`Insufficient disk space for browser tests at ${location}. Free at least 5 GB for sustained testing or relocate workspace and TEMP/TMP.`);
  }
  const folder = await mkdtemp(path.join(tmpdir(), 'lockin-chromium-'));
  const extension = path.join(folder, 'extension');
  await cp(path.resolve('dist'), extension, { recursive: true });
  const manifest = JSON.parse(await readFile(path.join(extension, 'manifest.json'), 'utf8'));
  manifest.host_permissions = ['*://example.test/*', 'https://www.youtube.com/*'];
  await writeFile(path.join(extension, 'manifest.json'), JSON.stringify(manifest));
  const policy = { sessionId: 'test-session', policyHash: 'a'.repeat(64), revision: 1, remainingSeconds: 120,
    endsAtUtc: new Date(Date.now() + 120000).toISOString(), websites: [{ id: 's', host: 'example.test', includeSubdomains: false }],
    youTube: { rules: [{ id: 't', source: 'visibleHashtags', value: 'education' }], approvedVideoIds: [], matchingMode: 'allowMatchingTags', tabAction: action, destinationKind: 'focusPage' }, studyResources: [] };
  await cp(path.join(extension, 'background.js'), path.join(extension, 'production.js'));
  const transport = simulatedTransport ? `globalThis.testPolls = []; chrome.runtime.connectNative = () => { const listeners = []; return { onMessage: { addListener: f => listeners.push(f) }, onDisconnect: { addListener: () => {} }, postMessage: r => { if (r.type === 'PollBrowser') globalThis.testPolls.push({appliedPolicyHash:r.payload.appliedPolicyHash,errorCode:r.payload.errorCode}); const result = r.type === 'PollBrowser' ? {policy:${JSON.stringify(policy)},requiredOrigins:[],serverUtc:new Date().toISOString(),stagedProfileHash:null} : {acknowledged:true}; setTimeout(() => listeners.forEach(f => f({protocolVersion:1,requestId:r.requestId,ok:true,result})),0); } }; };` : '';
  await writeFile(path.join(extension, 'background.js'), `${transport}const access = chrome.storage.local.setAccessLevel.bind(chrome.storage.local); const seed = chrome.storage.local.get('seeded').then(async s => { if (!s.seeded) await chrome.storage.local.set({seeded:true,${simulatedTransport ? "auth:{installationId:'a'.repeat(32),credential:'b'.repeat(64)}," : ''}cache:{policy:${JSON.stringify(policy)},deadline:${Date.now() + 120000},lastUtc:Date.now()}}); }); chrome.storage.local.setAccessLevel = async options => { await seed; return access(options); }; importScripts('production.js');`);
  const context = await chromium.launchPersistentContext(path.join(folder, 'profile'), { channel: 'chromium', headless: true, args: [`--disable-extensions-except=${extension}`, `--load-extension=${extension}`] });
  const worker = context.serviceWorkers()[0] ?? await context.waitForEvent('serviceworker');
  const id = new URL(worker.url()).host;
  try { await expect.poll(() => worker.evaluate(async () => (await chrome.declarativeNetRequest.getDynamicRules()).length), { timeout: 15000 }).toBe(1); }
  catch (error) { console.log(await worker.evaluate(async () => ({ storage: await chrome.storage.local.get(), permissions: await chrome.permissions.getAll() }))); await context.close(); throw error; }
  await context.route('https://www.youtube.com/**', route => {
    const url = new URL(route.request().url()); const video = url.searchParams.get('v') ?? 'abcdefghijk';
    const tag = video === 'abcdefghijk' ? 'education' : 'gaming';
    return route.fulfill({ contentType: 'text/html', body: `<html><head><link rel="canonical" href="${url.href}"></head><body><ytd-watch-flexy video-id="${video}"><ytd-watch-metadata><h1>Fixture <a href="/hashtag/${tag}">#${tag}</a></h1></ytd-watch-metadata></ytd-watch-flexy><video></video></body></html>` });
  });
  const reopen = () => chromium.launchPersistentContext(path.join(folder, 'profile'), { channel: 'chromium', headless: true, args: [`--disable-extensions-except=${extension}`, `--load-extension=${extension}`] });
  return { context, worker, id, folder, reopen };
}

test('browser restart after offline expiry removes persisted restrictions', async () => {
  const fixture = await browser();
  let context = fixture.context;
  try {
    // Drain startup before injecting the derived deadline; the browser transport
    // remains unavailable. No simulated successful host connection is used here.
    const page = await context.newPage();
    await page.goto(`chrome-extension://${fixture.id}/manifest.json`);
    await page.evaluate(() => chrome.runtime.sendMessage({ type: 'Sync' }));
    const deadline = await fixture.worker.evaluate(async () => {
      const { cache } = await chrome.storage.local.get('cache');
      cache.deadline = Date.now() + 5000;
      await chrome.storage.local.set({ cache });
      return cache.deadline as number;
    });
    await context.close();
    expect(Date.now()).toBeLessThan(deadline);
    await new Promise(resolve => setTimeout(resolve, Math.max(0, deadline - Date.now()) + 100));
    context = await fixture.reopen();
    const worker = context.serviceWorkers()[0] ?? await context.waitForEvent('serviceworker');
    await expect.poll(() => worker.evaluate(async () => ({
      rules: (await chrome.declarativeNetRequest.getDynamicRules()).length,
      scripts: (await chrome.scripting.getRegisteredContentScripts()).length,
      cache: !!(await chrome.storage.local.get('cache')).cache,
      ended: (await chrome.storage.local.get('ended')).ended?.includes('test-session') ?? false
    })), { timeout: 15000 }).toEqual({ rules: 0, scripts: 0, cache: false, ended: true });
  } finally { await context.close(); }
});
test('actual website rules install, redirect, and expire without a native host', async () => {
  const { context, worker, id } = await browser();
  try {
    const page = await context.newPage();
    await page.goto('https://example.test/').catch(() => undefined);
    await expect(page).toHaveURL(`chrome-extension://${id}/blocked.html`);
    await page.goto('https://example.test./').catch(() => undefined);
    await expect(page).toHaveURL(`chrome-extension://${id}/blocked.html`);
    await worker.evaluate(async () => { const { cache } = await chrome.storage.local.get('cache'); cache.deadline = Date.now() + 1000; await chrome.storage.local.set({ cache }); await chrome.alarms.create('lockin-expiry', { when: cache.deadline }); });
    await expect.poll(() => worker.evaluate(async () => (await chrome.declarativeNetRequest.getDynamicRules()).length), { timeout: 12000 }).toBe(0);
    expect(await worker.evaluate(async () => (await chrome.storage.local.get('cache')).cache)).toBeUndefined();
    expect(await worker.evaluate(async () => (await chrome.scripting.getRegisteredContentScripts()).length)).toBe(0);
  } finally { await context.close(); }
});
for (const action of ['replaceCurrent', 'switchExisting', 'openReplacement']) {
  test(`allow tags and ${action} operate on fixture video pages`, async ({}, testInfo) => {
    const { context, id } = await browser(action);
    try {
      const destination = `chrome-extension://${id}/focus.html`;
      const existing = action === 'switchExisting' ? await context.newPage() : null;
      if (existing) await existing.goto(destination);
      const video = await context.newPage();
      await video.goto('https://www.youtube.com/watch?v=abcdefghijk');
      await expect(video.getByRole('status')).toBeVisible();
      await expect(video.getByRole('status')).toHaveCount(0, { timeout: 10000 });
      await expect(video).toHaveURL('https://www.youtube.com/watch?v=abcdefghijk');
      await video.goto('https://www.youtube.com/watch?v=12345678901');
      if (action === 'openReplacement') await expect.poll(() => video.isClosed()).toBe(true);
      else await expect(video).toHaveURL(destination, { timeout: 10000 });
      await expect.poll(() => context.pages().filter(p => p.url() === destination).length).toBe(action === 'switchExisting' ? 2 : 1);
      const focus = context.pages().find(p => p.url() === destination)!;
      await focus.screenshot({ path: testInfo.outputPath('focus-page.png'), fullPage: true });
    } finally { await context.close(); }
  });
}
test('stale current-video metadata redirects instead of using sidebar hashtags', async () => {
  const { context, id } = await browser();
  try {
    await context.route('https://www.youtube.com/**', route => route.fulfill({ contentType: 'text/html', body: '<html><head><link rel="canonical" href="https://www.youtube.com/watch?v=12345678901"></head><body><ytd-watch-flexy video-id="12345678901"><ytd-watch-metadata><h1><a href="/hashtag/education">#education</a></h1></ytd-watch-metadata></ytd-watch-flexy></body></html>' }));
    const page = await context.newPage();
    await page.goto('https://www.youtube.com/watch?v=abcdefghijk');
    await expect(page).toHaveURL(`chrome-extension://${id}/focus.html`, { timeout: 10000 });
  } finally { await context.close(); }
});
test('coverage reports missing rules and refuses a locally expired policy with simulated transport', async () => {
  const { context, worker, id } = await browser('replaceCurrent', true);
  try {
    const page = await context.newPage();
    await page.goto(`chrome-extension://${id}/focus.html`);
    const sync = () => page.evaluate(() => chrome.runtime.sendMessage({ type: 'Sync' }));
    await sync();
    await expect.poll(() => worker.evaluate(() => (globalThis as any).testPolls.some((p: any) => p.appliedPolicyHash === 'a'.repeat(64) && p.errorCode === null))).toBe(true);
    await worker.evaluate(() => chrome.declarativeNetRequest.updateDynamicRules({ removeRuleIds: [10000] }));
    await sync();
    expect(await worker.evaluate(() => (globalThis as any).testPolls.some((p: any) => p.errorCode === 'applyFailed' && p.appliedPolicyHash === null))).toBe(true);
    await worker.evaluate(async () => { const { cache } = await chrome.storage.local.get('cache'); cache.deadline = Date.now() - 1; await chrome.storage.local.set({ cache }); });
    await sync();
    await sync();
    expect(await worker.evaluate(async () => (await chrome.declarativeNetRequest.getDynamicRules()).length)).toBe(0);
    expect(await worker.evaluate(() => (globalThis as any).testPolls.some((p: any) => p.errorCode === 'locallyReleased'))).toBe(true);
  } finally { await context.close(); }
});
test('syncs with an unchanged policy do not rewrite rules or sweep tabs again', async () => {
  const { context, worker, id } = await browser('replaceCurrent', true);
  try {
    const page = await context.newPage();
    await page.goto(`chrome-extension://${id}/manifest.json`);
    const sync = () => page.evaluate(() => chrome.runtime.sendMessage({ type: 'Sync' }));
    await sync();
    await worker.evaluate(() => {
      const counts = (globalThis as any).counts = { rules: 0, sweeps: 0 };
      const update = chrome.declarativeNetRequest.updateDynamicRules.bind(chrome.declarativeNetRequest);
      chrome.declarativeNetRequest.updateDynamicRules = (options: any) => { counts.rules++; return update(options); };
      const query = chrome.tabs.query.bind(chrome.tabs);
      chrome.tabs.query = (info: any) => { if (Object.keys(info).length === 0) counts.sweeps++; return query(info); };
    });
    await sync(); await sync(); await sync();
    expect(await worker.evaluate(() => (globalThis as any).counts)).toEqual({ rules: 0, sweeps: 0 });
    expect(await worker.evaluate(() => (globalThis as any).testPolls.at(-1))).toEqual({ appliedPolicyHash: 'a'.repeat(64), errorCode: null });
  } finally { await context.close(); }
});
test('frequent status checks do not rewrite the stored session cache', async () => {
  const { context, worker, id } = await browser('replaceCurrent', true);
  try {
    const page = await context.newPage();
    await page.goto(`chrome-extension://${id}/manifest.json`);
    const status = () => page.evaluate(() => chrome.runtime.sendMessage({ type: 'PopupStatus' }));
    await page.evaluate(() => chrome.runtime.sendMessage({ type: 'Sync' }));
    await status();
    await worker.evaluate(() => {
      const counts = (globalThis as any).counts = { cacheWrites: 0 };
      const set = chrome.storage.local.set.bind(chrome.storage.local);
      chrome.storage.local.set = (data: any) => { if (data.cache) counts.cacheWrites++; return set(data); };
    });
    await status(); await status(); await status();
    expect(await worker.evaluate(() => (globalThis as any).counts.cacheWrites)).toBe(0);
    expect((await status()).policy?.sessionId).toBe('test-session');
    // A clock rewind beyond 2 s still releases, even between throttled writes.
    await worker.evaluate(() => { const real = Date.now; Date.now = () => real() - 3000; });
    expect((await status()).policy).toBeNull();
  } finally { await context.close(); }
});
test('storage failure during expiry cannot erase ownership and reactivate the session', async () => {
  const { context, worker, id } = await browser('replaceCurrent', true);
  try {
    const page = await context.newPage();
    // Use an inert extension page and drain startup reconciliation before mutating
    // storage. Popup refresh/startup could otherwise overwrite this injected deadline.
    await page.goto(`chrome-extension://${id}/manifest.json`);
    await page.evaluate(() => chrome.runtime.sendMessage({ type: 'Sync' }));
    await worker.evaluate(async () => {
      const { cache } = await chrome.storage.local.get('cache');
      cache.deadline = Date.now() - 1;
      await chrome.storage.local.set({ cache });
      const original = chrome.storage.local.set.bind(chrome.storage.local);
      let failed = false;
      chrome.storage.local.set = async data => {
        if (!failed && data.ended) { failed = true; throw new Error('Injected storage failure'); }
        return original(data);
      };
    });
    await page.evaluate(() => chrome.runtime.sendMessage({ type: 'Sync' }));
    expect(await worker.evaluate(async () => (await chrome.declarativeNetRequest.getDynamicRules()).length)).toBe(0);
    await page.evaluate(() => chrome.runtime.sendMessage({ type: 'Sync' }));
    expect(await worker.evaluate(async () => (await chrome.declarativeNetRequest.getDynamicRules()).length)).toBe(0);
    expect(await worker.evaluate(async () => (await chrome.storage.local.get('ended')).ended)).toContain('test-session');
  } finally { await context.close(); }
});
test('popup respects system and local reduced motion and fits a narrow viewport', async ({}, testInfo) => {
  const { context, id } = await browser();
  try {
    const page = await context.newPage();
    await page.setViewportSize({ width: 320, height: 900 });
    await page.emulateMedia({ reducedMotion: 'reduce' });
    await page.goto(`chrome-extension://${id}/popup.html`);
    await page.getByText('Connection & appearance', { exact: true }).click();
    await expect(page.getByLabel('Reduce motion')).toBeVisible();
    expect(await page.locator('main').evaluate(e => getComputedStyle(e).animationName)).toBe('none');
    expect(await page.evaluate(() => document.documentElement.scrollWidth <= innerWidth)).toBe(true);
    await page.emulateMedia({ reducedMotion: 'no-preference' });
    await page.getByLabel('Reduce motion').check();
    await expect(page.locator('html')).toHaveAttribute('data-reduced-motion', 'true');
    expect(await page.locator('main').evaluate(e => getComputedStyle(e).animationName)).toBe('none');
    await page.reload();
    await page.getByText('Connection & appearance', { exact: true }).click();
    await expect(page.getByLabel('Reduce motion')).toBeChecked();
    await page.screenshot({ path: testInfo.outputPath('popup-reduced-motion.png'), fullPage: true });
  } finally { await context.close(); }
});
