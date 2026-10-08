// Presentation preference only. Enforcement never waits for visual transitions.
export async function initializeMotion(): Promise<void> {
  const render = (reduced: boolean) => {
    document.documentElement.dataset.reducedMotion = String(reduced);
    const checkbox = document.getElementById('reduce-motion') as HTMLInputElement | null;
    if (checkbox) checkbox.checked = reduced;
  };
  const saved = await chrome.storage.local.get('reduceMotion');
  render(saved.reduceMotion === true);
  document.getElementById('reduce-motion')?.addEventListener('change', event => {
    const reduced = (event.target as HTMLInputElement).checked;
    render(reduced);
    void chrome.storage.local.set({ reduceMotion: reduced });
  });
  chrome.storage.onChanged.addListener((changes, area) => {
    if (area === 'local' && changes.reduceMotion) render(changes.reduceMotion.newValue === true);
  });
}
