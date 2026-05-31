const STORAGE_KEY = "storymark.deviceId";

// A stable per-device identifier so the backend can keep one progress row per
// device and resolve the latest across devices. Generated once, then persisted.
export function getDeviceId(): string {
  const existing = localStorage.getItem(STORAGE_KEY);
  if (existing !== null && existing.length > 0) {
    return existing;
  }
  const generated = crypto.randomUUID();
  localStorage.setItem(STORAGE_KEY, generated);
  return generated;
}
