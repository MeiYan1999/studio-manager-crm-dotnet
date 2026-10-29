import { api, authApi, crmApi, salesApi, operationsApi } from './api.js';
import {
  icon,
  escapeHtml,
  escapeAttr,
  normalizeSearch,
  stripDiacritics,
  formatDate,
  formatTimestamp,
  formatShortTimestamp,
  formatRelativeTime,
  initials,
  pluralPhotos,
  shareUrl,
  copyText,
  debounce,
  navigateTo,
  notifySession,
  showToast,
  setButtonLoading,
  createModal,
  confirmDialog,
  emptyState,
  errorInline,
  todayLabel
} from './ui.js';

const STUDIO_FALLBACK = 'Mây Studio';

// ---------------------------------------------------------------- utilities

function number(value) {
  return Number(value || 0).toLocaleString('vi-VN');
}

function money(value) {
  return `${Number(value || 0).toLocaleString('vi-VN')} đ`;
}

function percent(part, total) {
  if (!total) return 0;
  return Math.round((part / total) * 100);
}

function safeUrl(value) {
  return escapeAttr(value || '');
}

function selectionLabel(count) {
  if (!count) return 'Chưa có lựa chọn';
  return `${number(count)} ảnh được chọn`;
}

function selectedPhotoCodes(album) {
  return album.photos
    .filter(photo => photo.isSelected)
    .sort((a, b) => a.code.localeCompare(b.code, 'vi', { numeric: true }))
    .map(photo => photo.code);
}

function greeting() {
  const hour = new Date().getHours();
  if (hour < 11) return 'Chào buổi sáng';
  if (hour < 14) return 'Chào buổi trưa';
  if (hour < 18) return 'Chào buổi chiều';
  return 'Chào buổi tối';
}

function statusPill(album) {
  if (!album.isPublished) {
    return `<span class="pill pill--draft">${icon('eyeOff')}Bản nháp</span>`;
  }
  if (album.isDemo) {
    return `<span class="pill pill--demo">${icon('sparkles')}Album mẫu</span>`;
  }
  return `<span class="pill pill--live"><span class="pill__dot"></span>Đang chia sẻ</span>`;
}

function coverMarkup(album, className = '', alt = '') {
  const url = album.coverThumbUrl || album.coverUrl;
  if (!url) {
    return `<div class="cover-fallback ${className}">${icon('image')}<span>Chưa có ảnh bìa</span></div>`;
  }
  return `<img class="${className}" src="${safeUrl(url)}" alt="${escapeAttr(alt || album.title || 'Ảnh bìa album')}" loading="lazy">`;
}

function selectionMeter(selected, total, className = '') {
  return `
    <div class="meter ${className}" role="progressbar" aria-valuemin="0" aria-valuemax="100" aria-valuenow="${percent(selected, total)}">
      <span style="width:${percent(selected, total)}%"></span>
    </div>
  `;
}

function errorBlock(message, code = '') {
  return errorInline(message || 'Có lỗi xảy ra. Vui lòng thử lại.', code);
}

function fieldError(input, message) {
  const holder = input.closest('.field');
  if (!holder) return;
  holder.classList.add('has-error');
  const noteId = `${input.id || input.name || 'field'}-error`;
  let note = holder.querySelector('.field__error');
  if (!note) {
    note = document.createElement('p');
    note.className = 'field__error';
    note.id = noteId;
    holder.append(note);
  }
  note.innerHTML = `${icon('alertCircle')}<span>${escapeHtml(message)}</span>`;
  input.setAttribute('aria-invalid', 'true');
  input.setAttribute('aria-describedby', noteId);
  input.focus();
}

function clearFieldErrors(form) {
  form.querySelectorAll('.field.has-error').forEach(field => {
    field.classList.remove('has-error');
    field.querySelectorAll('[aria-invalid="true"]').forEach(input => {
      input.removeAttribute('aria-invalid');
      input.removeAttribute('aria-describedby');
    });
  });
  form.querySelectorAll('.field__error').forEach(note => note.remove());
}

function formValue(form, name) {
  const field = form.elements.namedItem(name);
  if (!field) return '';
  return String(field.value ?? '').trim();
}

function formChecked(form, name) {
  const field = form.elements.namedItem(name);
  return Boolean(field?.checked);
}

// ---------------------------------------------------------------- shell

export function hasPermission(session, permission) {
  const user = session?.user;
  if (!user) return false;
  if (user.role === 'admin') return true;
  return Array.isArray(user.permissions) && user.permissions.includes(permission);
}

const NAV_GROUPS = [
  {
    label: 'Bảng điều khiển',
    items: [
      { key: 'dashboard', label: 'Tổng quan', href: '/admin', iconName: 'home', permission: 'dashboard' },
      { key: 'board', label: 'Bảng công việc', href: '/admin/board', iconName: 'board', permission: 'board' }
    ]
  },
  {
    label: 'Album & ảnh',
    items: [
      { key: 'albums', label: 'Album của bạn', href: '/admin/albums', iconName: 'albums', permission: 'albums' },
      { key: 'create', label: 'Tạo album mới', href: '/admin/create', iconName: 'plus', permission: 'albums' },
      { key: 'tools', label: 'Lọc ảnh theo mã', href: '/admin/tools', iconName: 'filter', permission: 'tools' }
    ]
  },
  {
    label: 'Khách hàng & lịch chụp',
    items: [
      { key: 'customers', label: 'Khách hàng', href: '/admin/customers', iconName: 'users', permission: 'customers' },
      { key: 'bookings', label: 'Lịch chụp', href: '/admin/bookings', iconName: 'calendar', permission: 'bookings' },
      { key: 'packages', label: 'Gói dịch vụ', href: '/admin/packages', iconName: 'tag', permission: 'packages' },
      { key: 'staff', label: 'Nhân sự', href: '/admin/staff', iconName: 'camera', permission: 'staff' }
    ]
  },
  {
    label: 'Kinh doanh',
    items: [
      { key: 'orders', label: 'Đơn hàng', href: '/admin/orders', iconName: 'receipt', permission: 'orders' },
      { key: 'contracts', label: 'Hợp đồng', href: '/admin/contracts', iconName: 'file', permission: 'contracts' },
      { key: 'reports', label: 'Báo cáo', href: '/admin/reports', iconName: 'chart', permission: 'reports' }
    ]
  },
  {
    label: 'Vận hành studio',
    items: [
      { key: 'finance', label: 'Tài chính & lương', href: '/admin/finance', iconName: 'wallet', permission: 'finance' },
      { key: 'shifts', label: 'Chia ca làm việc', href: '/admin/shifts', iconName: 'calendar', permission: 'attendance' },
      { key: 'attendance', label: 'Chấm công', href: '/admin/attendance', iconName: 'clock', permission: 'attendance' },
      { key: 'wardrobe', label: 'Tủ trang phục', href: '/admin/wardrobe', iconName: 'tag', permission: 'wardrobe' },
      { key: 'notifications', label: 'Thông báo', href: '/admin/notifications', iconName: 'bell', permission: 'notifications' }
    ]
  },
  {
    label: 'Quản trị',
    items: [
      { key: 'audit', label: 'Nhật ký studio', href: '/admin/audit', iconName: 'history', permission: 'audit' },
      { key: 'settings', label: 'Cài đặt studio', href: '/admin/settings', iconName: 'settings', permission: 'settings', adminOnly: true }
    ]
  }
];

export function adminShell({ active, session, content, topbarAction = '' }) {
  const studioName = session?.studioName || STUDIO_FALLBACK;
  const currentUser = session?.user;
  const isAdmin = currentUser?.role === 'admin';
  const displayName = currentUser?.displayName || studioName;
  const roleLabel = isAdmin ? 'Quản trị viên' : 'Nhân viên';
  const canCreateAlbum = hasPermission(session, 'albums');
  const nav = NAV_GROUPS.map(group => {
    const items = group.items.filter(item =>
      (!item.adminOnly || isAdmin) && hasPermission(session, item.permission));
    if (!items.length) return '';
    return `
      <p class="admin-nav__label">${escapeHtml(group.label)}</p>
      ${items.map(item => `
        <a class="admin-nav__item ${active === item.key ? 'is-active' : ''}" href="${item.href}" ${active === item.key ? 'aria-current="page"' : ''}>
          <span class="admin-nav__icon">${icon(item.iconName)}</span>
          <span>${escapeHtml(item.label)}</span>
        </a>
      `).join('')}
    `;
  }).join('');

  return `
    <div class="admin-shell">
      <div class="admin-scrim" data-sidebar-close></div>
      <aside class="admin-sidebar" aria-label="Điều hướng studio">
        <div class="admin-sidebar__brand">
          <a class="wordmark" href="/admin">mây<span>.</span><small>studio</small></a>
          <button class="icon-btn admin-sidebar__close" type="button" data-sidebar-close aria-label="Đóng menu">${icon('x')}</button>
        </div>
        ${canCreateAlbum ? `<a class="btn btn--primary admin-sidebar__create" href="/admin/create">${icon('plus')} Tạo album mới</a>` : ''}
        <nav class="admin-nav">
          ${nav}
        </nav>
        ${hasPermission(session, 'albums') ? `
          <div class="admin-sidebar__tip">
            <span class="admin-sidebar__tip-icon">${icon('sparkles')}</span>
            <p><strong>Album mẫu</strong><br>Xem trải nghiệm của khách qua hai album có sẵn.</p>
            <a href="/a/demo-ngay-chung-doi" target="_blank" rel="noopener">Mở album mẫu ${icon('arrowUpRight')}</a>
          </div>
        ` : ''}
        <div class="admin-sidebar__foot">
          <a class="admin-account" href="${isAdmin ? '/admin/settings' : '/admin/change-password'}" aria-label="${isAdmin ? 'Cài đặt tài khoản studio' : 'Đổi mật khẩu'}">
            <span class="avatar ${isAdmin ? 'avatar--ink' : 'avatar--olive'}">${escapeHtml(initials(displayName))}</span>
            <span class="admin-account__copy">
              <strong>${escapeHtml(displayName)}</strong>
              <small>${roleLabel}</small>
            </span>
            ${isAdmin ? icon('chevronRight') : ''}
          </a>
          <a class="icon-btn admin-account__logout" href="/admin/change-password" aria-label="Đổi mật khẩu" title="Đổi mật khẩu">${icon('key')}</a>
          <button class="icon-btn admin-account__logout" type="button" data-action="logout" aria-label="Đăng xuất" title="Đăng xuất">${icon('logout')}</button>
        </div>
      </aside>
      <div class="admin-main">
        <header class="admin-mobilebar">
          <button class="icon-btn" type="button" data-sidebar-open aria-label="Mở menu">${icon('menu')}</button>
          <a class="wordmark wordmark--small" href="/admin">mây<span>.</span></a>
          ${topbarAction || '<a class="icon-btn" href="/admin/create" aria-label="Tạo album mới">' + icon('plus') + '</a>'}
        </header>
        <main class="admin-page" id="admin-page">
          ${content}
        </main>
      </div>
    </div>
  `;
}

export function mountAdminShell(root, session) {
  document.body.classList.remove('sidebar-open');
  const open = () => document.body.classList.add('sidebar-open');
  const close = () => document.body.classList.remove('sidebar-open');

  const openButton = root.querySelector('[data-sidebar-open]');
  openButton?.addEventListener('click', open);
  root.querySelectorAll('[data-sidebar-close]').forEach(item => item.addEventListener('click', close));

  const onLogout = async () => {
    const button = root.querySelector('[data-action="logout"]');
    if (button?.disabled) return;
    try {
      if (button) button.disabled = true;
      await authApi.logout();
    } catch {
      // Local sign-out is still the correct outcome if the network fails.
    }
    notifySession({ authenticated: false, studioName: session?.studioName || STUDIO_FALLBACK, user: null });
    close();
    showToast('Bạn đã đăng xuất khỏi studio.', 'info');
    navigateTo('/admin/login');
  };
  root.querySelector('[data-action="logout"]')?.addEventListener('click', onLogout);

  return () => {
    close();
    document.body.classList.remove('sidebar-open');
  };
}

export function pageHead({ eyebrow, title, lead, actions = '', back = '' }) {
  return `
    <header class="admin-head">
      <div class="admin-head__copy">
        ${back ? `<a class="back-link" href="${back.href}">${icon('arrowLeft')} ${escapeHtml(back.label)}</a>` : ''}
        ${eyebrow ? `<p class="eyebrow">${escapeHtml(eyebrow)}</p>` : ''}
        <h1>${title}</h1>
        ${lead ? `<p class="admin-head__lead">${lead}</p>` : ''}
      </div>
      ${actions ? `<div class="admin-head__actions">${actions}</div>` : ''}
    </header>
  `;
}

// ---------------------------------------------------------------- login

export function loginView({ session }) {
  const next = new URLSearchParams(location.search).get('next') || '/admin';
  const html = `
    <div class="login-page">
      <section class="login-visual" aria-label="Mây Studio">
        <img class="login-visual__image" src="/assets/img/demo/07.jpg" alt="Khoảnh khắc cô dâu bên biển" fetchpriority="high">
        <div class="login-visual__veil"></div>
        <a class="wordmark wordmark--light" href="/">mây<span>.</span><small>album</small></a>
        <div class="login-visual__quote">
          <span class="quote-mark">${icon('quote')}</span>
          <blockquote>“Ảnh đẹp nhất là ảnh khiến ta nhớ lại cảm giác của ngày hôm đó.”</blockquote>
          <p>Mây Studio · Đà Lạt</p>
        </div>
        <div class="login-visual__foot">
          <span>${icon('lock')} Không gian riêng cho studio</span>
          <span>since 2021</span>
        </div>
      </section>

      <section class="login-panel">
        <div class="login-panel__top">
          <a class="wordmark wordmark--mobile" href="/">mây<span>.</span><small>album</small></a>
          <a class="btn btn--text btn--compact" href="/">${icon('arrowLeft')} Về trang chủ</a>
        </div>
        <div class="login-card">
          <p class="eyebrow"><span class="eyebrow__dot"></span>Khu vực studio</p>
          <h1>Chào mừng trở lại.</h1>
          <p class="login-card__lead">Đăng nhập bằng tài khoản riêng để mở bảng điều khiển và tiếp tục chăm sóc những album của bạn.</p>

          <form class="login-form" data-login-form novalidate>
            <div class="field">
              <label for="login-username">Tên đăng nhập</label>
              <div class="input-with-icon">
                <span>${icon('user')}</span>
                <input id="login-username" name="username" type="text" inputmode="text" autocomplete="username" autocapitalize="none" spellcheck="false" maxlength="32" placeholder="admin" required autofocus>
              </div>
            </div>
            <div class="field">
              <label for="login-password">Mật khẩu</label>
              <div class="input-with-action">
                <span class="input-with-action__icon">${icon('lock')}</span>
                <input id="login-password" name="password" type="password" autocomplete="current-password" maxlength="128" placeholder="Nhập mật khẩu" required>
                <button class="icon-btn input-with-action__button" type="button" data-toggle-password aria-label="Hiện mật khẩu">${icon('eye')}</button>
              </div>
            </div>
            <div data-login-error></div>
            <button class="btn btn--primary btn--lg btn--full" type="submit" data-login-submit>
              Vào studio ${icon('arrowRight')}
            </button>
          </form>

          <div class="login-card__foot">
            <p>${icon('lock')} Mỗi thành viên dùng tài khoản riêng do quản trị viên tạo trong Cài đặt studio.</p>
          </div>
        </div>
        <p class="login-panel__credit">© ${new Date().getFullYear()} Mây Studio · Mây Album</p>
      </section>
    </div>
  `;

  return {
    title: 'Đăng nhập studio',
    bodyClass: 'page-login',
    html,
    mount(root) {
      const form = root.querySelector('[data-login-form]');
      const usernameInput = form.elements.namedItem('username');
      const passwordInput = form.elements.namedItem('password');
      const errorRoot = form.querySelector('[data-login-error]');
      const submit = form.querySelector('[data-login-submit]');

      form.querySelector('[data-toggle-password]').addEventListener('click', event => {
        const button = event.currentTarget;
        const showing = passwordInput.type === 'text';
        passwordInput.type = showing ? 'password' : 'text';
        button.innerHTML = showing ? icon('eye') : icon('eyeOff');
        button.setAttribute('aria-label', showing ? 'Hiện mật khẩu' : 'Ẩn mật khẩu');
        passwordInput.focus();
      });

      form.addEventListener('submit', async event => {
        event.preventDefault();
        errorRoot.innerHTML = '';
        const username = usernameInput.value.trim().toLowerCase();
        const password = passwordInput.value;

        if (!username) {
          errorRoot.innerHTML = errorBlock('Vui lòng nhập tên đăng nhập.');
          usernameInput.focus();
          return;
        }

        if (!password) {
          errorRoot.innerHTML = errorBlock('Vui lòng nhập mật khẩu.');
          passwordInput.focus();
          return;
        }

        setButtonLoading(submit, true, 'Đang mở studio…');
        try {
          const result = await authApi.login(username, password);
          notifySession(result);
          const name = result?.user?.displayName;
          showToast(name ? `Chào mừng trở lại, ${name}!` : 'Chào mừng trở lại, studio!', 'success');
          const target = next.startsWith('/admin') && !next.startsWith('/admin/login') ? next : '/admin';
          navigateTo(target, { replace: true });
        } catch (error) {
          setButtonLoading(submit, false);
          errorRoot.innerHTML = errorBlock(error.message || 'Không thể đăng nhập.', error.code);
          passwordInput.select();
        }
      });
    }
  };
}

// ---------------------------------------------------------------- change password

export function changePasswordView({ session }) {
  const forced = Boolean(session?.user?.mustChangePassword);
  const next = new URLSearchParams(location.search).get('next') || '/admin';
  const safeNext = next.startsWith('/admin') && !next.startsWith('/admin/login') && !next.startsWith('/admin/change-password')
    ? next
    : '/admin';
  const displayName = session?.user?.displayName || 'thành viên studio';

  const html = `
    <div class="password-page">
      <header class="password-page__top">
        <a class="wordmark" href="/">mây<span>.</span><small>studio</small></a>
        ${forced
          ? `<button class="btn btn--ghost btn--compact" type="button" data-action="logout">${icon('logout')} Đăng xuất</button>`
          : `<a class="btn btn--text btn--compact" href="${escapeAttr(safeNext)}">${icon('arrowLeft')} Quay lại</a>`}
      </header>
      <main class="password-card">
        <span class="password-card__icon">${icon(forced ? 'shield' : 'key')}</span>
        <p class="eyebrow"><span class="eyebrow__dot"></span>${forced ? 'Bảo mật lần đầu' : 'Tài khoản của bạn'}</p>
        <h1>${forced ? 'Đổi mật khẩu trước khi tiếp tục.' : 'Đổi mật khẩu.'}</h1>
        <p class="password-card__lead">
          ${forced
            ? `Chào ${escapeHtml(displayName)}, quản trị viên đã tạo mật khẩu tạm cho bạn. Hãy đặt mật khẩu riêng để bảo vệ dữ liệu studio.`
            : `Chọn mật khẩu mới cho ${escapeHtml(displayName)}. Mật khẩu cần ít nhất 8 ký tự và khác mật khẩu hiện tại.`}
        </p>
        <form class="password-form" data-password-change-form novalidate>
          <div class="field">
            <label for="password-current">Mật khẩu hiện tại <span class="required">*</span></label>
            <div class="input-with-action">
              <span class="input-with-action__icon">${icon('lock')}</span>
              <input id="password-current" name="currentPassword" type="password" autocomplete="current-password" maxlength="128" required autofocus>
              <button class="icon-btn input-with-action__button" type="button" data-toggle-password aria-label="Hiện mật khẩu">${icon('eye')}</button>
            </div>
          </div>
          <div class="field">
            <label for="password-new">Mật khẩu mới <span class="required">*</span></label>
            <input id="password-new" name="newPassword" type="password" autocomplete="new-password" minlength="8" maxlength="128" placeholder="Ít nhất 8 ký tự" required>
          </div>
          <div class="field">
            <label for="password-confirm">Nhập lại mật khẩu mới <span class="required">*</span></label>
            <input id="password-confirm" name="confirmPassword" type="password" autocomplete="new-password" minlength="8" maxlength="128" placeholder="Nhập lại mật khẩu mới" required>
          </div>
          <div data-password-message></div>
          <button class="btn btn--primary btn--lg btn--full" type="submit" data-password-submit>
            ${icon('check')} Lưu mật khẩu mới
          </button>
        </form>
        <p class="password-card__foot">${icon('shield')} Mật khẩu được mã hoá một chiều trên máy chủ. Không ai trong studio đọc được mật khẩu của bạn.</p>
      </main>
      <p class="password-page__credit">© ${new Date().getFullYear()} Mây Studio · Mây Album</p>
    </div>
  `;

  return {
    title: 'Đổi mật khẩu',
    bodyClass: 'page-login',
    html,
    mount(root) {
      const form = root.querySelector('[data-password-change-form]');
      const currentField = form.elements.namedItem('currentPassword');
      const newField = form.elements.namedItem('newPassword');
      const confirmField = form.elements.namedItem('confirmPassword');
      const messageRoot = form.querySelector('[data-password-message]');
      const submit = form.querySelector('[data-password-submit]');

      form.querySelector('[data-toggle-password]')?.addEventListener('click', event => {
        const button = event.currentTarget;
        const showing = currentField.type === 'text';
        currentField.type = showing ? 'password' : 'text';
        button.innerHTML = showing ? icon('eye') : icon('eyeOff');
        button.setAttribute('aria-label', showing ? 'Hiện mật khẩu' : 'Ẩn mật khẩu');
        currentField.focus();
      });

      root.querySelector('[data-action="logout"]')?.addEventListener('click', async () => {
        try {
          await authApi.logout();
        } catch {
          // Local sign-out is the correct outcome if the network fails.
        }
        notifySession({ authenticated: false, studioName: session?.studioName || STUDIO_FALLBACK, user: null });
        navigateTo('/admin/login', { replace: true });
      });

      form.addEventListener('submit', async event => {
        event.preventDefault();
        clearFieldErrors(form);
        messageRoot.innerHTML = '';
        const current = currentField.value;
        const nextPassword = newField.value;
        if (!current) {
          fieldError(currentField, 'Vui lòng nhập mật khẩu hiện tại.');
          return;
        }
        if (nextPassword.length < 8) {
          fieldError(newField, 'Mật khẩu mới cần ít nhất 8 ký tự.');
          return;
        }
        if (nextPassword !== confirmField.value) {
          fieldError(confirmField, 'Hai mật khẩu mới chưa khớp nhau.');
          return;
        }
        if (nextPassword === current) {
          fieldError(newField, 'Mật khẩu mới cần khác mật khẩu hiện tại.');
          return;
        }

        setButtonLoading(submit, true, 'Đang lưu…');
        try {
          const result = await authApi.changePassword(current, nextPassword);
          notifySession(result);
          showToast('Đã đổi mật khẩu. Chào bạn quay lại studio!', 'success');
          navigateTo(safeNext, { replace: true });
        } catch (error) {
          if (error.name === 'AbortError') return;
          setButtonLoading(submit, false);
          messageRoot.innerHTML = errorBlock(error.message || 'Chưa đổi được mật khẩu.', error.code);
          currentField.select();
        }
      });
    }
  };
}

// ---------------------------------------------------------------- copy helper

async function copyShareLink(button, token) {
  if (!token || !button) return;
  const ok = await copyText(shareUrl(token));
  if (!ok) {
    showToast('Không thể sao chép. Hãy sao chép liên kết trong ô bên cạnh.', 'error');
    return;
  }

  if (button.dataset.copying === '1') return;
  button.dataset.copying = '1';
  const original = button.innerHTML;
  const iconOnly = button.classList.contains('icon-btn');
  if (iconOnly) {
    button.innerHTML = icon('check');
    button.classList.add('is-copied');
  } else {
    button.innerHTML = `${icon('check')}<span>Đã sao chép</span>`;
    button.classList.add('is-copied');
  }
  showToast('Đã sao chép liên kết album.', 'success');
  window.setTimeout(() => {
    button.innerHTML = original;
    button.classList.remove('is-copied');
    delete button.dataset.copying;
  }, 1900);
}

async function copySelectedCodes(button, codes) {
  if (!button) return;
  if (!codes.length) {
    showToast('Album chưa có mã ảnh nào khách đã chọn.', 'info');
    return;
  }

  const ok = await copyText(codes.join(','));
  if (!ok) {
    showToast('Không thể sao chép mã ảnh. Hãy thử lại.', 'error');
    return;
  }

  if (button.dataset.copying === '1') return;
  button.dataset.copying = '1';
  const original = button.innerHTML;
  button.innerHTML = `${icon('check')}<span>Đã sao chép</span>`;
  button.classList.add('is-copied');
  showToast(`Đã sao chép ${number(codes.length)} mã ảnh.`, 'success');
  window.setTimeout(() => {
    button.innerHTML = original;
    button.classList.remove('is-copied');
    delete button.dataset.copying;
  }, 1900);
}

// ---------------------------------------------------------------- dashboard

function localIsoDate(date = new Date()) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
}

function sixMonthRange() {
  const now = new Date();
  const start = new Date(now.getFullYear(), now.getMonth() - 5, 1);
  return { from: localIsoDate(start), to: localIsoDate(now) };
}

function clockNow() {
  return new Intl.DateTimeFormat('vi-VN', {
    timeZone: 'Asia/Ho_Chi_Minh',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23'
  }).format(new Date());
}

const DASH_BOOKING_STATUS = {
  pending: { label: 'Chờ xác nhận', tone: 'warn' },
  confirmed: { label: 'Đã xác nhận', tone: 'live' },
  shooting: { label: 'Đang chụp', tone: 'clay' },
  editing: { label: 'Đang chỉnh sửa', tone: 'olive' },
  delivered: { label: 'Đã giao', tone: 'submitted' },
  cancelled: { label: 'Đã huỷ', tone: 'neutral' }
};

const DASH_PAYMENT_STATUS = {
  paid: { label: 'Đã thanh toán', tone: 'paid' },
  deposit: { label: 'Đã cọc', tone: 'deposit' },
  unpaid: { label: 'Chưa thanh toán', tone: 'unpaid' }
};

export function dashboardView({ session }) {
  const studioName = session?.studioName || STUDIO_FALLBACK;
  const content = `
    <div class="dashboard-skeleton">
      <div class="skeleton skeleton-title"></div>
      <div class="skeleton skeleton-hero"></div>
      <div class="studio-kpis">
        ${Array.from({ length: 4 }, () => '<div class="skeleton skeleton-stat"></div>').join('')}
      </div>
      <div class="dash-columns">
        <div class="skeleton skeleton-panel"></div>
        <div class="skeleton skeleton-panel"></div>
      </div>
    </div>
  `;

  let teardown = () => {};
  let disposed = false;
  let loading = false;
  let clockTimer = 0;

  return {
    title: 'Tổng quan',
    bodyClass: 'page-admin',
    html: adminShell({
      active: 'dashboard',
      session,
      content: `<div class="admin-page__inner" data-dashboard>
        <div data-dash-content>${content}</div>
      </div>`
    }),
    mount(root) {
      teardown = mountAdminShell(root, session);
      const holder = root.querySelector('[data-dash-content]');
      const page = root.querySelector('[data-dashboard]');

      const startClock = () => {
        window.clearInterval(clockTimer);
        const tick = () => {
          const node = root.querySelector('[data-dash-clock]');
          if (node) node.textContent = clockNow();
        };
        tick();
        clockTimer = window.setInterval(tick, 20000);
      };

      page.addEventListener('click', event => {
        const copyButton = event.target.closest('[data-copy-token]');
        if (copyButton) {
          event.preventDefault();
          copyShareLink(copyButton, copyButton.dataset.copyToken);
          return;
        }
        if (event.target.closest('[data-retry]')) {
          load();
        }
      });

      const load = async () => {
        if (loading || disposed) return;
        loading = true;
        holder.innerHTML = content;
        const today = localIsoDate();
        const range = sixMonthRange();
        try {
          const [
            albumsResult,
            boardResult,
            crmResult,
            salesResult,
            financeResult,
            notificationsResult,
            outfitsResult,
            ordersResult,
            reportResult,
            attendanceResult
          ] = await Promise.allSettled([
            hasPermission(session, 'albums')
              ? api('/api/admin/albums')
              : Promise.resolve([]),
            hasPermission(session, 'board')
              ? api('/api/admin/board')
              : Promise.resolve(null),
            (hasPermission(session, 'dashboard') || hasPermission(session, 'customers') || hasPermission(session, 'bookings'))
              ? api('/api/admin/crm/summary')
              : Promise.resolve(null),
            (hasPermission(session, 'orders') || hasPermission(session, 'reports'))
              ? salesApi.overview()
              : Promise.resolve(null),
            hasPermission(session, 'finance')
              ? operationsApi.financeOverview()
              : Promise.resolve(null),
            hasPermission(session, 'notifications')
              ? operationsApi.notifications()
              : Promise.resolve(null),
            hasPermission(session, 'wardrobe')
              ? operationsApi.outfits()
              : Promise.resolve(null),
            hasPermission(session, 'orders')
              ? salesApi.orders()
              : Promise.resolve(null),
            hasPermission(session, 'reports')
              ? salesApi.reportSummary(range)
              : Promise.resolve(null),
            hasPermission(session, 'attendance')
              ? operationsApi.attendance(today)
              : Promise.resolve(null)
          ]);
          if (disposed) return;
          if (
            albumsResult.status === 'rejected' &&
            crmResult.status === 'rejected' &&
            salesResult.status === 'rejected'
          ) {
            throw albumsResult.reason;
          }
          const albums = albumsResult.status === 'fulfilled' && Array.isArray(albumsResult.value)
            ? albumsResult.value
            : [];
          const board = boardResult.status === 'fulfilled' ? boardResult.value : null;
          const crm = crmResult.status === 'fulfilled' ? crmResult.value : null;
          const sales = salesResult.status === 'fulfilled' ? salesResult.value : null;
          const operations = {
            finance: financeResult.status === 'fulfilled' ? financeResult.value : null,
            notifications: notificationsResult.status === 'fulfilled' ? notificationsResult.value : null,
            outfits: outfitsResult.status === 'fulfilled' ? outfitsResult.value : null
          };
          const extra = {
            board,
            orders: ordersResult.status === 'fulfilled' ? ordersResult.value : null,
            report: reportResult.status === 'fulfilled' ? reportResult.value : null,
            attendance: attendanceResult.status === 'fulfilled' ? attendanceResult.value : null,
            today
          };
          holder.innerHTML = renderDashboard(albums, studioName, board, crm, sales, operations, session, extra);
          startClock();
        } catch (error) {
          if (disposed || error.name === 'AbortError') return;
          holder.innerHTML = `
            <section class="panel panel--pad panel--error">
              ${errorInline(error.message || 'Không tải được tổng quan studio.', error.code)}
              <button class="btn btn--ghost" type="button" data-retry>${icon('refresh')} Thử lại</button>
            </section>
          `;
        } finally {
          loading = false;
        }
      };

      load();
    },
    cleanup() {
      disposed = true;
      window.clearInterval(clockTimer);
      teardown();
    }
  };
}

function renderDashboard(albums, studioName, board = null, crm = null, sales = null, operations = null, session = null, extra = {}) {
  const hasCrm = Boolean(crm && (
    Number(crm.customerCount || 0) > 0 ||
    Number(crm.upcomingBookingCount || 0) > 0 ||
    Number(crm.monthBookingCount || 0) > 0
  ));
  const hasSales = Boolean(sales && (
    Number(sales.orderCount || 0) > 0 ||
    Number(sales.contractCount || 0) > 0
  ));

  if (hasCrm || hasSales) {
    return renderStudioDashboard({ studioName, crm, sales, operations, session, ...extra });
  }

  return renderAlbumDashboard(albums, studioName, board, crm, sales, operations, session);
}

function renderStudioDashboard({
  studioName,
  crm = null,
  sales = null,
  operations = null,
  session = null,
  board = null,
  orders = null,
  report = null,
  attendance = null,
  today = localIsoDate()
}) {
  const canBookings = hasPermission(session, 'bookings');
  const canOrders = hasPermission(session, 'orders');
  const canReports = hasPermission(session, 'reports');
  const canCustomers = hasPermission(session, 'customers');
  const canAttendance = hasPermission(session, 'attendance');
  const canNotifications = hasPermission(session, 'notifications');
  const canBoard = hasPermission(session, 'board');

  const displayName = studioName;
  const bookings = Array.isArray(crm?.nextBookings) ? crm.nextBookings : [];
  const todayBookings = bookings.filter(item => item.date === today);
  const todayCount = Number(crm?.todayBookingCount || todayBookings.length || 0);
  const nextBooking = todayBookings[0] || bookings[0] || null;
  const upcoming = bookings.length ? bookings.slice(0, 4) : nextBooking ? [nextBooking] : [];
  const notifications = Array.isArray(operations?.notifications) ? operations.notifications : null;
  const unread = notifications ? notifications.filter(item => !item.isRead).length : 0;
  const attendanceRecords = Array.isArray(attendance) ? attendance : null;
  const activeStaff = Number(crm?.staffCount || 0);
  const attended = attendanceRecords ? attendanceRecords.length : 0;
  const reportMonths = Array.isArray(report?.months) ? report.months : [];
  const chartTotal = reportMonths.reduce((sum, item) => sum + Number(item.total || 0), 0);
  const orderList = (Array.isArray(orders) ? [...orders] : [])
    .sort((a, b) => new Date(b.createdAt || 0) - new Date(a.createdAt || 0));
  const recentOrders = orderList.slice(0, 5);

  const todayList = todayBookings.length
    ? todayBookings.slice(0, 2)
    : todayCount && nextBooking
      ? [nextBooking]
      : [];

  const kpis = [
    {
      href: canReports ? '/admin/reports' : canOrders ? '/admin/orders' : '',
      iconName: 'wallet',
      label: 'Doanh thu tháng',
      value: money(sales?.monthRevenue),
      note: report
        ? report.orderCount
          ? `${number(report.orderCount)} đơn · ${money(report.totalRevenue)} trong 6 tháng`
          : 'Chưa có doanh thu trong 6 tháng'
        : `${number(sales?.orderCount)} đơn hàng đã tạo`,
      tone: 'clay'
    },
    {
      href: canBookings ? '/admin/bookings' : '',
      iconName: 'calendar',
      label: 'Lịch chụp hôm nay',
      value: number(todayCount),
      note: `${number(crm?.upcomingBookingCount)} lịch sắp tới · ${number(crm?.monthBookingCount)} trong tháng`,
      tone: 'olive'
    },
    {
      href: canCustomers ? '/admin/customers' : '',
      iconName: 'users',
      label: 'Khách hàng',
      value: number(crm?.customerCount),
      note: `${number(crm?.activeCustomerCount)} đang hoạt động`,
      tone: 'sand'
    },
    canOrders || canReports
      ? {
          href: canOrders ? '/admin/orders' : '/admin/reports',
          iconName: 'clock',
          label: 'Công nợ cần thu',
          value: money(sales?.outstandingDebt),
          note: `${number(sales?.openOrderCount)} đơn đang mở`,
          tone: 'rose'
        }
      : {
          href: canBookings ? '/admin/bookings' : '',
          iconName: 'clock',
          label: 'Chờ xác nhận',
          value: number(crm?.pendingBookingCount),
          note: 'lịch cần liên hệ với khách',
          tone: 'rose'
        }
  ];

  return `
    <div class="studio-dash">
      <section class="studio-bar" aria-label="Tình hình studio hôm nay">
        <div class="studio-bar__when">
          <span class="studio-bar__date">${icon('calendar')} ${escapeHtml(todayLabel())}</span>
          <span class="studio-bar__clock"><span class="studio-bar__dot" aria-hidden="true"></span><strong data-dash-clock>${escapeHtml(clockNow())}</strong></span>
        </div>
        <div class="studio-bar__tools">
          ${canAttendance && attendanceRecords ? `
            <a class="studio-pill ${attended > 0 ? 'is-ok' : 'is-warn'}" href="/admin/attendance" title="Mở bảng chấm công hôm nay">
              ${icon('clock')}
              <span>${activeStaff ? `<strong>${number(attended)}/${number(activeStaff)}</strong> đã chấm công` : `<strong>${number(attended)}</strong> lượt chấm công`}</span>
            </a>
          ` : ''}
          ${canNotifications && notifications ? `
            <a class="studio-pill studio-pill--bell" href="/admin/notifications" aria-label="Thông báo${unread ? `: ${number(unread)} chưa đọc` : ''}">
              ${icon('bell')}
              <span>Thông báo</span>
              ${unread ? `<span class="studio-pill__badge">${unread > 99 ? '99+' : number(unread)}</span>` : ''}
            </a>
          ` : ''}
          <span class="studio-chip">
            <span class="studio-chip__icon">${icon('home')}</span>
            <span>
              <strong>${escapeHtml(studioName)}</strong>
              <small>Cơ sở chính</small>
            </span>
          </span>
        </div>
      </section>

      <section class="studio-hero" aria-labelledby="studio-hero-title">
        <div class="studio-hero__copy">
          <p class="eyebrow eyebrow--light"><span class="eyebrow__dot"></span>Tổng quan hôm nay</p>
          <h1 id="studio-hero-title">${greeting()}, <em>${escapeHtml(displayName)}</em>.</h1>
          <p class="studio-hero__lead">
            ${todayCount
              ? `Hôm nay có <strong>${number(todayCount)} buổi chụp</strong>${nextBooking ? `, gần nhất lúc <strong>${escapeHtml(nextBooking.startTime || '—')}</strong> với ${escapeHtml(nextBooking.customerName || 'khách lẻ')}` : ''}.`
              : 'Hôm nay chưa có lịch chụp nào. Một khoảng trống để chuẩn bị cho những bộ ảnh mới.'}
          </p>
          <div class="studio-hero__actions">
            ${canBookings ? `<a class="btn btn--cream" href="/admin/bookings">${icon('calendar')} Tạo lịch chụp</a>` : ''}
            ${canOrders ? `<a class="btn btn--ghost-light" href="/admin/orders">${icon('receipt')} Tạo đơn hàng</a>` : ''}
            ${canBoard ? `<a class="btn btn--ghost-light" href="/admin/board">${icon('board')} Bảng công việc</a>` : ''}
          </div>
        </div>
        <aside class="studio-hero__today" aria-label="Lịch chụp hôm nay">
          <header class="studio-hero__todayhead">
            <span>${icon('calendar')} Lịch hôm nay</span>
            <strong>${number(todayCount)}</strong>
          </header>
          ${todayList.length ? `
            <div class="studio-hero__bookings">
              ${todayList.map(booking => {
                const meta = DASH_BOOKING_STATUS[booking.status] || { label: booking.status || 'Lịch chụp', tone: 'neutral' };
                return `
                  <a class="studio-hero__booking" href="${canBookings ? '/admin/bookings' : '/admin'}">
                    <span class="studio-hero__time">${escapeHtml(booking.startTime || '--:--')}</span>
                    <span class="studio-hero__bookingmain">
                      <strong>${escapeHtml(booking.customerName || 'Khách lẻ')}</strong>
                      <small>${escapeHtml(booking.packageName || 'Chưa chọn gói')}${booking.photographerName ? ` · ${escapeHtml(booking.photographerName)}` : ''}</small>
                    </span>
                    <span class="pill pill--${meta.tone}">${escapeHtml(meta.label)}</span>
                  </a>
                `;
              }).join('')}
              ${todayCount > todayList.length ? `<p class="studio-hero__more">+ ${number(todayCount - todayList.length)} buổi chụp khác hôm nay</p>` : ''}
            </div>
          ` : `
            <div class="studio-hero__empty">
              ${icon('calendar')}
              <p>Chưa có lịch chụp nào hôm nay.</p>
              ${canBookings ? '<a href="/admin/bookings">Tạo lịch mới</a>' : ''}
            </div>
          `}
        </aside>
      </section>

      <section class="studio-kpis" aria-label="Số liệu chính">
        ${kpis.map(studioKpi).join('')}
      </section>

      <div class="studio-grid">
        <section class="panel studio-panel studio-chart" aria-labelledby="studio-chart-title">
          <header class="panel__head panel__head--row">
            <div>
              <p class="eyebrow">Doanh thu</p>
              <h2 id="studio-chart-title">6 tháng gần nhất</h2>
            </div>
            <div class="studio-chart__total">
              <span>Tổng 6 tháng</span>
              <strong>${money(chartTotal)}</strong>
            </div>
          </header>
          ${studioRevenueChart(reportMonths)}
          ${canReports ? `<a class="studio-panel__link" href="/admin/reports">Xem báo cáo chi tiết ${icon('arrowRight')}</a>` : ''}
        </section>

        <section class="panel studio-panel studio-upcoming" aria-labelledby="studio-upcoming-title">
          <header class="panel__head panel__head--row">
            <div>
              <p class="eyebrow">Lịch chụp</p>
              <h2 id="studio-upcoming-title">Sắp tới</h2>
            </div>
            ${canBookings ? `<a class="btn btn--text btn--compact" href="/admin/bookings">Tất cả ${icon('arrowRight')}</a>` : ''}
          </header>
          ${upcoming.length ? `
            <div class="studio-bookings">
              ${upcoming.map(booking => studioBookingRow(booking, today, canBookings)).join('')}
            </div>
          ` : `
            <div class="studio-empty studio-empty--compact">
              <span>${icon('calendar')}</span>
              <div>
                <strong>Chưa có lịch sắp tới</strong>
                <p>Tạo lịch chụp để giữ chỗ cho khách hàng.</p>
              </div>
              ${canBookings ? '<a class="btn btn--ghost btn--compact" href="/admin/bookings">Tạo lịch</a>' : ''}
            </div>
          `}
        </section>
      </div>

      ${canOrders ? `
        <section class="panel studio-panel studio-orders" aria-labelledby="studio-orders-title">
          <header class="panel__head panel__head--row">
            <div>
              <p class="eyebrow">Kinh doanh</p>
              <h2 id="studio-orders-title">Đơn hàng gần đây</h2>
            </div>
            <a class="btn btn--text btn--compact" href="/admin/orders">Tất cả đơn hàng ${icon('arrowRight')}</a>
          </header>
          ${recentOrders.length ? `
            <div class="studio-tablewrap">
              <table class="studio-table">
                <thead>
                  <tr>
                    <th scope="col">Mã đơn</th>
                    <th scope="col">Khách hàng</th>
                    <th scope="col">Ngày tạo</th>
                    <th scope="col" class="studio-table__num">Tổng</th>
                    <th scope="col" class="studio-table__num">Còn lại</th>
                    <th scope="col">Thanh toán</th>
                  </tr>
                </thead>
                <tbody>
                  ${recentOrders.map(order => {
                    const meta = DASH_PAYMENT_STATUS[order.paymentStatus] || { label: order.paymentStatus || 'Chưa rõ', tone: 'neutral' };
                    const remaining = Number(order.remaining || 0);
                    return `
                      <tr>
                        <td><a class="studio-table__code" href="/admin/orders">${escapeHtml(order.code)}</a></td>
                        <td>
                          <span class="studio-table__customer">${escapeHtml(order.customerName || 'Khách lẻ')}</span>
                          ${order.bookingCode ? `<small>${escapeHtml(order.bookingCode)}</small>` : ''}
                        </td>
                        <td class="studio-table__muted">${escapeHtml(formatShortTimestamp(order.createdAt) || '—')}</td>
                        <td class="studio-table__num">${money(order.total)}</td>
                        <td class="studio-table__num ${remaining > 0 ? 'is-due' : 'is-clear'}">${remaining > 0 ? money(remaining) : 'Đã thu đủ'}</td>
                        <td><span class="pill pill--${meta.tone}">${escapeHtml(meta.label)}</span></td>
                      </tr>
                    `;
                  }).join('')}
                </tbody>
              </table>
            </div>
          ` : `
            <div class="studio-empty">
              <span>${icon('receipt')}</span>
              <div>
                <strong>Chưa có đơn hàng nào</strong>
                <p>Đơn hàng đầu tiên sẽ xuất hiện ở đây kèm tình trạng thanh toán.</p>
              </div>
              <a class="btn btn--ghost btn--compact" href="/admin/orders">Tạo đơn hàng</a>
            </div>
          `}
        </section>
      ` : ''}
    </div>
  `;
}

function studioKpi({ href, iconName, label, value, note, tone }) {
  const inner = `
    <span class="studio-kpi__icon">${icon(iconName)}</span>
    <span class="studio-kpi__body">
      <span class="studio-kpi__label">${escapeHtml(label)}</span>
      <strong class="studio-kpi__value">${escapeHtml(value)}</strong>
      <small class="studio-kpi__note">${escapeHtml(note)}</small>
    </span>
    ${href ? `<span class="studio-kpi__arrow">${icon('arrowUpRight')}</span>` : ''}
  `;
  return href
    ? `<a class="studio-kpi studio-kpi--${tone}" href="${href}">${inner}</a>`
    : `<div class="studio-kpi studio-kpi--${tone}">${inner}</div>`;
}

function studioBookingRow(booking, today, canBookings) {
  const meta = DASH_BOOKING_STATUS[booking.status] || { label: booking.status || 'Lịch chụp', tone: 'neutral' };
  const date = String(booking.date || '');
  const day = date.length >= 10 ? date.slice(8, 10) : '--';
  const month = date.length >= 7 ? Number(date.slice(5, 7)) : 0;
  const isToday = date === today;
  return `
    <a class="studio-booking ${isToday ? 'is-today' : ''}" href="${canBookings ? '/admin/bookings' : '/admin'}">
      <span class="studio-booking__date">
        <strong>${escapeHtml(day)}</strong>
        <small>${month ? `Thg ${month}` : '—'}</small>
        ${isToday ? '<span>Hôm nay</span>' : ''}
      </span>
      <span class="studio-booking__main">
        <strong>${escapeHtml(booking.customerName || 'Khách lẻ')}</strong>
        <small>${escapeHtml(booking.startTime || '—')} · ${escapeHtml(booking.packageName || 'Chưa chọn gói')}</small>
        <small class="studio-booking__meta">${escapeHtml(booking.code)}${booking.photographerName ? ` · ${escapeHtml(booking.photographerName)}` : ''}</small>
      </span>
      <span class="pill pill--${meta.tone}">${escapeHtml(meta.label)}</span>
    </a>
  `;
}

function studioRevenueChart(months) {
  const items = (Array.isArray(months) ? months : []).slice(-6);
  if (!items.length || items.every(item => !Number(item.total || 0))) {
    return `
      <div class="studio-chart__empty">
        ${icon('chart')}
        <p>Chưa có doanh thu trong 6 tháng gần nhất.</p>
      </div>
    `;
  }

  const width = 640;
  const height = 230;
  const padX = 26;
  const padTop = 24;
  const padBottom = 30;
  const baseY = height - padBottom;
  const max = Math.max(...items.map(item => Number(item.total || 0)), 1);
  const step = items.length > 1 ? (width - padX * 2) / (items.length - 1) : 0;
  const points = items.map((item, index) => {
    const value = Number(item.total || 0);
    return {
      x: padX + step * index,
      y: padTop + (1 - value / max) * (baseY - padTop),
      value,
      month: item.month
    };
  });
  const line = points
    .map((point, index) => `${index === 0 ? 'M' : 'L'} ${point.x.toFixed(1)} ${point.y.toFixed(1)}`)
    .join(' ');
  const area = `${line} L ${points[points.length - 1].x.toFixed(1)} ${baseY} L ${points[0].x.toFixed(1)} ${baseY} Z`;
  const grid = [0, 0.5, 1].map(ratio => {
    const y = padTop + (1 - ratio) * (baseY - padTop);
    return `<line class="studio-chart__grid" x1="${padX}" y1="${y.toFixed(1)}" x2="${width - padX}" y2="${y.toFixed(1)}" />`;
  }).join('');
  const dots = points.map(point => `
    <g class="studio-chart__dot">
      <circle cx="${point.x.toFixed(1)}" cy="${point.y.toFixed(1)}" r="4.5" />
      <title>${escapeHtml(monthTitle(point.month))}: ${escapeHtml(money(point.value))}</title>
    </g>
  `).join('');

  return `
    <div class="studio-chart__canvas">
      <svg viewBox="0 0 ${width} ${height}" role="img" aria-label="Doanh thu 6 tháng gần nhất">
        <defs>
          <linearGradient id="studioRevenueArea" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stop-color="#b25f46" stop-opacity="0.34" />
            <stop offset="100%" stop-color="#b25f46" stop-opacity="0.02" />
          </linearGradient>
        </defs>
        ${grid}
        <path class="studio-chart__area" d="${area}" fill="url(#studioRevenueArea)" />
        <path class="studio-chart__line" d="${line}" />
        ${dots}
      </svg>
      <span class="studio-chart__max">${escapeHtml(money(max))}</span>
      <span class="studio-chart__zero">0 đ</span>
    </div>
    <div class="studio-chart__months">
      ${items.map((item, index) => `<span class="${index === items.length - 1 ? 'is-current' : ''}">${escapeHtml(monthShort(item.month))}</span>`).join('')}
    </div>
  `;
}

function monthShort(key) {
  const month = Number(String(key || '').slice(5, 7));
  return month ? `Thg ${month}` : '—';
}

function monthTitle(key) {
  const month = Number(String(key || '').slice(5, 7));
  const year = String(key || '').slice(0, 4);
  return month ? `Tháng ${month}/${year}` : 'Doanh thu';
}

function albumIntro(studioName, session) {
  return `
    <header class="admin-intro">
      <div>
        <p class="eyebrow"><span class="eyebrow__dot"></span>${escapeHtml(todayLabel())}</p>
        <h1>${greeting()}, <em>${escapeHtml(studioName)}</em>.</h1>
        <p class="admin-intro__lead">${hasPermission(session, 'albums')
          ? 'Một chút tổng quan để bạn biết album nào đang được khách hàng mở xem.'
          : 'Tổng quan công việc và số liệu bạn được phép theo dõi trong studio.'}</p>
      </div>
      <div class="admin-intro__actions">
        ${hasPermission(session, 'albums') ? `
          <a class="btn btn--ghost" href="/admin/albums">${icon('albums')} Tất cả album</a>
          <a class="btn btn--primary" href="/admin/create">${icon('plus')} Tạo album</a>
        ` : ''}
      </div>
    </header>
  `;
}

function renderAlbumDashboard(albums, studioName, board = null, crm = null, sales = null, operations = null, session = null) {
  const canAlbums = hasPermission(session, 'albums');
  const canBoard = hasPermission(session, 'board');
  const canTools = hasPermission(session, 'tools');
  const albumList = Array.isArray(albums) ? albums : [];
  const totalPhotos = albumList.reduce((sum, album) => sum + album.photoCount, 0);
  const totalSelected = albumList.reduce((sum, album) => sum + album.selectedCount, 0);
  const submitted = albumList.filter(album => album.submittedAt).length;
  const published = albumList.filter(album => album.isPublished).length;
  const featured = [...albumList]
    .filter(album => album.photoCount > 0)
    .sort((a, b) => new Date(b.createdAt) - new Date(a.createdAt))[0] || null;
  const recent = albumList.slice(0, 4);
  const events = buildActivity(albumList);

  const albumBlocks = canAlbums ? `
    ${featured ? featuredHero(featured) : emptyHero()}

    <section class="stat-grid" aria-label="Số liệu studio">
      ${statCard({
        iconName: 'albums',
        label: 'Album đang hoạt động',
        value: number(published),
        note: `${number(albumList.length)} album · ${number(albumList.length - published)} bản nháp`,
        tone: 'clay'
      })}
      ${statCard({
        iconName: 'image',
        label: 'Tổng số ảnh',
        value: number(totalPhotos),
        note: albumList.length ? `Trung bình ${pluralPhotos(Math.round(totalPhotos / Math.max(albumList.length, 1)))} mỗi album` : 'Chưa có ảnh nào',
        tone: 'olive'
      })}
      ${statCard({
        iconName: 'heart',
        label: 'Lựa chọn của khách',
        value: number(totalSelected),
        note: totalPhotos ? `${percent(totalSelected, totalPhotos)}% tổng số ảnh` : 'Đang chờ những lượt thả tim',
        tone: 'rose'
      })}
      ${statCard({
        iconName: 'send',
        label: 'Đã gửi lựa chọn',
        value: number(submitted),
        note: albumList.length ? `${percent(submitted, albumList.length)}% album đã nhận phản hồi` : 'Chưa có phản hồi nào',
        tone: 'sand'
      })}
    </section>
  ` : '';

  const recentBlock = canAlbums ? `
    <div class="dash-columns">
      <section class="panel dash-recent">
        <header class="panel__head panel__head--row">
          <div>
            <p class="eyebrow">Nhịp làm việc</p>
            <h2>Album gần đây</h2>
          </div>
          <a class="btn btn--text btn--compact" href="/admin/albums">Xem tất cả ${icon('arrowRight')}</a>
        </header>
        ${recent.length ? `
          <div class="recent-list">
            ${recent.map(recentAlbumRow).join('')}
          </div>
        ` : emptyState({
          iconName: 'albums',
          title: 'Chưa có album nào',
          message: 'Tạo album đầu tiên từ một thư mục Google Drive được chia sẻ công khai.',
          action: '<a class="btn btn--primary" href="/admin/create">Tạo album đầu tiên</a>'
        })}
      </section>

      <aside class="dash-side">
        <section class="panel activity-card">
          <header class="panel__head">
            <p class="eyebrow">Cập nhật</p>
            <h2>Hoạt động</h2>
          </header>
          ${events.length ? `
            <ol class="timeline">
              ${events.map(event => `
                <li class="timeline__item">
                  <span class="timeline__icon timeline__icon--${event.tone}">${icon(event.iconName)}</span>
                  <div>
                    <a href="/admin/albums/${encodeURIComponent(event.album.id)}">${escapeHtml(event.album.title)}</a>
                    <p>${event.label}</p>
                    <span>${escapeHtml(formatShortTimestamp(event.at))}</span>
                  </div>
                </li>
              `).join('')}
            </ol>
          ` : '<p class="panel__muted">Hoạt động mới sẽ xuất hiện ở đây.</p>'}
        </section>

        <section class="dash-note">
          <span class="dash-note__icon">${icon('sparkles')}</span>
          <p class="eyebrow">Một lời nhắc nhỏ</p>
          <h3>Gửi album đúng lúc,<br>nhận yêu thương đúng lúc.</h3>
          <p>Một tin nhắn ngắn kèm liên kết riêng luôn khiến khách hàng mở album sớm hơn.</p>
          ${featured && featured.isPublished ? `
            <button class="btn btn--cream btn--compact" type="button" data-copy-token="${escapeAttr(featured.shareToken)}">
              ${icon('link')} Sao chép liên kết mới nhất
            </button>
          ` : '<a class="btn btn--cream btn--compact" href="/admin/albums">Xem album</a>'}
        </section>
      </aside>
    </div>
  ` : '';

  return `
    ${albumIntro(studioName, session)}

    ${albumBlocks}

    ${crmOverview(crm, session)}

    ${salesOverview(sales, session)}

    ${operationsOverview(operations, session)}

    ${canBoard ? boardCta(board) : ''}

    ${canTools ? filterToolCta() : ''}

    ${recentBlock}
  `;
}

function featuredHero(album) {
  const url = shareUrl(album.shareToken);
  return `
    <section class="dash-hero">
      <div class="dash-hero__copy">
        <p class="eyebrow eyebrow--light"><span class="eyebrow__dot"></span>Album nổi bật</p>
        <h2>${escapeHtml(album.title)}</h2>
        <p class="dash-hero__client">${escapeHtml(album.clientName || 'Khách hàng')}${album.eventDate ? ` · ${escapeHtml(formatDate(album.eventDate, true))}` : ''}${album.location ? ` · ${escapeHtml(album.location)}` : ''}</p>
        <div class="dash-hero__metrics">
          <span><strong>${number(album.photoCount)}</strong> ảnh</span>
          <i></i>
          <span><strong>${number(album.selectedCount)}</strong> được chọn</span>
          <i></i>
          <span><strong>${percent(album.selectedCount, album.photoCount)}%</strong> yêu thích</span>
        </div>
        <div class="dash-hero__actions">
          <a class="btn btn--cream" href="/admin/albums/${encodeURIComponent(album.id)}">Mở album ${icon('arrowRight')}</a>
          <button class="btn btn--ghost-light" type="button" data-copy-token="${escapeAttr(album.shareToken)}">${icon('copy')} Liên kết chia sẻ</button>
        </div>
        ${!album.isPublished ? '<p class="dash-hero__warning">Album này đang ở chế độ bản nháp, khách chưa thể mở.</p>' : ''}
      </div>
      <div class="dash-hero__visual" aria-hidden="true">
        <div class="dash-stack">
          <div class="dash-stack__back"></div>
          <div class="dash-stack__mid"></div>
          <a class="dash-stack__photo" href="/admin/albums/${encodeURIComponent(album.id)}" tabindex="-1">
            ${coverMarkup(album, 'dash-stack__img')}
            <span class="dash-stack__label">${icon('heart')} ${number(album.selectedCount)} lựa chọn</span>
          </a>
        </div>
        <div class="dash-hero__stamp">
          <span>${formatDate(album.eventDate) || 'Mới tạo'}</span>
          <strong>${escapeHtml(album.location || 'Mây Studio')}</strong>
        </div>
      </div>
    </section>
  `;
}

function emptyHero() {
  return `
    <section class="dash-hero dash-hero--empty">
      <div class="dash-hero__copy">
        <p class="eyebrow eyebrow--light"><span class="eyebrow__dot"></span>Bắt đầu thật nhẹ</p>
        <h2>Album đầu tiên<br>đang chờ bạn.</h2>
        <p class="dash-hero__client">Dán một liên kết Google Drive, Mây sẽ đọc ảnh và tạo album trong vài giây.</p>
        <div class="dash-hero__actions">
          <a class="btn btn--cream" href="/admin/create">Tạo album đầu tiên ${icon('arrowRight')}</a>
          <a class="btn btn--ghost-light" href="/admin/settings">${icon('settings')} Kết nối Drive</a>
        </div>
      </div>
      <div class="dash-hero__visual" aria-hidden="true">
        <div class="dash-empty-art">
          <span class="dash-empty-art__frame dash-empty-art__frame--one">${icon('image')}</span>
          <span class="dash-empty-art__frame dash-empty-art__frame--two">${icon('heart')}</span>
          <span class="dash-empty-art__frame dash-empty-art__frame--three">${icon('sparkles')}</span>
        </div>
      </div>
    </section>
  `;
}

function statCard({ iconName, label, value, note, tone }) {
  return `
    <article class="stat-card stat-card--${tone}">
      <div class="stat-card__top">
        <span class="stat-card__icon">${icon(iconName)}</span>
        <span class="stat-card__label">${label}</span>
      </div>
      <strong class="stat-card__value">${value}</strong>
      <p class="stat-card__note">${note}</p>
    </article>
  `;
}

function filterToolCta() {
  return `
    <section class="panel filter-cta" aria-label="Công cụ lọc ảnh theo mã">
      <div class="filter-cta__icon">${icon('filter')}</div>
      <div class="filter-cta__copy">
        <p class="eyebrow">Công cụ studio</p>
        <h2>Lọc ảnh khách chọn ngay trên máy tính.</h2>
        <p>Sao chép mã hình khách đã thả tim, rồi lọc đúng những tệp RAW/JPG trong thư mục ổ cứng. Ảnh được xử lý tại máy, không tải lên máy chủ.</p>
        <div class="filter-cta__chips" aria-hidden="true">
          <span>${icon('heart')} Lấy mã đã chọn</span>
          <span>${icon('copy')} Sao chép nhanh</span>
          <span>${icon('folder')} Lọc thư mục</span>
        </div>
      </div>
      <div class="filter-cta__actions">
        <a class="btn btn--primary" href="/admin/tools">${icon('filter')} Mở công cụ lọc ảnh</a>
        <span>${icon('lock')} Xử lý cục bộ trên trình duyệt</span>
      </div>
    </section>
  `;
}

function crmOverview(crm, session) {
  if (!crm) return '';
  const canCustomers = hasPermission(session, 'customers');
  const canBookings = hasPermission(session, 'bookings');
  const next = Array.isArray(crm.nextBookings) ? crm.nextBookings.slice(0, 3) : [];
  const labels = {
    pending: 'Chờ xác nhận',
    confirmed: 'Đã xác nhận',
    shooting: 'Đang chụp',
    editing: 'Đang chỉnh sửa',
    delivered: 'Đã giao',
    cancelled: 'Đã huỷ'
  };
  const stats = [
    { href: canCustomers ? '/admin/customers' : '', iconName: 'users', label: 'Khách hàng', value: number(crm.customerCount), note: `${number(crm.activeCustomerCount)} đang hoạt động`, tone: 'clay' },
    { href: canBookings ? '/admin/bookings' : '', iconName: 'calendar', label: 'Lịch sắp tới', value: number(crm.upcomingBookingCount), note: `${number(crm.monthBookingCount)} lịch trong tháng`, tone: 'olive' },
    { href: canBookings ? '/admin/bookings' : '', iconName: 'clock', label: 'Chờ xác nhận', value: number(crm.pendingBookingCount), note: 'cần liên hệ với khách', tone: 'sand' }
  ];

  return `
    <section class="panel crm-dashboard" aria-label="Khách hàng và lịch chụp">
      <header class="panel__head panel__head--row">
        <div>
          <p class="eyebrow">Khách hàng &amp; lịch chụp</p>
          <h2>Nhịp chăm sóc khách</h2>
        </div>
        <div class="crm-dashboard__links">
          ${canBookings ? `<a class="btn btn--text btn--compact" href="/admin/bookings">Lịch chụp ${icon('arrowRight')}</a>` : ''}
          ${canCustomers ? `<a class="btn btn--text btn--compact" href="/admin/customers">Khách hàng ${icon('arrowRight')}</a>` : ''}
        </div>
      </header>
      <div class="crm-dashboard__grid">
        ${stats.map(item => {
          const inner = `
            <span class="crm-dashboard__icon crm-dashboard__icon--${item.tone}">${icon(item.iconName)}</span>
            <div>
              <span class="crm-dashboard__label">${item.label}</span>
              <strong>${item.value}</strong>
              <small>${item.note}</small>
            </div>
          `;
          return item.href
            ? `<a class="crm-dashboard__stat" href="${item.href}">${inner}</a>`
            : `<div class="crm-dashboard__stat">${inner}</div>`;
        }).join('')}
      </div>
      ${next.length ? `
        <div class="crm-dashboard__next">
          <p class="crm-dashboard__next-title">${icon('calendar')} Lịch chụp sắp tới</p>
          <div class="crm-dashboard__bookings">
            ${next.map(booking => `
              <a class="crm-dashboard__booking" href="${canBookings ? '/admin/bookings' : '/admin'}">
                <span class="crm-dashboard__date">
                  <strong>${escapeHtml((booking.date || '').slice(8, 10) || '--')}</strong>
                  <small>Tháng ${escapeHtml(String(Number((booking.date || '').slice(5, 7)) || ''))}</small>
                </span>
                <div>
                  <strong>${escapeHtml(booking.customerName || 'Khách lẻ')}</strong>
                  <small>${escapeHtml(booking.code)} · ${escapeHtml(formatDate(booking.date, true))} · ${escapeHtml(booking.startTime || '')}</small>
                </div>
                <span class="pill pill--neutral">${escapeHtml(labels[booking.status] || booking.status)}</span>
              </a>
            `).join('')}
          </div>
        </div>
      ` : `
        <p class="crm-dashboard__empty">${icon('calendar')} Chưa có lịch chụp sắp tới.${canBookings ? ' <a href="/admin/bookings">Tạo lịch đầu tiên</a>' : ''}</p>
      `}
    </section>
  `;
}

function salesOverview(sales, session) {
  if (!sales) return '';
  const canOrders = hasPermission(session, 'orders');
  const canContracts = hasPermission(session, 'contracts');
  const canReports = hasPermission(session, 'reports');
  const stats = [
    {
      href: canReports ? '/admin/reports' : canOrders ? '/admin/orders' : '',
      iconName: 'chart',
      label: 'Doanh thu tháng',
      value: money(sales.monthRevenue),
      note: 'Đơn hàng + hợp đồng đã thu',
      tone: 'clay'
    },
    {
      href: canOrders ? '/admin/orders' : '',
      iconName: 'clock',
      label: 'Công nợ',
      value: money(sales.outstandingDebt),
      note: `${number(sales.openOrderCount)} đơn đang mở`,
      tone: 'sand'
    },
    {
      href: canContracts ? '/admin/contracts' : '',
      iconName: 'file',
      label: 'Hợp đồng chờ ký',
      value: number(sales.pendingContractCount),
      note: `${money(sales.contractOutstanding)} còn lại`,
      tone: 'olive'
    }
  ];

  return `
    <section class="panel crm-dashboard" aria-label="Kinh doanh">
      <header class="panel__head panel__head--row">
        <div>
          <p class="eyebrow">Kinh doanh</p>
          <h2>Doanh thu &amp; công nợ</h2>
        </div>
        <div class="crm-dashboard__links">
          ${canOrders ? `<a class="btn btn--text btn--compact" href="/admin/orders">Đơn hàng ${icon('arrowRight')}</a>` : ''}
          ${canReports ? `<a class="btn btn--text btn--compact" href="/admin/reports">Báo cáo ${icon('arrowRight')}</a>` : ''}
        </div>
      </header>
      <div class="crm-dashboard__grid">
        ${stats.map(item => {
          const inner = `
            <span class="crm-dashboard__icon crm-dashboard__icon--${item.tone}">${icon(item.iconName)}</span>
            <div>
              <span class="crm-dashboard__label">${item.label}</span>
              <strong>${item.value}</strong>
              <small>${item.note}</small>
            </div>
          `;
          return item.href
            ? `<a class="crm-dashboard__stat" href="${item.href}">${inner}</a>`
            : `<div class="crm-dashboard__stat">${inner}</div>`;
        }).join('')}
      </div>
    </section>
  `;
}

function operationsOverview(operations, session) {
  if (!operations) return '';
  const finance = operations.finance;
  const notifications = Array.isArray(operations.notifications) ? operations.notifications : null;
  const outfits = Array.isArray(operations.outfits) ? operations.outfits : null;
  if (!finance && !notifications && !outfits) return '';

  const stats = [];
  if (finance) {
    const unpaidPayroll = Math.max(0, Number(finance.payrollTotal || 0) - Number(finance.payrollPaid || 0));
    stats.push({
      href: '/admin/finance',
      iconName: 'wallet',
      label: 'Lợi nhuận tháng',
      value: money(finance.profit),
      note: `${money(finance.totalRevenue)} doanh thu · ${money(finance.expenseTotal)} chi phí`,
      tone: 'clay'
    });
    stats.push({
      href: '/admin/finance',
      iconName: 'users',
      label: 'Lương chưa chi',
      value: money(unpaidPayroll),
      note: finance.unpaidPayrollCount ? `${number(finance.unpaidPayrollCount)} nhân sự cần chi` : 'Đã chi đủ trong tháng',
      tone: 'sand'
    });
  }
  if (notifications) {
    stats.push({
      href: '/admin/notifications',
      iconName: 'bell',
      label: 'Thông báo chưa đọc',
      value: number(notifications.filter(item => !item.isRead).length),
      note: `${number(notifications.length)} thông báo trong hộp thư`,
      tone: 'rose'
    });
  }
  if (outfits) {
    const loaned = outfits.reduce((sum, outfit) => sum + Number(outfit.loanedQuantity || 0), 0);
    stats.push({
      href: '/admin/wardrobe',
      iconName: 'tag',
      label: 'Trang phục đang thuê',
      value: number(loaned),
      note: `${number(outfits.length)} mẫu trong tủ`,
      tone: 'olive'
    });
  }

  const canAttendance = hasPermission(session, 'attendance');
  const canWardrobe = hasPermission(session, 'wardrobe');

  return `
    <section class="panel crm-dashboard" aria-label="Vận hành studio">
      <header class="panel__head panel__head--row">
        <div>
          <p class="eyebrow">Vận hành studio</p>
          <h2>Tài chính &amp; hậu cần</h2>
        </div>
        <div class="crm-dashboard__links">
          ${canAttendance ? `<a class="btn btn--text btn--compact" href="/admin/attendance">Chấm công ${icon('arrowRight')}</a>` : ''}
          ${canWardrobe ? `<a class="btn btn--text btn--compact" href="/admin/wardrobe">Tủ đồ ${icon('arrowRight')}</a>` : ''}
        </div>
      </header>
      <div class="crm-dashboard__grid">
        ${stats.map(item => `
          <a class="crm-dashboard__stat" href="${item.href}">
            <span class="crm-dashboard__icon crm-dashboard__icon--${item.tone}">${icon(item.iconName)}</span>
            <div>
              <span class="crm-dashboard__label">${item.label}</span>
              <strong>${item.value}</strong>
              <small>${item.note}</small>
            </div>
          </a>
        `).join('')}
      </div>
    </section>
  `;
}

function boardCta(board) {
  if (!board) {
    return `
      <section class="panel board-cta" aria-label="Bảng công việc">
        <div class="board-cta__icon">${icon('board')}</div>
        <div class="board-cta__copy">
          <p class="eyebrow">Bảng công việc</p>
          <h2>Giữ nhịp studio trên một bảng duy nhất.</h2>
          <p>Sắp xếp các bước chuẩn bị, chụp, hậu kỳ và giao ảnh. Kéo thẻ giữa các danh sách để cập nhật tiến độ.</p>
        </div>
        <div class="board-cta__actions">
          <a class="btn btn--primary" href="/admin/board">${icon('board')} Mở bảng công việc</a>
        </div>
      </section>
    `;
  }

  const cards = board.lists.flatMap(list => list.cards);
  const checklistTotal = cards.reduce((sum, card) => sum + (card.checklist?.length || 0), 0);
  const checklistDone = cards.reduce(
    (sum, card) => sum + (card.checklist || []).filter(item => item.done).length,
    0
  );
  const checklistPercent = checklistTotal ? Math.round((checklistDone / checklistTotal) * 100) : 0;

  return `
    <section class="panel board-cta" aria-label="Bảng công việc">
      <div class="board-cta__icon">${icon('board')}</div>
      <div class="board-cta__copy">
        <p class="eyebrow">Bảng công việc</p>
        <h2>${escapeHtml(board.title)}</h2>
        <p>${escapeHtml(board.description || 'Theo dõi từng album từ lúc nhận yêu cầu đến khi trao ảnh cho khách.')}</p>
        <div class="board-cta__chips" aria-hidden="true">
          <span>${icon('board')} ${board.lists.length} danh sách</span>
          <span>${icon('credit')} ${cards.length} thẻ</span>
          <span>${icon('listChecks')} ${checklistDone}/${checklistTotal} việc đã xong</span>
        </div>
      </div>
      <div class="board-cta__actions">
        <a class="btn btn--primary" href="/admin/board">${icon('board')} Mở bảng</a>
        <span>${icon('checkCircle')} ${checklistPercent}% checklist hoàn tất</span>
      </div>
    </section>
  `;
}

function recentAlbumRow(album) {
  return `
    <article class="recent-album">
      <a class="recent-album__cover" href="/admin/albums/${encodeURIComponent(album.id)}" tabindex="-1">
        ${coverMarkup(album, '', album.title)}
      </a>
      <div class="recent-album__main">
        <div class="recent-album__titleline">
          <h3><a href="/admin/albums/${encodeURIComponent(album.id)}">${escapeHtml(album.title)}</a></h3>
          ${album.submittedAt ? `<span class="pill pill--submitted">${icon('checkCircle')}Đã gửi</span>` : ''}
          ${!album.isPublished ? `<span class="pill pill--draft">Bản nháp</span>` : ''}
        </div>
        <p class="recent-album__meta">${escapeHtml(album.clientName || 'Chưa có tên khách')}${album.eventDate ? ` · ${escapeHtml(formatDate(album.eventDate))}` : ''}${album.location ? ` · ${escapeHtml(album.location)}` : ''}</p>
        <div class="recent-album__progress">
          ${selectionMeter(album.selectedCount, album.photoCount)}
          <span>${number(album.selectedCount)}/${number(album.photoCount)} ảnh được chọn</span>
        </div>
      </div>
      <div class="recent-album__actions">
        <button class="icon-btn" type="button" data-copy-token="${escapeAttr(album.shareToken)}" aria-label="Sao chép liên kết ${escapeAttr(album.title)}" title="Sao chép liên kết">${icon('link')}</button>
        <a class="icon-btn" href="/admin/albums/${encodeURIComponent(album.id)}" aria-label="Mở album ${escapeAttr(album.title)}" title="Mở album">${icon('chevronRight')}</a>
      </div>
    </article>
  `;
}

function buildActivity(albums) {
  const events = [];
  for (const album of albums) {
    if (album.submittedAt) {
      events.push({
        album,
        at: album.submittedAt,
        label: `${selectionLabel(album.selectedCount)} · khách đã gửi`,
        iconName: 'send',
        tone: 'rose'
      });
    }
    events.push({
      album,
      at: album.createdAt,
      label: album.isDemo ? 'Album mẫu được khởi tạo' : 'Album được tạo từ Google Drive',
      iconName: album.isDemo ? 'sparkles' : 'plus',
      tone: 'olive'
    });
  }

  return events
    .filter(event => event.at)
    .sort((a, b) => new Date(b.at) - new Date(a.at))
    .slice(0, 5);
}

// ---------------------------------------------------------------- albums

const ALBUM_FILTERS = [
  { key: 'all', label: 'Tất cả' },
  { key: 'live', label: 'Đang chia sẻ' },
  { key: 'draft', label: 'Bản nháp' },
  { key: 'submitted', label: 'Đã nhận lựa chọn' }
];

export function albumsView({ session }) {
  const skeleton = `
    <div class="album-grid album-grid--skeleton">
      ${Array.from({ length: 6 }, () => `
        <div class="skeleton-card">
          <div class="skeleton skeleton-media"></div>
          <div class="skeleton skeleton-line skeleton-line--lg"></div>
          <div class="skeleton skeleton-line skeleton-line--sm"></div>
          <div class="skeleton skeleton-line skeleton-line--xs"></div>
        </div>
      `).join('')}
    </div>
  `;

  const html = adminShell({
    active: 'albums',
    session,
    content: `
      <div class="admin-page__inner" data-albums-page>
        ${pageHead({
          eyebrow: 'Thư viện studio',
          title: 'Album của bạn',
          lead: 'Mỗi album là một câu chuyện riêng. Tìm nhanh theo tên, khách hàng hoặc mã chia sẻ.',
          actions: `<a class="btn btn--primary" href="/admin/create">${icon('plus')} Tạo album mới</a>`
        })}

        <section class="albums-toolbar" aria-label="Bộ lọc album">
          <div class="searchbox searchbox--grow">
            <span class="searchbox__icon">${icon('search')}</span>
            <input type="search" data-album-search placeholder="Tìm theo tên album, khách hàng, địa điểm…" autocomplete="off" aria-label="Tìm album">
            <button class="searchbox__clear" type="button" data-search-clear aria-label="Xoá tìm kiếm" hidden>${icon('x')}</button>
          </div>
          <div class="segmented" role="tablist" aria-label="Lọc album">
            ${ALBUM_FILTERS.map((filter, index) => `
              <button class="segmented__item ${index === 0 ? 'is-active' : ''}" type="button" role="tab" aria-selected="${index === 0}" data-filter="${filter.key}">
                ${filter.label}<span class="segmented__count" data-filter-count="${filter.key}">0</span>
              </button>
            `).join('')}
          </div>
        </section>

        <div class="albums-results-head">
          <p data-results-summary>Đang tải album…</p>
          <span class="albums-results-head__hint">${icon('lock')} Liên kết chỉ chia sẻ cho khách của bạn</span>
        </div>

        <div data-albums-content>${skeleton}</div>
      </div>
    `
  });

  let teardown = () => {};
  let disposed = false;
  let albums = [];
  let filter = 'all';
  let query = '';
  let loaded = false;

  return {
    title: 'Album của bạn',
    bodyClass: 'page-admin',
    html,
    mount(root) {
      teardown = mountAdminShell(root, session);
      const page = root.querySelector('[data-albums-page]');
      const holder = root.querySelector('[data-albums-content]');
      const search = root.querySelector('[data-album-search]');
      const clear = root.querySelector('[data-search-clear]');
      const summary = root.querySelector('[data-results-summary]');

      const isVisible = album => {
        if (filter === 'live' && !album.isPublished) return false;
        if (filter === 'draft' && album.isPublished) return false;
        if (filter === 'submitted' && !album.submittedAt) return false;
        if (!query) return true;
        const haystack = stripDiacritics([
          album.title,
          album.clientName,
          album.location,
          album.shareToken,
          album.eventDate
        ].join(' '));
        return haystack.includes(query);
      };

      const updateCounts = () => {
        const counts = {
          all: albums.length,
          live: albums.filter(item => item.isPublished).length,
          draft: albums.filter(item => !item.isPublished).length,
          submitted: albums.filter(item => item.submittedAt).length
        };
        root.querySelectorAll('[data-filter-count]').forEach(item => {
          item.textContent = number(counts[item.dataset.filterCount] || 0);
        });
      };

      const renderFiltered = () => {
        const visible = albums.filter(isVisible);
        root.querySelectorAll('[data-filter]').forEach(button => {
          const active = button.dataset.filter === filter;
          button.classList.toggle('is-active', active);
          button.setAttribute('aria-selected', String(active));
        });
        clear.hidden = !query;

        if (!visible.length) {
          holder.innerHTML = albums.length
            ? `<div class="panel panel--pad">${emptyState({
                iconName: 'search',
                title: 'Không tìm thấy album phù hợp',
                message: query
                  ? `Không có album nào khớp với “${query}”. Thử một từ khoá khác nhé.`
                  : 'Chưa có album nào trong mục này.',
                action: '<button class="btn btn--ghost" type="button" data-reset-filters>Xoá bộ lọc</button>'
              })}</div>`
            : `<div class="panel panel--pad">${emptyState({
                iconName: 'albums',
                title: 'Thư viện đang chờ album đầu tiên',
                message: 'Tạo album từ một thư mục Google Drive được chia sẻ công khai, sau đó gửi liên kết riêng cho khách.',
                action: '<a class="btn btn--primary" href="/admin/create">Tạo album đầu tiên</a>'
              })}</div>`;
        } else {
          holder.innerHTML = `<div class="album-grid">${visible.map(albumCard).join('')}</div>`;
        }

        const label = visible.length === albums.length
          ? `${number(albums.length)} album`
          : `${number(visible.length)} trên ${number(albums.length)} album`;
        summary.innerHTML = `<strong>${label}</strong>${query ? ` cho “${escapeHtml(query)}”` : ''}`;
      };

      const load = async () => {
        if (disposed || loaded) return;
        try {
          albums = await api('/api/admin/albums');
          if (disposed) return;
          loaded = true;
          updateCounts();
          renderFiltered();
        } catch (error) {
          if (disposed || error.name === 'AbortError') return;
          summary.textContent = 'Không tải được album';
          holder.innerHTML = `
            <div class="panel panel--pad panel--error">
              ${errorInline(error.message || 'Không tải được danh sách album.', error.code)}
              <button class="btn btn--ghost" type="button" data-retry>${icon('refresh')} Thử lại</button>
            </div>
          `;
        }
      };

      page.addEventListener('click', event => {
        const filterButton = event.target.closest('[data-filter]');
        if (filterButton) {
          filter = filterButton.dataset.filter;
          renderFiltered();
          return;
        }

        if (event.target.closest('[data-search-clear]')) {
          query = '';
          search.value = '';
          renderFiltered();
          search.focus();
          return;
        }

        if (event.target.closest('[data-reset-filters]')) {
          filter = 'all';
          query = '';
          search.value = '';
          renderFiltered();
          return;
        }

        if (event.target.closest('[data-retry]')) {
          load();
          return;
        }

        const copyButton = event.target.closest('[data-copy-token]');
        if (copyButton) {
          event.preventDefault();
          copyShareLink(copyButton, copyButton.dataset.copyToken);
        }
      });

      search.addEventListener('input', () => {
        query = stripDiacritics(search.value.trim());
        renderFiltered();
      });

      load();
    },
    cleanup() {
      disposed = true;
      teardown();
    }
  };
}

function albumCard(album) {
  const progress = percent(album.selectedCount, album.photoCount);
  return `
    <article class="album-card ${album.isPublished ? '' : 'is-draft'}">
      <a class="album-card__media" href="/admin/albums/${encodeURIComponent(album.id)}" aria-label="Mở album ${escapeAttr(album.title)}">
        ${coverMarkup(album, 'album-card__img', album.title)}
        <div class="album-card__badges">
          ${statusPill(album)}
          ${album.submittedAt ? `<span class="pill pill--submitted">${icon('checkCircle')}Đã gửi</span>` : ''}
        </div>
        <span class="album-card__open">${icon('arrowUpRight')}</span>
      </a>
      <div class="album-card__body">
        <p class="album-card__meta">
          ${album.eventDate ? `<span>${icon('calendar')} ${escapeHtml(formatDate(album.eventDate))}</span>` : ''}
          ${album.location ? `<span>${icon('mapPin')} ${escapeHtml(album.location)}</span>` : ''}
          ${!album.eventDate && !album.location ? `<span>${icon('calendar')} Tạo ${escapeHtml(formatShortTimestamp(album.createdAt))}</span>` : ''}
        </p>
        <h3 class="album-card__title"><a href="/admin/albums/${encodeURIComponent(album.id)}">${escapeHtml(album.title)}</a></h3>
        <p class="album-card__client">${icon('users')} ${escapeHtml(album.clientName || 'Chưa có tên khách hàng')}</p>
        <div class="album-card__selection">
          <div class="album-card__selection-top">
            <span>${album.selectedCount ? `${number(album.selectedCount)} ảnh được chọn` : 'Chưa có lựa chọn'}</span>
            <strong>${progress}%</strong>
          </div>
          ${selectionMeter(album.selectedCount, album.photoCount)}
          <span class="album-card__count">${number(album.photoCount)} ảnh trong album</span>
        </div>
        <div class="album-card__foot">
          <span class="album-card__updated">Tạo ${escapeHtml(formatShortTimestamp(album.createdAt))}</span>
          <div class="album-card__actions">
            <button class="icon-btn" type="button" data-copy-token="${escapeAttr(album.shareToken)}" aria-label="Sao chép liên kết ${escapeAttr(album.title)}" title="Sao chép liên kết">${icon('link')}</button>
            <a class="icon-btn" href="/a/${encodeURIComponent(album.shareToken)}" target="_blank" rel="noopener" aria-label="Xem album ${escapeAttr(album.title)}" title="Xem như khách hàng">${icon('eye')}</a>
          </div>
        </div>
      </div>
    </article>
  `;
}

// ---------------------------------------------------------------- form pieces

function toggleRow({ name, title, description, checked = false, disabled = false, iconName = 'check', dataAttr = '', tone = '' }) {
  return `
    <label class="switch-row ${tone ? `switch-row--${tone}` : ''} ${disabled ? 'is-disabled' : ''}">
      <span class="switch-row__icon">${icon(iconName)}</span>
      <span class="switch-row__copy">
        <strong>${escapeHtml(title)}</strong>
        <span>${escapeHtml(description)}</span>
      </span>
      <span class="switch">
        <input type="checkbox" name="${escapeAttr(name)}" ${checked ? 'checked' : ''} ${disabled ? 'disabled' : ''} ${dataAttr}>
        <span class="switch__track" aria-hidden="true"><span class="switch__thumb"></span></span>
      </span>
    </label>
  `;
}

// ---------------------------------------------------------------- create album

export function createView({ session }) {
  const html = adminShell({
    active: 'create',
    session,
    content: `
      <div class="admin-page__inner admin-page__inner--form" data-create-page>
        ${pageHead({
          eyebrow: 'Album mới',
          title: 'Tạo album từ Drive',
          lead: 'Chọn thư mục, đặt tên và quyết định cách khách hàng tương tác với album.',
          back: { href: '/admin/albums', label: 'Album của bạn' },
          actions: `<a class="btn btn--ghost" href="/admin/albums">${icon('x')} Huỷ</a>`
        })}

        <form class="create-layout" id="create-album-form" novalidate>
          <div class="create-form">
            <section class="panel form-section">
              <header class="panel__head panel__head--icon">
                <span class="panel__icon">${icon('edit')}</span>
                <div>
                  <p class="eyebrow">Bước 1</p>
                  <h2>Thông tin album</h2>
                  <p>Những dòng này sẽ xuất hiện ở phần mở đầu album của khách.</p>
                </div>
              </header>
              <div class="form-grid">
                <div class="field field--full">
                  <label for="album-title">Tên album <span class="required">*</span></label>
                  <input id="album-title" name="title" type="text" maxlength="100" placeholder="Ví dụ: Ngày Mình Chung Đôi" autocomplete="off" required>
                  <p class="field__hint">Một cái tên dễ nhớ sẽ giúp khách tìm lại album sau này.</p>
                </div>
                <div class="field">
                  <label for="album-client">Tên khách hàng</label>
                  <input id="album-client" name="clientName" type="text" maxlength="80" placeholder="Linh & Minh" autocomplete="off">
                </div>
                <div class="field">
                  <label for="album-date">Ngày chụp</label>
                  <input id="album-date" name="eventDate" type="date">
                </div>
                <div class="field field--full">
                  <label for="album-location">Địa điểm</label>
                  <div class="input-with-icon">
                    <span>${icon('mapPin')}</span>
                    <input id="album-location" name="location" type="text" maxlength="120" placeholder="Đà Lạt, Lâm Đồng" autocomplete="off">
                  </div>
                </div>
                <div class="field field--full">
                  <label for="album-description">Lời nhắn dành cho khách</label>
                  <textarea id="album-description" name="description" rows="4" maxlength="1200" placeholder="Một đôi dòng về ngày hôm đó, hoặc lời cảm ơn bạn muốn gửi riêng…"></textarea>
                  <div class="field__foot">
                    <span>Hiển thị ngay dưới tên album.</span>
                    <span data-description-count>0 / 1200</span>
                  </div>
                </div>
              </div>
            </section>

            <section class="panel form-section">
              <header class="panel__head panel__head--icon">
                <span class="panel__icon panel__icon--drive">${icon('drive')}</span>
                <div>
                  <p class="eyebrow">Bước 2</p>
                  <h2>Thư mục Google Drive</h2>
                  <p>Album sẽ đọc ảnh trực tiếp từ thư mục này, không cần tải lên lại.</p>
                </div>
                <span class="pill pill--neutral" data-drive-status>${icon('loader', 'spin')}Đang kiểm tra</span>
              </header>

              <div class="field">
                <label for="album-drive">Liên kết thư mục <span class="required">*</span></label>
                <div class="input-with-action">
                  <span class="input-with-action__icon">${icon('folder')}</span>
                  <input id="album-drive" name="driveFolderUrl" type="url" placeholder="https://drive.google.com/drive/folders/…" autocomplete="off" spellcheck="false" required>
                  <button class="btn btn--dark input-with-action__button" type="button" data-preview-drive>
                    ${icon('search')} <span>Xem trước</span>
                  </button>
                </div>
                <p class="field__hint">Thư mục cần được chia sẻ ở chế độ <strong>“Bất kỳ ai có liên kết”</strong>. Bạn cũng có thể dán riêng ID thư mục.</p>
              </div>

              <div class="subfolder-row">
                ${toggleRow({
                  name: 'includeSubfolders',
                  title: 'Bao gồm thư mục con',
                  description: 'Lấy cả ảnh nằm trong các thư mục bên trong.',
                  checked: false,
                  iconName: 'folder'
                })}
              </div>

              <div class="drive-preview" data-drive-preview>
                <div class="drive-placeholder">
                  <span class="drive-placeholder__icon">${icon('drive')}</span>
                  <h3>Chưa đọc thư mục nào</h3>
                  <p>Dán liên kết Drive rồi bấm <strong>Xem trước</strong> để kiểm tra ảnh, chọn ảnh bìa và số lượng trước khi tạo album.</p>
                  <ol class="drive-steps">
                    <li><span>1</span>Chia sẻ thư mục ở chế độ “Bất kỳ ai có liên kết”</li>
                    <li><span>2</span>Dán liên kết vào ô phía trên</li>
                    <li><span>3</span>Bấm Xem trước và chọn ảnh bìa</li>
                  </ol>
                </div>
              </div>
            </section>

            <section class="panel form-section">
              <header class="panel__head panel__head--icon">
                <span class="panel__icon">${icon('share')}</span>
                <div>
                  <p class="eyebrow">Bước 3</p>
                  <h2>Cách khách hàng tương tác</h2>
                  <p>Bạn có thể thay đổi những tuỳ chọn này bất cứ lúc nào sau khi tạo.</p>
                </div>
              </header>
              <div class="switch-list">
                ${toggleRow({
                  name: 'allowFavorites',
                  title: 'Cho phép thả tim và gửi lựa chọn',
                  description: 'Khách có thể đánh dấu ảnh yêu thích và gửi danh sách lại cho studio.',
                  checked: true,
                  iconName: 'heart',
                  tone: 'rose'
                })}
                ${toggleRow({
                  name: 'allowDownloads',
                  title: 'Cho phép tải ảnh gốc',
                  description: 'Hiển thị nút tải xuống trong album và chế độ xem ảnh lớn.',
                  checked: false,
                  iconName: 'download'
                })}
                ${toggleRow({
                  name: 'isPublished',
                  title: 'Chia sẻ album ngay',
                  description: 'Tắt để lưu thành bản nháp và chỉ mở liên kết khi đã sẵn sàng.',
                  checked: true,
                  iconName: 'eye',
                  tone: 'olive'
                })}
              </div>
            </section>

            <div data-create-error></div>

            <div class="form-actions">
              <a class="btn btn--ghost" href="/admin/albums">${icon('arrowLeft')} Quay lại</a>
              <button class="btn btn--primary btn--lg" type="submit" data-create-submit>
                ${icon('sparkles')} Tạo album
              </button>
            </div>
          </div>

          <aside class="create-aside">
            <section class="panel summary-card">
              <div class="summary-card__head">
                <p class="eyebrow">Xem nhanh</p>
                <span class="summary-card__badge">Album mới</span>
              </div>
              <div class="summary-card__cover" data-summary-cover>
                <div class="cover-fallback cover-fallback--tall">${icon('camera')}<span>Ảnh bìa</span></div>
              </div>
              <h3 data-summary-title>Album chưa đặt tên</h3>
              <p class="summary-card__meta" data-summary-meta>Thông tin album sẽ hiện ở đây</p>
              <dl class="summary-facts">
                <div><dt>Số ảnh</dt><dd data-summary-photos>—</dd></div>
                <div><dt>Ảnh bìa</dt><dd data-summary-cover-label>Chưa chọn</dd></div>
                <div><dt>Chia sẻ</dt><dd data-summary-published>Có</dd></div>
              </dl>
            </section>

            <section class="side-note">
              <span class="side-note__icon">${icon('sparkles')}</span>
              <h3>Một album hay bắt đầu từ một cái tên.</h3>
              <p>Đặt tên theo ngày hoặc cảm xúc — “Ngày Mình Chung Đôi” luôn dễ nhớ hơn “Album 24.09”.</p>
            </section>

            <section class="side-card side-card--warn" data-api-card hidden>
              <span class="side-card__icon">${icon('alertTriangle')}</span>
              <div>
                <h3>Chưa kết nối Google Drive</h3>
                <p>Thêm API key trong Cài đặt để đọc ảnh từ Drive. Album mẫu vẫn xem được bình thường.</p>
                <a class="text-link" href="/admin/settings">Mở cài đặt ${icon('arrowRight')}</a>
              </div>
            </section>

            <section class="side-card side-card--quiet">
              <span class="side-card__icon">${icon('lock')}</span>
              <div>
                <h3>Quyền riêng tư</h3>
                <p>Mỗi album nhận một liên kết riêng. Bạn có thể đổi liên kết bất cứ lúc nào trong trang album.</p>
              </div>
            </section>
          </aside>
        </form>
      </div>
    `
  });

  let teardown = () => {};
  let disposed = false;
  let settings = null;
  let preview = null;
  let previewUrl = '';
  let coverId = '';
  let previewLoading = false;

  return {
    title: 'Tạo album',
    bodyClass: 'page-admin',
    html,
    mount(root) {
      teardown = mountAdminShell(root, session);
      const page = root.querySelector('[data-create-page]');
      const form = root.querySelector('#create-album-form');
      const previewRoot = root.querySelector('[data-drive-preview]');
      const previewButton = root.querySelector('[data-preview-drive]');
      const submitButton = root.querySelector('[data-create-submit]');
      const errorRoot = root.querySelector('[data-create-error]');
      const driveInput = form.elements.namedItem('driveFolderUrl');
      const titleInput = form.elements.namedItem('title');
      const description = form.elements.namedItem('description');
      const descCount = root.querySelector('[data-description-count]');

      const summary = {
        cover: root.querySelector('[data-summary-cover]'),
        title: root.querySelector('[data-summary-title]'),
        meta: root.querySelector('[data-summary-meta]'),
        photos: root.querySelector('[data-summary-photos]'),
        coverLabel: root.querySelector('[data-summary-cover-label]'),
        published: root.querySelector('[data-summary-published]')
      };

      const coverPhoto = () => {
        if (!preview || driveInput.value.trim() !== previewUrl) return null;
        return preview.photos.find(photo => photo.driveFileId === coverId) || null;
      };

      const syncSummary = () => {
        const title = titleInput.value.trim() || 'Album chưa đặt tên';
        summary.title.textContent = title;
        const bits = [];
        const client = formValue(form, 'clientName');
        const date = formValue(form, 'eventDate');
        const location = formValue(form, 'location');
        if (client) bits.push(client);
        if (date) bits.push(formatDate(date, true));
        if (location) bits.push(location);
        summary.meta.textContent = bits.length ? bits.join(' · ') : 'Thông tin album sẽ hiện ở đây';
        summary.photos.textContent = preview && driveInput.value.trim() === previewUrl
          ? `${number(preview.totalCount)} ảnh`
          : '—';
        const cover = coverPhoto();
        summary.coverLabel.textContent = cover ? cover.code : 'Chưa chọn';
        summary.published.textContent = form.elements.namedItem('isPublished').checked ? 'Có' : 'Bản nháp';
        if (cover) {
          summary.cover.innerHTML = `<img src="${safeUrl(cover.thumbUrl)}" alt="Ảnh bìa đã chọn">`;
        } else if (preview?.photos?.length && driveInput.value.trim() === previewUrl) {
          const first = preview.photos[0];
          summary.cover.innerHTML = `<img src="${safeUrl(first.thumbUrl)}" alt="Ảnh bìa mặc định">`;
        } else {
          summary.cover.innerHTML = `<div class="cover-fallback cover-fallback--tall">${icon('camera')}<span>Ảnh bìa</span></div>`;
        }
      };

      const renderPreviewResult = () => {
        if (!preview) return;
        const photos = preview.photos || [];
        const selected = coverPhoto();
        const folderName = preview.folderName || 'Thư mục Drive';
        previewRoot.innerHTML = `
          <div class="preview-result">
            <div class="preview-result__head">
              <div>
                <p class="eyebrow">Đã đọc thư mục</p>
                <h3>${escapeHtml(folderName)}</h3>
              </div>
              <span class="pill pill--live">${number(preview.totalCount)} ảnh</span>
            </div>
            <p class="preview-result__hint">${icon('image')} Chọn một ảnh làm bìa album.${preview.truncated ? ' Album có nhiều ảnh, phần xem trước chỉ hiển thị một số.' : ''}</p>
            <div class="cover-grid">
              ${photos.map(photo => `
                <button class="cover-choice ${photo.driveFileId === (selected?.driveFileId || '') ? 'is-selected' : ''}" type="button" data-cover-choice="${escapeAttr(photo.driveFileId)}" aria-label="Chọn ${escapeAttr(photo.code)} làm ảnh bìa" aria-pressed="${photo.driveFileId === (selected?.driveFileId || '')}">
                  <img src="${safeUrl(photo.thumbUrl)}" alt="${escapeAttr(photo.fileName)}" loading="lazy">
                  <span class="cover-choice__check">${icon('check')}</span>
                  <span class="cover-choice__code">${escapeHtml(photo.code)}</span>
                </button>
              `).join('')}
            </div>
            <p class="preview-result__foot">${icon('checkCircle')} Khi tạo album, Mây sẽ nhập toàn bộ ${number(preview.totalCount)} ảnh trong thư mục.</p>
          </div>
        `;
      };

      const loadSettings = async () => {
        try {
          settings = await api('/api/admin/settings');
          if (disposed) return;
          const status = root.querySelector('[data-drive-status]');
          if (settings.googleConfigured) {
            status.className = 'pill pill--live';
            status.innerHTML = '<span class="pill__dot"></span>Drive đã kết nối';
          } else {
            status.className = 'pill pill--warn';
            status.innerHTML = `${icon('alertCircle')}Chưa có API key`;
            root.querySelector('[data-api-card]').hidden = false;
          }
        } catch {
          const status = root.querySelector('[data-drive-status]');
          if (status) {
            status.className = 'pill pill--neutral';
            status.innerHTML = `${icon('alertCircle')}Chưa kiểm tra được`;
          }
        }
      };

      const resetPreview = () => {
        preview = null;
        previewUrl = '';
        coverId = '';
        previewRoot.innerHTML = `
          <div class="drive-placeholder">
            <span class="drive-placeholder__icon">${icon('drive')}</span>
            <h3>Chưa đọc thư mục nào</h3>
            <p>Dán liên kết Drive rồi bấm <strong>Xem trước</strong> để kiểm tra ảnh, chọn ảnh bìa và số lượng trước khi tạo album.</p>
          </div>
        `;
        syncSummary();
      };

      const previewDrive = async () => {
        if (previewLoading) return;
        clearFieldErrors(form);
        errorRoot.innerHTML = '';
        const url = driveInput.value.trim();
        if (!url) {
          fieldError(driveInput, 'Hãy dán liên kết thư mục Google Drive trước.');
          return;
        }

        previewLoading = true;
        setButtonLoading(previewButton, true, 'Đang đọc…');
        previewRoot.innerHTML = `
          <div class="preview-loading">
            <div class="preview-loading__head"><span class="skeleton skeleton-line skeleton-line--sm"></span><span class="skeleton skeleton-badge"></span></div>
            <div class="cover-grid">
              ${Array.from({ length: 8 }, () => '<div class="skeleton skeleton-thumb"></div>').join('')}
            </div>
            <p>${icon('loader', 'spin')} Đang đọc thư mục từ Google Drive…</p>
          </div>
        `;

        try {
          const result = await api('/api/admin/drive/preview', {
            method: 'POST',
            body: {
              url,
              includeSubfolders: form.elements.namedItem('includeSubfolders').checked
            }
          });
          if (disposed) return;
          preview = result;
          previewUrl = url;
          coverId = result.photos?.[0]?.driveFileId || '';
          renderPreviewResult();
          syncSummary();
          previewRoot.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        } catch (error) {
          if (disposed || error.name === 'AbortError') return;
          preview = null;
          previewUrl = '';
          coverId = '';
          const settingsLink = ['missing_api_key', 'invalid_api_key'].includes(error.code)
            ? `<a class="text-link" href="/admin/settings">Mở Cài đặt Google Drive ${icon('arrowRight')}</a>`
            : '';
          previewRoot.innerHTML = `
            <div class="preview-error">
              ${errorInline(error.message || 'Chưa đọc được thư mục này.', error.code)}
              <div class="preview-error__actions">
                <button class="btn btn--ghost btn--compact" type="button" data-preview-drive>${icon('refresh')} Thử lại</button>
                ${settingsLink}
              </div>
            </div>
          `;
          syncSummary();
        } finally {
          previewLoading = false;
          setButtonLoading(previewButton, false);
        }
      };

      form.addEventListener('input', event => {
        if (event.target === description) {
          descCount.textContent = `${description.value.length} / 1200`;
        }
        if (event.target === driveInput && preview && driveInput.value.trim() !== previewUrl) {
          const notice = previewRoot.querySelector('[data-stale-note]');
          if (!notice) {
            const p = document.createElement('p');
            p.className = 'preview-stale';
            p.dataset.staleNote = '1';
            p.innerHTML = `${icon('alertCircle')} Liên kết đã thay đổi — bấm Xem trước để chọn lại ảnh bìa cho thư mục mới.`;
            previewRoot.prepend(p);
          }
        }
        syncSummary();
      });

      form.addEventListener('change', event => {
        if (event.target.name === 'isPublished' || event.target.name === 'allowFavorites') {
          syncSummary();
        }
      });

      page.addEventListener('click', event => {
        const previewAction = event.target.closest('[data-preview-drive]');
        if (previewAction) {
          previewDrive();
          return;
        }
        const choice = event.target.closest('[data-cover-choice]');
        if (choice) {
          coverId = choice.dataset.coverChoice;
          previewRoot.querySelectorAll('[data-cover-choice]').forEach(button => {
            const active = button === choice;
            button.classList.toggle('is-selected', active);
            button.setAttribute('aria-pressed', String(active));
          });
          syncSummary();
        }
      });

      form.addEventListener('submit', async event => {
        event.preventDefault();
        clearFieldErrors(form);
        errorRoot.innerHTML = '';

        const title = titleInput.value.trim();
        const url = driveInput.value.trim();
        let invalid = false;
        if (!title) {
          fieldError(titleInput, 'Hãy đặt tên cho album.');
          invalid = true;
        }
        if (!url) {
          fieldError(driveInput, 'Hãy dán liên kết thư mục Google Drive.');
          invalid = true;
        }
        if (invalid) return;

        const samePreview = preview && driveInput.value.trim() === previewUrl;
        setButtonLoading(submitButton, true, 'Đang tạo album…');
        try {
          const album = await api('/api/admin/albums', {
            method: 'POST',
            body: {
              title,
              clientName: formValue(form, 'clientName'),
              eventDate: formValue(form, 'eventDate'),
              location: formValue(form, 'location'),
              description: formValue(form, 'description'),
              driveFolderUrl: url,
              includeSubfolders: form.elements.namedItem('includeSubfolders').checked,
              allowFavorites: form.elements.namedItem('allowFavorites').checked,
              allowDownloads: form.elements.namedItem('allowDownloads').checked,
              isPublished: form.elements.namedItem('isPublished').checked,
              coverDriveFileId: samePreview && coverId ? coverId : null
            }
          });
          if (disposed) return;
          showToast(`Đã tạo album “${album.title}”.`, 'success');
          navigateTo(`/admin/albums/${encodeURIComponent(album.id)}`);
        } catch (error) {
          if (disposed || error.name === 'AbortError') return;
          setButtonLoading(submitButton, false);
          const settingsLink = ['missing_api_key', 'invalid_api_key', 'drive_forbidden'].includes(error.code)
            ? `<a class="text-link" href="/admin/settings">Kiểm tra Cài đặt Google Drive ${icon('arrowRight')}</a>`
            : '';
          errorRoot.innerHTML = `
            <div class="form-error">
              ${errorInline(error.message || 'Chưa tạo được album.', error.code)}
              ${settingsLink}
            </div>
          `;
          errorRoot.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
      });

      titleInput.addEventListener('input', syncSummary);
      description.addEventListener('input', () => {
        descCount.textContent = `${description.value.length} / 1200`;
      });

      loadSettings();
      syncSummary();
    },
    cleanup() {
      disposed = true;
      teardown();
    }
  };
}

// ---------------------------------------------------------------- album detail

export function albumDetailView({ session, albumId }) {
  const html = adminShell({
    active: 'albums',
    session,
    content: `
      <div class="admin-page__inner" data-album-page>
        <div data-detail-root>
          <div class="detail-skeleton">
            <div class="skeleton skeleton-line skeleton-line--sm"></div>
            <div class="skeleton skeleton-detail"></div>
            <div class="detail-columns">
              <div class="skeleton skeleton-panel"></div>
              <div class="skeleton skeleton-panel"></div>
            </div>
          </div>
        </div>
      </div>
    `
  });

  let teardown = () => {};
  let disposed = false;
  let album = null;
  let filter = 'all';
  let query = '';
  let queryDisplay = '';
  let activeModal = null;
  let loading = false;

  return {
    title: 'Chi tiết album',
    bodyClass: 'page-admin',
    html,
    mount(root) {
      teardown = mountAdminShell(root, session);
      const page = root.querySelector('[data-album-page]');
      const holder = root.querySelector('[data-detail-root]');

      const visiblePhotos = () => {
        if (!album) return [];
        const needle = stripDiacritics(query);
        return album.photos.filter(photo => {
          if (filter === 'selected' && !photo.isSelected) return false;
          if (!needle) return true;
          return stripDiacritics(`${photo.code} ${photo.fileName}`).includes(needle);
        });
      };

      const applyGalleryFilter = () => {
        if (!album) return;
        const visible = visiblePhotos();
        const visibleIds = new Set(visible.map(photo => photo.id));
        holder.querySelectorAll('[data-photo-index]').forEach(button => {
          const photo = album.photos[Number(button.dataset.photoIndex)];
          const show = photo ? visibleIds.has(photo.id) : false;
          button.hidden = !show;
        });

        holder.querySelectorAll('[data-detail-filter]').forEach(button => {
          const active = button.dataset.detailFilter === filter;
          button.classList.toggle('is-active', active);
          button.setAttribute('aria-selected', String(active));
        });

        const count = holder.querySelector('[data-gallery-visible]');
        if (count) count.textContent = `${number(visible.length)} ảnh`;
        const empty = holder.querySelector('[data-gallery-empty]');
        if (empty) {
          empty.hidden = visible.length > 0;
          if (!visible.length) {
            empty.innerHTML = album.photos.length === 0
              ? emptyState({
                  iconName: 'image',
                  title: 'Album chưa có ảnh',
                  message: 'Hãy đồng bộ lại thư mục Google Drive để thêm ảnh.'
                })
              : emptyState({
                  iconName: filter === 'selected' ? 'heart' : 'search',
                  title: filter === 'selected' && !query ? 'Khách chưa chọn ảnh nào' : 'Không tìm thấy ảnh phù hợp',
                  message: query
                    ? `Không có ảnh nào khớp với mã “${escapeHtml(queryDisplay)}”.`
                    : 'Thử chuyển bộ lọc hoặc xoá từ khoá tìm kiếm.',
                  action: '<button class="btn btn--ghost" type="button" data-reset-gallery>Xem tất cả ảnh</button>'
                });
          }
        }

        const search = holder.querySelector('[data-detail-search]');
        if (search && search.value !== queryDisplay) search.value = queryDisplay;
      };

      const paint = () => {
        if (!album) return;
        holder.innerHTML = renderDetail(album, queryDisplay);
        document.title = `${album.title} · Mây Album`;
        applyGalleryFilter();
      };

      const load = async () => {
        if (loading) return;
        loading = true;
        try {
          const result = await api(`/api/admin/albums/${encodeURIComponent(albumId)}`);
          if (disposed) return;
          album = result;
          paint();
        } catch (error) {
          if (disposed || error.name === 'AbortError') return;
          holder.innerHTML = `
            <div class="detail-load-error">
              <a class="back-link" href="/admin/albums">${icon('arrowLeft')} Album của bạn</a>
              <div class="panel panel--pad panel--error">
                ${errorInline(error.message || 'Không tìm thấy album này.', error.code)}
                <div class="inline-actions">
                  <button class="btn btn--ghost" type="button" data-retry>${icon('refresh')} Thử lại</button>
                  <a class="btn btn--primary" href="/admin/albums">Xem tất cả album</a>
                </div>
              </div>
            </div>
          `;
        } finally {
          loading = false;
        }
      };

      const updateAlbum = async (patch, successMessage) => {
        const updated = await api(`/api/admin/albums/${encodeURIComponent(album.id)}`, {
          method: 'PATCH',
          body: patch
        });
        album = updated;
        paint();
        if (successMessage) showToast(successMessage, 'success');
      };

      const openEdit = () => {
        const current = album;
        const modal = createModal(`
          <form class="dialog-card dialog-card--form" data-edit-form>
            <button class="dialog-close" type="button" data-close-dialog aria-label="Đóng">${icon('x')}</button>
            <p class="eyebrow">Thông tin album</p>
            <h2>Chỉnh sửa câu chuyện</h2>
            <p class="dialog-copy">Những thay đổi sẽ hiển thị ngay với khách hàng khi họ mở lại liên kết.</p>
            <div class="form-grid form-grid--dialog">
              <div class="field field--full">
                <label for="edit-title">Tên album <span class="required">*</span></label>
                <input id="edit-title" name="title" type="text" maxlength="100" value="${escapeAttr(current.title)}" required>
              </div>
              <div class="field">
                <label for="edit-client">Tên khách hàng</label>
                <input id="edit-client" name="clientName" type="text" maxlength="80" value="${escapeAttr(current.clientName)}">
              </div>
              <div class="field">
                <label for="edit-date">Ngày chụp</label>
                <input id="edit-date" name="eventDate" type="date" value="${escapeAttr(current.eventDate)}">
              </div>
              <div class="field field--full">
                <label for="edit-location">Địa điểm</label>
                <input id="edit-location" name="location" type="text" maxlength="120" value="${escapeAttr(current.location)}">
              </div>
              <div class="field field--full">
                <label for="edit-description">Lời nhắn dành cho khách</label>
                <textarea id="edit-description" name="description" rows="4" maxlength="1200">${escapeHtml(current.description)}</textarea>
              </div>
            </div>
            <div data-edit-error></div>
            <div class="dialog-actions">
              <button class="btn btn--ghost" type="button" data-close-dialog>Quay lại</button>
              <button class="btn btn--primary" type="submit" data-edit-submit>Lưu thay đổi</button>
            </div>
          </form>
        `, {
          className: 'dialog--form',
          onClose: () => {
            if (activeModal?.dialog === modal.dialog) activeModal = null;
          }
        });

        activeModal = modal;
        const form = modal.dialog.querySelector('[data-edit-form]');
        const submit = modal.dialog.querySelector('[data-edit-submit]');
        form.addEventListener('submit', async event => {
          event.preventDefault();
          const title = form.elements.namedItem('title').value.trim();
          if (!title) {
            fieldError(form.elements.namedItem('title'), 'Tên album không được để trống.');
            return;
          }

          setButtonLoading(submit, true, 'Đang lưu…');
          try {
            await updateAlbum({
              title,
              clientName: form.elements.namedItem('clientName').value.trim(),
              eventDate: form.elements.namedItem('eventDate').value,
              location: form.elements.namedItem('location').value.trim(),
              description: form.elements.namedItem('description').value.trim()
            }, 'Đã cập nhật thông tin album.');
            modal.close();
          } catch (error) {
            setButtonLoading(submit, false);
            modal.dialog.querySelector('[data-edit-error]').innerHTML =
              errorBlock(error.message || 'Chưa lưu được thay đổi.', error.code);
          }
        });
      };

      const openPhoto = index => {
        if (!album) return;
        const list = visiblePhotos();
        if (!list.length) return;
        let currentIndex = Math.max(0, list.findIndex(photo => photo.id === album.photos[index]?.id));
        if (currentIndex < 0) currentIndex = 0;

        const modal = createModal(`
          <div class="photo-dialog">
            <button class="dialog-close photo-dialog__close" type="button" data-close-dialog aria-label="Đóng">${icon('x')}</button>
            <div class="photo-dialog__stage">
              <img data-photo-image src="" alt="">
              <button class="photo-dialog__nav photo-dialog__nav--prev" type="button" data-photo-prev aria-label="Ảnh trước">${icon('arrowLeft')}</button>
              <button class="photo-dialog__nav photo-dialog__nav--next" type="button" data-photo-next aria-label="Ảnh sau">${icon('arrowRight')}</button>
              <span class="photo-dialog__count" data-photo-count></span>
            </div>
            <aside class="photo-dialog__side">
              <p class="eyebrow">Khoảnh khắc</p>
              <h2 data-photo-code></h2>
              <p class="photo-dialog__filename" data-photo-filename></p>
              <div class="photo-dialog__status" data-photo-status></div>
              <dl class="photo-dialog__meta" data-photo-meta></dl>
              <div class="photo-dialog__actions">
                <button class="btn btn--primary" type="button" data-set-cover>${icon('image')} <span>Đặt làm ảnh bìa</span></button>
                <a class="btn btn--ghost" data-photo-download href="#" target="_blank" rel="noopener">${icon('download')} Tải ảnh gốc</a>
              </div>
              <p class="photo-dialog__hint">${icon('credit')} Dùng phím ← → để xem ảnh khác</p>
            </aside>
          </div>
        `, {
          className: 'dialog--photo',
          onClose: () => {
            document.removeEventListener('keydown', onKeydown);
            if (activeModal?.dialog === modal.dialog) activeModal = null;
          }
        });

        activeModal = modal;
        const image = modal.dialog.querySelector('[data-photo-image]');
        const count = modal.dialog.querySelector('[data-photo-count]');
        const code = modal.dialog.querySelector('[data-photo-code]');
        const filename = modal.dialog.querySelector('[data-photo-filename]');
        const status = modal.dialog.querySelector('[data-photo-status]');
        const meta = modal.dialog.querySelector('[data-photo-meta]');
        const coverButton = modal.dialog.querySelector('[data-set-cover]');
        const download = modal.dialog.querySelector('[data-photo-download]');
        const prev = modal.dialog.querySelector('[data-photo-prev]');
        const next = modal.dialog.querySelector('[data-photo-next]');

        const renderPhoto = () => {
          const photo = list[currentIndex];
          if (!photo) return;
          image.src = photo.previewUrl || photo.thumbUrl;
          image.alt = `Ảnh ${photo.code}`;
          count.textContent = `${currentIndex + 1} / ${list.length}`;
          code.textContent = photo.code;
          filename.textContent = photo.fileName || '';
          status.innerHTML = `
            ${photo.isSelected
              ? `<span class="pill pill--selected">${icon('heart')}Khách đã chọn</span>`
              : `<span class="pill pill--neutral">${icon('heart')}Chưa được chọn</span>`}
            ${photo.id === album.coverPhotoId ? `<span class="pill pill--cover">${icon('image')}Ảnh bìa</span>` : ''}
          `;
          meta.innerHTML = `
            <div><dt>Kích thước</dt><dd>${photo.width && photo.height ? `${number(photo.width)} × ${number(photo.height)} px` : 'Không rõ'}</dd></div>
            <div><dt>Khách chọn lúc</dt><dd>${photo.selectedAt ? escapeHtml(formatTimestamp(photo.selectedAt)) : '—'}</dd></div>
            <div><dt>Mã hình</dt><dd class="mono">${escapeHtml(photo.code)}</dd></div>
          `;
          const isCover = photo.id === album.coverPhotoId;
          coverButton.disabled = isCover;
          coverButton.innerHTML = isCover
            ? `${icon('checkCircle')} <span>Đang là ảnh bìa</span>`
            : `${icon('image')} <span>Đặt làm ảnh bìa</span>`;
          if (photo.downloadUrl) {
            download.href = photo.downloadUrl;
            download.hidden = false;
          } else {
            download.hidden = true;
          }
          prev.disabled = currentIndex === 0;
          next.disabled = currentIndex === list.length - 1;
        };

        const move = delta => {
          currentIndex = Math.min(Math.max(currentIndex + delta, 0), list.length - 1);
          renderPhoto();
        };

        const onKeydown = event => {
          if (event.defaultPrevented) return;
          if (event.key === 'ArrowLeft') {
            event.preventDefault();
            move(-1);
          } else if (event.key === 'ArrowRight') {
            event.preventDefault();
            move(1);
          }
        };

        prev.addEventListener('click', () => move(-1));
        next.addEventListener('click', () => move(1));
        document.addEventListener('keydown', onKeydown);
        coverButton.addEventListener('click', async () => {
          const photo = list[currentIndex];
          if (!photo || photo.id === album.coverPhotoId) return;
          setButtonLoading(coverButton, true, 'Đang đặt bìa…');
          try {
            await updateAlbum({ coverPhotoId: photo.id }, `Đã đặt ${photo.code} làm ảnh bìa.`);
            modal.close();
          } catch (error) {
            setButtonLoading(coverButton, false);
            showToast(error.message || 'Chưa đổi được ảnh bìa.', 'error');
          }
        });

        renderPhoto();
      };

      const refreshDrive = async button => {
        if (!album || album.isDemo) return;
        setButtonLoading(button, true, 'Đang đồng bộ…');
        try {
          const result = await api(`/api/admin/albums/${encodeURIComponent(album.id)}/refresh`, { method: 'POST' });
          album = result.album;
          paint();
          const bits = [];
          if (result.added) bits.push(`thêm ${number(result.added)} ảnh`);
          if (result.removed) bits.push(`gỡ ${number(result.removed)} ảnh`);
          showToast(bits.length ? `Đã đồng bộ: ${bits.join(', ')}.` : 'Album đã là bản mới nhất.', 'success');
        } catch (error) {
          setButtonLoading(button, false);
          showToast(error.message || 'Chưa đồng bộ được thư mục.', 'error');
        }
      };

      const rotateLink = async () => {
        const confirmed = await confirmDialog({
          title: 'Đổi liên kết chia sẻ?',
          message: 'Liên kết cũ sẽ ngừng hoạt động ngay lập tức. Hãy gửi liên kết mới cho khách sau khi đổi.',
          confirmLabel: 'Đổi liên kết',
          cancelLabel: 'Giữ liên kết cũ',
          iconName: 'link',
          danger: true
        });
        if (!confirmed) return;
        try {
          const result = await api(`/api/admin/albums/${encodeURIComponent(album.id)}/rotate-link`, { method: 'POST' });
          album.shareToken = result.shareToken;
          paint();
          showToast('Đã tạo liên kết chia sẻ mới.', 'success');
        } catch (error) {
          showToast(error.message || 'Chưa đổi được liên kết.', 'error');
        }
      };

      const deleteAlbum = async () => {
        const confirmed = await confirmDialog({
          title: `Xoá “${album.title}”?`,
          message: 'Album, danh sách lựa chọn và ghi chú của khách sẽ bị xoá khỏi studio. Thao tác này không thể hoàn tác.',
          confirmLabel: 'Xoá album',
          cancelLabel: 'Giữ lại',
          danger: true
        });
        if (!confirmed) return;
        try {
          await api(`/api/admin/albums/${encodeURIComponent(album.id)}`, { method: 'DELETE' });
          showToast('Đã xoá album.', 'success');
          navigateTo('/admin/albums');
        } catch (error) {
          showToast(error.message || 'Chưa xoá được album.', 'error');
        }
      };

      page.addEventListener('click', event => {
        const copyButton = event.target.closest('[data-copy-token]');
        if (copyButton) {
          event.preventDefault();
          copyShareLink(copyButton, copyButton.dataset.copyToken);
          return;
        }

        const copyCodesButton = event.target.closest('[data-copy-selected-codes]');
        if (copyCodesButton && album) {
          copySelectedCodes(copyCodesButton, selectedPhotoCodes(album));
          return;
        }

        if (event.target.closest('[data-select-share]')) {
          event.target.closest('[data-select-share]').select();
          return;
        }

        if (event.target.closest('[data-action="edit"]')) {
          openEdit();
          return;
        }

        if (event.target.closest('[data-action="refresh"]')) {
          refreshDrive(event.target.closest('[data-action="refresh"]'));
          return;
        }

        if (event.target.closest('[data-action="rotate"]')) {
          rotateLink();
          return;
        }

        if (event.target.closest('[data-action="delete"]')) {
          deleteAlbum();
          return;
        }

        const filterButton = event.target.closest('[data-detail-filter]');
        if (filterButton) {
          filter = filterButton.dataset.detailFilter;
          applyGalleryFilter();
          return;
        }

        if (event.target.closest('[data-reset-gallery]')) {
          filter = 'all';
          query = '';
          queryDisplay = '';
          applyGalleryFilter();
          return;
        }

        const clearButton = event.target.closest('[data-detail-search-clear]');
        if (clearButton) {
          query = '';
          queryDisplay = '';
          applyGalleryFilter();
          holder.querySelector('[data-detail-search]')?.focus();
          return;
        }

        const photoButton = event.target.closest('[data-photo-index]');
        if (photoButton) {
          openPhoto(Number(photoButton.dataset.photoIndex));
          return;
        }

        if (event.target.closest('[data-retry]')) {
          load();
        }
      });

      page.addEventListener('input', event => {
        if (event.target.matches('[data-detail-search]')) {
          queryDisplay = event.target.value;
          query = event.target.value.trim();
          applyGalleryFilter();
          const clear = holder.querySelector('[data-detail-search-clear]');
          if (clear) clear.hidden = !queryDisplay;
        }
      });

      page.addEventListener('change', async event => {
        const toggle = event.target.closest('[data-toggle-field]');
        if (!toggle || !album) return;
        const field = toggle.dataset.toggleField;
        const value = toggle.checked;
        toggle.disabled = true;
        try {
          await updateAlbum({ [field]: value }, field === 'isPublished'
            ? (value ? 'Album đã được chia sẻ.' : 'Album đã chuyển thành bản nháp.')
            : 'Đã cập nhật tuỳ chọn album.');
        } catch (error) {
          toggle.checked = !value;
          toggle.disabled = false;
          showToast(error.message || 'Chưa cập nhật được tuỳ chọn.', 'error');
        }
      });

      load();
    },
    cleanup() {
      disposed = true;
      activeModal?.close?.();
      teardown();
    }
  };
}

function renderDetail(album, queryDisplay = '') {
  const url = shareUrl(album.shareToken);
  const selected = album.selectedCount || 0;
  const selectedPhotos = album.photos.filter(photo => photo.isSelected).length;
  const total = album.photoCount || 0;
  const cover = album.coverUrl;
  const demo = album.isDemo;

  return `
    <div class="detail-topline">
      <a class="back-link" href="/admin/albums">${icon('arrowLeft')} Album của bạn</a>
      <div class="detail-topline__actions">
        <button class="btn btn--ghost btn--compact" type="button" data-action="edit">${icon('edit')} Sửa thông tin</button>
        <a class="btn btn--primary btn--compact" href="/a/${encodeURIComponent(album.shareToken)}" target="_blank" rel="noopener">${icon('eye')} Xem album</a>
      </div>
    </div>

    <section class="detail-hero">
      <div class="detail-hero__cover">
        ${cover
          ? `<img src="${safeUrl(cover)}" alt="Ảnh bìa album ${escapeAttr(album.title)}">`
          : `<div class="cover-fallback cover-fallback--hero">${icon('image')}<span>Chưa có ảnh bìa</span></div>`}
        <div class="detail-hero__cover-tags">
          ${statusPill(album)}
          ${album.submittedAt ? `<span class="pill pill--submitted">${icon('checkCircle')}Đã nhận lựa chọn</span>` : ''}
        </div>
      </div>
      <div class="detail-hero__body">
        <div class="detail-hero__title">
          <p class="eyebrow">${demo ? 'Album mẫu' : 'Album khách hàng'}</p>
          <h1>${escapeHtml(album.title)}</h1>
          <p class="detail-hero__client">${icon('users')} ${escapeHtml(album.clientName || 'Chưa có tên khách hàng')}</p>
          <div class="detail-meta">
            ${album.eventDate ? `<span>${icon('calendar')} ${escapeHtml(formatDate(album.eventDate, true))}</span>` : ''}
            ${album.location ? `<span>${icon('mapPin')} ${escapeHtml(album.location)}</span>` : ''}
            <span>${icon('clock')} Tạo ${escapeHtml(formatShortTimestamp(album.createdAt))}</span>
          </div>
        </div>
        ${album.description ? `<p class="detail-hero__description">${escapeHtml(album.description)}</p>` : ''}
        <div class="detail-hero__stats">
          <div><strong>${number(total)}</strong><span>ảnh trong album</span></div>
          <div><strong>${number(selected)}</strong><span>ảnh được chọn</span></div>
          <div><strong>${percent(selected, total)}%</strong><span>tỷ lệ yêu thích</span></div>
        </div>
        <div class="share-field">
          <span class="share-field__label">${icon('link')} Liên kết dành cho khách</span>
          <div class="share-field__row">
            <input type="text" readonly value="${escapeAttr(url)}" data-select-share aria-label="Liên kết chia sẻ album">
            <button class="btn btn--dark btn--compact" type="button" data-copy-token="${escapeAttr(album.shareToken)}">${icon('copy')} Sao chép</button>
          </div>
        </div>
        ${!album.isPublished ? `<div class="notice notice--warning">${icon('eyeOff')} Album đang ở chế độ bản nháp — liên kết sẽ chưa mở được với khách.</div>` : ''}
      </div>
    </section>

    ${album.submittedAt ? `
      <section class="submission-banner">
        <span class="submission-banner__icon">${icon('checkCircle')}</span>
        <div>
          <p class="eyebrow">Khách đã gửi lựa chọn</p>
          <h3>${escapeHtml(formatTimestamp(album.submittedAt))} · ${number(selected)} ảnh</h3>
          ${album.clientNote ? `<blockquote>“${escapeHtml(album.clientNote)}”</blockquote>` : '<p>Khách không để lại ghi chú.</p>'}
        </div>
        <a class="btn btn--cream btn--compact" href="/api/admin/albums/${encodeURIComponent(album.id)}/selections.csv">${icon('download')} Tải CSV</a>
      </section>
    ` : ''}

    <div class="detail-columns">
      <section class="panel gallery-panel">
        <header class="gallery-head">
          <div class="gallery-head__title">
            <p class="eyebrow">Thư viện ảnh</p>
            <h2>${number(total)} khoảnh khắc</h2>
          </div>
          <div class="gallery-tools">
            <button class="btn btn--ghost btn--compact" type="button" data-copy-selected-codes ${selectedPhotos ? '' : 'disabled'}>${icon('copy')} Sao chép mã ảnh đã chọn</button>
            <div class="segmented segmented--sm" role="tablist" aria-label="Lọc ảnh">
              <button class="segmented__item" type="button" role="tab" data-detail-filter="all" aria-selected="true">Tất cả <span>${number(total)}</span></button>
              <button class="segmented__item" type="button" role="tab" data-detail-filter="selected" aria-selected="false">${icon('heart')} Đã chọn <span>${number(selectedPhotos)}</span></button>
            </div>
            <div class="searchbox searchbox--sm">
              <span class="searchbox__icon">${icon('search')}</span>
              <input type="search" data-detail-search placeholder="Tìm mã hình…" aria-label="Tìm ảnh theo mã hình" autocomplete="off">
              <button class="searchbox__clear" type="button" data-detail-search-clear aria-label="Xoá tìm kiếm" ${queryDisplay ? '' : 'hidden'}>${icon('x')}</button>
            </div>
          </div>
        </header>
        <div class="gallery-subhead">
          <span><strong data-gallery-visible>${number(total)} ảnh</strong> · bấm vào ảnh để xem lớn</span>
          ${selected ? `<span class="gallery-subhead__selected">${icon('heart')} ${number(selected)} ảnh khách đã chọn</span>` : '<span class="gallery-subhead__muted">Chưa có lựa chọn nào</span>'}
        </div>
        <div class="admin-gallery" data-gallery>
          ${album.photos.map((photo, index) => `
            <button class="admin-photo ${photo.isSelected ? 'is-selected' : ''}" type="button" data-photo-index="${index}" aria-label="Xem ảnh ${escapeAttr(photo.code)}">
              <img src="${safeUrl(photo.thumbUrl)}" alt="${escapeAttr(photo.code)}" loading="lazy">
              <span class="admin-photo__shade" aria-hidden="true"></span>
              <span class="admin-photo__code">${escapeHtml(photo.code)}</span>
              ${photo.isSelected ? `<span class="admin-photo__heart" title="Khách đã chọn">${icon('heart')}</span>` : ''}
              ${photo.id === album.coverPhotoId ? `<span class="admin-photo__cover">${icon('image')} Bìa</span>` : ''}
              <span class="admin-photo__zoom">${icon('zoomIn')}</span>
            </button>
          `).join('')}
        </div>
        <div data-gallery-empty hidden></div>
      </section>

      <aside class="detail-side">
        <section class="panel options-panel">
          <header class="panel__head">
            <p class="eyebrow">Chia sẻ</p>
            <h2>Tuỳ chọn album</h2>
          </header>
          <div class="switch-list">
            ${toggleRow({
              name: 'allowFavorites',
              title: 'Nhận lựa chọn',
              description: 'Cho phép khách thả tim và gửi danh sách ảnh.',
              checked: album.allowFavorites,
              iconName: 'heart',
              tone: 'rose',
              dataAttr: 'data-toggle-field="allowFavorites"'
            })}
            ${toggleRow({
              name: 'allowDownloads',
              title: 'Cho phép tải ảnh',
              description: 'Khách có thể tải ảnh gốc từ chế độ xem lớn.',
              checked: album.allowDownloads,
              iconName: 'download',
              dataAttr: 'data-toggle-field="allowDownloads"'
            })}
            ${toggleRow({
              name: 'isPublished',
              title: 'Đang chia sẻ',
              description: 'Tắt để tạm ẩn album khỏi mọi liên kết.',
              checked: album.isPublished,
              iconName: 'eye',
              tone: 'olive',
              dataAttr: 'data-toggle-field="isPublished"'
            })}
          </div>
          ${!album.allowFavorites && selected ? `<div class="notice notice--info">${icon('alertCircle')} Album đang tắt nhận lựa chọn. Khách vẫn xem được các ảnh đã chọn trước đó.</div>` : ''}
        </section>

        <section class="panel activity-panel">
          <header class="panel__head panel__head--row">
            <div>
              <p class="eyebrow">Dòng thời gian</p>
              <h2>Hoạt động</h2>
            </div>
            ${album.submittedAt ? `<span class="pill pill--submitted">${icon('checkCircle')}Đã gửi</span>` : ''}
          </header>
          <dl class="activity-list">
            <div><dt>${icon('plus')} Tạo album</dt><dd>${escapeHtml(formatTimestamp(album.createdAt))}</dd></div>
            <div><dt>${icon('refreshCw')} Cập nhật gần nhất</dt><dd>${escapeHtml(formatTimestamp(album.updatedAt))}</dd></div>
            <div><dt>${icon('heart')} Lựa chọn cập nhật</dt><dd>${album.favoritesUpdatedAt ? escapeHtml(formatTimestamp(album.favoritesUpdatedAt)) : 'Chưa có'}</dd></div>
            <div><dt>${icon('send')} Khách gửi lựa chọn</dt><dd>${album.submittedAt ? escapeHtml(formatTimestamp(album.submittedAt)) : 'Chưa gửi'}</dd></div>
          </dl>
          ${album.clientNote && !album.submittedAt ? `<blockquote class="client-note">“${escapeHtml(album.clientNote)}”</blockquote>` : ''}
        </section>

        <section class="panel source-panel">
          <header class="panel__head">
            <p class="eyebrow">Nguồn ảnh</p>
            <h2>Google Drive</h2>
          </header>
          <div class="source-row">
            <span class="source-row__icon">${icon('drive')}</span>
            <div>
              <strong>${demo ? 'Album mẫu' : 'Thư mục Drive'}</strong>
              <span>${demo ? 'Không kết nối Drive' : album.includeSubfolders ? 'Bao gồm thư mục con' : 'Chỉ ảnh cấp đầu tiên'}</span>
            </div>
          </div>
          ${album.driveFolderUrl && !demo
            ? `<a class="source-link" href="${safeUrl(album.driveFolderUrl)}" target="_blank" rel="noopener">Mở thư mục trên Drive ${icon('external')}</a>`
            : `<p class="panel__muted">${demo ? 'Album mẫu dùng ảnh có sẵn trong ứng dụng.' : 'Chưa có liên kết Drive.'}</p>`}
          <button class="btn btn--ghost btn--compact btn--full" type="button" data-action="refresh" ${demo ? 'disabled' : ''}>
            ${icon('refresh')} Đồng bộ ảnh từ Drive
          </button>
          ${demo ? `<p class="panel__footnote">Album mẫu không đồng bộ với Google Drive.</p>` : '<p class="panel__footnote">Ảnh mới trong thư mục sẽ được thêm, lựa chọn cũ được giữ nguyên.</p>'}
        </section>

        <section class="panel export-panel">
          <header class="panel__head">
            <p class="eyebrow">Dữ liệu</p>
            <h2>Xuất lựa chọn</h2>
          </header>
          <p class="panel__muted">Tải danh sách ${number(selected)} ảnh khách đã chọn, kèm mã hình, thời điểm và ghi chú.</p>
          ${selected > 0
            ? `
              <div class="export-actions">
                <button class="btn btn--ghost btn--compact" type="button" data-copy-selected-codes>${icon('copy')} Sao chép mã</button>
                <a class="btn btn--ghost btn--compact" href="/api/admin/albums/${encodeURIComponent(album.id)}/selections.csv">${icon('download')} Xuất CSV</a>
              </div>
              <a class="export-tool-link" href="/admin/tools?album=${encodeURIComponent(album.id)}">${icon('filter')} Lọc ảnh trên máy tính ${icon('arrowRight')}</a>
            `
            : `<button class="btn btn--ghost btn--full" type="button" disabled>${icon('download')} Chưa có lựa chọn</button>`}
        </section>

        <section class="danger-zone">
          <p class="eyebrow">Quản lý liên kết</p>
          <button class="danger-action" type="button" data-action="rotate">
            <span>${icon('refreshCw')}</span>
            <span><strong>Đổi liên kết chia sẻ</strong><small>Liên kết cũ sẽ ngừng hoạt động</small></span>
            ${icon('chevronRight')}
          </button>
          <button class="danger-action danger-action--delete" type="button" data-action="delete">
            <span>${icon('trash')}</span>
            <span><strong>Xoá album</strong><small>Xoá vĩnh viễn khỏi studio</small></span>
            ${icon('chevronRight')}
          </button>
        </section>
      </aside>
    </div>
  `;
}

// ---------------------------------------------------------------- settings

export function settingsView({ session }) {
  const skeleton = `
    <div class="settings-layout">
      <div class="skeleton skeleton-settings"></div>
      <div class="skeleton skeleton-settings skeleton-settings--side"></div>
    </div>
  `;

  const html = adminShell({
    active: 'settings',
    session,
    content: `
      <div class="admin-page__inner" data-settings-page>
        ${pageHead({
          eyebrow: 'Cấu hình',
          title: 'Cài đặt studio',
          lead: 'Thông tin này xuất hiện trên album của khách và kết nối studio với Google Drive.',
          actions: `<a class="btn btn--ghost" href="/admin">${icon('arrowLeft')} Về tổng quan</a>`
        })}
        <div data-settings-content>${skeleton}</div>
      </div>
    `
  });

  let teardown = () => {};
  let disposed = false;

  return {
    title: 'Cài đặt',
    bodyClass: 'page-admin',
    html,
    mount(root) {
      teardown = mountAdminShell(root, session);
      const page = root.querySelector('[data-settings-page]');
      const holder = root.querySelector('[data-settings-content]');
      let settings = null;
      let users = [];
      let staff = [];

      const paint = () => {
        holder.innerHTML = renderSettings(settings, users, session, staff);
      };

      const paintAccounts = () => {
        const panel = holder.querySelector('[data-accounts-panel]');
        if (panel) panel.innerHTML = renderAccounts(users, session, staff);
      };

      const reloadUsers = async () => {
        try {
          const result = await api('/api/admin/users');
          if (disposed) return;
          users = Array.isArray(result) ? result : [];
          paintAccounts();
          refreshShellIdentity(root, session);
        } catch (error) {
          if (disposed || error.name === 'AbortError') return;
          showToast(error.message || 'Không tải lại được danh sách tài khoản.', 'error');
        }
      };

      const saved = async message => {
        await reloadUsers();
        if (!disposed) showToast(message, 'success');
      };

      const load = async () => {
        holder.innerHTML = skeleton;
        try {
          const [settingsResult, usersResult, staffResult] = await Promise.all([
            api('/api/admin/settings'),
            api('/api/admin/users'),
            api('/api/admin/staff').catch(() => [])
          ]);
          if (disposed) return;
          settings = settingsResult;
          users = Array.isArray(usersResult) ? usersResult : [];
          staff = Array.isArray(staffResult) ? staffResult : [];
          paint();
        } catch (error) {
          if (disposed || error.name === 'AbortError') return;
          holder.innerHTML = `
            <section class="panel panel--pad panel--error">
              ${errorInline(error.message || 'Không tải được cài đặt.', error.code)}
              <button class="btn btn--ghost" type="button" data-retry>${icon('refresh')} Thử lại</button>
            </section>
          `;
        }
      };

      const toggleUserActive = async user => {
        const nextActive = !user.isActive;
        const ok = await confirmDialog({
          title: nextActive ? 'Kích hoạt lại tài khoản?' : 'Tạm khoá tài khoản?',
          message: nextActive
            ? `${user.displayName} sẽ có thể đăng nhập lại bằng tài khoản @${user.username}.`
            : `${user.displayName} sẽ bị đăng xuất khỏi phiên hiện tại và không thể đăng nhập cho đến khi được kích hoạt lại.`,
          confirmLabel: nextActive ? 'Kích hoạt' : 'Tạm khoá',
          danger: !nextActive
        });
        if (!ok || disposed) return;

        try {
          await api(`/api/admin/users/${encodeURIComponent(user.id)}`, {
            method: 'PUT',
            body: { isActive: nextActive }
          });
          if (disposed) return;
          await reloadUsers();
          if (!disposed) {
            showToast(
              nextActive ? `Đã kích hoạt ${user.displayName}.` : `Đã tạm khoá ${user.displayName}.`,
              'success'
            );
          }
        } catch (error) {
          if (disposed || error.name === 'AbortError') return;
          showToast(error.message || 'Chưa cập nhật được tài khoản.', 'error');
        }
      };

      page.addEventListener('submit', async event => {
        const form = event.target.closest('[data-settings-form]');
        if (!form) return;
        event.preventDefault();
        clearFieldErrors(form);
        const messageRoot = form.querySelector('[data-settings-message]');
        messageRoot.innerHTML = '';
        const submit = form.querySelector('[data-settings-submit]');
        const studioName = form.elements.namedItem('studioName');
        const contactEmail = form.elements.namedItem('contactEmail');
        const apiKey = form.elements.namedItem('googleApiKey');
        const clearKey = form.elements.namedItem('clearGoogleApiKey');

        if (!studioName.value.trim()) {
          fieldError(studioName, 'Tên studio không được để trống.');
          return;
        }

        if (contactEmail.value.trim() && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(contactEmail.value.trim())) {
          fieldError(contactEmail, 'Email liên hệ chưa đúng định dạng.');
          return;
        }

        setButtonLoading(submit, true, 'Đang lưu…');
        try {
          const result = await api('/api/admin/settings', {
            method: 'PUT',
            body: {
              studioName: studioName.value.trim(),
              contactEmail: contactEmail.value.trim(),
              googleApiKey: apiKey.value.trim() || null,
              clearGoogleApiKey: Boolean(clearKey?.checked)
            }
          });
          if (disposed) return;
          settings = result;
          if (session) {
            session.studioName = result.studioName;
            notifySession(session);
          }
          paint();
          const preview = root.querySelector('[data-studio-name-preview]');
          if (preview) preview.textContent = result.studioName;
          showToast(`Đã lưu cài đặt studio “${result.studioName}”.`, 'success');
        } catch (error) {
          if (disposed || error.name === 'AbortError') return;
          setButtonLoading(submit, false);
          messageRoot.innerHTML = errorBlock(error.message || 'Chưa lưu được cài đặt.', error.code);
          messageRoot.scrollIntoView({ behavior: 'smooth', block: 'center' });
        }
      });

      page.addEventListener('click', async event => {
        if (event.target.closest('[data-retry]')) {
          load();
          return;
        }

        const toggle = event.target.closest('[data-toggle-api-key]');
        if (toggle) {
          const input = page.querySelector('[data-api-key-input]');
          if (!input) return;
          const showing = input.type === 'text';
          input.type = showing ? 'password' : 'text';
          toggle.innerHTML = showing ? icon('eye') : icon('eyeOff');
          toggle.setAttribute('aria-label', showing ? 'Hiện API key' : 'Ẩn API key');
          input.focus();
          return;
        }

        if (event.target.closest('[data-action="user-create"]')) {
          openUserForm({ session, onSaved: saved, staff });
          return;
        }

        const editButton = event.target.closest('[data-action="user-edit"]');
        if (editButton) {
          const user = users.find(item => item.id === editButton.dataset.userId);
          if (user) openUserForm({ user, session, onSaved: saved, staff });
          return;
        }

        const passwordButton = event.target.closest('[data-action="user-password"]');
        if (passwordButton) {
          const user = users.find(item => item.id === passwordButton.dataset.userId);
          if (user) openPasswordForm({ user, onSaved: saved });
          return;
        }

        const toggleButton = event.target.closest('[data-action="user-toggle"]');
        if (toggleButton) {
          const user = users.find(item => item.id === toggleButton.dataset.userId);
          if (user) await toggleUserActive(user);
        }
      });

      page.addEventListener('change', async event => {
        const input = event.target.closest('[data-import-file]');
        if (!input) return;
        const file = input.files?.[0];
        input.value = '';
        if (!file) return;
        if (file.size > 20 * 1024 * 1024) {
          showToast('Tệp JSON quá lớn. Giới hạn 20 MB.', 'error');
          return;
        }
        let json = '';
        try {
          json = await file.text();
        } catch {
          showToast('Không đọc được tệp. Hãy thử lại.', 'error');
          return;
        }
        openImportPreview({
          json,
          fileName: file.name,
          onImported: async message => {
            await load();
            if (!disposed) showToast(message, 'success');
          }
        });
      });

      load();
    },
    cleanup() {
      disposed = true;
      teardown();
    }
  };
}

function refreshShellIdentity(root, session) {
  const user = session?.user;
  if (!root || !user) return;
  const account = root.querySelector('.admin-account');
  if (!account) return;
  const name = account.querySelector('.admin-account__copy strong');
  if (name) name.textContent = user.displayName;
  const role = account.querySelector('.admin-account__copy small');
  if (role) role.textContent = user.role === 'admin' ? 'Quản trị viên' : 'Nhân viên';
  const avatar = account.querySelector('.avatar');
  if (avatar) avatar.textContent = initials(user.displayName);
  account.setAttribute(
    'aria-label',
    user.role === 'admin' ? 'Cài đặt tài khoản studio' : 'Tài khoản của bạn'
  );
}

// ------------------------------------------------------------- CRM import

function importCount({ label, value, iconName, tone }) {
  return `
    <div class="import-count import-count--${tone}">
      <span class="import-count__icon">${icon(iconName)}</span>
      <strong>${number(value)}</strong>
      <span class="import-count__label">${escapeHtml(label)}</span>
    </div>
  `;
}

function importPreviewMarkup(result) {
  const warnings = Array.isArray(result.warnings) ? result.warnings : [];
  const total = Number(result.customers || 0) + Number(result.packages || 0) +
    Number(result.staff || 0) + Number(result.bookings || 0) +
    Number(result.orders || 0) + Number(result.payments || 0) + Number(result.contracts || 0) +
    Number(result.expenses || 0) + Number(result.payrolls || 0) + Number(result.attendance || 0) +
    Number(result.outfits || 0) + Number(result.concepts || 0) + Number(result.loans || 0) +
    Number(result.notifications || 0);
  return `
    <div class="import-summary">
      <p class="import-summary__lead">
        ${icon('checkCircle')}
        <span>${total > 0
          ? `Sẵn sàng nhập <strong>${number(total)}</strong> mục vào studio.`
          : 'Không có mục CRM mới nào để nhập.'}</span>
      </p>
      <div class="import-counts">
        ${importCount({ label: 'Khách hàng', value: result.customers, iconName: 'users', tone: 'clay' })}
        ${importCount({ label: 'Gói dịch vụ', value: result.packages, iconName: 'tag', tone: 'sand' })}
        ${importCount({ label: 'Nhân sự', value: result.staff, iconName: 'camera', tone: 'olive' })}
        ${importCount({ label: 'Lịch chụp', value: result.bookings, iconName: 'calendar', tone: 'ink' })}
        ${importCount({ label: 'Đơn hàng', value: result.orders, iconName: 'receipt', tone: 'clay' })}
        ${importCount({ label: 'Thanh toán', value: result.payments, iconName: 'wallet', tone: 'sand' })}
        ${importCount({ label: 'Hợp đồng', value: result.contracts, iconName: 'file', tone: 'olive' })}
        ${importCount({ label: 'Chi phí', value: result.expenses, iconName: 'receipt', tone: 'sand' })}
        ${importCount({ label: 'Bảng lương', value: result.payrolls, iconName: 'credit', tone: 'ink' })}
        ${importCount({ label: 'Chấm công', value: result.attendance, iconName: 'clock', tone: 'clay' })}
        ${importCount({ label: 'Trang phục', value: result.outfits, iconName: 'tag', tone: 'olive' })}
        ${importCount({ label: 'Concept', value: result.concepts, iconName: 'palette', tone: 'rose' })}
        ${importCount({ label: 'Cho thuê', value: result.loans, iconName: 'share', tone: 'sand' })}
        ${importCount({ label: 'Thông báo', value: result.notifications, iconName: 'bell', tone: 'ink' })}
      </div>
      ${result.skipped > 0 ? `
        <p class="import-skipped">${icon('alertTriangle')} ${number(result.skipped)} mục bị bỏ qua (trùng mã hoặc chưa hỗ trợ).</p>
      ` : ''}
    </div>
    ${warnings.length ? `
      <section class="import-warnings" aria-label="Cảnh báo khi nhập">
        <h3>${icon('alertCircle')} Cảnh báo (${number(warnings.length)})</h3>
        <ul>
          ${warnings.map(warning => `<li>${escapeHtml(warning)}</li>`).join('')}
        </ul>
      </section>
    ` : `
      <p class="import-clean">${icon('shield')} Không phát hiện dữ liệu cần cảnh báo.</p>
    `}
  `;
}

function openImportPreview({ json, fileName = 'crm-backup.json', onImported }) {
  let replaceExisting = false;
  let preview = null;
  let busy = false;
  let disposed = false;

  const modal = createModal(`
    <div class="dialog-card dialog-card--crm dialog-card--wide import-dialog">
      <button class="dialog-close" type="button" data-close-dialog aria-label="Đóng">${icon('x')}</button>
      <header class="task-dialog__head">
        <span class="task-dialog__icon">${icon('drive')}</span>
        <div>
          <p class="eyebrow">Nhập bản sao lưu CRM</p>
          <h2>Xem trước dữ liệu</h2>
          <p class="import-dialog__file">${icon('file')}<span>${escapeHtml(fileName)}</span></p>
        </div>
      </header>
      <div class="import-dialog__body" data-import-body aria-live="polite">
        <div class="crm-skeleton" aria-label="Đang đọc tệp">
          <div class="crm-skeleton__row">
            <span class="skeleton skeleton-avatar"></span>
            <span class="skeleton skeleton-line skeleton-line--lg"></span>
            <span class="skeleton skeleton-line skeleton-line--sm"></span>
          </div>
        </div>
      </div>
      <label class="check-row import-dialog__replace">
        <input type="checkbox" data-import-replace>
        <span class="check-row__box">${icon('check')}</span>
        <span>
          <strong>Thay thế toàn bộ dữ liệu CRM hiện có</strong>
          <small>Xoá khách hàng, gói dịch vụ, nhân sự và lịch chụp hiện tại trước khi nhập. Album và tài khoản được giữ lại.</small>
        </span>
      </label>
      <div data-import-error></div>
      <div class="dialog-actions">
        <button class="btn btn--ghost" type="button" data-close-dialog>Huỷ</button>
        <button class="btn btn--primary" type="button" data-import-confirm disabled>${icon('check')} Nhập vào studio</button>
      </div>
    </div>
  `, { className: 'dialog--crm dialog--wide dialog--import' });

  const root = modal.dialog;
  const body = root.querySelector('[data-import-body]');
  const errorRoot = root.querySelector('[data-import-error]');
  const confirm = root.querySelector('[data-import-confirm]');
  const replaceField = root.querySelector('[data-import-replace]');

  const runPreview = async () => {
    if (busy || disposed) return;
    busy = true;
    errorRoot.innerHTML = '';
    body.innerHTML = `
      <div class="crm-skeleton" aria-label="Đang kiểm tra dữ liệu">
        <div class="crm-skeleton__row">
          <span class="skeleton skeleton-avatar"></span>
          <span class="skeleton skeleton-line skeleton-line--lg"></span>
          <span class="skeleton skeleton-line skeleton-line--sm"></span>
        </div>
        <div class="crm-skeleton__row">
          <span class="skeleton skeleton-avatar"></span>
          <span class="skeleton skeleton-line skeleton-line--lg"></span>
          <span class="skeleton skeleton-line skeleton-line--sm"></span>
        </div>
      </div>
    `;
    confirm.disabled = true;
    try {
      preview = await crmApi.importBackup(json, { dryRun: true, replaceExisting });
      if (disposed) return;
      body.innerHTML = importPreviewMarkup(preview);
      const total = Number(preview.customers || 0) + Number(preview.packages || 0) +
        Number(preview.staff || 0) + Number(preview.bookings || 0) +
        Number(preview.orders || 0) + Number(preview.payments || 0) + Number(preview.contracts || 0);
      confirm.disabled = total <= 0;
    } catch (error) {
      if (disposed || error.name === 'AbortError') return;
      preview = null;
      body.innerHTML = errorInline(error.message || 'Không đọc được tệp CRM.', error.code);
      confirm.disabled = true;
    } finally {
      busy = false;
    }
  };

  replaceField.addEventListener('change', () => {
    replaceExisting = replaceField.checked;
    runPreview();
  });

  confirm.addEventListener('click', async () => {
    if (busy || disposed || confirm.disabled) return;
    busy = true;
    setButtonLoading(confirm, true, 'Đang nhập…');
    errorRoot.innerHTML = '';
    try {
      const result = await crmApi.importBackup(json, { dryRun: false, replaceExisting });
      if (disposed) return;
      modal.close();
      const total = Number(result.customers || 0) + Number(result.packages || 0) +
        Number(result.staff || 0) + Number(result.bookings || 0) +
        Number(result.orders || 0) + Number(result.payments || 0) + Number(result.contracts || 0);
      await onImported?.(
        `Đã nhập ${number(total)} mục CRM (${number(result.customers)} khách hàng, ${number(result.bookings)} lịch chụp, ${number(result.orders)} đơn hàng).`
      );
    } catch (error) {
      if (disposed || error.name === 'AbortError') return;
      busy = false;
      setButtonLoading(confirm, false);
      errorRoot.innerHTML = errorInline(error.message || 'Chưa nhập được dữ liệu CRM.', error.code);
      errorRoot.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
  });

  root.addEventListener('close', () => {
    disposed = true;
  });

  runPreview();
  return modal;
}

const PERMISSION_GROUPS = [
  {
    label: 'Bảng điều khiển',
    items: [
      { key: 'dashboard', label: 'Tổng quan' },
      { key: 'board', label: 'Bảng công việc' }
    ]
  },
  {
    label: 'Album & ảnh',
    items: [
      { key: 'albums', label: 'Album' },
      { key: 'tools', label: 'Lọc ảnh theo mã' }
    ]
  },
  {
    label: 'Khách hàng & lịch chụp',
    items: [
      { key: 'customers', label: 'Khách hàng' },
      { key: 'bookings', label: 'Lịch chụp' },
      { key: 'packages', label: 'Gói dịch vụ' },
      { key: 'staff', label: 'Nhân sự' }
    ]
  },
  {
    label: 'Kinh doanh',
    items: [
      { key: 'orders', label: 'Đơn hàng & thanh toán' },
      { key: 'contracts', label: 'Hợp đồng' },
      { key: 'reports', label: 'Báo cáo doanh thu' }
    ]
  },
  {
    label: 'Vận hành studio',
    items: [
      { key: 'finance', label: 'Tài chính & lương' },
      { key: 'attendance', label: 'Chấm công' },
      { key: 'wardrobe', label: 'Tủ trang phục & concept' },
      { key: 'notifications', label: 'Thông báo' }
    ]
  },
  {
    label: 'Quản trị',
    items: [
      { key: 'audit', label: 'Nhật ký studio' }
    ]
  }
];

const PERMISSION_LABELS = PERMISSION_GROUPS.reduce((map, group) => {
  group.items.forEach(item => { map[item.key] = item.label; });
  return map;
}, {});

function permissionSummary(user) {
  if (user.role === 'admin') return 'Toàn quyền quản trị';
  const permissions = Array.isArray(user.permissions) ? user.permissions : [];
  if (!permissions.length) return 'Chưa được cấp quyền';
  return permissions.map(permission => PERMISSION_LABELS[permission] || permission).join(', ');
}

function renderAccounts(users, session, staff = []) {
  const activeCount = users.filter(user => user.isActive).length;
  const adminCount = users.filter(user => user.isActive && user.role === 'admin').length;
  return `
    <header class="panel__head panel__head--icon">
      <span class="panel__icon">${icon('users')}</span>
      <div>
        <p class="eyebrow">Thành viên</p>
        <h2>Tài khoản studio</h2>
        <p>${number(users.length)} tài khoản · ${number(activeCount)} đang hoạt động · ${number(adminCount)} quản trị viên.</p>
      </div>
      <button class="btn btn--ghost btn--compact" type="button" data-action="user-create">${icon('userPlus')} Thêm tài khoản</button>
    </header>
    <div class="account-list">
      ${users.length
        ? users.map(user => accountRow(user, session, staff)).join('')
        : `<p class="account-list__empty">Chưa có tài khoản nào. Hãy thêm thành viên đầu tiên.</p>`}
    </div>
    <p class="account-list__note">${icon('shield')} Mỗi thao tác tạo, sửa hoặc kéo thẻ sẽ ghi tên người thực hiện vào nhật ký của thẻ.</p>
  `;
}

function accountRow(user, session, staff = []) {
  const isCurrent = user.isCurrent || user.id === session?.user?.id;
  const isAdmin = user.role === 'admin';
  const role = isAdmin ? 'Quản trị viên' : 'Nhân viên';
  const lastLogin = user.lastLoginAt
    ? `Đăng nhập ${formatRelativeTime(user.lastLoginAt)}`
    : 'Chưa đăng nhập lần nào';
  const linkedStaff = staff.find(member => member.id === user.staffId);
  const permissions = permissionSummary(user);
  return `
    <article class="account-row ${user.isActive ? '' : 'is-inactive'}" data-user-row="${escapeAttr(user.id)}">
      <span class="avatar ${isAdmin ? 'avatar--ink' : 'avatar--olive'}" aria-hidden="true">${escapeHtml(initials(user.displayName))}</span>
      <div class="account-row__identity">
        <p class="account-row__name">
          <strong>${escapeHtml(user.displayName)}</strong>
          ${isCurrent ? '<span class="account-row__tag account-row__tag--you">Bạn</span>' : ''}
          ${user.isActive ? '' : '<span class="account-row__tag account-row__tag--locked">Đã tạm khoá</span>'}
          ${user.mustChangePassword ? '<span class="account-row__tag account-row__tag--locked">Cần đổi mật khẩu</span>' : ''}
        </p>
        <p class="account-row__meta"><span>@${escapeHtml(user.username)}</span><span aria-hidden="true">·</span><span>${escapeHtml(lastLogin)}</span></p>
        <p class="account-row__permissions">
          <span>${icon('shield')}${escapeHtml(permissions)}</span>
          ${linkedStaff ? `<span>${icon('camera')}${escapeHtml(linkedStaff.name)}</span>` : ''}
        </p>
      </div>
      <span class="pill ${isAdmin ? 'pill--live' : 'pill--neutral'}">${icon(isAdmin ? 'shield' : 'user')}${role}</span>
      <div class="account-row__actions">
        <button class="btn btn--ghost btn--compact" type="button" data-action="user-edit" data-user-id="${escapeAttr(user.id)}" aria-label="Sửa tài khoản ${escapeAttr(user.displayName)}">${icon('edit')} Sửa</button>
        <button class="btn btn--ghost btn--compact" type="button" data-action="user-password" data-user-id="${escapeAttr(user.id)}" aria-label="Đặt lại mật khẩu cho ${escapeAttr(user.displayName)}">${icon('key')} Mật khẩu</button>
        <button class="btn btn--ghost btn--compact ${user.isActive ? 'account-row__lock' : ''}" type="button" data-action="user-toggle" data-user-id="${escapeAttr(user.id)}" ${isCurrent ? `disabled title="Bạn không thể tự tạm khoá tài khoản đang đăng nhập"` : `aria-label="${user.isActive ? 'Tạm khoá' : 'Kích hoạt'} tài khoản ${escapeAttr(user.displayName)}"`}>${icon(user.isActive ? 'lock' : 'checkCircle')} ${user.isActive ? 'Tạm khoá' : 'Kích hoạt'}</button>
      </div>
    </article>
  `;
}

const USERNAME_PATTERN = /^[a-z0-9][a-z0-9._-]{2,31}$/;

function bindPasswordToggle(dialog) {
  const toggle = dialog.querySelector('[data-toggle-password]');
  const input = dialog.querySelector('input[name="password"]');
  if (!toggle || !input) return;
  toggle.addEventListener('click', () => {
    const showing = input.type === 'text';
    input.type = showing ? 'password' : 'text';
    toggle.innerHTML = showing ? icon('eye') : icon('eyeOff');
    toggle.setAttribute('aria-label', showing ? 'Hiện mật khẩu' : 'Ẩn mật khẩu');
    input.focus();
  });
}

function openUserForm({ user = null, session, onSaved, staff = [] }) {
  const isEdit = Boolean(user);
  const isSelf = Boolean(user?.isCurrent || (user && user.id === session?.user?.id));
  const roleOptions = [
    ['employee', 'Nhân viên'],
    ['admin', 'Quản trị viên']
  ].map(([value, label]) =>
    `<option value="${value}" ${((user?.role || 'employee') === value) ? 'selected' : ''}>${label}</option>`
  ).join('');
  const selectedPermissions = Array.isArray(user?.permissions)
    ? user.permissions
    : ['dashboard', 'albums', 'board', 'tools'];
  const linkableStaff = staff.filter(member => !member.userId || member.userId === user?.id);
  const staffOptions = linkableStaff.map(member => `
    <option value="${escapeAttr(member.id)}" ${member.id === user?.staffId ? 'selected' : ''}>${escapeHtml(member.name)} · ${escapeHtml(member.role || 'Nhân sự')}</option>
  `).join('');
  const permissionGroups = PERMISSION_GROUPS.map(group => `
    <fieldset class="permission-group">
      <legend>${escapeHtml(group.label)}</legend>
      ${group.items.map(item => `
        <label class="permission-option">
          <input type="checkbox" name="permissions" value="${escapeAttr(item.key)}" ${selectedPermissions.includes(item.key) ? 'checked' : ''}>
          <span class="permission-option__box">${icon('check')}</span>
          <span>${escapeHtml(item.label)}</span>
        </label>
      `).join('')}
    </fieldset>
  `).join('');

  const modal = createModal(`
    <div class="dialog-card dialog-card--account dialog-card--wide">
      <button class="dialog-close" type="button" data-close-dialog aria-label="Đóng">${icon('x')}</button>
      <header class="task-dialog__head">
        <span class="task-dialog__icon">${icon(isEdit ? 'user' : 'userPlus')}</span>
        <div>
          <p class="eyebrow">${isEdit ? 'Cập nhật thành viên' : 'Thành viên mới'}</p>
          <h2 id="account-dialog-title">${isEdit ? 'Sửa tài khoản' : 'Thêm tài khoản'}</h2>
        </div>
      </header>
      <form class="account-form account-form--wide" data-user-form data-mode="${isEdit ? 'edit' : 'create'}" ${isEdit ? `data-user-id="${escapeAttr(user.id)}"` : ''} novalidate>
        <div class="account-form__grid">
          <div class="field">
            <label for="account-name">Tên hiển thị <span class="required">*</span></label>
            <input id="account-name" name="displayName" type="text" maxlength="60" value="${escapeAttr(user?.displayName || '')}" placeholder="Ví dụ: Linh Nguyễn" autocomplete="off" required autofocus>
            <p class="field__hint">Tên này xuất hiện trong nhật ký hoạt động.</p>
          </div>
          <div class="field">
            <label for="account-username">Tên đăng nhập <span class="required">*</span></label>
            <div class="input-with-icon">
              <span>${icon('user')}</span>
              <input id="account-username" name="username" type="text" maxlength="32" value="${escapeAttr(user?.username || '')}" placeholder="linh.nguyen" autocomplete="off" autocapitalize="none" spellcheck="false" required>
            </div>
            <p class="field__hint">3–32 ký tự: chữ thường không dấu, số, dấu chấm, gạch ngang hoặc gạch dưới.</p>
          </div>
          <div class="field">
            <label for="account-role">Vai trò</label>
            <select id="account-role" name="role" ${isSelf ? 'disabled' : ''}>${roleOptions}</select>
            <p class="field__hint">${isSelf ? 'Bạn không thể tự đổi vai trò của mình.' : 'Quản trị viên luôn có toàn quyền và quản lý được cài đặt studio.'}</p>
          </div>
          <div class="field">
            <label for="account-staff">Hồ sơ nhân sự liên kết</label>
            <select id="account-staff" name="staffId">
              <option value="">Không liên kết hồ sơ</option>
              ${staffOptions}
            </select>
            <p class="field__hint">Liên kết để lịch chụp và thẻ công việc hiển thị đúng người phụ trách.</p>
          </div>
          ${isEdit ? `
            <label class="check-row account-form__full">
              <input type="checkbox" name="isActive" ${user.isActive ? 'checked' : ''} ${isSelf ? 'disabled' : ''}>
              <span class="check-row__box">${icon('check')}</span>
              <span><strong>Tài khoản đang hoạt động</strong><small>${isSelf ? 'Bạn không thể tự tạm khoá tài khoản đang đăng nhập.' : 'Bỏ chọn để chặn đăng nhập nhưng vẫn giữ lịch sử.'}</small></span>
            </label>
          ` : `
            <div class="field">
              <label for="account-password">Mật khẩu <span class="required">*</span></label>
              <div class="input-with-action">
                <span class="input-with-action__icon">${icon('lock')}</span>
                <input id="account-password" name="password" type="password" minlength="8" maxlength="128" autocomplete="new-password" placeholder="Ít nhất 8 ký tự" required>
                <button class="icon-btn input-with-action__button" type="button" data-toggle-password aria-label="Hiện mật khẩu">${icon('eye')}</button>
              </div>
            </div>
            <div class="field">
              <label for="account-password-confirm">Nhập lại mật khẩu <span class="required">*</span></label>
              <input id="account-password-confirm" name="passwordConfirm" type="password" minlength="8" maxlength="128" autocomplete="new-password" placeholder="Nhập lại mật khẩu" required>
            </div>
          `}
        </div>

        <section class="permission-editor" data-permission-editor aria-labelledby="permission-editor-title">
          <header class="permission-editor__head">
            <div>
              <h3 id="permission-editor-title">${icon('shield')} Quyền truy cập</h3>
              <p>Quản trị viên luôn có toàn quyền. Nhân viên chỉ thấy những phân hệ được chọn.</p>
            </div>
            <span class="permission-editor__badge" data-permission-count></span>
          </header>
          <div class="permission-grid">${permissionGroups}</div>
        </section>

        <label class="check-row account-form__full">
          <input type="checkbox" name="mustChangePassword" ${user ? (user.mustChangePassword ? 'checked' : '') : 'checked'}>
          <span class="check-row__box">${icon('check')}</span>
          <span><strong>Yêu cầu đổi mật khẩu ở lần đăng nhập tới</strong><small>Nên bật khi tạo tài khoản mới hoặc đặt lại mật khẩu tạm.</small></span>
        </label>

        <div data-form-error></div>
        <div class="dialog-actions">
          <button class="btn btn--ghost" type="button" data-close-dialog>Huỷ</button>
          <button class="btn btn--primary" type="submit" data-user-submit>${icon('check')} ${isEdit ? 'Lưu thay đổi' : 'Tạo tài khoản'}</button>
        </div>
      </form>
    </div>
  `, { className: 'dialog--account dialog--account-wide' });

  bindPasswordToggle(modal.dialog);
  const form = modal.dialog.querySelector('[data-user-form]');
  const roleField = form.elements.namedItem('role');
  const editor = form.querySelector('[data-permission-editor]');
  const countBadge = form.querySelector('[data-permission-count]');

  const syncPermissions = () => {
    const isAdminRole = roleField.value === 'admin';
    editor?.classList.toggle('is-disabled', isAdminRole);
    editor?.querySelectorAll('input[name="permissions"]').forEach(input => {
      input.disabled = isAdminRole;
    });
    const checked = form.querySelectorAll('input[name="permissions"]:checked').length;
    if (countBadge) {
      countBadge.textContent = isAdminRole ? 'Toàn quyền' : `${checked} quyền`;
    }
  };

  roleField.addEventListener('change', syncPermissions);
  editor?.addEventListener('change', event => {
    if (event.target.matches('input[name="permissions"]')) syncPermissions();
  });
  syncPermissions();

  form.addEventListener('submit', event => {
    event.preventDefault();
    submitUserForm(event.currentTarget, { session, onSaved });
  });
}

async function submitUserForm(form, { session, onSaved }) {
  const mode = form.dataset.mode;
  const isEdit = mode === 'edit';
  const errorRoot = form.querySelector('[data-form-error]');
  const submit = form.querySelector('[data-user-submit]');
  const displayNameField = form.elements.namedItem('displayName');
  const usernameField = form.elements.namedItem('username');
  const roleField = form.elements.namedItem('role');
  const activeField = form.elements.namedItem('isActive');
  const passwordField = form.elements.namedItem('password');
  const confirmField = form.elements.namedItem('passwordConfirm');
  const staffField = form.elements.namedItem('staffId');
  const mustChangeField = form.elements.namedItem('mustChangePassword');
  const permissions = [...form.querySelectorAll('input[name="permissions"]:checked')].map(input => input.value);
  const staffId = staffField?.value || '';
  const mustChangePassword = Boolean(mustChangeField?.checked);
  const displayName = displayNameField.value.trim();
  const username = usernameField.value.trim().toLowerCase();

  errorRoot.innerHTML = '';
  clearFieldErrors(form);

  if (!displayName) {
    fieldError(displayNameField, 'Tên hiển thị không được để trống.');
    return;
  }

  if (!USERNAME_PATTERN.test(username)) {
    fieldError(usernameField, 'Tên đăng nhập cần 3–32 ký tự, bắt đầu bằng chữ hoặc số và chỉ gồm chữ thường, số, dấu chấm, gạch ngang hoặc gạch dưới.');
    return;
  }

  if (!isEdit) {
    if (passwordField.value.length < 8) {
      fieldError(passwordField, 'Mật khẩu cần ít nhất 8 ký tự.');
      return;
    }

    if (passwordField.value !== confirmField.value) {
      fieldError(confirmField, 'Hai mật khẩu chưa khớp nhau.');
      return;
    }
  }

  setButtonLoading(submit, true, 'Đang lưu…');
  let message = '';
  try {
    if (isEdit) {
      const isSelf = form.elements.namedItem('role')?.disabled === true;
      const result = await api(`/api/admin/users/${encodeURIComponent(form.dataset.userId)}`, {
        method: 'PUT',
        body: {
          displayName,
          username,
          role: isSelf ? null : roleField.value,
          isActive: isSelf ? null : Boolean(activeField?.checked),
          staffId,
          permissions,
          mustChangePassword
        }
      });
      if (result?.isCurrent && session?.user) {
        session.user.displayName = result.displayName;
        session.user.username = result.username;
        session.user.role = result.role;
        session.user.permissions = result.permissions;
        session.user.mustChangePassword = result.mustChangePassword;
      }
      message = `Đã cập nhật tài khoản ${result.displayName}.`;
    } else {
      const result = await api('/api/admin/users', {
        method: 'POST',
        body: {
          displayName,
          username,
          role: roleField.value,
          password: passwordField.value,
          staffId,
          permissions,
          mustChangePassword
        }
      });
      message = `Đã tạo tài khoản cho ${result.displayName}.`;
    }
  } catch (error) {
    if (error.name === 'AbortError') return;
    setButtonLoading(submit, false);
    errorRoot.innerHTML = errorBlock(error.message || 'Chưa lưu được tài khoản.', error.code);
    errorRoot.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    return;
  }

  modalFrom(form)?.close();
  await onSaved(message);
}

function openPasswordForm({ user, onSaved }) {
  const modal = createModal(`
    <div class="dialog-card dialog-card--account">
      <button class="dialog-close" type="button" data-close-dialog aria-label="Đóng">${icon('x')}</button>
      <header class="task-dialog__head">
        <span class="task-dialog__icon">${icon('key')}</span>
        <div>
          <p class="eyebrow">Bảo mật</p>
          <h2 id="account-dialog-title">Đặt lại mật khẩu</h2>
        </div>
      </header>
      <p class="dialog-copy">Mật khẩu mới cho <strong>${escapeHtml(user.displayName)}</strong> (@${escapeHtml(user.username)}). Hãy gửi mật khẩu này cho thành viên qua kênh riêng.</p>
      <form class="account-form" data-password-form data-user-id="${escapeAttr(user.id)}" novalidate>
        <div class="field">
          <label for="reset-password">Mật khẩu mới <span class="required">*</span></label>
          <div class="input-with-action">
            <span class="input-with-action__icon">${icon('lock')}</span>
            <input id="reset-password" name="password" type="password" minlength="8" maxlength="128" autocomplete="new-password" placeholder="Ít nhất 8 ký tự" required autofocus>
            <button class="icon-btn input-with-action__button" type="button" data-toggle-password aria-label="Hiện mật khẩu">${icon('eye')}</button>
          </div>
        </div>
        <div class="field">
          <label for="reset-password-confirm">Nhập lại mật khẩu <span class="required">*</span></label>
          <input id="reset-password-confirm" name="passwordConfirm" type="password" minlength="8" maxlength="128" autocomplete="new-password" placeholder="Nhập lại mật khẩu" required>
        </div>
        <div data-form-error></div>
        <div class="dialog-actions">
          <button class="btn btn--ghost" type="button" data-close-dialog>Huỷ</button>
          <button class="btn btn--primary" type="submit" data-user-submit>${icon('key')} Đặt lại mật khẩu</button>
        </div>
      </form>
    </div>
  `, { className: 'dialog--account' });

  bindPasswordToggle(modal.dialog);
  modal.dialog.querySelector('[data-password-form]').addEventListener('submit', event => {
    event.preventDefault();
    submitPasswordForm(event.currentTarget, { onSaved });
  });
}

async function submitPasswordForm(form, { onSaved }) {
  const errorRoot = form.querySelector('[data-form-error]');
  const submit = form.querySelector('[data-user-submit]');
  const passwordField = form.elements.namedItem('password');
  const confirmField = form.elements.namedItem('passwordConfirm');
  errorRoot.innerHTML = '';
  clearFieldErrors(form);

  if (passwordField.value.length < 8) {
    fieldError(passwordField, 'Mật khẩu cần ít nhất 8 ký tự.');
    return;
  }

  if (passwordField.value !== confirmField.value) {
    fieldError(confirmField, 'Hai mật khẩu chưa khớp nhau.');
    return;
  }

  setButtonLoading(submit, true, 'Đang lưu…');
  try {
    await api(`/api/admin/users/${encodeURIComponent(form.dataset.userId)}/password`, {
      method: 'POST',
      body: { password: passwordField.value }
    });
  } catch (error) {
    if (error.name === 'AbortError') return;
    setButtonLoading(submit, false);
    errorRoot.innerHTML = errorBlock(error.message || 'Chưa đặt lại được mật khẩu.', error.code);
    errorRoot.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
    return;
  }

  modalFrom(form)?.close();
  await onSaved('Đã đặt lại mật khẩu.');
}

function modalFrom(element) {
  const dialog = element.closest('dialog');
  if (!dialog) return null;
  return { close: () => dialog.close() };
}

function renderSettings(settings, users, session, staff = []) {
  const hint = settings.googleKeyHint || '';
  return `
    <div class="settings-layout">
      <div class="settings-main">
        <form data-settings-form novalidate>
          <section class="panel settings-section">
            <header class="panel__head panel__head--icon">
              <span class="panel__icon">${icon('sparkles')}</span>
              <div>
                <p class="eyebrow">Nhận diện</p>
                <h2>Thông tin studio</h2>
                <p>Tên và email này hiển thị ở chân mỗi album khách hàng mở.</p>
              </div>
            </header>
            <div class="form-grid">
              <div class="field field--full">
                <label for="settings-name">Tên studio <span class="required">*</span></label>
                <input id="settings-name" name="studioName" type="text" maxlength="60" value="${escapeAttr(settings.studioName)}" required>
                <p class="field__hint">Tên này hiển thị ở đầu bảng điều khiển và chân mỗi album khách hàng mở.</p>
                <p class="settings-live-name">Tên hiện tại: <strong data-studio-name-preview>${escapeHtml(settings.studioName)}</strong></p>
              </div>
              <div class="field field--full">
                <label for="settings-email">Email liên hệ</label>
                <div class="input-with-icon">
                  <span>${icon('mail')}</span>
                  <input id="settings-email" name="contactEmail" type="email" maxlength="160" value="${escapeAttr(settings.contactEmail)}" placeholder="hello@studio.vn" autocomplete="email">
                </div>
                <p class="field__hint">Khách có thể dùng email này để liên hệ khi cần hỏi thêm về album.</p>
              </div>
            </div>
          </section>

          <section class="panel settings-section">
            <header class="panel__head panel__head--icon">
              <span class="panel__icon panel__icon--drive">${icon('drive')}</span>
              <div>
                <p class="eyebrow">Kết nối ảnh</p>
                <h2>Google Drive API</h2>
                <p>Dùng API key công khai để đọc thư mục ảnh được chia sẻ “Bất kỳ ai có liên kết”.</p>
              </div>
              ${settings.googleConfigured
                ? `<span class="pill pill--live"><span class="pill__dot"></span>Đã kết nối</span>`
                : `<span class="pill pill--warn">${icon('alertCircle')}Chưa kết nối</span>`}
            </header>
            <div class="field">
              <label for="settings-key">Google API key</label>
              <div class="input-with-action">
                <span class="input-with-action__icon">${icon('lock')}</span>
                <input id="settings-key" name="googleApiKey" type="password" maxlength="500" placeholder="${settings.googleConfigured ? `Đã lưu ${escapeAttr(hint)} — nhập key mới để thay` : 'AIzaSy…'}" autocomplete="off" spellcheck="false" data-api-key-input>
                <button class="icon-btn input-with-action__button" type="button" data-toggle-api-key aria-label="Hiện API key">${icon('eye')}</button>
              </div>
              <p class="field__hint">Để trống nếu không muốn thay đổi key hiện tại. Key chỉ được lưu trên máy chủ của studio.</p>
            </div>
            ${settings.googleConfigured ? `
              <label class="check-row">
                <input type="checkbox" name="clearGoogleApiKey">
                <span class="check-row__box">${icon('check')}</span>
                <span><strong>Xoá API key hiện tại</strong><small>Tắt kết nối Drive cho đến khi thêm key mới.</small></span>
              </label>
            ` : ''}
            <div class="settings-status">
              <span class="settings-status__icon ${settings.googleConfigured ? 'is-on' : ''}">${icon(settings.googleConfigured ? 'checkCircle' : 'alertCircle')}</span>
              <p>${settings.googleConfigured
                ? `Drive đã sẵn sàng. Key đang dùng: <strong>${escapeHtml(hint)}</strong>.`
                : 'Chưa có API key. Bạn vẫn xem được album mẫu, nhưng chưa thể tạo album từ Drive.'}</p>
            </div>
          </section>

          <div data-settings-message></div>

          <div class="form-actions form-actions--settings">
            <a class="btn btn--ghost" href="/admin">${icon('arrowLeft')} Quay lại</a>
            <button class="btn btn--primary btn--lg" type="submit" data-settings-submit>${icon('check')} Lưu cài đặt</button>
          </div>
        </form>

        <section class="panel settings-section accounts-panel" data-accounts-panel>
          ${renderAccounts(users, session, staff)}
        </section>

        <section class="panel settings-section import-panel" data-crm-import>
          <header class="panel__head panel__head--icon">
            <span class="panel__icon">${icon('drive')}</span>
            <div>
              <p class="eyebrow">Chuyển dữ liệu</p>
              <h2>Nhập bản sao lưu CRM</h2>
              <p>Đọc tệp JSON xuất từ studio-manager-CRM, xem trước và cảnh báo dữ liệu chưa hỗ trợ trước khi ghi.</p>
            </div>
          </header>
          <div class="import-panel__body">
            <label class="import-drop" data-import-drop>
              ${icon('file')}
              <strong>Chọn tệp JSON CRM</strong>
              <span>Kéo tệp vào đây hoặc bấm để chọn · tối đa 20 MB</span>
              <input type="file" accept="application/json,.json" data-import-file>
            </label>
            <div class="import-panel__notes">
              <p>${icon('checkCircle')} Giữ nguyên mã khách hàng, gói dịch vụ, nhân sự và lịch chụp.</p>
              <p>${icon('alertCircle')} Đơn hàng, thanh toán, hợp đồng… được bỏ qua ở giai đoạn 1 kèm cảnh báo.</p>
              <p>${icon('shield')} Luôn xem trước số lượng và cảnh báo trước khi xác nhận ghi dữ liệu.</p>
            </div>
          </div>
        </section>
      </div>

      <aside class="settings-side">
        <section class="panel guide-card">
          <header class="panel__head">
            <p class="eyebrow">Hướng dẫn nhanh</p>
            <h2>Lấy Google API key</h2>
          </header>
          <ol class="guide-steps">
            <li><span>1</span><p>Mở <a href="https://console.cloud.google.com/projectcreate" target="_blank" rel="noopener">Google Cloud Console</a> và tạo một project.</p></li>
            <li><span>2</span><p>Bật <a href="https://console.cloud.google.com/apis/library/drive.googleapis.com" target="_blank" rel="noopener">Google Drive API</a> cho project.</p></li>
            <li><span>3</span><p>Vào <strong>APIs &amp; Services → Credentials</strong>, chọn <strong>Create credentials → API key</strong>.</p></li>
            <li><span>4</span><p>Nên giới hạn key theo website hoặc IP để tránh bị lạm dụng.</p></li>
            <li><span>5</span><p>Dán key vào ô bên trái và bấm <strong>Lưu cài đặt</strong>.</p></li>
          </ol>
          <div class="guide-note">${icon('folder')} Thư mục ảnh phải được chia sẻ ở chế độ <strong>“Bất kỳ ai có liên kết”</strong>.</div>
        </section>

        <section class="panel access-card">
          <header class="panel__head">
            <p class="eyebrow">Bảo mật</p>
            <h2>Tài khoản &amp; phân quyền</h2>
          </header>
          <p>Quản trị viên tạo tài khoản riêng cho từng thành viên. Mỗi thao tác trên bảng công việc sẽ ghi đúng người thực hiện.</p>
          <p class="access-card__note">${icon('shield')} Chỉ quản trị viên mới xem và sửa được trang cài đặt này.</p>
        </section>

        <section class="side-note side-note--soft">
          <span class="side-note__icon">${icon('quote')}</span>
          <p>Mọi thay đổi về tên studio sẽ xuất hiện ngay trong album kế tiếp khách mở.</p>
        </section>
      </aside>
    </div>
  `;
}

