const ICONS = {
  home: '<path d="M3 10.5 12 3l9 7.5"/><path d="M5.5 9.5V21h13V9.5"/><path d="M9.5 21v-6h5v6"/>',
  albums: '<rect x="3" y="5" width="18" height="16" rx="2"/><path d="M8 3v4M16 3v4"/><path d="m7.5 16 3.1-3.1a1.4 1.4 0 0 1 2 0L16 16"/><circle cx="16.7" cy="9.6" r="1.1"/>',
  plus: '<path d="M12 5v14M5 12h14"/>',
  minus: '<path d="M5 12h14"/>',
  heart: '<path d="M20.8 4.6a5.5 5.5 0 0 0-7.8 0L12 5.7l-1.1-1.1a5.5 5.5 0 0 0-7.8 7.8l1.1 1.1L12 21l7.8-7.5 1.1-1.1a5.5 5.5 0 0 0-.1-7.8Z"/>',
  users: '<path d="M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2"/><circle cx="9" cy="7" r="4"/><path d="M22 21v-2a4 4 0 0 0-3-3.9M16 3.1a4 4 0 0 1 0 7.8"/>',
  settings: '<path d="M12 15.5a3.5 3.5 0 1 0 0-7 3.5 3.5 0 0 0 0 7Z"/><path d="M19.4 15a1.7 1.7 0 0 0 .3 1.9l.1.1a2 2 0 1 1-2.8 2.8l-.1-.1a1.7 1.7 0 0 0-1.9-.3 1.7 1.7 0 0 0-1 1.5V21a2 2 0 1 1-4 0v-.1a1.7 1.7 0 0 0-1.1-1.5 1.7 1.7 0 0 0-1.9.3l-.1.1a2 2 0 1 1-2.8-2.8l.1-.1a1.7 1.7 0 0 0 .3-1.9 1.7 1.7 0 0 0-1.5-1H3a2 2 0 1 1 0-4h.1a1.7 1.7 0 0 0 1.5-1.1 1.7 1.7 0 0 0-.3-1.9l-.1-.1a2 2 0 1 1 2.8-2.8l.1.1a1.7 1.7 0 0 0 1.9.3H9a1.7 1.7 0 0 0 1-1.5V3a2 2 0 1 1 4 0v.1a1.7 1.7 0 0 0 1 1.5 1.7 1.7 0 0 0 1.9-.3l.1-.1a2 2 0 1 1 2.8 2.8l-.1.1a1.7 1.7 0 0 0-.3 1.9V9a1.7 1.7 0 0 0 1.5 1H21a2 2 0 1 1 0 4h-.1a1.7 1.7 0 0 0-1.5 1Z"/>',
  search: '<circle cx="11" cy="11" r="7.5"/><path d="m21 21-4.3-4.3"/>',
  arrowLeft: '<path d="M19 12H5"/><path d="m12 19-7-7 7-7"/>',
  arrowRight: '<path d="M5 12h14"/><path d="m12 5 7 7-7 7"/>',
  arrowUpRight: '<path d="M7 17 17 7"/><path d="M8 7h9v9"/>',
  external: '<path d="M14 3h7v7"/><path d="M10 14 21 3"/><path d="M21 14v5a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h5"/>',
  copy: '<rect x="9" y="9" width="12" height="12" rx="2"/><path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"/>',
  check: '<path d="m20 6-11 11-5-5"/>',
  checkCircle: '<circle cx="12" cy="12" r="9"/><path d="m8 12 2.7 2.7L16.5 9"/>',
  x: '<path d="M18 6 6 18M6 6l12 12"/>',
  download: '<path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"/><path d="m7 10 5 5 5-5M12 15V3"/>',
  share: '<circle cx="18" cy="5" r="3"/><circle cx="6" cy="12" r="3"/><circle cx="18" cy="19" r="3"/><path d="m8.6 13.5 6.8 4M15.4 6.5l-6.8 4"/>',
  trash: '<path d="M3 6h18M8 6V4a1 1 0 0 1 1-1h6a1 1 0 0 1 1 1v2M19 6l-1 14a2 2 0 0 1-2 2H8a2 2 0 0 1-2-2L5 6"/><path d="M10 11v6M14 11v6"/>',
  refresh: '<path d="M21 12a9 9 0 1 1-2.6-6.4"/><path d="M21 3v6h-6"/>',
  link: '<path d="M10.5 13.5a5 5 0 0 0 7.1 0l2-2a5 5 0 0 0-7.1-7.1l-1.1 1.1"/><path d="M13.5 10.5a5 5 0 0 0-7.1 0l-2 2a5 5 0 0 0 7.1 7.1l1.1-1.1"/>',
  more: '<circle cx="5" cy="12" r="1"/><circle cx="12" cy="12" r="1"/><circle cx="19" cy="12" r="1"/>',
  menu: '<path d="M4 7h16M4 12h16M4 17h16"/>',
  logout: '<path d="M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4"/><path d="m16 17 5-5-5-5M21 12H9"/>',
  eye: '<path d="M2.1 12S5.6 5.5 12 5.5 21.9 12 21.9 12 18.4 18.5 12 18.5 2.1 12 2.1 12Z"/><circle cx="12" cy="12" r="3"/>',
  eyeOff: '<path d="m3 3 18 18"/><path d="M10.6 10.6a2 2 0 0 0 2.8 2.8"/><path d="M9.9 5.8A10.8 10.8 0 0 1 12 5.5c6.4 0 9.9 6.5 9.9 6.5a18 18 0 0 1-2.1 3.1"/><path d="M6.6 6.6A17 17 0 0 0 2.1 12S5.6 18.5 12 18.5c1.4 0 2.7-.3 3.9-.8"/>',
  folder: '<path d="M3 7a2 2 0 0 1 2-2h5l2 2h7a2 2 0 0 1 2 2v9a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2V7Z"/>',
  filter: '<path d="M4 5h16l-6.4 7.8V19l-3.2 2v-8.2L4 5Z"/>',
  hardDrive: '<rect x="3" y="4" width="18" height="7" rx="2"/><rect x="3" y="13" width="18" height="7" rx="2"/><path d="M7 7.5h.01M7 16.5h.01"/>',
  file: '<path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8Z"/><path d="M14 2v6h6"/>',
  drive: '<path d="m9.3 3.8 3.2 5.5H7.1L3.9 3.8h5.4Zm5.4 0h5.4l-6.9 12H2.3l4.3-7.5h5.8l2.3-4.5Z" fill="currentColor" stroke="none"/><path d="M21.7 16.3 19 20.9H8.7l2.6-4.6h10.4Z" fill="currentColor" stroke="none" opacity=".55"/>',
  alertCircle: '<circle cx="12" cy="12" r="9"/><path d="M12 8v5M12 16.5v.01"/>',
  alertTriangle: '<path d="M10.3 3.9 2.1 18a2 2 0 0 0 1.7 3h16.4a2 2 0 0 0 1.7-3L13.7 3.9a2 2 0 0 0-3.4 0Z"/><path d="M12 9v4M12 17v.01"/>',
  calendar: '<rect x="3" y="5" width="18" height="16" rx="2"/><path d="M16 3v4M8 3v4M3 10h18"/>',
  mapPin: '<path d="M20 10c0 5-8 12-8 12S4 15 4 10a8 8 0 1 1 16 0Z"/><circle cx="12" cy="10" r="2.5"/>',
  chevronDown: '<path d="m6 9 6 6 6-6"/>',
  chevronRight: '<path d="m9 18 6-6-6-6"/>',
  loader: '<path d="M21 12a9 9 0 1 1-6.2-8.6"/>',
  camera: '<path d="M14.5 4h-5L7 7H4a2 2 0 0 0-2 2v9a2 2 0 0 0 2 2h16a2 2 0 0 0 2-2V9a2 2 0 0 0-2-2h-3l-2.5-3Z"/><circle cx="12" cy="13" r="4"/>',
  edit: '<path d="M12 20h9"/><path d="M16.5 3.5a2.1 2.1 0 0 1 3 3L7 19l-4 1 1-4L16.5 3.5Z"/>',
  send: '<path d="m22 2-7 20-4-9-9-4 20-7Z"/><path d="M22 2 11 13"/>',
  clock: '<circle cx="12" cy="12" r="9"/><path d="M12 7v5l3.5 2"/>',
  mail: '<rect x="3" y="5" width="18" height="14" rx="2"/><path d="m3 7 9 6 9-6"/>',
  sparkles: '<path d="m12 3 1.2 3.7L17 8l-3.8 1.3L12 13l-1.2-3.7L7 8l3.8-1.3L12 3ZM19 14l.8 2.2L22 17l-2.2.8L19 20l-.8-2.2L16 17l2.2-.8L19 14ZM5 13l.7 2.3L8 16l-2.3.7L5 19l-.7-2.3L2 16l2.3-.7L5 13Z"/>',
  image: '<rect x="3" y="3" width="18" height="18" rx="2"/><circle cx="8.5" cy="8.5" r="1.5"/><path d="m21 15-5-5L5 21"/>',
  lock: '<rect x="4" y="10" width="16" height="11" rx="2"/><path d="M8 10V7a4 4 0 0 1 8 0v3"/>',
  credit: '<path d="M4 7h16M4 12h10M4 17h7"/>',
  refreshCw: '<path d="M20 7h-5V2"/><path d="M20 7a9 9 0 1 0 1 7"/><path d="M4 17h5v5"/>',
  at: '<circle cx="12" cy="12" r="4"/><path d="M16 8v5a3 3 0 0 0 6 0v-1a10 10 0 1 0-3.9 7.9"/>',
  zoomIn: '<circle cx="11" cy="11" r="7.5"/><path d="m21 21-4.3-4.3M11 8v6M8 11h6"/>',
  quote: '<path d="M9.5 11H5a1 1 0 0 1-1-1V7.5A2.5 2.5 0 0 1 6.5 5H8a1 1 0 0 1 1 1v8.5c0 2.5-1.5 4-4 4.5M20.5 11H16a1 1 0 0 1-1-1V7.5A2.5 2.5 0 0 1 17.5 5H19a1 1 0 0 1 1 1v8.5c0 2.5-1.5 4-4 4.5"/>',
  board: '<rect x="3" y="4" width="18" height="16" rx="2"/><path d="M9 8v8M15 8v5"/>',
  gripVertical: '<circle cx="9" cy="6" r="1"/><circle cx="15" cy="6" r="1"/><circle cx="9" cy="12" r="1"/><circle cx="15" cy="12" r="1"/><circle cx="9" cy="18" r="1"/><circle cx="15" cy="18" r="1"/>',
  tag: '<path d="M20.6 13.4 11 3H3v8l10.6 10.6a2 2 0 0 0 2.8 0l4.2-4.2a2 2 0 0 0 0-2.8Z"/><circle cx="7.5" cy="7.5" r="1.3"/>',
  listChecks: '<path d="m3 6 1.7 1.7L8 4.5"/><path d="m3 12 1.7 1.7L8 10.5"/><path d="m3 18 1.7 1.7L8 16.5"/><path d="M12 6h9M12 12h9M12 18h9"/>',
  palette: '<path d="M12 3a9 9 0 0 0 0 18h1.5a2 2 0 0 0 1.6-3.2 1.8 1.8 0 0 1 1.4-2.9H19a2 2 0 0 0 2-2A9 9 0 0 0 12 3Z"/><circle cx="7.5" cy="11.5" r="1"/><circle cx="9.5" cy="7.5" r="1"/><circle cx="14" cy="6.5" r="1"/><circle cx="17" cy="10" r="1"/>',
  alignLeft: '<path d="M4 6h16M4 12h11M4 18h16"/>',
  arrowUp: '<path d="M12 19V5"/><path d="m5 12 7-7 7 7"/>',
  arrowDown: '<path d="M12 5v14"/><path d="m5 12 7 7 7-7"/>',
  history: '<path d="M3 12a9 9 0 1 0 3-6.7"/><path d="M3 4v5h5"/><path d="M12 7v5l3 2"/>',
  user: '<path d="M20 21v-2a4 4 0 0 0-4-4H8a4 4 0 0 0-4 4v2"/><circle cx="12" cy="7" r="4"/>',
  userPlus: '<path d="M15 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2"/><circle cx="8" cy="7" r="4"/><path d="M19 8v6M22 11h-6"/>',
  shield: '<path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10Z"/><path d="m9 12 2 2 4-4"/>',
  key: '<circle cx="7.5" cy="15.5" r="4.5"/><path d="m10.8 12.2 8-8"/><path d="m16 5 3 3M18.5 2.5l3 3"/>',
  wallet: '<path d="M3 7.5A2.5 2.5 0 0 1 5.5 5H19a2 2 0 0 1 2 2v10a2 2 0 0 1-2 2H5.5A2.5 2.5 0 0 1 3 16.5v-9Z"/><path d="M3 8h18"/><circle cx="17" cy="13" r="1.2"/>',
  receipt: '<path d="M6 3h12v18l-3-2-3 2-3-2-3 2V3Z"/><path d="M9 7h6M9 11h6M9 15h4"/>',
  chart: '<path d="M4 20V10M10 20V4M16 20v-7M22 20H2"/>',
  bell: '<path d="M18 8a6 6 0 1 0-12 0c0 7-3 8-3 8h18s-3-1-3-8"/><path d="M13.7 21a2 2 0 0 1-3.4 0"/>',
  printer: '<path d="M6 9V3h12v6"/><rect x="3" y="9" width="18" height="8" rx="2"/><path d="M7 17h10v4H7z"/>'
};

export function icon(name, className = '') {
  const path = ICONS[name] || ICONS.image;
  return `<svg class="icon ${className}" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true" focusable="false">${path}</svg>`;
}

export function escapeHtml(value) {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#039;');
}

export function escapeAttr(value) {
  return escapeHtml(value);
}

export function normalizeSearch(value) {
  return String(value ?? '')
    .normalize('NFC')
    .toLocaleLowerCase('vi')
    .trim();
}

export function stripDiacritics(value) {
  return String(value ?? '')
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replaceAll('đ', 'd')
    .replaceAll('Đ', 'D')
    .toLowerCase();
}

export function initials(name) {
  const parts = String(name || 'Mây Studio')
    .replaceAll('&', ' ')
    .split(/\s+/)
    .filter(Boolean);
  if (parts.length === 0) return 'M';
  if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
  return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
}

export function pluralPhotos(count) {
  return `${Number(count).toLocaleString('vi-VN')} ảnh`;
}

export function parseAlbumDate(value) {
  if (!value) return null;
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(value);
  if (!match) return null;
  const date = new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
  return Number.isNaN(date.getTime()) ? null : date;
}

export function formatDate(value, long = false) {
  const date = parseAlbumDate(value);
  if (!date) return '';
  if (long) {
    return `${date.getDate()} tháng ${date.getMonth() + 1}, ${date.getFullYear()}`;
  }
  return `${String(date.getDate()).padStart(2, '0')}.${String(date.getMonth() + 1).padStart(2, '0')}.${date.getFullYear()}`;
}

export function formatTimestamp(value) {
  if (!value) return '';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  return new Intl.DateTimeFormat('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23'
  }).format(date);
}

export function formatShortTimestamp(value) {
  if (!value) return '';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  return new Intl.DateTimeFormat('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh',
    day: '2-digit',
    month: '2-digit',
    year: 'numeric'
  }).format(date);
}

export function formatRelativeTime(value) {
  if (!value) return '';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  const diff = date.getTime() - Date.now();
  const absolute = Math.abs(diff);
  if (absolute < 60_000) return 'vừa xong';

  const formatter = new Intl.RelativeTimeFormat('vi', { numeric: 'auto' });
  if (absolute < 3_600_000) {
    return formatter.format(Math.round(diff / 60_000), 'minute');
  }

  if (absolute < 86_400_000) {
    return formatter.format(Math.round(diff / 3_600_000), 'hour');
  }

  if (absolute < 7 * 86_400_000) {
    return formatter.format(Math.round(diff / 86_400_000), 'day');
  }

  return formatShortTimestamp(value);
}

export function todayLabel() {
  return new Intl.DateTimeFormat('vi-VN', {
    weekday: 'long',
    day: '2-digit',
    month: 'long',
    year: 'numeric'
  }).format(new Date());
}

export function shareUrl(shareToken) {
  return `${window.location.origin}/a/${encodeURIComponent(shareToken)}`;
}

export async function copyText(value) {
  try {
    await navigator.clipboard.writeText(value);
    return true;
  } catch {
    try {
      const area = document.createElement('textarea');
      area.value = value;
      area.setAttribute('readonly', '');
      area.style.position = 'fixed';
      area.style.opacity = '0';
      document.body.append(area);
      area.select();
      const ok = document.execCommand('copy');
      area.remove();
      return ok;
    } catch {
      return false;
    }
  }
}

export function debounce(callback, wait = 160) {
  let timer;
  return (...args) => {
    window.clearTimeout(timer);
    timer = window.setTimeout(() => callback(...args), wait);
  };
}

export function navigateTo(path, options = {}) {
  window.dispatchEvent(new CustomEvent('may:navigate', {
    detail: { path, replace: Boolean(options.replace) }
  }));
}

export function notifySession(session) {
  window.dispatchEvent(new CustomEvent('may:session', { detail: session }));
}

export async function downloadFile(url, fallbackName = 'may-album.xlsx') {
  let response;
  try {
    response = await fetch(url, { credentials: 'same-origin' });
  } catch {
    throw new Error('Không thể kết nối máy chủ để tải tệp.');
  }

  if (!response.ok) {
    let message = 'Chưa xuất được tệp. Vui lòng thử lại.';
    try {
      const payload = await response.json();
      if (payload?.error) message = payload.error;
    } catch {
      // Tệp lỗi không phải JSON — dùng thông báo mặc định.
    }
    throw new Error(message);
  }

  const disposition = response.headers.get('content-disposition') || '';
  const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition);
  let fileName = fallbackName;
  if (match && match[1]) {
    try {
      fileName = decodeURIComponent(match[1].trim());
    } catch {
      fileName = match[1].trim();
    }
  }

  const blob = await response.blob();
  const objectUrl = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = objectUrl;
  anchor.download = fileName;
  anchor.rel = 'noopener';
  document.body.append(anchor);
  anchor.click();
  anchor.remove();
  window.setTimeout(() => URL.revokeObjectURL(objectUrl), 4000);
  return fileName;
}

export function showToast(message, type = 'success', duration = 3800) {
  const root = document.getElementById('toast-root');
  if (!root) return;

  const toast = document.createElement('div');
  toast.className = `toast toast--${type}`;
  const toastIcon = type === 'error' ? 'alertCircle' : type === 'info' ? 'sparkles' : 'checkCircle';
  toast.innerHTML = `
    <span class="toast__icon">${icon(toastIcon)}</span>
    <p>${escapeHtml(message)}</p>
    <button class="toast__close" type="button" aria-label="Đóng">${icon('x')}</button>
  `;
  root.append(toast);
  requestAnimationFrame(() => toast.classList.add('is-visible'));

  let removed = false;
  const remove = () => {
    if (removed) return;
    removed = true;
    toast.classList.remove('is-visible');
    window.setTimeout(() => toast.remove(), 260);
  };

  toast.querySelector('.toast__close').addEventListener('click', remove);
  window.setTimeout(remove, duration);
}

export function setButtonLoading(button, isLoading, loadingLabel = 'Đang xử lý…') {
  if (!button) return;
  if (isLoading) {
    if (!button.dataset.originalHtml) button.dataset.originalHtml = button.innerHTML;
    button.disabled = true;
    button.classList.add('is-loading');
    button.innerHTML = `${icon('loader', 'spin')}<span>${escapeHtml(loadingLabel)}</span>`;
  } else {
    button.disabled = false;
    button.classList.remove('is-loading');
    if (button.dataset.originalHtml) {
      button.innerHTML = button.dataset.originalHtml;
      delete button.dataset.originalHtml;
    }
  }
}

export function createModal(html, options = {}) {
  const root = document.getElementById('modal-root');
  const dialog = document.createElement('dialog');
  dialog.className = `dialog ${options.className || ''}`.trim();
  dialog.innerHTML = html;
  root.append(dialog);

  let closed = false;
  const finish = () => {
    if (closed) return;
    closed = true;
    dialog.remove();
    options.onClose?.();
  };

  const close = (returnValue = '') => {
    if (closed) return;
    if (dialog.open) {
      dialog.close(returnValue);
    } else {
      finish();
    }
  };

  dialog.addEventListener('close', finish);
  dialog.addEventListener('click', event => {
    if (event.target === dialog) close();
  });
  dialog.addEventListener('click', event => {
    if (event.target.closest('[data-close-dialog]')) close();
  });

  if (!dialog.open) {
    dialog.showModal();
  }

  options.onMount?.(dialog, close);
  const focusTarget = dialog.querySelector('[autofocus], input, button, a[href]');
  if (focusTarget && !dialog.querySelector('[autofocus]')) {
    window.requestAnimationFrame(() => focusTarget.focus());
  }

  return { dialog, close };
}

export function confirmDialog({
  title = 'Xác nhận',
  message = '',
  confirmLabel = 'Xác nhận',
  cancelLabel = 'Quay lại',
  danger = false,
  iconName = danger ? 'alertTriangle' : 'alertCircle'
} = {}) {
  return new Promise(resolve => {
    let settled = false;
    const modal = createModal(`
      <div class="dialog-card dialog-card--confirm">
        <button class="dialog-close" type="button" data-close-dialog aria-label="Đóng">${icon('x')}</button>
        <span class="dialog-symbol ${danger ? 'is-danger' : ''}">${icon(iconName)}</span>
        <p class="eyebrow">${danger ? 'LƯU Ý' : 'XÁC NHẬN'}</p>
        <h2 id="confirm-title">${escapeHtml(title)}</h2>
        <p class="dialog-copy">${escapeHtml(message)}</p>
        <div class="dialog-actions">
          <button class="btn btn--ghost" type="button" data-cancel>${escapeHtml(cancelLabel)}</button>
          <button class="btn ${danger ? 'btn--danger' : 'btn--primary'}" type="button" data-confirm>${escapeHtml(confirmLabel)}</button>
        </div>
      </div>
    `, {
      className: 'dialog--sm',
      onClose: () => {
        if (!settled) {
          settled = true;
          resolve(false);
        }
      }
    });

    const complete = value => {
      if (settled) return;
      settled = true;
      resolve(value);
      modal.close(String(value));
    };

    modal.dialog.querySelector('[data-cancel]').addEventListener('click', () => complete(false));
    modal.dialog.querySelector('[data-confirm]').addEventListener('click', () => complete(true));
    window.requestAnimationFrame(() => modal.dialog.querySelector('[data-confirm]')?.focus());
  });
}

export const PLACEHOLDER_IMAGE = 'data:image/svg+xml;charset=utf-8,' + encodeURIComponent(`
<svg xmlns="http://www.w3.org/2000/svg" width="900" height="1100" viewBox="0 0 900 1100">
  <defs>
    <linearGradient id="g" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#e9e0d3"/>
      <stop offset="1" stop-color="#d6c9b9"/>
    </linearGradient>
  </defs>
  <rect width="900" height="1100" fill="url(#g)"/>
  <g fill="none" stroke="#9a8e7d" stroke-width="10" stroke-linecap="round" stroke-linejoin="round">
    <rect x="315" y="425" width="270" height="210" rx="24"/>
    <circle cx="450" cy="530" r="58"/>
    <path d="M375 425l24-37h102l24 37"/>
  </g>
  <text x="450" y="725" fill="#8b7e6d" font-family="Georgia,serif" font-size="30" text-anchor="middle">Không tải được ảnh</text>
</svg>`);

export function applyImageFallbacks(root = document) {
  root.querySelectorAll('img').forEach(image => {
    image.addEventListener('error', () => {
      if (image.dataset.fallbackApplied) return;
      image.dataset.fallbackApplied = '1';
      image.src = PLACEHOLDER_IMAGE;
      image.classList.add('is-placeholder');
    }, { once: true });
  });
}

export function skeletonCards(count = 3) {
  return Array.from({ length: count }, () => `
    <div class="skeleton-card">
      <div class="skeleton skeleton-media"></div>
      <div class="skeleton-line skeleton-line--lg"></div>
      <div class="skeleton-line skeleton-line--sm"></div>
    </div>
  `).join('');
}

export function emptyState({ iconName = 'image', title, message, action = '' }) {
  return `
    <div class="empty-state">
      <span class="empty-state__icon">${icon(iconName)}</span>
      <h3>${escapeHtml(title)}</h3>
      <p>${escapeHtml(message)}</p>
      ${action}
    </div>
  `;
}

export function errorInline(message, code = '') {
  return `
    <div class="inline-alert inline-alert--error" role="alert" data-error-code="${escapeAttr(code)}">
      ${icon('alertCircle')}
      <p>${escapeHtml(message)}</p>
    </div>
  `;
}
