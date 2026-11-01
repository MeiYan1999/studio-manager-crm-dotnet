// Every API request, download and SPA link stays in the path tenant selected by the URL.
export const tenantPrefix = /^\/s\/[a-z0-9-]+(?=\/|$)/i.exec(location.pathname)?.[0] || '';
export const tenantPath = value => tenantPrefix && value.startsWith('/') && !value.startsWith('//') &&
    !value.startsWith('/assets/') && !value.startsWith(tenantPrefix + '/') && value !== tenantPrefix
  ? tenantPrefix + value : value;
export const localPath = value => tenantPrefix && value.startsWith(tenantPrefix) ? value.slice(tenantPrefix.length) || '/' : value;
if (tenantPrefix) {
  const originalFetch = window.fetch.bind(window);
  window.fetch = (input, options) => originalFetch(typeof input === 'string' ? tenantPath(input) : input, options);
  const patch = node => {
    if (!(node instanceof Element)) return;
    for (const item of [node, ...node.querySelectorAll('[href], [src]')]) {
      for (const attr of ['href', 'src']) {
        const value = item.getAttribute(attr);
        if (value?.startsWith('/') && !value.startsWith('/assets/')) item.setAttribute(attr, tenantPath(value));
      }
    }
  };
  new MutationObserver(changes => changes.forEach(change => change.addedNodes.forEach(patch)))
    .observe(document.documentElement, { childList: true, subtree: true });
}
