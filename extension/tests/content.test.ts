import test from 'node:test';
import assert from 'node:assert/strict';
import vm from 'node:vm';
import { build } from 'esbuild';

// Execute the actual bundled content script; DOM/runtime/clock are synthetic.
const code = (await build({ entryPoints: ['src/content.ts'], bundle: true, write: false, format: 'iife' })).outputFiles[0].text;
const settle = () => new Promise<void>(resolve => setImmediate(resolve));
async function fixture(evaluate: (attempt: number) => Promise<unknown>) {
  let tick: () => void, listener: (message: unknown) => void;
  let time = 1000, utc = Date.now(), evaluations = 0, removed = false;
  const badge = { textContent: '', style: {}, setAttribute() {}, remove() { removed = true; } };
  const video = { paused: false, plays: 0, pause() { this.paused = true; }, play() { this.paused = false; this.plays++; return Promise.resolve(); } };
  const location = { href: 'https://www.youtube.com/watch?v=AbCdEf12345' };
  const policy = { policyHash: 'a'.repeat(64), endsAtUtc: new Date(utc + 60000).toISOString(), youTube: { rules: [{ value: 'education' }], approvedVideoIds: [], matchingMode: 'allowMatchingTags' } };
  const metadata = { querySelectorAll: () => [{ textContent: '#education' }] };
  const document = {
    querySelectorAll: () => [video],
    querySelector: (selector: string) => selector.startsWith('ytd-watch')
      ? { getAttribute: () => new URL(location.href).searchParams.get('v'), querySelector: () => metadata }
      : selector.startsWith('link') ? { href: location.href } : video,
    createElement: () => badge, documentElement: { append() { removed = false; } }, addEventListener() {}, removeEventListener() {}
  };
  const context = { document, location, Date: { now: () => utc, parse: Date.parse }, performance: { now: () => time }, URL,
    crypto: { randomUUID: () => String(time) }, setInterval: (callback: () => void) => { tick = callback; return 1; }, clearInterval() {},
    chrome: { runtime: { onMessage: { addListener(callback: typeof listener) { listener = callback; }, removeListener() {} },
      async sendMessage(message: { type: string }) { return message.type === 'ContentPolicy' ? { policy } : evaluate(++evaluations); } } }
  };
  vm.runInNewContext(code, context);
  await settle();
  return { video, badge, location, policy, get evaluations() { return evaluations; }, get removed() { return removed; },
    async advance(ms = 500) { time += ms; utc += ms; tick!(); await settle(); },
    async release() { listener!({ type: 'Release' }); await settle(); }
  };
}

for (const failure of ['reply', 'reject', 'malformed']) {
  test(`content retries a ${failure} failure and resumes an allowed video once`, async () => {
    const f = await fixture(async attempt => {
      if (attempt > 1) return { decision: 'allow' };
      if (failure === 'reject') throw new Error('Transient transport failure');
      return failure === 'reply' ? { error: 'Transient storage failure' } : {};
    });
    await f.advance();
    assert.equal(f.video.paused, true);
    assert.match(f.badge.textContent, /retry/i);
    await f.advance();
    assert.equal(f.evaluations, 1, 'retry must not run on every tick');
    await f.advance();
    assert.equal(f.evaluations, 2);
    assert.equal(f.video.paused, false);
    assert.equal(f.removed, true);
    await f.advance(10000);
    assert.equal(f.evaluations, 2, 'successful decisions remain deduplicated');
    assert.equal(f.video.plays, 1);
  });
}

test('persistent content failures back off and still release at the deadline', async () => {
  const f = await fixture(async () => ({ error: 'Unavailable' }));
  await f.advance();
  await f.advance(1000);
  await f.advance(1000);
  assert.equal(f.evaluations, 2);
  await f.advance(1000);
  assert.equal(f.evaluations, 3);
  await f.advance(4000);
  await f.advance(8000);
  assert.equal(f.evaluations, 5);
  await f.advance(60000);
  assert.equal(f.removed, true);
  assert.equal(f.evaluations, 5);
  assert.equal(f.video.plays, 0, 'expiry must not restart media');
});

for (const outcome of ['allow', 'reject']) {
  test(`release ignores a late ${outcome} response and clears the pause gate`, async () => {
    let resolve!: (value: unknown) => void, reject!: (error: Error) => void;
    const f = await fixture(() => new Promise((yes, no) => { resolve = yes; reject = no; }));
    await f.advance();
    await f.release();
    if (outcome === 'allow') resolve({ decision: 'allow' }); else reject(new Error('late'));
    await settle();
    await f.advance(10000);
    assert.equal(f.removed, true);
    assert.equal(f.evaluations, 1);
    assert.equal(f.video.plays, 0);
  });
}

for (const outcome of ['allow', 'reject']) {
test(`navigation ignores a late ${outcome} response and evaluates the next video`, async () => {
  let resolve!: (value: unknown) => void, reject!: (error: Error) => void;
  const f = await fixture(attempt => attempt === 1 ? new Promise((yes, no) => { resolve = yes; reject = no; }) : Promise.resolve({ decision: 'allow' }));
  await f.advance();
  f.location.href = 'https://www.youtube.com/watch?v=12345678901';
  if (outcome === 'allow') resolve({ decision: 'allow' }); else reject(new Error('late'));
  await settle();
  assert.equal(f.video.plays, 0);
  await f.advance();
  await f.advance();
  assert.equal(f.evaluations, 2);
  assert.equal(f.removed, true);
});
}
