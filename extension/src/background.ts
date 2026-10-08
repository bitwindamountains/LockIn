import { blockedHost, cacheValid, compileSites, matchVideo, originsFor, ownedRule, parseVideoId, safeOrigins, validPolicy } from './shared/policy';
import type { Cache, Policy } from './shared/policy';

type Auth = { installationId: string; credential: string };
type Poll = { policy: Policy | null; releaseSessionId: string | null; releasePolicyHash: string | null; serverUtc: string; stagedProfileHash: string | null; requiredOrigins: string[] };
let port: chrome.runtime.Port | null = null;
const requests = new Map<string, { resolve: (value: any) => void; reject: (reason: Error) => void; timer: ReturnType<typeof setTimeout> }>();
let serial: Promise<unknown> = Promise.resolve();
let lastPoll: Poll | null = null;
let message = 'Pair with the desktop to begin.';
type CoverageIssue = 'permissionMissing' | 'applyFailed' | 'locallyReleased' | 'clockDiscrepancy' | 'cleanupFailed';
let coverageIssue: CoverageIssue | null = null;
let syncQueued = false;
let inFlight = new Set<number>();
const redirected = new Map<number, string>();
const initialize = chrome.storage.local.setAccessLevel({ accessLevel: 'TRUSTED_CONTEXTS' });
const enqueue = <T>(work: () => Promise<T>): Promise<T> => {
  const next = serial.then(work, work); serial = next.catch(() => undefined); return next;
};
function scheduleSync(): void {
  if (syncQueued) return;
  syncQueued = true;
  void enqueue(synchronize).finally(() => { syncQueued = false; }).catch(() => {
    coverageIssue = 'cleanupFailed'; message = 'Browser reconciliation failed. Reopen the browser and use desktop recovery.';
  });
}
function native(type: string, payload: unknown): Promise<any> {
  if (!port) {
    port = chrome.runtime.connectNative('com.lockin.bridge');
    port.onMessage.addListener(reply => {
      const pending = requests.get(reply?.requestId);
      if (!pending) return;
      requests.delete(reply.requestId); clearTimeout(pending.timer);
      if (reply.protocolVersion !== 1 || !reply.ok) pending.reject(new Error(reply.error || 'Native host rejected the message.'));
      else pending.resolve(reply.result);
    });
    port.onDisconnect.addListener(() => {
      const detail = chrome.runtime.lastError?.message ?? 'Native host disconnected.';
      port = null;
      for (const request of requests.values()) { clearTimeout(request.timer); request.reject(new Error(`Windows bridge: ${detail} Reload the extension after checking its native registration.`)); }
      requests.clear();
    });
  }
  return new Promise((resolve, reject) => {
    const id = crypto.randomUUID();
    const timer = setTimeout(() => { requests.delete(id); reject(new Error('Windows bridge timed out.')); }, 8000);
    requests.set(id, { resolve, reject, timer });
    port!.postMessage({ protocolVersion: 1, requestId: id, type, payload });
  });
}
async function state(): Promise<{ cache?: Cache; auth?: Auth; ended?: string[] }> { await initialize; return await chrome.storage.local.get(['cache', 'auth', 'ended']); }
async function release(): Promise<void> {
  const { cache, ended = [] } = await state();
  // Persist the release decision before removing ownership. A failed write leaves
  // the cache available for retry instead of allowing a stale host policy to return.
  let markerFailure: unknown;
  if (cache) {
    try { await chrome.storage.local.set({ ended: [...new Set([...ended, cache.policy.sessionId])].slice(-20) }); }
    catch (error) { markerFailure = error; }
  }
  const rules = await chrome.declarativeNetRequest.getDynamicRules();
  await chrome.declarativeNetRequest.updateDynamicRules({ removeRuleIds: rules.filter(r => ownedRule(r.id)).map(r => r.id) });
  const scripts = await chrome.scripting.getRegisteredContentScripts();
  if (scripts.some(s => s.id === 'lockin-youtube')) await chrome.scripting.unregisterContentScripts({ ids: ['lockin-youtube'] });
  const tabs = await chrome.tabs.query({ url: 'https://www.youtube.com/*' });
  await Promise.all(tabs.filter(tab => tab.id !== undefined).map(tab =>
    chrome.tabs.sendMessage(tab.id!, { type: 'Release' }).catch(() => undefined)));
  // Storage failure must not prevent best-effort removal of restrictions. Keep
  // ownership for the next retry and do not acknowledge release in this state.
  if (markerFailure) throw markerFailure;
  await chrome.alarms.clear('lockin-expiry');
  await chrome.storage.local.remove('cache');
}
async function current(): Promise<Cache | null> {
  const { cache, ended = [] } = await state();
  if (!cacheValid(cache, Date.now()) || ended.includes(cache.policy.sessionId)) { if (cache) await release(); return null; }
  cache.lastUtc = Math.max(cache.lastUtc, Date.now());
  await chrome.storage.local.set({ cache });
  return cache;
}
function ruleShape(rule: chrome.declarativeNetRequest.Rule): string {
  return JSON.stringify([rule.id, rule.priority, rule.action.type, rule.action.redirect?.extensionPath,
    rule.condition.regexFilter, rule.condition.resourceTypes, rule.condition.isUrlFilterCaseSensitive]);
}
async function coverage(): Promise<{ appliedSessionId: string | null; appliedPolicyHash: string | null; errorCode: CoverageIssue | null }> {
  const cache = await current();
  if (!cache) return { appliedSessionId: null, appliedPolicyHash: null, errorCode: coverageIssue };
  if (!await chrome.permissions.contains({ origins: originsFor(cache.policy) }))
    return { appliedSessionId: null, appliedPolicyHash: null, errorCode: 'permissionMissing' };
  const rules = (await chrome.declarativeNetRequest.getDynamicRules()).filter(r => ownedRule(r.id)).map(ruleShape).sort();
  const expected = compileSites(cache.policy.websites).map(r => ruleShape(r as chrome.declarativeNetRequest.Rule)).sort();
  const scripts = await chrome.scripting.getRegisteredContentScripts();
  const youtubeReady = !originsFor(cache.policy).includes('https://www.youtube.com/*') ||
    scripts.some(s => s.id === 'lockin-youtube' && s.js?.includes('content.js') && s.matches?.includes('https://www.youtube.com/*'));
  if (JSON.stringify(rules) !== JSON.stringify(expected) || !youtubeReady)
    return { appliedSessionId: null, appliedPolicyHash: null, errorCode: 'applyFailed' };
  return { appliedSessionId: cache.policy.sessionId, appliedPolicyHash: cache.policy.policyHash, errorCode: null };
}
async function apply(policy: Policy): Promise<void> {
  if (!validPolicy(policy) || Date.parse(policy.endsAtUtc) <= Date.now()) throw new Error('Expired or invalid browser policy.');
  const { cache: old, ended = [] } = await state();
  if (ended.includes(policy.sessionId)) { coverageIssue = 'locallyReleased'; throw new Error('This browser session has already released locally. Waiting for host reconciliation.'); }
  if (!await chrome.permissions.contains({ origins: originsFor(policy) })) { coverageIssue = 'permissionMissing'; throw new Error('Grant the requested site permissions in the popup.'); }
  const deadline = Math.min(Date.parse(policy.endsAtUtc), Date.now() + policy.remainingSeconds * 1000, old?.policy.sessionId === policy.sessionId ? old.deadline : Infinity);
  const cache: Cache = { policy, deadline, lastUtc: Date.now() };
  // Persist pending ownership before installing rules so restart reconciliation can clean up.
  await chrome.storage.local.set({ cache });
  const existing = await chrome.declarativeNetRequest.getDynamicRules();
  const expected = compileSites(policy.websites);
  if (JSON.stringify(existing.filter(r => ownedRule(r.id))) !== JSON.stringify(expected))
    await chrome.declarativeNetRequest.updateDynamicRules({ removeRuleIds: existing.filter(r => ownedRule(r.id)).map(r => r.id), addRules: expected as chrome.declarativeNetRequest.Rule[] });
  await chrome.alarms.create('lockin-expiry', { when: deadline });
  if (originsFor(policy).includes('https://www.youtube.com/*')) {
    const scripts = await chrome.scripting.getRegisteredContentScripts();
    if (!scripts.some(s => s.id === 'lockin-youtube'))
      await chrome.scripting.registerContentScripts([{ id: 'lockin-youtube', matches: ['https://www.youtube.com/*'], js: ['content.js'], runAt: 'document_start', persistAcrossSessions: true }]);
  }
  {
    for (const tab of await chrome.tabs.query({})) {
      if (tab.id === undefined || !tab.url || !cacheValid(cache, Date.now())) continue;
      if (blockedHost(tab.url, policy.websites)) await chrome.tabs.update(tab.id, { url: chrome.runtime.getURL('blocked.html') });
      else if (tab.url.startsWith('https://www.youtube.com/') && originsFor(policy).includes('https://www.youtube.com/*'))
        await chrome.scripting.executeScript({ target: { tabId: tab.id }, files: ['content.js'] }).catch(() => undefined);
    }
  }
}
async function synchronize(): Promise<void> {
  await initialize;
  await current();
  const { auth } = await state();
  if (!auth) {
    try { await native('GetBridgeStatus', {}); message = 'Desktop connected. Paste the setup code to finish connecting.'; }
    catch (error) { message = error instanceof Error ? error.message : 'Open LockIn on your PC to connect.'; }
    return;
  }
  try {
    const readyProfileHash = lastPoll?.stagedProfileHash && await chrome.permissions.contains({ origins: lastPoll.requiredOrigins }) ? lastPoll.stagedProfileHash : null;
    const response = await native('PollBrowser', { auth, readyProfileHash, ...await coverage() }) as Poll;
    if (!response || !safeOrigins(response.requiredOrigins) || !Number.isFinite(Date.parse(response.serverUtc))) throw new Error('Invalid host response.');
    lastPoll = response;
    if (Date.now() < Date.parse(response.serverUtc) - 2000) { coverageIssue = 'clockDiscrepancy'; await release(); throw new Error('Clock discrepancy: browser rules released for recovery.'); }
    if (response.policy) {
      try {
        await apply(response.policy);
        await native('AcknowledgeBrowserPolicy', { auth, sessionId: response.policy.sessionId, policyHash: response.policy.policyHash });
        coverageIssue = null;
        message = 'Browser session active. Other browser profiles are not covered.';
      } catch (error) {
        coverageIssue ??= 'applyFailed';
        try { await release(); } catch { coverageIssue = 'cleanupFailed'; }
        throw error;
      }
    } else {
      try { await release(); } catch (error) { coverageIssue = 'cleanupFailed'; throw error; }
      if (response.releaseSessionId && response.releasePolicyHash)
        await native('AcknowledgeBrowserPolicy', { auth, sessionId: response.releaseSessionId, policyHash: response.releasePolicyHash, released: true });
      coverageIssue = null;
      message = response.stagedProfileHash ? (readyProfileHash ? 'Ready for a browser session.' : 'Review and grant this profile’s site access.') : 'Connected. Send a profile from the desktop.';
    }
  } catch (error) { message = error instanceof Error ? error.message : 'Browser connection unavailable.'; }
}
function destination(policy: Policy): string {
  return policy.youTube.destinationKind === 'focusPage' ? chrome.runtime.getURL('focus.html') : policy.youTube.destinationKind === 'customHttps' ? policy.youTube.customDestination! : 'https://www.youtube.com/';
}
async function stillCurrent(tabId: number, videoId: string, hash: string): Promise<boolean> {
  const { cache } = await state();
  const tab = await chrome.tabs.get(tabId).catch(() => null);
  return cacheValid(cache, Date.now()) && cache.policy.policyHash === hash && !!tab?.url && parseVideoId(tab.url) === videoId;
}
async function redirectVideo(tabId: number, videoId: string, policy: Policy, key: string): Promise<void> {
  if (inFlight.has(tabId) || redirected.get(tabId) === key) return;
  inFlight.add(tabId); redirected.set(tabId, key);
  try {
    if (!await stillCurrent(tabId, videoId, policy.policyHash)) return;
    const url = destination(policy);
    if (policy.youTube.tabAction === 'replaceCurrent') { await chrome.tabs.update(tabId, { url }); return; }
    if (policy.youTube.tabAction === 'switchExisting') {
      const existing = (await chrome.tabs.query({})).find(t => t.id !== tabId && t.url === url);
      if (!await stillCurrent(tabId, videoId, policy.policyHash)) return;
      if (existing?.id !== undefined) {
        await chrome.tabs.update(tabId, { url: chrome.runtime.getURL('focus.html') });
        await chrome.tabs.update(existing.id, { active: true });
        await chrome.windows.update(existing.windowId, { focused: true });
      } else await chrome.tabs.update(tabId, { url });
      return;
    }
    const created = await chrome.tabs.create({ url, active: false });
    if (!await stillCurrent(tabId, videoId, policy.policyHash)) {
      if (created.id !== undefined) {
        const check = await chrome.tabs.get(created.id).catch(() => null);
        if (check && !check.active && (check.url === url || check.pendingUrl === url)) await chrome.tabs.remove(created.id);
      }
      return;
    }
    if (created.id === undefined) throw new Error('Destination tab was not created.');
    await chrome.tabs.update(created.id, { active: true });
    await chrome.tabs.remove(tabId);
  } catch {
    if (await stillCurrent(tabId, videoId, policy.policyHash)) await chrome.tabs.update(tabId, { url: chrome.runtime.getURL('focus.html') }).catch(() => undefined);
  } finally { inFlight.delete(tabId); }
}
async function handle(request: any, sender: chrome.runtime.MessageSender): Promise<unknown> {
  const extensionPage = sender.id === chrome.runtime.id && sender.url?.startsWith(chrome.runtime.getURL(''));
  if (extensionPage) {
    if (request?.type === 'PopupStatus') {
      const { auth } = await state(); const cache = await current();
      const requiredOrigins = lastPoll?.requiredOrigins ?? [];
      const needsPermissions = requiredOrigins.length > 0 && !await chrome.permissions.contains({ origins: requiredOrigins });
      return { paired: !!auth, message, coverageIssue, policy: cache?.policy ?? null, requiredOrigins, needsPermissions, remaining: cache ? Math.max(0, (cache.deadline - Date.now()) / 1000) : 0 };
    }
    if (request?.type === 'Sync') { await synchronize(); return { ok: true }; }
    if (request?.type === 'Pair' && typeof request.code === 'string' && /^\d{6}$/.test(request.code) && typeof request.label === 'string' && request.label.length <= 80) {
      const local = await chrome.storage.local.get('installationId');
      const installationId = local.installationId ?? crypto.randomUUID().replaceAll('-', '');
      const auth = await native('PairBrowser', { code: request.code, label: request.label, installationId });
      await chrome.storage.local.set({ auth, installationId }); await synchronize(); return { ok: true };
    }
    return { error: 'Unknown extension command.' };
  }
  if (sender.id !== chrome.runtime.id || sender.frameId !== 0 || sender.tab?.id === undefined || !sender.url?.startsWith('https://www.youtube.com/')) return { error: 'Sender is not supported.' };
  const cache = await current();
  if (request?.type === 'ContentPolicy') return { policy: cache?.policy ?? null };
  if (request?.type !== 'EvaluateVideo' || !cache || request.policyHash !== cache.policy.policyHash || typeof request.videoId !== 'string' || request.videoId !== parseVideoId(sender.url) ||
      typeof request.generation !== 'string' || request.generation.length > 80 || (request.hashtags !== null && (!Array.isArray(request.hashtags) || request.hashtags.length > 100 || request.hashtags.some((s: unknown) => typeof s !== 'string' || s.length > 100)))) return { decision: 'ignore' };
  if (!await stillCurrent(sender.tab.id, request.videoId, cache.policy.policyHash)) return { decision: 'ignore' };
  const decision = matchVideo(cache.policy.youTube, request.videoId, request.hashtags);
  if (decision === 'redirect') await redirectVideo(sender.tab.id, request.videoId, cache.policy, `${cache.policy.sessionId}:${request.videoId}:${request.generation}`);
  return { decision };
}
chrome.runtime.onMessage.addListener((request, sender, respond) => {
  enqueue(() => handle(request, sender)).then(respond).catch(error => respond({ error: error instanceof Error ? error.message : 'Browser action failed.' })); return true;
});
chrome.alarms.onAlarm.addListener(scheduleSync);
chrome.runtime.onStartup.addListener(scheduleSync);
chrome.permissions.onRemoved.addListener(() => { void enqueue(async () => { coverageIssue = 'permissionMissing'; try { await release(); } catch { coverageIssue = 'cleanupFailed'; } await synchronize(); }).catch(() => undefined); });
chrome.tabs.onRemoved.addListener(id => { redirected.delete(id); inFlight.delete(id); });
chrome.tabs.onUpdated.addListener((id, change, tab) => {
  if (!change.url) return;
  redirected.delete(id);
  void enqueue(async () => {
    const cache = await current();
    if (cache && tab.url && blockedHost(tab.url, cache.policy.websites)) await chrome.tabs.update(id, { url: chrome.runtime.getURL('blocked.html') });
  }).catch(() => { coverageIssue = 'applyFailed'; });
});
void chrome.alarms.create('lockin-sync', { periodInMinutes: 0.5 });
setInterval(scheduleSync, 5000);
void enqueue(async () => {
  const { cache } = await state();
  if (!cacheValid(cache, Date.now())) await release();
  else await apply(cache.policy).catch(() => release());
  await synchronize();
}).catch(() => { coverageIssue = 'cleanupFailed'; message = 'Startup cleanup could not be confirmed. Reopen the browser and use desktop recovery.'; });
