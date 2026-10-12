// Decorative, static markup only (no user data). State is shown by text first; the lamp only echoes it.
const desk = `<svg class="scene-art" viewBox="0 0 320 180" aria-hidden="true" focusable="false">
<defs><linearGradient id="desk-fade" x1="0" x2="1"><stop offset="0" stop-color="#0c1812" stop-opacity="0"/><stop offset=".18" stop-color="#0c1812"/><stop offset=".82" stop-color="#0c1812"/><stop offset="1" stop-color="#0c1812" stop-opacity="0"/></linearGradient><radialGradient id="lamp-light" cx="0.45" cy="0" r="1"><stop offset="0" stop-color="#F7D58A" stop-opacity=".9"/><stop offset=".7" stop-color="#F2B84B" stop-opacity=".16"/><stop offset="1" stop-color="#F2B84B" stop-opacity="0"/></radialGradient></defs>
<path class="lamp-glow" d="M124 70 L140 82 L258 158 L104 158 Z" fill="url(#lamp-light)"/>
<rect class="desk" x="0" y="158" width="320" height="22" fill="url(#desk-fade)"/><path class="desk-edge" d="M40 158.5 H280"/>
<g class="book"><path class="book-page" d="M150 156 C168 149 188 149 208 155 L208 121 C188 115 168 115 150 122 Z"/><path class="book-page" d="M208 155 C228 149 248 149 266 156 L266 122 C248 115 228 115 208 121 Z"/>
<path class="ink-line" d="M160 130 C172 126 186 126 198 129 M160 138 C172 134 186 134 198 137 M218 129 C230 126 244 126 256 130 M218 137 C230 134 240 134 248 136"/></g>
<path class="pencil" d="M222 163 L282 151 L286 155 L226 167 Z"/>
<g class="lamp"><ellipse class="lamp-base" cx="70" cy="156" rx="26" ry="5"/><path class="lamp-rod" d="M70 152 L84 100"/>
<g class="lamp-upper"><path class="lamp-rod" d="M84 100 L120 72"/><path class="lamp-head" d="M112 58 L142 68 L132 88 Z"/><circle class="bulb" cx="132" cy="80" r="5"/><circle class="hinge" cx="84" cy="100" r="4.5"/></g></g>
<g class="mug"><path class="mug-body" d="M276 132 H298 V150 A6 6 0 0 1 292 156 H282 A6 6 0 0 1 276 150 Z"/><path class="mug-handle" d="M298 137 A6 6 0 0 1 298 149"/>
<path class="steam" d="M283 126 C279 120 287 116 283 108"/><path class="steam late" d="M291 126 C287 120 295 116 291 108"/></g>
</svg>`;

const letter = `<svg class="scene-art" viewBox="0 0 320 180" aria-hidden="true" focusable="false">
<defs><linearGradient id="desk-fade" x1="0" x2="1"><stop offset="0" stop-color="#0c1812" stop-opacity="0"/><stop offset=".18" stop-color="#0c1812"/><stop offset=".82" stop-color="#0c1812"/><stop offset="1" stop-color="#0c1812" stop-opacity="0"/></linearGradient></defs>
<rect class="desk" x="0" y="158" width="320" height="22" fill="url(#desk-fade)"/><path class="desk-edge" d="M40 158.5 H280"/>
<rect class="envelope-back" x="96" y="92" width="128" height="62" rx="4"/>
<path class="envelope-flap-open" d="M96 94 L160 58 L224 94 Z"/>
<g class="letter"><rect class="letter-card" x="110" y="52" width="100" height="56" rx="5"/><path class="letter-bar" d="M110 64 H210"/>
<circle class="letter-dot" cx="118" cy="58" r="2"/><circle class="letter-dot" cx="125" cy="58" r="2"/><circle class="letter-dot" cx="132" cy="58" r="2"/>
<path class="ink-line" d="M120 76 H186 M120 86 H200 M120 96 H172"/></g>
<path class="envelope-front" d="M96 100 L160 132 L224 100 V150 A4 4 0 0 1 220 154 H100 A4 4 0 0 1 96 150 Z"/>
<path class="envelope-flap" d="M96 94 L160 128 L224 94 Z"/>
<g class="seal"><circle cx="160" cy="128" r="11"/><text x="160" y="131.5" text-anchor="middle">later</text></g>
</svg>`;

export function renderScene(): void {
  for (const host of document.querySelectorAll<HTMLElement>('[data-scene]'))
    host.innerHTML = host.dataset.scene === 'letter' ? letter : desk;
}

export type SceneState = 'idle' | 'active' | 'attention';
export function setSceneState(state: SceneState): void {
  if (document.body.dataset.state !== state) document.body.dataset.state = state;
}
