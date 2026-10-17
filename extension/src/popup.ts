import { initializeMotion } from './motion';
import { coverageMessage } from './shared/presentation';
import { renderScene, setSceneState } from './scene';
renderScene();
void initializeMotion();
const element = <T extends HTMLElement>(id: string) => document.getElementById(id) as T;
let requestedOrigins: string[] = [];
let busy = false;
let refreshing = false;
async function refresh(): Promise<void> {
  if (refreshing) return;
  refreshing = true;
  try {
    const state = await chrome.runtime.sendMessage({ type: 'PopupStatus' });
    if (!state || state.error) throw new Error(state?.error ?? 'Connection status unavailable.');
    const moveFocus = state.paired && element('pairing').contains(document.activeElement)
      || !state.needsPermissions && element('permissions').contains(document.activeElement);
    setSceneState(state.policy ? 'active' : state.coverageIssue ? 'attention' : 'idle');
    element('status').textContent = coverageMessage(state.coverageIssue, state.message ?? 'Browser connection unavailable.');
    element('pairing').hidden = state.paired;
    element('heading').textContent = state.policy ? 'Stay with one thing.' : state.paired ? 'Your browser session' : 'Connect your browser';
    element('timer').textContent = state.policy ? `${Math.ceil(state.remaining / 60)} min remaining` : state.coverageIssue ? 'Recovery needed' : state.paired ? 'No active session' : 'One-time setup';
    element('mode').textContent = state.policy?.youTube.matchingMode === 'allowMatchingTags' ? 'Exact allowed hashtags or approved videos only.' : state.policy ? 'Legacy mode: matching hashtags redirect.' : '';
    const origins: string[] = state.requiredOrigins ?? [];
    if (JSON.stringify(origins) !== JSON.stringify(requestedOrigins)) {
      element('origins').replaceChildren(...origins.map(origin => { const li = document.createElement('li'); li.textContent = origin; return li; }));
      requestedOrigins = origins;
    }
    element('permissions').hidden = !state.needsPermissions;
    if (moveFocus) { const target = element('heading'); target.tabIndex = -1; target.focus(); }
  } finally { refreshing = false; }
}
async function action(buttonId: string, label: string, work: () => Promise<unknown>): Promise<void> {
  if (busy) return;
  busy = true;
  const button = element<HTMLButtonElement>(buttonId);
  const original = button.textContent;
  button.textContent = label;
  for (const id of ['pair', 'grant', 'sync']) element<HTMLButtonElement>(id).disabled = true;
  try {
    element('error').textContent = '';
    const result: any = await work();
    if (result?.error) throw new Error(result.error);
    await refresh();
  } catch (error) { element('error').textContent = error instanceof Error ? error.message : 'Could not connect. Try again.'; }
  finally { busy = false; button.textContent = original; for (const id of ['pair', 'grant', 'sync']) element<HTMLButtonElement>(id).disabled = false; }
}
function pair(): void {
  const code = element<HTMLInputElement>('code').value.trim();
  if (!/^\d{6}$/.test(code)) { element('error').textContent = 'Enter the six-digit code from LockIn.'; element('code').focus(); return; }
  void action('pair', 'Connecting…', () => chrome.runtime.sendMessage({ type: 'Pair', code, label: element<HTMLInputElement>('label').value }));
}
element('pair').addEventListener('click', pair);
element('code').addEventListener('input', () => { if (/^\d{6}$/.test(element<HTMLInputElement>('code').value.trim())) pair(); });
element('code').addEventListener('keydown', event => { if (event.key === 'Enter') { event.preventDefault(); pair(); } });
element('grant').addEventListener('click', () => {
  if (busy) return;
  // Keep permission requests inside the user's gesture; only request staged hosts.
  const permission = chrome.permissions.request({ origins: requestedOrigins });
  void action('grant', 'Waiting for access…', async () => { if (!await permission) throw new Error('Site access was not granted.'); return chrome.runtime.sendMessage({ type: 'Sync' }); });
});
element('sync').addEventListener('click', () => void action('sync', 'Checking…', () => chrome.runtime.sendMessage({ type: 'Sync' })));
void action('sync', 'Checking…', () => chrome.runtime.sendMessage({ type: 'Sync' }));
setInterval(() => { if (!busy) void refresh().catch(() => { setSceneState('attention'); element('timer').textContent = 'Status unavailable'; element('status').textContent = 'Connection unavailable. Open LockIn or check again.'; }); }, 1000);
