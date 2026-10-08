import { matchVideo, parseVideoId } from './shared/policy';
import type { Policy } from './shared/policy';

const scope = globalThis as typeof globalThis & { __lockinActive?: boolean };
if (!scope.__lockinActive) {
  scope.__lockinActive = true;
  let policy: Policy | null = null;
  let videoId: string | null = null;
  let generation = '';
  let started = 0;
  let lastUtc = Date.now();
  let lastSignature = '';
  let failures = 0;
  let retryAt = 0;
  let retryVideoId: string | null = null;
  let busy = false;
  let disposed = false;
  let gated = false;
  let wasPlaying = false;
  let badge: HTMLDivElement | null = null;
  function pause(): void { if (gated) document.querySelectorAll('video').forEach(video => video.pause()); }
  function gate(): void {
    if (!gated) {
      wasPlaying = [...document.querySelectorAll('video')].some(v => !v.paused);
      gated = true;
      badge = document.createElement('div'); badge.setAttribute('role', 'status');
      badge.textContent = 'LockIn · Checking this video’s allowed hashtags…';
      badge.style.cssText = 'position:fixed;right:24px;bottom:24px;z-index:2147483647;padding:16px 20px;border-radius:12px;background:#15251f;color:#d4f5e0;font:14px system-ui;box-shadow:0 6px 24px #0004';
      document.documentElement.append(badge);
    }
    pause();
  }
  function ungate(resume: boolean): void {
    gated = false; badge?.remove(); badge = null;
    if (resume && wasPlaying) document.querySelector('video')?.play().catch(() => undefined);
    wasPlaying = false;
  }
  function dispose(): void {
    disposed = true;
    ungate(false); clearInterval(timer); document.removeEventListener('play', pause, true);
    chrome.runtime.onMessage.removeListener(onMessage); scope.__lockinActive = false;
  }
  function onMessage(message: any): void { if (message?.type === 'Release') dispose(); }
  chrome.runtime.onMessage.addListener(onMessage);
  document.addEventListener('play', pause, true);
  async function tick(): Promise<void> {
    if (disposed) return;
    const now = Date.now();
    if (now < lastUtc - 2000 || policy && now >= Date.parse(policy.endsAtUtc)) { dispose(); return; }
    lastUtc = now;
    if (busy) return;
    // Limit retries without delaying deadline checks, release or a new video.
    if (performance.now() < retryAt && parseVideoId(location.href) === retryVideoId) return;
    const attemptVideoId = parseVideoId(location.href);
    busy = true;
    try {
      if (!policy) {
        const response = await chrome.runtime.sendMessage({ type: 'ContentPolicy' });
        if (disposed) return;
        if (!response || response.error || !('policy' in response)) throw new Error('Policy check failed.');
        policy = response?.policy ?? null;
        if (!policy) { dispose(); return; }
      }
      const currentId = parseVideoId(location.href);
      if (!currentId) { videoId = null; ungate(false); return; }
      if (currentId !== videoId) {
        failures = 0; retryAt = 0;
        ungate(false); videoId = currentId; generation = crypto.randomUUID(); started = performance.now(); lastSignature = ''; gate();
      }
      const capturedGeneration = generation;
      const flexy = document.querySelector('ytd-watch-flexy[video-id]');
      const metadata = flexy?.querySelector('ytd-watch-metadata');
      const canonical = document.querySelector<HTMLLinkElement>('link[rel="canonical"]')?.href;
      let hashtags: string[] | null = null;
      // Only current-video title/description anchors are inspected; sidebar/comment tags are excluded.
      if (flexy?.getAttribute('video-id') === currentId && metadata && canonical && parseVideoId(canonical) === currentId && performance.now() - started >= 500) {
        hashtags = [...metadata.querySelectorAll<HTMLAnchorElement>('h1 a[href*="/hashtag/"], #description-inline-expander a[href*="/hashtag/"], ytd-text-inline-expander a[href*="/hashtag/"]')]
          .slice(0, 100).map(a => a.textContent?.trim() ?? '').filter(t => t.startsWith('#') && t.length <= 100);
      }
      if (hashtags === null && performance.now() - started < 3000) return;
      const signature = JSON.stringify([policy.policyHash, currentId, hashtags]);
      if (signature === lastSignature) return;
      if (matchVideo(policy.youTube, currentId, hashtags) === 'redirect') gate();
      const response = await chrome.runtime.sendMessage({ type: 'EvaluateVideo', videoId: currentId, hashtags, generation, policyHash: policy.policyHash });
      if (disposed) return;
      if (capturedGeneration !== generation || parseVideoId(location.href) !== currentId) return;
      if (Date.now() >= Date.parse(policy.endsAtUtc)) { dispose(); return; }
      if (response?.error || !['allow', 'coverage-warning', 'ignore', 'redirect'].includes(response?.decision))
        throw new Error('Video check failed.');
      // Only successful decisions suppress duplicate checks. Transport/storage
      // failures must remain retryable even when the page metadata is unchanged.
      lastSignature = signature;
      failures = 0; retryAt = 0;
      if (response?.decision === 'allow' || response?.decision === 'coverage-warning') ungate(true);
      else if (response?.decision === 'ignore') ungate(false);
    } catch {
      if (disposed) return;
      if (policy && Date.now() >= Date.parse(policy.endsAtUtc)) { dispose(); return; }
      if (parseVideoId(location.href) !== attemptVideoId) return;
      const delay = Math.min(8000, 1000 * 2 ** Math.min(failures++, 3));
      retryAt = performance.now() + delay;
      retryVideoId = attemptVideoId;
      if (badge) badge.textContent = 'LockIn · Video check unavailable. Retrying automatically. Open LockIn for connection or recovery options.';
    }
    finally { busy = false; }
  }
  const timer = setInterval(() => { void tick(); }, 500);
  void tick();
}
