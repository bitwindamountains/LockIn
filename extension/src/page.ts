import { initializeMotion } from './motion';
import { coverageMessage } from './shared/presentation';
import { renderScene, setSceneState } from './scene';
renderScene();
void initializeMotion();
const element = <T extends HTMLElement>(id: string) => document.getElementById(id) as T;
let resourceKey: string | undefined;
// Headings and notes carry an optional idle variant in data-idle; session wording stays the default.
const variants = [element('page-title'), element('note')].map(node => ({ node, active: node.textContent ?? '' }));
const showIdle = (idle: boolean) => { for (const { node, active } of variants) node.textContent = idle ? node.dataset.idle ?? active : active; };
let updating = false;

async function update(): Promise<void> {
  if (updating) return;
  updating = true;
  const button = element<HTMLButtonElement>('refresh');
  button.disabled = true;
  button.textContent = 'Checking…';
  try {
    const sync = await chrome.runtime.sendMessage({ type: 'Sync' });
    if (sync?.error) throw new Error(sync.error);
    const state = await chrome.runtime.sendMessage({ type: 'PopupStatus' });
    if (!state || state.error) throw new Error(state?.error ?? 'Session status unavailable.');
    element('error').textContent = '';
    setSceneState(state.policy ? 'active' : state.coverageIssue ? 'attention' : 'idle');
    showIdle(!state.policy);
    element('timer').textContent = state.policy
      ? `${Math.ceil(state.remaining / 60)} min remaining` : state.coverageIssue ? 'Recovery needed' : 'No active session';
    element('status').textContent = coverageMessage(state.coverageIssue, state.policy
      ? `Ends at ${new Date(state.policy.endsAtUtc).toLocaleTimeString([], { hour: 'numeric', minute: '2-digit' })}. ${state.message}`
      : 'No session is running in this browser.');
    const links: string[] = (state.policy?.studyResources ?? []).filter((value: string) => {
      try { const url = new URL(value); return url.protocol === 'https:' && !url.username && !url.password; } catch { return false; }
    });
    const key = JSON.stringify(links);
    // Preserve focused links across polling when the session's resources are unchanged.
    if (key !== resourceKey) {
      const resources = element('resources');
      const heldFocus = resources.contains(document.activeElement);
      resources.replaceChildren(...links.map(value => {
        const url = new URL(value);
        const li = document.createElement('li');
        const link = document.createElement('a');
        link.href = value; link.rel = 'noreferrer'; link.className = 'resource-link';
        link.append(document.createTextNode(url.hostname));
        const path = document.createElement('small'); path.textContent = url.pathname + url.search;
        link.append(path); li.append(link); return li;
      }));
      resourceKey = key;
      if (heldFocus) { element('resources-title').tabIndex = -1; element('resources-title').focus(); }
    }
    element('resources-empty').hidden = links.length > 0;
    element('resources-empty').textContent = state.policy
      ? 'No study links in this session. Add links in LockIn for next time.'
      : 'Your study links will appear when a browser session starts.';
  } catch (error) {
    setSceneState('attention');
    element('timer').textContent = 'Status unavailable';
    element('error').textContent = 'Could not check this session. Open LockIn or try again.';
    element('status').textContent = 'Current coverage could not be confirmed.';
    element('resources-empty').textContent = 'Study links may be out of date until the connection returns.';
    element('resources-empty').hidden = false;
  } finally {
    updating = false; button.disabled = false; button.textContent = 'Check session';
  }
}
element('refresh').addEventListener('click', () => void update());
// Embedded in another site's frame, stay inert: never poll the host from there.
if (window.top === window) {
  void update();
  setInterval(() => void update(), 5000);
}
