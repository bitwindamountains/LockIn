export interface SiteRule { id: string; host: string; includeSubdomains: boolean }
export interface YouTubeSettings {
  rules: { id: string; source: string; value: string }[];
  approvedVideoIds: string[];
  matchingMode?: 'allowMatchingTags' | 'filterDistractions' | null;
  tabAction: 'replaceCurrent' | 'switchExisting' | 'openReplacement';
  destinationKind: 'youtubeHome' | 'focusPage' | 'customHttps';
  customDestination?: string | null;
}
export interface Policy {
  sessionId: string; policyHash: string; endsAtUtc: string; revision: number; remainingSeconds: number;
  websites: SiteRule[]; youTube: YouTubeSettings; studyResources: string[];
}
export interface Cache { policy: Policy; lastUtc: number; deadline: number }
export const ownedRule = (id: number) => id >= 10000 && id < 11000;
export const normalizeToken = (value: string) => [...value.normalize('NFKC').trim().replace(/^#+/u, '')].map(c => c === '\u0130' ? c : c.toLowerCase()).join('');
export function safeHttps(value: unknown): value is string {
  try { const url = new URL(String(value)); return typeof value === 'string' && value.length <= 2048 && url.protocol === 'https:' && !url.username && !url.password; } catch { return false; }
}
export function validPolicy(value: unknown): value is Policy {
  if (!value || typeof value !== 'object') return false;
  const p = value as Policy;
  return typeof p.sessionId === 'string' && p.sessionId.length > 0 && p.sessionId.length <= 80 && /^[a-f0-9]{64}$/.test(p.policyHash) &&
    Number.isInteger(p.revision) && p.revision > 0 && Number.isFinite(Date.parse(p.endsAtUtc)) && Number.isFinite(p.remainingSeconds) && p.remainingSeconds > 0 && p.remainingSeconds <= 28800 &&
    Array.isArray(p.websites) && p.websites.length <= 500 && p.websites.every(s => s && typeof s.host === 'string' && /^[a-z0-9.-]+$/.test(s.host) && !s.host.startsWith('.') && typeof s.includeSubdomains === 'boolean') &&
    Array.isArray(p.studyResources) && p.studyResources.length <= 50 && p.studyResources.every(safeHttps) && !!p.youTube &&
    Array.isArray(p.youTube.rules) && p.youTube.rules.length <= 100 && p.youTube.rules.every(r => r && r.source === 'visibleHashtags' && typeof r.value === 'string' && r.value.length > 0 && r.value.length <= 100) &&
    Array.isArray(p.youTube.approvedVideoIds) && p.youTube.approvedVideoIds.length <= 500 && p.youTube.approvedVideoIds.every(id => /^[A-Za-z0-9_-]{11}$/.test(id)) &&
    [null, undefined, 'filterDistractions', 'allowMatchingTags'].includes(p.youTube.matchingMode) &&
    ['replaceCurrent', 'switchExisting', 'openReplacement'].includes(p.youTube.tabAction) && ['youtubeHome', 'focusPage', 'customHttps'].includes(p.youTube.destinationKind) &&
    (p.youTube.destinationKind !== 'customHttps' || safeHttps(p.youTube.customDestination));
}
export function originsFor(policy: Pick<Policy, 'websites' | 'youTube'>): string[] {
  const origins = policy.websites.map(s => `*://${s.includeSubdomains ? '*.' : ''}${s.host}/*`);
  if (policy.youTube.rules.length || policy.youTube.approvedVideoIds.length || policy.youTube.matchingMode === 'allowMatchingTags') origins.push('https://www.youtube.com/*');
  return [...new Set(origins)];
}
export function safeOrigins(value: unknown): value is string[] {
  return Array.isArray(value) && value.length <= 501 && value.every(v => typeof v === 'string' && /^(\*|https):\/\/(\*\.)?[a-z0-9][a-z0-9.-]*\/\*$/.test(v));
}
export function blockedHost(url: string, rules: SiteRule[]): boolean {
  try { const u = new URL(url); return ['http:', 'https:'].includes(u.protocol) && rules.some(r => u.hostname === r.host || r.includeSubdomains && u.hostname.endsWith(`.${r.host}`)); } catch { return false; }
}
export function compileSites(sites: SiteRule[]) {
  return sites.map((site, i) => ({
    id: 10000 + i, priority: 10, action: { type: 'redirect' as const, redirect: { extensionPath: '/blocked.html' } },
    condition: { regexFilter: `^https?://${site.includeSubdomains ? '([^/.:]+\\.)*' : ''}${site.host.replaceAll('.', '\\.')}(:[0-9]+)?/`, resourceTypes: ['main_frame' as const], isUrlFilterCaseSensitive: false }
  }));
}
export function cacheValid(cache: Cache | null | undefined, now: number): cache is Cache {
  return !!cache && validPolicy(cache.policy) && Number.isFinite(cache.lastUtc) && Number.isFinite(cache.deadline) && now >= cache.lastUtc - 2000 && now < Math.min(cache.deadline, Date.parse(cache.policy.endsAtUtc));
}
export function parseVideoId(value: string): string | null {
  if (/^[A-Za-z0-9_-]{11}$/.test(value)) return value;
  try {
    const u = new URL(value); if (u.protocol !== 'https:' || u.username || u.password) return null;
    let id: string | null = null;
    if (u.hostname === 'youtu.be') id = u.pathname.replace(/^\/|\/$/g, '');
    else if (['youtube.com', 'www.youtube.com', 'm.youtube.com'].includes(u.hostname)) {
      if (u.pathname === '/watch' && u.searchParams.getAll('v').length === 1) id = u.searchParams.get('v');
      else if (u.pathname.startsWith('/shorts/')) id = u.pathname.slice(8).replace(/\/$/, '');
    }
    return id && /^[A-Za-z0-9_-]{11}$/.test(id) ? id : null;
  } catch { return null; }
}
export function matchVideo(settings: YouTubeSettings, videoId: string, hashtags: string[] | null): 'allow' | 'redirect' | 'coverage-warning' {
  if (settings.approvedVideoIds.includes(videoId)) return 'allow';
  if (hashtags === null) return settings.matchingMode === 'allowMatchingTags' ? 'redirect' : 'coverage-warning';
  const values = new Set(hashtags.map(normalizeToken));
  const match = settings.rules.some(rule => values.has(normalizeToken(rule.value)));
  return settings.matchingMode === 'allowMatchingTags' ? (match ? 'allow' : 'redirect') : (match ? 'redirect' : 'allow');
}
