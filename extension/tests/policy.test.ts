import test from 'node:test';
import assert from 'node:assert/strict';
import { matchVideo, compileSites, blockedHost, cacheValid, parseVideoId, withoutTrailingDot } from '../src/shared/policy.ts';
import type { Policy, YouTubeSettings } from '../src/shared/policy.ts';

const settings: YouTubeSettings = { rules: [{ id: '1', source: 'visibleHashtags', value: 'education' }], approvedVideoIds: [], matchingMode: 'allowMatchingTags', tabAction: 'replaceCurrent', destinationKind: 'focusPage' };
test('only exact allowed tags pass; unavailable metadata redirects', () => {
  for (const [tags, expected] of [[['#EDUCATION'], 'allow'], [['ｅｄｕｃａｔｉｏｎ'], 'allow'], [['gaming', 'education'], 'allow'], [['educationExtra'], 'redirect'], [[], 'redirect'], [null, 'redirect']] as const)
    assert.equal(matchVideo(settings, 'abcdefghijk', tags && [...tags]), expected);
  assert.equal(matchVideo({ ...settings, approvedVideoIds: ['abcdefghijk'] }, 'abcdefghijk', null), 'allow');
});
test('legacy block semantics remain explicit', () => {
  assert.equal(matchVideo({ ...settings, matchingMode: null }, 'abcdefghijk', ['education']), 'redirect');
  assert.equal(matchVideo({ ...settings, matchingMode: null }, 'abcdefghijk', null), 'coverage-warning');
});
test('website compiler matches host boundaries and only top-level pages', () => {
  const rules = [{ id: 'site', host: 'example.com', includeSubdomains: true }];
  const compiled = compileSites(rules)[0];
  const regex = new RegExp(compiled.condition.regexFilter);
  for (const url of ['https://example.com/', 'https://a.example.com:443/path']) { assert.ok(regex.test(url)); assert.ok(blockedHost(url, rules)); }
  for (const url of ['https://notexample.com/', 'https://example.com.evil.test/', 'https://other.test/example.com/']) { assert.ok(!regex.test(url)); assert.ok(!blockedHost(url, rules)); }
  assert.deepEqual(compiled.condition.resourceTypes, ['main_frame']);
});
test('a trailing-dot hostname cannot bypass website rules', () => {
  const rules = [{ id: 'site', host: 'example.com', includeSubdomains: true }];
  const regex = new RegExp(compileSites(rules)[0].condition.regexFilter);
  for (const url of ['https://example.com./', 'https://www.example.com.:443/path']) { assert.ok(regex.test(url), url); assert.ok(blockedHost(url, rules), url); }
  assert.ok(!regex.test('https://example.com.evil.test/'));
});
test('trailing-dot URLs map to their canonical host', () => {
  assert.equal(withoutTrailingDot('https://www.youtube.com./watch?v=abcdefghijk'), 'https://www.youtube.com/watch?v=abcdefghijk');
  assert.equal(withoutTrailingDot('https://www.youtube.com/watch?v=abcdefghijk'), null);
  assert.equal(withoutTrailingDot('chrome://extensions./'), null);
});
test('cache expires independently and detects clock rewind', () => {
  const now = Date.now();
  const policy: Policy = { sessionId: 's', policyHash: 'a'.repeat(64), revision: 1, remainingSeconds: 60, endsAtUtc: new Date(now + 60000).toISOString(), websites: [], youTube: settings, studyResources: [] };
  const cache = { policy, lastUtc: now, deadline: now + 10000 };
  assert.ok(cacheValid(cache, now)); assert.ok(!cacheValid(cache, now + 10000)); assert.ok(!cacheValid(cache, now - 3000));
});
test('video URLs reject ambiguous IDs and unrelated hosts', () => {
  assert.equal(parseVideoId('https://www.youtube.com/watch?v=abcdefghijk'), 'abcdefghijk');
  assert.equal(parseVideoId('https://www.youtube.com/watch?v=abcdefghijk&v=12345678901'), null);
  assert.equal(parseVideoId('https://youtube.com.evil.test/watch?v=abcdefghijk'), null);
});
