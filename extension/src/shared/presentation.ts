// Display host/browser evidence without implying verified protection or cleanup.
export function coverageMessage(issue: string | null | undefined, fallback: string): string {
  switch (issue) {
    case 'permissionMissing': return 'Site access is missing. Allow the requested sites in the extension.';
    case 'cleanupFailed': return 'Cleanup is unconfirmed. Reopen the browser and use Help & recovery in LockIn.';
    case 'locallyReleased': return 'Rules were released locally. Open LockIn to finish recovery.';
    case 'clockDiscrepancy': return 'The clock changed unexpectedly. Check recovery in LockIn.';
    case 'applyFailed': return 'Browser rules could not be confirmed. Open Help & recovery in LockIn.';
    default: return issue ? 'Browser coverage is unconfirmed. Check LockIn before continuing.' : fallback;
  }
}
