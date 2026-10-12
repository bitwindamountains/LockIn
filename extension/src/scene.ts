// Decorative, static markup only (no user data). Text states the session; the eclipse only echoes it:
// idle = partial (a crescent of sun shows), active = total (the moon blocks the noise), attention = slipping, red corona.
const eclipse = `<svg class="eclipse-art" viewBox="-200 -200 400 400" aria-hidden="true" focusable="false">
<defs>
<radialGradient id="corona"><stop offset=".54" stop-color="#FFE0A3" stop-opacity=".95"/><stop offset=".6" stop-color="#FFA35C" stop-opacity=".6"/><stop offset=".76" stop-color="#FF6B3D" stop-opacity=".18"/><stop offset="1" stop-color="#FF6B3D" stop-opacity="0"/></radialGradient>
<radialGradient id="sun" cx=".38" cy=".34"><stop offset="0" stop-color="#FFF6DC"/><stop offset=".55" stop-color="#FFC66E"/><stop offset="1" stop-color="#FF7E45"/></radialGradient>
<radialGradient id="moon" cx=".36" cy=".3"><stop offset="0" stop-color="#1C1F2E"/><stop offset=".7" stop-color="#0B0C13"/><stop offset="1" stop-color="#06070B"/></radialGradient>
</defs>
<circle class="corona" r="198" fill="url(#corona)"/>
<circle class="sun" r="112" fill="url(#sun)"/>
<g class="moon"><circle r="117" fill="url(#moon)"/><circle class="rim" r="116.2" fill="none"/></g>
<circle class="bead" cx="80" cy="-84" r="5.5"/>
</svg>`;

export function renderScene(): void {
  for (const host of document.querySelectorAll<HTMLElement>('[data-scene]')) host.innerHTML = eclipse;
}

export type SceneState = 'idle' | 'active' | 'attention';
export function setSceneState(state: SceneState): void {
  if (document.body.dataset.state !== state) document.body.dataset.state = state;
}
