import { api, crmApi } from './api.js';
import { adminShell, mountAdminShell, pageHead, hasPermission } from './admin.js';
import {
  icon,
  escapeHtml,
  escapeAttr,
  stripDiacritics,
  formatDate,
  formatTimestamp,
  formatRelativeTime,
  initials,
  showToast,
  setButtonLoading,
  createModal,
  confirmDialog,
  emptyState,
  errorInline,
  navigateTo
} from './ui.js';

// --------------------------------------------------------------- shared bits

const CUSTOMER_TYPES = [
  { key: 'individual', label: 'Cá nhân' },
  { key: 'company', label: 'Doanh nghiệp' }
];

const STAFF_ROLES = [
  { key: 'photographer', label: 'Nhiếp ảnh' },
  { key: 'editor', label: 'Hậu kỳ' },
  { key: 'assistant', label: 'Trợ lý' },
  { key: 'makeup', label: 'Trang điểm' },
  { key: 'manager', label: 'Quản lý' },
  { key: 'other', label: 'Khác' }
];

const BOOKING_STATUSES = [
  { key: 'pending', label: 'Chờ xác nhận', tone: 'warn', iconName: 'clock' },
  { key: 'confirmed', label: 'Đã xác nhận', tone: 'live', iconName: 'checkCircle' },
  { key: 'shooting', label: 'Đang chụp', tone: 'clay', iconName: 'camera' },
  { key: 'editing', label: 'Đang chỉnh sửa', tone: 'sand', iconName: 'edit' },
  { key: 'delivered', label: 'Đã giao', tone: 'olive', iconName: 'send' },
  { key: 'cancelled', label: 'Đã huỷ', tone: 'draft', iconName: 'x' }
];

const AUDIT_MODULES = [
  { key: '', label: 'Tất cả phân hệ' },
  { key: 'customers', label: 'Khách hàng' },
  { key: 'packages', label: 'Gói dịch vụ' },
  { key: 'staff', label: 'Nhân sự' },
  { key: 'bookings', label: 'Lịch chụp' },
  { key: 'orders', label: 'Đơn hàng' },
  { key: 'contracts', label: 'Hợp đồng' },
  { key: 'albums', label: 'Album' },
  { key: 'board', label: 'Bảng công việc' },
  { key: 'accounts', label: 'Tài khoản' },
  { key: 'settings', label: 'Cài đặt' },
  { key: 'crm', label: 'Nhập CRM' }
];

export function money(value) {
  return `${Number(value || 0).toLocaleString('vi-VN')} đ`;
}

function hoursLabel(value) {
  const amount = Number(value || 0);
  if (!amount) return 'Chưa đặt';
  return `${amount.toLocaleString('vi-VN', { maximumFractionDigits: 2 })} giờ`;
}

function customerTypeLabel(type) {
  return CUSTOMER_TYPES.find(option => option.key === type)?.label || 'Cá nhân';
}

function staffRoleLabel(role) {
  return STAFF_ROLES.find(option => option.key === role)?.label || (role || 'Nhân sự');
}

function statusMeta(key) {
  return BOOKING_STATUSES.find(option => option.key === key) || BOOKING_STATUSES[0];
}

function statusPill(status) {
  const meta = statusMeta(status);
  return `<span class="pill pill--${meta.tone}">${icon(meta.iconName)}${escapeHtml(meta.label)}</span>`;
}

function matches(query, values) {
  const needle = stripDiacritics(query);
  if (!needle) return true;
  return values.some(value => stripDiacritics(value).includes(needle));
}

export function crmPage({ session, active, eyebrow, title, lead, actions = '', body }) {
  return adminShell({
    active,
    session,
    content: `
      <div class="admin-page__inner crm-page" data-crm-page>
        ${pageHead({ eyebrow, title, lead, actions })}
        ${body}
      </div>
    `
  });
}

export function metric({ label, value, note, tone = 'clay', iconName = 'users' }) {
  return `
    <article class="crm-metric crm-metric--${tone}">
      <span class="crm-metric__icon">${icon(iconName)}</span>
      <div>
        <span class="crm-metric__label">${escapeHtml(label)}</span>
        <strong>${escapeHtml(value)}</strong>
        <span class="crm-metric__note">${escapeHtml(note || '')}</span>
      </div>
    </article>
  `;
}

export function skeletonRows(count = 4) {
  return `
    <div class="crm-skeleton" aria-label="Đang tải dữ liệu">
      ${Array.from({ length: count }, () => `
        <div class="crm-skeleton__row">
          <span class="skeleton skeleton-avatar"></span>
          <span class="skeleton skeleton-line skeleton-line--lg"></span>
          <span class="skeleton skeleton-line skeleton-line--sm"></span>
        </div>
      `).join('')}
    </div>
  `;
}

export function fieldError(input, message) {
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

export function clearFieldErrors(form) {
  form.querySelectorAll('.field.has-error').forEach(field => field.classList.remove('has-error'));
  form.querySelectorAll('.field__error').forEach(note => note.remove());
  form.querySelectorAll('[aria-invalid="true"]').forEach(input => {
    input.removeAttribute('aria-invalid');
    input.removeAttribute('aria-describedby');
  });
}

export function closeModalFrom(element) {
  const dialog = element.closest('dialog');
  if (dialog) dialog.close();
}

export function optionList(options, selected, placeholder = '') {
  const head = placeholder ? `<option value="">${escapeHtml(placeholder)}</option>` : '';
  return head + options.map(option => `
    <option value="${escapeAttr(option.value)}" ${option.value === selected ? 'selected' : ''}>${escapeHtml(option.label)}</option>
  `).join('');
}

export function loadError(error, retryAttr = 'data-retry') {
  return `
    <section class="panel panel--pad panel--error">
      ${errorInline(error.message || 'Không tải được dữ liệu.', error.code)}
      <button class="btn btn--ghost" type="button" ${retryAttr}>${icon('refresh')} Thử lại</button>
    </section>
  `;
}

// ----------------------------------------------------------------- customers

export function customersView({ session }) {
  let teardown = () => {};
  let disposed = false;
  let customers = [];
  let query = '';
  let type = 'all';
  let loading = false;

  const html = crmPage({
    session,
    active: 'customers',
    eyebrow: 'Quan hệ khách hàng',
    title: 'Khách hàng',
    lead: 'Lưu thông tin liên hệ, lịch sử chụp và album của từng khách trong một nơi.',
    actions: `<button class="btn btn--primary" type="button" data-action="customer-create">${icon('userPlus')} Thêm khách hàng</button>`,
    body: `
      <section class="crm-overview" data-customer-stats></section>
      <section class="panel crm-toolbar" aria-label="Tìm và lọc khách hàng">
        <label class="crm-search">
          <span>${icon('search')}</span>
          <input type="search" data-customer-search placeholder="Tìm theo tên, số điện thoại, email…" aria-label="Tìm khách hàng" autocomplete="off">
        </label>
        <div class="segmented crm-segmented" data-customer-filter role="group" aria-label="Lọc loại khách hàng">
          <button class="segmented__item is-active" type="button" data-type="all" aria-pressed="true">Tất cả <span class="segmented__count" data-type-count="all">0</span></button>
          <button class="segmented__item" type="button" data-type="individual" aria-pressed="false">Cá nhân <span class="segmented__count" data-type-count="individual">0</span></button>
          <button class="segmented__item" type="button" data-type="company" aria-pressed="false">Doanh nghiệp <span class="segmented__count" data-type-count="company">0</span></button>
        </div>
        <span class="crm-toolbar__count" data-customer-count></span>
      </section>
      <section class="panel crm-list" data-customer-results aria-live="polite">${skeletonRows()}</section>
    `
  });

  const page = () => document.querySelector('[data-crm-page]');
  const statsRoot = () => page()?.querySelector('[data-customer-stats]');
  const resultsRoot = () => page()?.querySelector('[data-customer-results]');

  const filtered = () => customers.filter(customer => {
    if (type !== 'all' && customer.type !== type) return false;
    return matches(query, [
      customer.name,
      customer.phone,
      customer.email,
      customer.address,
      customer.source,
      customer.notes
    ]);
  });

  function paintStats() {
    const root = statsRoot();
    if (!root) return;
    const active = customers.filter(customer => customer.isActive).length;
    const withBookings = customers.filter(customer => customer.bookingCount > 0).length;
    const archived = customers.length - active;
    root.innerHTML = [
      metric({ label: 'Tổng khách hàng', value: String(customers.length), note: `${active} đang hoạt động`, tone: 'clay', iconName: 'users' }),
      metric({ label: 'Đã có lịch chụp', value: String(withBookings), note: 'khách đã đặt ít nhất một buổi', tone: 'olive', iconName: 'calendar' }),
      metric({ label: 'Đang lưu trữ', value: String(archived), note: 'không hiện trong danh sách hoạt động', tone: 'sand', iconName: 'history' })
    ].join('');
  }

  function paintCounts() {
    const root = page();
    if (!root) return;
    const counts = {
      all: customers.length,
      individual: customers.filter(customer => customer.type === 'individual').length,
      company: customers.filter(customer => customer.type === 'company').length
    };
    Object.entries(counts).forEach(([key, value]) => {
      const node = root.querySelector(`[data-type-count="${key}"]`);
      if (node) node.textContent = String(value);
    });
    const list = filtered();
    const count = root.querySelector('[data-customer-count]');
    if (count) count.textContent = `${list.length} khách hàng`;
  }

  function rowMarkup(customer) {
    const contacts = [
      customer.phone,
      customer.email,
      customer.address
    ].filter(Boolean);
    const lastBooking = customer.lastBookingDate
      ? `Lần chụp gần nhất ${formatDate(String(customer.lastBookingDate).slice(0, 10), true)}`
      : 'Chưa có lịch chụp';
    return `
      <article class="crm-row ${customer.isActive ? '' : 'is-archived'}" data-customer-id="${escapeAttr(customer.id)}">
        <span class="avatar ${customer.type === 'company' ? 'avatar--ink' : 'avatar--olive'}" aria-hidden="true">${escapeHtml(initials(customer.name))}</span>
        <div class="crm-row__main">
          <p class="crm-row__title">
            <strong>${escapeHtml(customer.name)}</strong>
            <span class="pill pill--neutral">${icon(customer.type === 'company' ? 'users' : 'user')}${escapeHtml(customerTypeLabel(customer.type))}</span>
            ${customer.isActive ? '' : `<span class="pill pill--draft">${icon('history')}Lưu trữ</span>`}
          </p>
          <p class="crm-row__meta">${contacts.length ? contacts.map(value => `<span>${escapeHtml(value)}</span>`).join('<span aria-hidden="true">·</span>') : '<span>Chưa có thông tin liên hệ</span>'}</p>
          <p class="crm-row__meta crm-row__meta--soft">
            <span>${icon('calendar')}${customer.bookingCount} lịch chụp</span>
            <span>${icon('albums')}${customer.albumCount} album</span>
            <span>${escapeHtml(lastBooking)}</span>
          </p>
        </div>
        <div class="crm-row__actions">
          <button class="btn btn--ghost btn--compact" type="button" data-action="customer-view" data-id="${escapeAttr(customer.id)}">${icon('eye')} Chi tiết</button>
          <button class="btn btn--ghost btn--compact" type="button" data-action="customer-edit" data-id="${escapeAttr(customer.id)}">${icon('edit')} Sửa</button>
          <button class="btn btn--ghost btn--compact" type="button" data-action="customer-archive" data-id="${escapeAttr(customer.id)}">${icon(customer.isActive ? 'history' : 'refresh')} ${customer.isActive ? 'Lưu trữ' : 'Kích hoạt'}</button>
          <button class="icon-btn crm-row__delete" type="button" data-action="customer-delete" data-id="${escapeAttr(customer.id)}" aria-label="Xoá khách hàng ${escapeAttr(customer.name)}" title="Xoá khách hàng">${icon('trash')}</button>
        </div>
      </article>
    `;
  }

  function paintResults() {
    const root = resultsRoot();
    if (!root) return;
    paintCounts();
    const list = filtered();
    if (!list.length) {
      root.innerHTML = customers.length
        ? emptyState({
            iconName: 'search',
            title: 'Không tìm thấy khách hàng',
            message: 'Thử từ khoá khác hoặc bỏ bộ lọc loại khách hàng.',
            action: '<button class="btn btn--ghost" type="button" data-action="customer-clear-filter">Xoá bộ lọc</button>'
          })
        : emptyState({
            iconName: 'users',
            title: 'Chưa có khách hàng nào',
            message: 'Thêm khách hàng đầu tiên để gắn album và lịch chụp với đúng người.',
            action: '<button class="btn btn--primary" type="button" data-action="customer-create">Thêm khách hàng</button>'
          });
      return;
    }

    root.innerHTML = `
      <div class="crm-list__head">
        <span>${list.length} khách hàng</span>
        <span>${customers.filter(customer => customer.isActive).length} đang hoạt động</span>
      </div>
      <div class="crm-rows">${list.map(rowMarkup).join('')}</div>
    `;
  }

  async function load() {
    if (loading) return;
    loading = true;
    try {
      const result = await crmApi.customers();
      if (disposed) return;
      customers = Array.isArray(result) ? result : [];
      paintStats();
      paintResults();
    } catch (error) {
      if (disposed || error.name === 'AbortError') return;
      const root = resultsRoot();
      if (root) root.innerHTML = loadError(error, 'data-action="customer-retry"');
    } finally {
      loading = false;
    }
  }

  async function reload(message) {
    await load();
    if (!disposed && message) showToast(message, 'success');
  }

  function findCustomer(id) {
    return customers.find(customer => customer.id === id) || null;
  }

  function openForm(customer = null) {
    const isEdit = Boolean(customer);
    const typeOptions = CUSTOMER_TYPES.map(option => `
      <option value="${option.key}" ${(customer?.type || 'individual') === option.key ? 'selected' : ''}>${option.label}</option>
    `).join('');
    const modal = createModal(`
      <div class="dialog-card dialog-card--crm">
        <button class="dialog-close" type="button" data-close-dialog aria-label="Đóng">${icon('x')}</button>
        <header class="task-dialog__head">
          <span class="task-dialog__icon">${icon(isEdit ? 'user' : 'userPlus')}</span>
          <div>
            <p class="eyebrow">${isEdit ? 'Cập nhật hồ sơ' : 'Khách hàng mới'}</p>
            <h2>${isEdit ? 'Sửa khách hàng' : 'Thêm khách hàng'}</h2>
          </div>
        </header>
        <form class="crm-form" data-customer-form novalidate>
          <div class="crm-form__grid">
            <div class="field crm-form__full">
              <label for="customer-name">Tên khách hàng <span class="required">*</span></label>
              <input id="customer-name" name="name" type="text" maxlength="120" value="${escapeAttr(customer?.name || '')}" placeholder="Ví dụ: Linh & Minh" required autofocus>
            </div>
            <div class="field">
              <label for="customer-type">Loại khách</label>
              <select id="customer-type" name="type">${typeOptions}</select>
            </div>
            <div class="field">
              <label for="customer-phone">Số điện thoại</label>
              <input id="customer-phone" name="phone" type="tel" maxlength="40" value="${escapeAttr(customer?.phone || '')}" placeholder="0901 234 567" autocomplete="tel">
            </div>
            <div class="field crm-form__full">
              <label for="customer-email">Email</label>
              <input id="customer-email" name="email" type="email" maxlength="160" value="${escapeAttr(customer?.email || '')}" placeholder="khachhang@email.com" autocomplete="email">
            </div>
            <div class="field crm-form__full">
              <label for="customer-address">Địa chỉ</label>
              <input id="customer-address" name="address" type="text" maxlength="240" value="${escapeAttr(customer?.address || '')}" placeholder="Địa chỉ liên hệ" autocomplete="street-address">
            </div>
            <div class="field crm-form__full">
              <label for="customer-source">Nguồn khách</label>
              <input id="customer-source" name="source" type="text" maxlength="80" value="${escapeAttr(customer?.source || '')}" placeholder="Facebook, giới thiệu, website…">
            </div>
            <div class="field crm-form__full">
              <label for="customer-notes">Ghi chú</label>
              <textarea id="customer-notes" name="notes" maxlength="2000" rows="3" placeholder="Sở thích, yêu cầu riêng, lịch sử trao đổi…">${escapeHtml(customer?.notes || '')}</textarea>
            </div>
            ${isEdit ? `
              <label class="check-row crm-form__full">
                <input type="checkbox" name="isActive" ${customer.isActive ? 'checked' : ''}>
                <span class="check-row__box">${icon('check')}</span>
                <span><strong>Khách hàng đang hoạt động</strong><small>Bỏ chọn để lưu trữ hồ sơ mà vẫn giữ lịch sử.</small></span>
              </label>
            ` : ''}
          </div>
          <div data-form-error></div>
          <div class="dialog-actions">
            <button class="btn btn--ghost" type="button" data-close-dialog>Huỷ</button>
            <button class="btn btn--primary" type="submit" data-submit>${icon('check')} ${isEdit ? 'Lưu thay đổi' : 'Thêm khách hàng'}</button>
          </div>
        </form>
      </div>
    `, { className: 'dialog--crm' });

    const form = modal.dialog.querySelector('[data-customer-form]');
    form.addEventListener('submit', async event => {
      event.preventDefault();
      clearFieldErrors(form);
      const errorRoot = form.querySelector('[data-form-error]');
      errorRoot.innerHTML = '';
      const nameField = form.elements.namedItem('name');
      const emailField = form.elements.namedItem('email');
      const name = nameField.value.trim();
      const email = emailField.value.trim();
      if (!name) {
        fieldError(nameField, 'Tên khách hàng không được để trống.');
        return;
      }
      if (email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
        fieldError(emailField, 'Email chưa đúng định dạng.');
        return;
      }
      const payload = {
        name,
        type: form.elements.namedItem('type').value,
        phone: form.elements.namedItem('phone').value.trim(),
        email,
        address: form.elements.namedItem('address').value.trim(),
        source: form.elements.namedItem('source').value.trim(),
        notes: form.elements.namedItem('notes').value.trim()
      };
      if (isEdit) payload.isActive = Boolean(form.elements.namedItem('isActive')?.checked);
      const submit = form.querySelector('[data-submit]');
      setButtonLoading(submit, true, 'Đang lưu…');
      try {
        if (isEdit) {
          await crmApi.updateCustomer(customer.id, payload);
        } else {
          await crmApi.createCustomer(payload);
        }
      } catch (error) {
        if (error.name === 'AbortError') return;
        setButtonLoading(submit, false);
        errorRoot.innerHTML = errorInline(error.message || 'Chưa lưu được khách hàng.', error.code);
        return;
      }
      closeModalFrom(form);
      await reload(isEdit ? 'Đã cập nhật khách hàng.' : 'Đã thêm khách hàng.');
    });
  }

  async function openDetail(customer) {
    const modal = createModal(`
      <div class="dialog-card dialog-card--crm dialog-card--wide">
        <button class="dialog-close" type="button" data-close-dialog aria-label="Đóng">${icon('x')}</button>
        <header class="task-dialog__head">
          <span class="task-dialog__icon">${icon('user')}</span>
          <div>
            <p class="eyebrow">Hồ sơ khách hàng</p>
            <h2>${escapeHtml(customer.name)}</h2>
          </div>
        </header>
        <div data-detail-body>${skeletonRows(2)}</div>
      </div>
    `, { className: 'dialog--crm dialog--wide' });

    try {
      const detail = await crmApi.customer(customer.id);
      if (disposed) return;
      const body = modal.dialog.querySelector('[data-detail-body]');
      if (!body) return;
      const bookings = Array.isArray(detail.bookings) ? detail.bookings : [];
      const albums = Array.isArray(detail.albums) ? detail.albums : [];
      const profile = detail.customer || customer;
      const contacts = [
        ['Điện thoại', profile.phone],
        ['Email', profile.email],
        ['Địa chỉ', profile.address],
        ['Nguồn', profile.source]
      ].filter(([, value]) => value);
      body.innerHTML = `
        <div class="crm-detail">
          <div class="crm-detail__profile">
            <span class="avatar avatar--olive">${escapeHtml(initials(profile.name))}</span>
            <div>
              <p class="crm-row__title"><strong>${escapeHtml(profile.name)}</strong> <span class="pill pill--neutral">${escapeHtml(customerTypeLabel(profile.type))}</span></p>
              <p class="crm-row__meta">${escapeHtml(profile.phone || profile.email || 'Chưa có thông tin liên hệ')}</p>
            </div>
          </div>
          ${contacts.length ? `
            <dl class="crm-detail__facts">
              ${contacts.map(([label, value]) => `<div><dt>${escapeHtml(label)}</dt><dd>${escapeHtml(value)}</dd></div>`).join('')}
            </dl>
          ` : ''}
          ${profile.notes ? `<div class="crm-detail__note">${icon('alignLeft')}<p>${escapeHtml(profile.notes)}</p></div>` : ''}
          <section class="crm-detail__section">
            <h3>${icon('calendar')} Lịch chụp (${bookings.length})</h3>
            ${bookings.length ? `
              <div class="crm-detail__bookings">
                ${bookings.map(booking => `
                  <article class="crm-mini-row">
                    <div>
                      <strong>${escapeHtml(booking.code)}</strong>
                      <span>${formatDate(booking.date, true)} · ${escapeHtml(booking.startTime)}</span>
                      <small>${escapeHtml(booking.packageName || 'Chưa chọn gói')}${booking.photographerName ? ` · ${escapeHtml(booking.photographerName)}` : ''}</small>
                    </div>
                    ${statusPill(booking.status)}
                  </article>
                `).join('')}
              </div>
            ` : '<p class="crm-detail__empty">Khách chưa có lịch chụp nào.</p>'}
          </section>
          <section class="crm-detail__section">
            <h3>${icon('albums')} Album (${albums.length})</h3>
            ${albums.length ? `
              <div class="crm-detail__albums">
                ${albums.map(album => `
                  <a class="crm-album-link" href="/admin/albums/${encodeURIComponent(album.id)}">
                    <span>${escapeHtml(album.title)}</span>
                    <small>${album.eventDate ? formatDate(album.eventDate, true) : 'Chưa có ngày'} · ${album.photoCount} ảnh</small>
                    ${icon('chevronRight')}
                  </a>
                `).join('')}
              </div>
            ` : '<p class="crm-detail__empty">Chưa có album nào liên kết với khách này.</p>'}
          </section>
          <div class="dialog-actions">
            <button class="btn btn--ghost" type="button" data-close-dialog>Đóng</button>
            <button class="btn btn--primary" type="button" data-detail-edit>${icon('edit')} Sửa hồ sơ</button>
          </div>
        </div>
      `;
      body.querySelector('[data-detail-edit]')?.addEventListener('click', () => {
        modal.close();
        openForm(profile);
      });
    } catch (error) {
      if (disposed || error.name === 'AbortError') return;
      const body = modal.dialog.querySelector('[data-detail-body]');
      if (body) body.innerHTML = errorInline(error.message || 'Không tải được hồ sơ khách hàng.', error.code);
    }
  }

  async function archive(customer) {
    const nextActive = !customer.isActive;
    const ok = await confirmDialog({
      title: nextActive ? 'Kích hoạt lại khách hàng?' : 'Lưu trữ khách hàng?',
      message: nextActive
        ? `${customer.name} sẽ trở lại danh sách khách hàng đang hoạt động.`
        : `${customer.name} sẽ được ẩn khỏi danh sách hoạt động nhưng vẫn giữ lịch sử chụp và album.`,
      confirmLabel: nextActive ? 'Kích hoạt' : 'Lưu trữ',
      danger: !nextActive
    });
    if (!ok || disposed) return;
    try {
      await crmApi.updateCustomer(customer.id, { isActive: nextActive });
      await reload(nextActive ? 'Đã kích hoạt khách hàng.' : 'Đã lưu trữ khách hàng.');
    } catch (error) {
      if (disposed || error.name === 'AbortError') return;
      showToast(error.message || 'Chưa cập nhật được khách hàng.', 'error');
    }
  }

  async function remove(customer) {
    const ok = await confirmDialog({
      title: 'Xoá khách hàng?',
      message: `Hồ sơ của ${customer.name} sẽ bị xoá vĩnh viễn. Chỉ xoá được khi khách không còn lịch chụp hoặc album.`,
      confirmLabel: 'Xoá khách hàng',
      danger: true
    });
    if (!ok || disposed) return;
    try {
      await crmApi.deleteCustomer(customer.id);
      await reload('Đã xoá khách hàng.');
    } catch (error) {
      if (disposed || error.name === 'AbortError') return;
      showToast(error.message || 'Chưa xoá được khách hàng.', 'error');
    }
  }

  return {
    title: 'Khách hàng',
    bodyClass: 'page-admin',
    html,
    mount(root) {
      teardown = mountAdminShell(root, session);
      const container = root.querySelector('[data-crm-page]');
      let searchTimer = null;

      container.addEventListener('click', event => {
        const action = event.target.closest('[data-action]');
        if (action) {
          const id = action.dataset.id;
          if (action.dataset.action === 'customer-create') {
            openForm();
            return;
          }
          if (action.dataset.action === 'customer-clear-filter') {
            query = '';
            type = 'all';
            const input = container.querySelector('[data-customer-search]');
            if (input) input.value = '';
            container.querySelectorAll('[data-customer-filter] .segmented__item').forEach(button => {
              const active = button.dataset.type === 'all';
              button.classList.toggle('is-active', active);
              button.setAttribute('aria-pressed', String(active));
            });
            paintResults();
            return;
          }
          if (action.dataset.action === 'customer-retry') {
            load();
            return;
          }
          const customer = findCustomer(id);
          if (!customer) return;
          if (action.dataset.action === 'customer-view') openDetail(customer);
          if (action.dataset.action === 'customer-edit') openForm(customer);
          if (action.dataset.action === 'customer-archive') archive(customer);
          if (action.dataset.action === 'customer-delete') remove(customer);
          return;
        }
        const filter = event.target.closest('[data-type]');
        if (filter) {
          type = filter.dataset.type;
          container.querySelectorAll('[data-customer-filter] .segmented__item').forEach(button => {
            const active = button === filter;
            button.classList.toggle('is-active', active);
            button.setAttribute('aria-pressed', String(active));
          });
          paintResults();
        }
      });

      container.addEventListener('input', event => {
        if (!event.target.matches('[data-customer-search]')) return;
        query = event.target.value;
        window.clearTimeout(searchTimer);
        searchTimer = window.setTimeout(paintResults, 140);
      });

      load();
    },
    cleanup() {
      disposed = true;
      teardown();
    }
  };
}

// ------------------------------------------------------------------ packages

export function packagesView({ session }) {
  let teardown = () => {};
  let disposed = false;
  let packages = [];
  let query = '';
  let showArchived = false;
  let loading = false;

  const html = crmPage({
    session,
    active: 'packages',
    eyebrow: 'Danh mục dịch vụ',
    title: 'Gói Chụp',
    lead: 'Chuẩn hoá giá, thời lượng và quyền lợi để mỗi lịch chụp được báo giá nhất quán.',
    actions: `<button class="btn btn--primary" type="button" data-action="package-create">${icon('plus')} Thêm gói dịch vụ</button>`,
    body: `
      <section class="crm-overview" data-package-stats></section>
      <section class="panel crm-toolbar" aria-label="Tìm gói dịch vụ">
        <label class="crm-search">
          <span>${icon('search')}</span>
          <input type="search" data-package-search placeholder="Tìm theo tên gói, danh mục, quyền lợi…" aria-label="Tìm gói dịch vụ" autocomplete="off">
        </label>
        <label class="check-row crm-toolbar__check">
          <input type="checkbox" data-package-archived>
          <span class="check-row__box">${icon('check')}</span>
          <span><strong>Hiện gói đã ngừng bán</strong></span>
        </label>
        <span class="crm-toolbar__count" data-package-count></span>
      </section>
      <section class="crm-results" data-package-results aria-live="polite">${skeletonRows()}</section>
    `
  });

  const page = () => document.querySelector('[data-crm-page]');
  const resultsRoot = () => page()?.querySelector('[data-package-results]');

  const filtered = () => packages.filter(packageItem => {
    if (!showArchived && !packageItem.isActive) return false;
    return matches(query, [packageItem.name, packageItem.category, packageItem.includes, packageItem.notes]);
  });

  function paintStats() {
    const root = page()?.querySelector('[data-package-stats]');
    if (!root) return;
    const active = packages.filter(item => item.isActive).length;
    const booked = packages.filter(item => item.bookingCount > 0).length;
    const average = active
      ? Math.round(packages.filter(item => item.isActive).reduce((sum, item) => sum + Number(item.price || 0), 0) / active)
      : 0;
    root.innerHTML = [
      metric({ label: 'Gói đang bán', value: String(active), note: `${packages.length} gói trong danh mục`, tone: 'clay', iconName: 'tag' }),
      metric({ label: 'Giá trung bình', value: money(average), note: 'trên các gói đang bán', tone: 'sand', iconName: 'credit' }),
      metric({ label: 'Đã dùng trong lịch', value: String(booked), note: 'gói đã gắn với ít nhất một buổi chụp', tone: 'olive', iconName: 'calendar' })
    ].join('');
  }

  function cardMarkup(packageItem) {
    return `
      <article class="crm-card ${packageItem.isActive ? '' : 'is-archived'}" data-package-id="${escapeAttr(packageItem.id)}">
        <header class="crm-card__head">
          <div>
            <p class="crm-card__kicker">${escapeHtml(packageItem.category || 'Chưa phân loại')}</p>
            <h2>${escapeHtml(packageItem.name)}</h2>
          </div>
          ${packageItem.isActive
            ? `<span class="pill pill--live"><span class="pill__dot"></span>Đang bán</span>`
            : `<span class="pill pill--draft">${icon('history')}Ngừng bán</span>`}
        </header>
        <div class="crm-card__price">
          <strong>${money(packageItem.price)}</strong>
          <span>${icon('clock')}${hoursLabel(packageItem.hours)}</span>
        </div>
        ${packageItem.includes ? `<p class="crm-card__includes">${escapeHtml(packageItem.includes)}</p>` : '<p class="crm-card__includes crm-card__includes--empty">Chưa ghi quyền lợi chi tiết.</p>'}
        ${packageItem.notes ? `<p class="crm-card__note">${escapeHtml(packageItem.notes)}</p>` : ''}
        <footer class="crm-card__foot">
          <span>${icon('calendar')}${packageItem.bookingCount} lịch chụp</span>
          <div class="crm-card__actions">
            <button class="btn btn--ghost btn--compact" type="button" data-action="package-edit" data-id="${escapeAttr(packageItem.id)}">${icon('edit')} Sửa</button>
            <button class="btn btn--ghost btn--compact" type="button" data-action="package-toggle" data-id="${escapeAttr(packageItem.id)}">${icon(packageItem.isActive ? 'history' : 'refresh')} ${packageItem.isActive ? 'Ngừng bán' : 'Mở bán'}</button>
            <button class="icon-btn crm-row__delete" type="button" data-action="package-delete" data-id="${escapeAttr(packageItem.id)}" aria-label="Xoá gói ${escapeAttr(packageItem.name)}" title="Xoá gói">${icon('trash')}</button>
          </div>
        </footer>
      </article>
    `;
  }

  function paintResults() {
    const root = resultsRoot();
    if (!root) return;
    const list = filtered();
    const count = page()?.querySelector('[data-package-count]');
    if (count) count.textContent = `${list.length} gói dịch vụ`;
    if (!list.length) {
      root.innerHTML = emptyState({
        iconName: 'tag',
        title: packages.length ? 'Không tìm thấy gói phù hợp' : 'Chưa có gói dịch vụ nào',
        message: packages.length
          ? 'Thử từ khoá khác hoặc bật hiển thị gói đã ngừng bán.'
          : 'Tạo gói đầu tiên để lịch chụp có sẵn giá và thời lượng.',
        action: packages.length
          ? ''
          : '<button class="btn btn--primary" type="button" data-action="package-create">Thêm gói dịch vụ</button>'
      });
      return;
    }
    root.innerHTML = `<div class="crm-card-grid">${list.map(cardMarkup).join('')}</div>`;
  }

  async function load() {
    if (loading) return;
    loading = true;
    try {
      const result = await crmApi.packages();
      if (disposed) return;
      packages = Array.isArray(result) ? result : [];
      paintStats();
      paintResults();
    } catch (error) {
      if (disposed || error.name === 'AbortError') return;
      const root = resultsRoot();
      if (root) root.innerHTML = loadError(error, 'data-action="package-retry"');
    } finally {
      loading = false;
    }
  }

  async function reload(message) {
    await load();
    if (!disposed && message) showToast(message, 'success');
  }

  function findPackage(id) {
    return packages.find(item => item.id === id) || null;
  }

  function openForm(packageItem = null) {
    const isEdit = Boolean(packageItem);
    const modal = createModal(`
      <div class="dialog-card dialog-card--crm">
        <button class="dialog-close" type="button" data-close-dialog aria-label="Đóng">${icon('x')}</button>
        <header class="task-dialog__head">
          <span class="task-dialog__icon">${icon('tag')}</span>
          <div>
            <p class="eyebrow">${isEdit ? 'Cập nhật danh mục' : 'Gói dịch vụ mới'}</p>
            <h2>${isEdit ? 'Sửa gói dịch vụ' : 'Thêm gói dịch vụ'}</h2>
          </div>
        </header>
        <form class="crm-form" data-package-form novalidate>
          <div class="crm-form__grid">
            <div class="field crm-form__full">
              <label for="package-name">Tên gói <span class="required">*</span></label>
              <input id="package-name" name="name" type="text" maxlength="120" value="${escapeAttr(packageItem?.name || '')}" placeholder="Ví dụ: Cưới trọn ngày" required autofocus>
            </div>
            <div class="field">
              <label for="package-category">Danh mục</label>
              <input id="package-category" name="category" type="text" maxlength="60" value="${escapeAttr(packageItem?.category || '')}" placeholder="Cưới, portrait, gia đình…">
            </div>
            <div class="field">
              <label for="package-price">Giá (đ)</label>
              <input id="package-price" name="price" type="number" min="0" step="1000" value="${Number(packageItem?.price || 0)}">
            </div>
            <div class="field">
              <label for="package-hours">Thời lượng (giờ)</label>
              <input id="package-hours" name="hours" type="number" min="0" max="1000" step="0.5" value="${Number(packageItem?.hours || 0)}">
            </div>
            <div class="field crm-form__full">
              <label for="package-includes">Quyền lợi bao gồm</label>
              <textarea id="package-includes" name="includes" maxlength="4000" rows="3" placeholder="Số ảnh, thời gian giao, concept, trang phục…">${escapeHtml(packageItem?.includes || '')}</textarea>
            </div>
            <div class="field crm-form__full">
              <label for="package-notes">Ghi chú nội bộ</label>
              <textarea id="package-notes" name="notes" maxlength="2000" rows="2" placeholder="Lưu ý khi tư vấn hoặc báo giá…">${escapeHtml(packageItem?.notes || '')}</textarea>
            </div>
            ${isEdit ? `
              <label class="check-row crm-form__full">
                <input type="checkbox" name="isActive" ${packageItem.isActive ? 'checked' : ''}>
                <span class="check-row__box">${icon('check')}</span>
                <span><strong>Gói đang được bán</strong><small>Bỏ chọn để ngừng bán mà vẫn giữ lịch sử.</small></span>
              </label>
            ` : ''}
          </div>
          <div data-form-error></div>
          <div class="dialog-actions">
            <button class="btn btn--ghost" type="button" data-close-dialog>Huỷ</button>
            <button class="btn btn--primary" type="submit" data-submit>${icon('check')} ${isEdit ? 'Lưu thay đổi' : 'Thêm gói'}</button>
          </div>
        </form>
      </div>
    `, { className: 'dialog--crm' });

    const form = modal.dialog.querySelector('[data-package-form]');
    form.addEventListener('submit', async event => {
      event.preventDefault();
      clearFieldErrors(form);
      const errorRoot = form.querySelector('[data-form-error]');
      errorRoot.innerHTML = '';
      const nameField = form.elements.namedItem('name');
      const name = nameField.value.trim();
      if (!name) {
        fieldError(nameField, 'Tên gói không được để trống.');
        return;
      }
      const price = Number(form.elements.namedItem('price').value || 0);
      const hours = Number(form.elements.namedItem('hours').value || 0);
      if (!Number.isFinite(price) || price < 0) {
        fieldError(form.elements.namedItem('price'), 'Giá không hợp lệ.');
        return;
      }
      if (!Number.isFinite(hours) || hours < 0) {
        fieldError(form.elements.namedItem('hours'), 'Thời lượng không hợp lệ.');
        return;
      }
      const payload = {
        name,
        category: form.elements.namedItem('category').value.trim(),
        price,
        hours,
        includes: form.elements.namedItem('includes').value.trim(),
        notes: form.elements.namedItem('notes').value.trim()
      };
      if (isEdit) payload.isActive = Boolean(form.elements.namedItem('isActive')?.checked);
      const submit = form.querySelector('[data-submit]');
      setButtonLoading(submit, true, 'Đang lưu…');
      try {
        if (isEdit) {
          await crmApi.updatePackage(packageItem.id, payload);
        } else {
          await crmApi.createPackage(payload);
        }
      } catch (error) {
        if (error.name === 'AbortError') return;
        setButtonLoading(submit, false);
        errorRoot.innerHTML = errorInline(error.message || 'Chưa lưu được gói dịch vụ.', error.code);
        return;
      }
      closeModalFrom(form);
      await reload(isEdit ? 'Đã cập nhật gói dịch vụ.' : 'Đã thêm gói dịch vụ.');
    });
  }

  async function toggle(packageItem) {
    const nextActive = !packageItem.isActive;
    try {
      await crmApi.updatePackage(packageItem.id, { isActive: nextActive });
      await reload(nextActive ? 'Đã mở bán gói dịch vụ.' : 'Đã ngừng bán gói dịch vụ.');
    } catch (error) {
      if (disposed || error.name === 'AbortError') return;
      showToast(error.message || 'Chưa cập nhật được gói dịch vụ.', 'error');
    }
  }

  async function remove(packageItem) {
    const ok = await confirmDialog({
      title: 'Xoá gói dịch vụ?',
      message: `Gói “${packageItem.name}” sẽ bị xoá vĩnh viễn. Chỉ xoá được khi gói chưa được dùng trong lịch chụp.`,
      confirmLabel: 'Xoá gói',
      danger: true
    });
    if (!ok || disposed) return;
    try {
      await crmApi.deletePackage(packageItem.id);
      await reload('Đã xoá gói dịch vụ.');
    } catch (error) {
      if (disposed || error.name === 'AbortError') return;
      showToast(error.message || 'Chưa xoá được gói dịch vụ.', 'error');
    }
  }

  return {
    title: 'Gói dịch vụ',
    bodyClass: 'page-admin',
    html,
    mount(root) {
      teardown = mountAdminShell(root, session);
      const container = root.querySelector('[data-crm-page]');
      let searchTimer = null;

      container.addEventListener('click', event => {
        const action = event.target.closest('[data-action]');
        if (!action) return;
        if (action.dataset.action === 'package-create') {
          openForm();
          return;
        }
        if (action.dataset.action === 'package-retry') {
          load();
          return;
        }
        const packageItem = findPackage(action.dataset.id);
        if (!packageItem) return;
        if (action.dataset.action === 'package-edit') openForm(packageItem);
        if (action.dataset.action === 'package-toggle') toggle(packageItem);
        if (action.dataset.action === 'package-delete') remove(packageItem);
      });

      container.addEventListener('input', event => {
        if (event.target.matches('[data-package-search]')) {
          query = event.target.value;
          window.clearTimeout(searchTimer);
          searchTimer = window.setTimeout(paintResults, 140);
          return;
        }
        if (event.target.matches('[data-package-archived]')) {
          showArchived = event.target.checked;
          paintResults();
        }
      });

      load();
    },
    cleanup() {
      disposed = true;
      teardown();
    }
  };
}

// --------------------------------------------------------------------- staff

export function staffView({ session }) {
  let teardown = () => {};
  let disposed = false;
  let staff = [];
  let users = [];
  let query = '';
  let showInactive = false;
  let loading = false;

  const html = crmPage({
    session,
    active: 'staff',
    eyebrow: 'Đội ngũ studio',
    title: 'Nhân sự',
    lead: 'Quản lý người phụ trách, liên kết tài khoản đăng nhập và thông tin lương, hoa hồng.',
    actions: `<button class="btn btn--primary" type="button" data-action="staff-create">${icon('userPlus')} Thêm nhân sự</button>`,
    body: `
      <section class="crm-overview" data-staff-stats></section>
      <section class="panel crm-toolbar" aria-label="Tìm nhân sự">
        <label class="crm-search">
          <span>${icon('search')}</span>
          <input type="search" data-staff-search placeholder="Tìm theo tên, vai trò, điện thoại…" aria-label="Tìm nhân sự" autocomplete="off">
        </label>
        <label class="check-row crm-toolbar__check">
          <input type="checkbox" data-staff-inactive>
          <span class="check-row__box">${icon('check')}</span>
          <span><strong>Hiện nhân sự đã nghỉ</strong></span>
        </label>
        <span class="crm-toolbar__count" data-staff-count></span>
      </section>
      <section class="panel crm-list" data-staff-results aria-live="polite">${skeletonRows()}</section>
    `
  });

  const page = () => document.querySelector('[data-crm-page]');
  const resultsRoot = () => page()?.querySelector('[data-staff-results]');

  const filtered = () => staff.filter(member => {
    if (!showInactive && !member.isActive) return false;
    return matches(query, [member.name, member.role, member.phone, member.email, member.notes, member.userName]);
  });

  function paintStats() {
    const root = page()?.querySelector('[data-staff-stats]');
    if (!root) return;
    const active = staff.filter(member => member.isActive).length;
    const linked = staff.filter(member => member.userId).length;
    const photographers = staff.filter(member => member.isActive && member.role === 'photographer').length;
    root.innerHTML = [
      metric({ label: 'Đang làm việc', value: String(active), note: `${staff.length} hồ sơ nhân sự`, tone: 'clay', iconName: 'users' }),
      metric({ label: 'Nhiếp ảnh', value: String(photographers), note: 'có thể nhận lịch chụp', tone: 'olive', iconName: 'camera' }),
      metric({ label: 'Đã liên kết tài khoản', value: String(linked), note: 'đăng nhập bằng tài khoản riêng', tone: 'sand', iconName: 'shield' })
    ].join('');
  }

  function rowMarkup(member) {
    const contacts = [member.phone, member.email].filter(Boolean);
    return `
      <article class="crm-row ${member.isActive ? '' : 'is-archived'}" data-staff-id="${escapeAttr(member.id)}">
        <span class="avatar ${member.isActive ? 'avatar--olive' : 'avatar--sand'}" aria-hidden="true">${escapeHtml(initials(member.name))}</span>
        <div class="crm-row__main">
          <p class="crm-row__title">
            <strong>${escapeHtml(member.name)}</strong>
            <span class="pill pill--neutral">${icon('camera')}${escapeHtml(staffRoleLabel(member.role))}</span>
            ${member.isActive ? '' : `<span class="pill pill--draft">${icon('history')}Đã nghỉ</span>`}
          </p>
          <p class="crm-row__meta">${contacts.length ? contacts.map(value => `<span>${escapeHtml(value)}</span>`).join('<span aria-hidden="true">·</span>') : '<span>Chưa có thông tin liên hệ</span>'}</p>
          <p class="crm-row__meta crm-row__meta--soft">
            <span>${icon('calendar')}${member.bookingCount} lịch chụp</span>
            <span>${icon('credit')}${member.payMode === 'hourly' ? `${money(member.hourlyRate)}/giờ` : money(member.baseSalary)}</span>
            <span>${icon('sparkles')}${Number(member.commissionRate || 0).toLocaleString('vi-VN')}% hoa hồng</span>
            <span>${icon('clock')}${member.shift === 'ca1' ? 'Ca 1' : member.shift === 'ca2' ? 'Ca 2' : 'Ca thường'}</span>
            <span>${icon('shield')}${member.userName ? `@${escapeHtml(member.userName)}` : 'Chưa liên kết tài khoản'}</span>
          </p>
        </div>
        <div class="crm-row__actions">
          <button class="btn btn--ghost btn--compact" type="button" data-action="staff-edit" data-id="${escapeAttr(member.id)}">${icon('edit')} Sửa</button>
          <button class="btn btn--ghost btn--compact" type="button" data-action="staff-toggle" data-id="${escapeAttr(member.id)}">${icon(member.isActive ? 'history' : 'refresh')} ${member.isActive ? 'Đánh dấu nghỉ' : 'Kích hoạt'}</button>
          <button class="icon-btn crm-row__delete" type="button" data-action="staff-delete" data-id="${escapeAttr(member.id)}" aria-label="Xoá nhân sự ${escapeAttr(member.name)}" title="Xoá nhân sự">${icon('trash')}</button>
        </div>
      </article>
    `;
  }

  function paintResults() {
    const root = resultsRoot();
    if (!root) return;
    const list = filtered();
    const count = page()?.querySelector('[data-staff-count]');
    if (count) count.textContent = `${list.length} nhân sự`;
    if (!list.length) {
      root.innerHTML = emptyState({
        iconName: 'users',
        title: staff.length ? 'Không tìm thấy nhân sự' : 'Chưa có nhân sự nào',
        message: staff.length
          ? 'Thử từ khoá khác hoặc bật hiển thị nhân sự đã nghỉ.'
          : 'Thêm nhiếp ảnh, hậu kỳ hoặc trợ lý để phân công lịch chụp và thẻ công việc.',
        action: staff.length ? '' : '<button class="btn btn--primary" type="button" data-action="staff-create">Thêm nhân sự</button>'
      });
      return;
    }
    root.innerHTML = `
      <div class="crm-list__head">
        <span>${list.length} nhân sự</span>
        <span>${staff.filter(member => member.isActive).length} đang làm việc</span>
      </div>
      <div class="crm-rows">${list.map(rowMarkup).join('')}</div>
    `;
  }

  async function load() {
    if (loading) return;
    loading = true;
    try {
      const tasks = [crmApi.staff()];
      if (session?.user?.role === 'admin') tasks.push(api('/api/admin/users'));
      const [staffResult, usersResult] = await Promise.allSettled(tasks);
      if (disposed) return;
      if (staffResult.status === 'rejected') throw staffResult.reason;
      staff = Array.isArray(staffResult.value) ? staffResult.value : [];
      users = usersResult?.status === 'fulfilled' && Array.isArray(usersResult.value) ? usersResult.value : [];
      paintStats();
      paintResults();
    } catch (error) {
      if (disposed || error.name === 'AbortError') return;
      const root = resultsRoot();
      if (root) root.innerHTML = loadError(error, 'data-action="staff-retry"');
    } finally {
      loading = false;
    }
  }

  async function reload(message) {
    await load();
    if (!disposed && message) showToast(message, 'success');
  }

  function findMember(id) {
    return staff.find(member => member.id === id) || null;
  }

  function openForm(member = null) {
    const isEdit = Boolean(member);
    const roleOptions = STAFF_ROLES.map(option => `
      <option value="${option.key}" ${(member?.role || 'photographer') === option.key ? 'selected' : ''}>${option.label}</option>
    `).join('');
    const linkableUsers = users.filter(user => !user.staffId || user.staffId === member?.id);
    const userOptions = linkableUsers.map(user => ({
      value: user.id,
      label: `${user.displayName} (@${user.username})`
    }));
    const modal = createModal(`
      <div class="dialog-card dialog-card--crm">
        <button class="dialog-close" type="button" data-close-dialog aria-label="Đóng">${icon('x')}</button>
        <header class="task-dialog__head">
          <span class="task-dialog__icon">${icon('user')}</span>
          <div>
            <p class="eyebrow">${isEdit ? 'Cập nhật hồ sơ' : 'Nhân sự mới'}</p>
            <h2>${isEdit ? 'Sửa nhân sự' : 'Thêm nhân sự'}</h2>
          </div>
        </header>
        <form class="crm-form" data-staff-form novalidate>
          <div class="crm-form__grid">
            <div class="field crm-form__full">
              <label for="staff-name">Tên nhân sự <span class="required">*</span></label>
              <input id="staff-name" name="name" type="text" maxlength="120" value="${escapeAttr(member?.name || '')}" placeholder="Ví dụ: Minh Trần" required autofocus>
            </div>
            <div class="field">
              <label for="staff-role">Vai trò</label>
              <select id="staff-role" name="role">${roleOptions}</select>
            </div>
            <div class="field">
              <label for="staff-phone">Số điện thoại</label>
              <input id="staff-phone" name="phone" type="tel" maxlength="40" value="${escapeAttr(member?.phone || '')}" placeholder="0901 234 567" autocomplete="tel">
            </div>
            <div class="field crm-form__full">
              <label for="staff-email">Email</label>
              <input id="staff-email" name="email" type="email" maxlength="160" value="${escapeAttr(member?.email || '')}" placeholder="minh@studio.vn" autocomplete="email">
            </div>
            <div class="field">
              <label for="staff-paymode">Hình thức lương</label>
              <select id="staff-paymode" name="payMode">
                <option value="monthly" ${(member?.payMode || 'monthly') === 'monthly' ? 'selected' : ''}>Lương tháng</option>
                <option value="hourly" ${member?.payMode === 'hourly' ? 'selected' : ''}>Lương theo giờ</option>
              </select>
            </div>
            <div class="field">
              <label for="staff-salary">Lương cơ bản (đ)</label>
              <input id="staff-salary" name="baseSalary" type="number" min="0" step="100000" value="${Number(member?.baseSalary || 0)}">
              <p class="field__hint">Dùng cho hình thức lương tháng.</p>
            </div>
            <div class="field">
              <label for="staff-hourly">Đơn giá giờ (đ/giờ)</label>
              <input id="staff-hourly" name="hourlyRate" type="number" min="0" step="1000" value="${Number(member?.hourlyRate || 0)}">
              <p class="field__hint">Dùng cho hình thức lương theo giờ.</p>
            </div>
            <div class="field">
              <label for="staff-shift">Ca làm việc</label>
              <select id="staff-shift" name="shift">
                <option value="ca1" ${member?.shift === 'ca1' ? 'selected' : ''}>Ca 1 (7:30–17:30)</option>
                <option value="normal" ${(member?.shift || 'normal') === 'normal' ? 'selected' : ''}>Ca thường (8:30–18:30)</option>
                <option value="ca2" ${member?.shift === 'ca2' ? 'selected' : ''}>Ca 2 (9:00–19:00)</option>
              </select>
            </div>
            <div class="field">
              <label for="staff-commission">Hoa hồng (%)</label>
              <input id="staff-commission" name="commissionRate" type="number" min="0" max="100" step="0.5" value="${Number(member?.commissionRate || 0)}">
            </div>
            ${session?.user?.role === 'admin' ? `
              <div class="field crm-form__full">
                <label for="staff-user">Tài khoản đăng nhập liên kết</label>
                <select id="staff-user" name="userId">${optionList(userOptions, member?.userId || '', 'Không liên kết tài khoản')}</select>
                <p class="field__hint">Liên kết để lịch chụp và thẻ công việc hiển thị đúng người phụ trách.</p>
              </div>
            ` : ''}
            <div class="field crm-form__full">
              <label for="staff-notes">Ghi chú</label>
              <textarea id="staff-notes" name="notes" maxlength="2000" rows="2" placeholder="Thế mạnh, lịch rảnh, ghi chú nội bộ…">${escapeHtml(member?.notes || '')}</textarea>
            </div>
            ${isEdit ? `
              <label class="check-row crm-form__full">
                <input type="checkbox" name="isActive" ${member.isActive ? 'checked' : ''}>
                <span class="check-row__box">${icon('check')}</span>
                <span><strong>Nhân sự đang làm việc</strong><small>Bỏ chọn khi nhân sự đã nghỉ.</small></span>
              </label>
            ` : ''}
          </div>
          <div data-form-error></div>
          <div class="dialog-actions">
            <button class="btn btn--ghost" type="button" data-close-dialog>Huỷ</button>
            <button class="btn btn--primary" type="submit" data-submit>${icon('check')} ${isEdit ? 'Lưu thay đổi' : 'Thêm nhân sự'}</button>
          </div>
        </form>
      </div>
    `, { className: 'dialog--crm' });

    const form = modal.dialog.querySelector('[data-staff-form]');
    form.addEventListener('submit', async event => {
      event.preventDefault();
      clearFieldErrors(form);
      const errorRoot = form.querySelector('[data-form-error]');
      errorRoot.innerHTML = '';
      const nameField = form.elements.namedItem('name');
      const emailField = form.elements.namedItem('email');
      const name = nameField.value.trim();
      const email = emailField.value.trim();
      if (!name) {
        fieldError(nameField, 'Tên nhân sự không được để trống.');
        return;
      }
      if (email && !/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
        fieldError(emailField, 'Email chưa đúng định dạng.');
        return;
      }
      const payload = {
        name,
        role: form.elements.namedItem('role').value,
        phone: form.elements.namedItem('phone').value.trim(),
        email,
        baseSalary: Number(form.elements.namedItem('baseSalary').value || 0),
        commissionRate: Number(form.elements.namedItem('commissionRate').value || 0),
        payMode: form.elements.namedItem('payMode').value,
        hourlyRate: Number(form.elements.namedItem('hourlyRate').value || 0),
        shift: form.elements.namedItem('shift').value,
        notes: form.elements.namedItem('notes').value.trim()
      };
      if (isEdit) payload.isActive = Boolean(form.elements.namedItem('isActive')?.checked);
      const userField = form.elements.namedItem('userId');
      if (userField) payload.userId = userField.value;
      const submit = form.querySelector('[data-submit]');
      setButtonLoading(submit, true, 'Đang lưu…');
      try {
        if (isEdit) {
          await crmApi.updateStaff(member.id, payload);
        } else {
          await crmApi.createStaff(payload);
        }
      } catch (error) {
        if (error.name === 'AbortError') return;
        setButtonLoading(submit, false);
        errorRoot.innerHTML = errorInline(error.message || 'Chưa lưu được nhân sự.', error.code);
        return;
      }
      closeModalFrom(form);
      await reload(isEdit ? 'Đã cập nhật nhân sự.' : 'Đã thêm nhân sự.');
    });
  }

  async function toggle(member) {
    const nextActive = !member.isActive;
    try {
      await crmApi.updateStaff(member.id, { isActive: nextActive });
      await reload(nextActive ? 'Đã kích hoạt nhân sự.' : 'Đã đánh dấu nhân sự đã nghỉ.');
    } catch (error) {
      if (disposed || error.name === 'AbortError') return;
      showToast(error.message || 'Chưa cập nhật được nhân sự.', 'error');
    }
  }

  async function remove(member) {
    const ok = await confirmDialog({
      title: 'Xoá nhân sự?',
      message: `Hồ sơ của ${member.name} sẽ bị xoá vĩnh viễn. Chỉ xoá được khi nhân sự không phụ trách lịch chụp và không liên kết tài khoản.`,
      confirmLabel: 'Xoá nhân sự',
      danger: true
    });
    if (!ok || disposed) return;
    try {
      await crmApi.deleteStaff(member.id);
      await reload('Đã xoá nhân sự.');
    } catch (error) {
      if (disposed || error.name === 'AbortError') return;
      showToast(error.message || 'Chưa xoá được nhân sự.', 'error');
    }
  }

  return {
    title: 'Nhân sự',
    bodyClass: 'page-admin',
    html,
    mount(root) {
      teardown = mountAdminShell(root, session);
      const container = root.querySelector('[data-crm-page]');
      let searchTimer = null;

      container.addEventListener('click', event => {
        const action = event.target.closest('[data-action]');
        if (!action) return;
        if (action.dataset.action === 'staff-create') {
          openForm();
          return;
        }
        if (action.dataset.action === 'staff-retry') {
          load();
          return;
        }
        const member = findMember(action.dataset.id);
        if (!member) return;
        if (action.dataset.action === 'staff-edit') openForm(member);
        if (action.dataset.action === 'staff-toggle') toggle(member);
        if (action.dataset.action === 'staff-delete') remove(member);
      });

      container.addEventListener('input', event => {
        if (event.target.matches('[data-staff-search]')) {
          query = event.target.value;
          window.clearTimeout(searchTimer);
          searchTimer = window.setTimeout(paintResults, 140);
          return;
        }
        if (event.target.matches('[data-staff-inactive]')) {
          showInactive = event.target.checked;
          paintResults();
        }
      });

      load();
    },
    cleanup() {
      disposed = true;
      teardown();
    }
  };
}

// ------------------------------------------------------------------ bookings

export function bookingsView({ session }) {
  let teardown = () => {};
  let disposed = false;
  let bookings = [];
  let query = '';
  let status = 'all';
  let sortKey = 'date';
  let sortDir = 'desc';
  let view = 'month';
  let cursor = new Date();
  let dragId = null;
  let loading = false;
  let options = null;
  let optionsLoading = null;

  const html = crmPage({
    session,
    active: 'bookings',
    eyebrow: 'Lịch chụp',
    title: 'Lịch chụp',
    lead: 'Theo dõi từng buổi chụp từ lúc nhận yêu cầu đến khi giao ảnh, kèm khách hàng và người phụ trách.',
    actions: `<button class="btn btn--primary" type="button" data-action="booking-create">${icon('calendar')} Thêm lịch chụp</button>`,
    body: `
      <section class="crm-overview" data-booking-stats></section>
      <section class="panel crm-toolbar crm-toolbar--wrap" aria-label="Tìm và lọc lịch chụp">
        <label class="crm-search">
          <span>${icon('search')}</span>
          <input type="search" data-booking-search placeholder="Tìm theo mã, khách hàng, gói, địa điểm…" aria-label="Tìm lịch chụp" autocomplete="off">
        </label>
        <div class="segmented crm-segmented crm-segmented--scroll" data-booking-filter role="group" aria-label="Lọc trạng thái lịch chụp">
          <button class="segmented__item is-active" type="button" data-status="all" aria-pressed="true">Tất cả <span class="segmented__count" data-status-count="all">0</span></button>
          ${BOOKING_STATUSES.map(item => `
            <button class="segmented__item" type="button" data-status="${item.key}" aria-pressed="false">${escapeHtml(item.label)} <span class="segmented__count" data-status-count="${item.key}">0</span></button>
          `).join('')}
        </div>
        <span class="crm-toolbar__count" data-booking-count></span>
      </section>
      <section class="panel crm-list" data-booking-results aria-live="polite">${skeletonRows(5)}</section>
    `
  });

  const page = () => document.querySelector('[data-crm-page]');
  const resultsRoot = () => page()?.querySelector('[data-booking-results]');

  const filtered = () => bookings.filter(booking => {
    if (status !== 'all' && booking.status !== status) return false;
    return matches(query, [
      booking.code,
      booking.customerName,
      booking.customerPhone,
      booking.packageName,
      booking.photographerName,
      booking.location,
      booking.notes,
      booking.date
    ]);
  });

  function todayString() {
    const now = new Date();
    return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
  }

  function paintStats() {
    const root = page()?.querySelector('[data-booking-stats]');
    if (!root) return;
    const today = todayString();
    const month = today.slice(0, 7);
    const upcoming = bookings.filter(booking => booking.date >= today && booking.status !== 'cancelled').length;
    const thisMonth = bookings.filter(booking => booking.date.startsWith(month) && booking.status !== 'cancelled').length;
    const pending = bookings.filter(booking => booking.status === 'pending').length;
    root.innerHTML = [
      metric({ label: 'Sắp tới', value: String(upcoming), note: 'buổi chụp từ hôm nay trở đi', tone: 'clay', iconName: 'calendar' }),
      metric({ label: 'Trong tháng này', value: String(thisMonth), note: 'không tính lịch đã huỷ', tone: 'olive', iconName: 'camera' }),
      metric({ label: 'Chờ xác nhận', value: String(pending), note: 'cần liên hệ với khách', tone: 'sand', iconName: 'clock' })
    ].join('');
  }

  function paintCounts() {
    const root = page();
    if (!root) return;
    const counts = { all: bookings.length };
    BOOKING_STATUSES.forEach(item => {
      counts[item.key] = bookings.filter(booking => booking.status === item.key).length;
    });
    Object.entries(counts).forEach(([key, value]) => {
      const node = root.querySelector(`[data-status-count="${key}"]`);
      if (node) node.textContent = String(value);
    });
    const count = root.querySelector('[data-booking-count]');
    if (count) count.textContent = `${filtered().length} lịch chụp`;
  }

  const HOUR_HEIGHT = 48;
  const START_HOUR = 6;
  const END_HOUR = 22;

  function pad2(value) {
    return String(value).padStart(2, '0');
  }

  function dateKey(date) {
    return `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())}`;
  }

  function parseDateKey(key) {
    const [year, month, day] = String(key).split('-').map(Number);
    return new Date(year || new Date().getFullYear(), (month || 1) - 1, day || 1);
  }

  function addDays(date, amount) {
    const next = new Date(date);
    next.setDate(next.getDate() + amount);
    return next;
  }

  function startOfWeek(date) {
    const start = new Date(date);
    const offset = (start.getDay() + 6) % 7;
    start.setDate(start.getDate() - offset);
    start.setHours(0, 0, 0, 0);
    return start;
  }

  function monthGrid(anchor) {
    const start = startOfWeek(new Date(anchor.getFullYear(), anchor.getMonth(), 1));
    return Array.from({ length: 42 }, (_, index) => addDays(start, index));
  }

  function minutesOf(time) {
    const [hour, minute] = String(time || '09:00').split(':').map(Number);
    return (Number.isFinite(hour) ? hour : 9) * 60 + (Number.isFinite(minute) ? minute : 0);
  }

  function slotTime(offsetY) {
    const raw = START_HOUR * 60 + (offsetY / HOUR_HEIGHT) * 60;
    const minutes = Math.max(START_HOUR * 60, Math.min(END_HOUR * 60 - 15, Math.round(raw / 15) * 15));
    return `${pad2(Math.floor(minutes / 60))}:${pad2(minutes % 60)}`;
  }

  function bookingsByDate(list) {
    const map = new Map();
    list.forEach(booking => {
      if (!booking.date) return;
      if (!map.has(booking.date)) map.set(booking.date, []);
      map.get(booking.date).push(booking);
    });
    map.forEach(items => items.sort((a, b) => minutesOf(a.startTime) - minutesOf(b.startTime)));
    return map;
  }

  function calendarTitle() {
    if (view === 'week') {
      const start = startOfWeek(cursor);
      return `${formatDate(dateKey(start))} – ${formatDate(dateKey(addDays(start, 6)))}`;
    }
    return `Tháng ${cursor.getMonth() + 1}, ${cursor.getFullYear()}`;
  }

  function eventChip(booking) {
    const label = `${booking.startTime || ''} · ${booking.customerName || 'Khách lẻ'} · ${statusMeta(booking.status).label}`;
    return `
      <button class="cal-event cal-event--${booking.status}" type="button" draggable="true"
        data-cal-event="${escapeAttr(booking.id)}" data-cal-date="${escapeAttr(booking.date)}"
        title="${escapeAttr(label)}" aria-label="${escapeAttr(label)}">
        <span class="cal-event__time">${escapeHtml(booking.startTime || '--:--')}</span>
        <span class="cal-event__name">${escapeHtml(booking.customerName || booking.code)}</span>
      </button>
    `;
  }

  function layoutDay(list) {
    const items = list.map(booking => {
      const start = minutesOf(booking.startTime);
      const end = start + Math.max(Number(booking.hours || 0), 0.5) * 60;
      return { booking, start, end, lane: 0, lanes: 1 };
    }).sort((a, b) => a.start - b.start || a.end - b.end);

    let cluster = [];
    let clusterEnd = -1;

    const flush = () => {
      if (!cluster.length) return;
      const laneEnds = [];
      cluster.forEach(item => {
        let lane = laneEnds.findIndex(end => end <= item.start);
        if (lane === -1) {
          lane = laneEnds.length;
          laneEnds.push(item.end);
        } else {
          laneEnds[lane] = item.end;
        }
        item.lane = lane;
      });
      const lanes = Math.max(laneEnds.length, 1);
      cluster.forEach(item => { item.lanes = lanes; });
      cluster = [];
      clusterEnd = -1;
    };

    items.forEach(item => {
      if (cluster.length && item.start >= clusterEnd) flush();
      cluster.push(item);
      clusterEnd = Math.max(clusterEnd, item.end);
    });
    flush();
    return items;
  }

  function weekEvent(item) {
    const { booking, lane, lanes } = item;
    const totalHeight = (END_HOUR - START_HOUR) * HOUR_HEIGHT;
    const top = Math.max(0, Math.min(totalHeight - 26, ((item.start - START_HOUR * 60) / 60) * HOUR_HEIGHT));
    const rawHeight = Math.max(((item.end - item.start) / 60) * HOUR_HEIGHT, 26);
    const height = Math.max(26, Math.min(rawHeight, totalHeight - top));
    const width = 100 / lanes;
    const left = width * lane;
    const label = `${booking.startTime || ''} · ${booking.customerName || 'Khách lẻ'}`;
    return `
      <button class="cal-event cal-event--week cal-event--${booking.status}" type="button" draggable="true"
        data-cal-event="${escapeAttr(booking.id)}" data-cal-date="${escapeAttr(booking.date)}"
        style="top:${top}px;height:${height}px;left:calc(${left}% + 2px);width:calc(${width}% - 4px)"
        title="${escapeAttr(label)}" aria-label="${escapeAttr(label)}">
        <span class="cal-event__time">${escapeHtml(booking.startTime || '--:--')}</span>
        <span class="cal-event__name">${escapeHtml(booking.customerName || booking.code)}</span>
        ${booking.location ? `<span class="cal-event__meta">${escapeHtml(booking.location)}</span>` : ''}
      </button>
    `;
  }

  function renderMonth(map) {
    const days = monthGrid(cursor);
    const month = cursor.getMonth();
    const today = dateKey(new Date());
    return `
      <div class="cal-month">
        <div class="cal-month__weekdays" aria-hidden="true">
          ${['T2', 'T3', 'T4', 'T5', 'T6', 'T7', 'CN'].map(label => `<span>${label}</span>`).join('')}
        </div>
        <div class="cal-month__grid">
          ${days.map(day => {
            const key = dateKey(day);
            const items = map.get(key) || [];
            const outside = day.getMonth() !== month;
            const max = 3;
            return `
              <div class="cal-day${outside ? ' is-outside' : ''}${key === today ? ' is-today' : ''}" data-cal-date="${key}">
                <div class="cal-day__head">
                  <span class="cal-day__num">${day.getDate()}</span>
                  <button class="cal-day__add" type="button" data-cal-add="${key}" aria-label="Thêm lịch chụp ${formatDate(key)}" title="Thêm lịch chụp">${icon('plus')}</button>
                </div>
                <div class="cal-day__events">
                  ${items.slice(0, max).map(eventChip).join('')}
                  ${items.length > max ? `<button class="cal-day__more" type="button" data-cal-more="${key}">+${items.length - max} lịch</button>` : ''}
                </div>
              </div>
            `;
          }).join('')}
        </div>
      </div>
    `;
  }

  function renderWeek(map) {
    const start = startOfWeek(cursor);
    const days = Array.from({ length: 7 }, (_, index) => addDays(start, index));
    const today = dateKey(new Date());
    const hours = Array.from({ length: END_HOUR - START_HOUR }, (_, index) => START_HOUR + index);
    return `
      <div class="cal-week">
        <div class="cal-week__head">
          <div class="cal-week__gutter"></div>
          ${days.map(day => {
            const key = dateKey(day);
            return `
              <div class="cal-week__day${key === today ? ' is-today' : ''}">
                <span>${['CN', 'T2', 'T3', 'T4', 'T5', 'T6', 'T7'][day.getDay()]}</span>
                <strong>${day.getDate()}</strong>
                <button class="cal-week__add" type="button" data-cal-add="${key}" aria-label="Thêm lịch chụp ${formatDate(key)}" title="Thêm lịch chụp">${icon('plus')}</button>
              </div>
            `;
          }).join('')}
        </div>
        <div class="cal-week__scroll">
          <div class="cal-week__body">
            <div class="cal-week__times">
              ${hours.map(hour => `<span>${pad2(hour)}:00</span>`).join('')}
            </div>
            ${days.map(day => {
              const key = dateKey(day);
              const items = layoutDay(map.get(key) || []);
              return `
                <div class="cal-week__col" data-cal-date="${key}">
                  ${hours.map(() => '<div class="cal-week__line"></div>').join('')}
                  ${items.map(weekEvent).join('')}
                </div>
              `;
            }).join('')}
          </div>
        </div>
      </div>
    `;
  }

  function renderTable(list) {
    return `
      <div class="booking-table__scroll" tabindex="0" role="region" aria-label="Bảng lịch chụp">
        <table class="booking-table">
          <caption class="sr-only">Danh sách lịch chụp</caption>
          <thead>
            <tr>
              <th scope="col" class="booking-table__col-code">Mã</th>
              ${sortHeader('date', 'Ngày chụp')}
              ${sortHeader('customer', 'Khách hàng')}
              <th scope="col" class="booking-table__col-package">Gói dịch vụ</th>
              <th scope="col" class="booking-table__col-staff">Phụ trách</th>
              <th scope="col" class="booking-table__col-location">Địa điểm</th>
              ${sortHeader('status', 'Trạng thái')}
              <th scope="col" class="booking-table__actions"><span class="sr-only">Thao tác</span></th>
            </tr>
          </thead>
          <tbody>${list.map(rowMarkup).join('')}</tbody>
        </table>
      </div>
    `;
  }

  function repaint(focusSelector) {
    paintResults();
    if (focusSelector) {
      const target = page()?.querySelector(focusSelector);
      if (target && typeof target.focus === 'function') target.focus();
    }
  }

  function shiftCalendar(delta) {
    if (view === 'week') {
      cursor = addDays(cursor, delta * 7);
    } else {
      cursor = new Date(cursor.getFullYear(), cursor.getMonth() + delta, 1);
    }
    repaint(`[data-cal-${delta < 0 ? 'prev' : 'next'}]`);
  }

  function statusRank(value) {
    const index = BOOKING_STATUSES.findIndex(item => item.key === value);
    return index < 0 ? BOOKING_STATUSES.length : index;
  }

  function visibleBookings() {
    const list = filtered().slice();
    list.sort((a, b) => {
      let result;
      if (sortKey === 'customer') {
        result = String(a.customerName || '').localeCompare(String(b.customerName || ''), 'vi', { sensitivity: 'base' });
      } else if (sortKey === 'status') {
        result = statusRank(a.status) - statusRank(b.status);
      } else {
        result = `${a.date || ''} ${a.startTime || ''}`.localeCompare(`${b.date || ''} ${b.startTime || ''}`);
      }
      return sortDir === 'asc' ? result : -result;
    });
    return list;
  }

  function sortLabel() {
    if (sortKey === 'customer') return `Sắp xếp: khách hàng ${sortDir === 'asc' ? 'A → Z' : 'Z → A'}`;
    if (sortKey === 'status') return `Sắp xếp: trạng thái ${sortDir === 'asc' ? 'theo quy trình' : 'ngược quy trình'}`;
    return `Sắp xếp: ngày chụp ${sortDir === 'asc' ? 'sớm nhất' : 'mới nhất'}`;
  }

  function sortHeader(key, label) {
    const active = sortKey === key;
    const ariaSort = active ? (sortDir === 'asc' ? 'ascending' : 'descending') : 'none';
    return `
      <th scope="col" aria-sort="${ariaSort}">
        <button class="booking-table__sort${active ? ' is-active' : ''}" type="button" data-booking-sort="${key}">
          ${escapeHtml(label)}${icon(active && sortDir === 'asc' ? 'arrowUp' : 'arrowDown')}
        </button>
      </th>
    `;
  }

  function rowMarkup(booking) {
    const customer = booking.customerName || 'Khách lẻ';
    const phone = booking.customerPhone || '';
    const photographer = booking.photographerName || '';
    const location = booking.location || '';
    return `
      <tr class="${booking.status === 'cancelled' ? 'is-cancelled' : ''}" data-booking-id="${escapeAttr(booking.id)}">
        <td class="booking-table__col-code">
          <span class="booking-table__code">${escapeHtml(booking.code)}</span>
        </td>
        <td>
          <strong>${booking.date ? escapeHtml(formatDate(booking.date)) : 'Chưa có ngày'}</strong>
          <span class="booking-table__sub">${icon('clock')}${escapeHtml(booking.startTime || '--:--')} · ${escapeHtml(hoursLabel(booking.hours))}</span>
        </td>
        <td>
          <strong>${escapeHtml(customer)}</strong>
          ${phone ? `<span class="booking-table__sub">${escapeHtml(phone)}</span>` : ''}
        </td>
        <td class="booking-table__col-package">
          <strong>${escapeHtml(booking.packageName || '—')}</strong>
          <span class="booking-table__sub">${money(booking.price)}</span>
        </td>
        <td class="booking-table__col-staff">
          ${photographer ? escapeHtml(photographer) : '<span class="booking-table__muted">Chưa phân công</span>'}
        </td>
        <td class="booking-table__col-location" title="${escapeAttr(location)}">
          ${location ? escapeHtml(location) : '<span class="booking-table__muted">—</span>'}
        </td>
        <td>
          <label class="booking-table__status">
            <span class="sr-only">Trạng thái lịch ${escapeAttr(booking.code)}</span>
            <select data-booking-status="${escapeAttr(booking.id)}" aria-label="Đổi trạng thái lịch ${escapeAttr(booking.code)}">
              ${BOOKING_STATUSES.map(item => `<option value="${item.key}" ${booking.status === item.key ? 'selected' : ''}>${escapeHtml(item.label)}</option>`).join('')}
            </select>
          </label>
        </td>
        <td class="booking-table__actions">
          <button class="btn btn--ghost btn--compact" type="button" data-action="booking-edit" data-id="${escapeAttr(booking.id)}">${icon('edit')} Tùy chỉnh</button>
          <button class="icon-btn crm-row__delete" type="button" data-action="booking-delete" data-id="${escapeAttr(booking.id)}" aria-label="Xoá lịch ${escapeAttr(booking.code)}" title="Xoá lịch">${icon('trash')}</button>
        </td>
      </tr>
    `;
  }

  function paintResults() {
    const root = resultsRoot();
    if (!root) return;
    paintCounts();
    const list = visibleBookings();
    const map = bookingsByDate(list);
    const isList = view === 'list';
    let body;
    if (isList) {
      body = list.length
        ? renderTable(list)
        : emptyState({
            iconName: bookings.length ? 'search' : 'calendar',
            title: bookings.length ? 'Không tìm thấy lịch chụp' : 'Chưa có lịch chụp nào',
            message: bookings.length
              ? 'Thử từ khoá khác hoặc chọn trạng thái khác.'
              : 'Tạo lịch đầu tiên để gắn khách hàng, gói dịch vụ và người phụ trách.',
            action: bookings.length
              ? '<button class="btn btn--ghost" type="button" data-action="booking-clear-filter">Xoá bộ lọc</button>'
              : '<button class="btn btn--primary" type="button" data-action="booking-create">Thêm lịch chụp</button>'
          });
    } else if (view === 'week') {
      body = renderWeek(map);
    } else {
      body = renderMonth(map);
    }

    root.innerHTML = `
      <div class="cal-toolbar">
        <div class="segmented cal-view-switch" role="group" aria-label="Kiểu xem lịch chụp">
          ${[['month', 'Tháng'], ['week', 'Tuần'], ['list', 'Danh sách']].map(([key, label]) => `
            <button class="segmented__item${view === key ? ' is-active' : ''}" type="button" data-cal-view="${key}" aria-pressed="${view === key}">${label}</button>
          `).join('')}
        </div>
        ${isList ? `<span class="cal-toolbar__hint">${escapeHtml(sortLabel())}</span>` : `
          <div class="cal-nav">
            <button class="icon-btn cal-nav__btn" type="button" data-cal-prev aria-label="Khoảng thời gian trước" title="Trước">${icon('arrowLeft')}</button>
            <button class="btn btn--ghost btn--compact" type="button" data-cal-today>Hôm nay</button>
            <button class="icon-btn cal-nav__btn" type="button" data-cal-next aria-label="Khoảng thời gian sau" title="Sau">${icon('arrowRight')}</button>
            <strong class="cal-nav__title">${escapeHtml(calendarTitle())}</strong>
          </div>
        `}
      </div>
      <div class="cal-body">${body}</div>
      ${isList ? '' : '<p class="cal-hint">Bấm vào một ngày hoặc khung giờ để thêm lịch · bấm vào lịch để tùy chỉnh · kéo thả để đổi ngày giờ.</p>'}
    `;
  }

  async function loadOptions(force = false) {
    if (options && !force) return options;
    if (optionsLoading) return optionsLoading;
    optionsLoading = (async () => {
      const tasks = [
        crmApi.customers(),
        crmApi.packages(),
        crmApi.staff()
      ];
      const withAlbums = hasPermission(session, 'albums');
      if (withAlbums) tasks.push(api('/api/admin/albums'));
      const [customersResult, packagesResult, staffResult, albumsResult] = await Promise.allSettled(tasks);
      if (customersResult.status === 'rejected') throw customersResult.reason;
      options = {
        customers: Array.isArray(customersResult.value) ? customersResult.value : [],
        packages: packagesResult.status === 'fulfilled' && Array.isArray(packagesResult.value) ? packagesResult.value : [],
        staff: staffResult.status === 'fulfilled' && Array.isArray(staffResult.value) ? staffResult.value : [],
        albums: albumsResult?.status === 'fulfilled' && Array.isArray(albumsResult.value) ? albumsResult.value : []
      };
      return options;
    })();
    try {
      return await optionsLoading;
    } finally {
      optionsLoading = null;
    }
  }

  async function load() {
    if (loading) return;
    loading = true;
    try {
      const result = await crmApi.bookings();
      if (disposed) return;
      bookings = Array.isArray(result) ? result : [];
      paintStats();
      paintResults();
    } catch (error) {
      if (disposed || error.name === 'AbortError') return;
      const root = resultsRoot();
      if (root) root.innerHTML = loadError(error, 'data-action="booking-retry"');
    } finally {
      loading = false;
    }
  }

  async function reload(message) {
    await load();
    if (!disposed && message) showToast(message, 'success');
  }

  function findBooking(id) {
    return bookings.find(booking => booking.id === id) || null;
  }

  async function openForm(booking = null, preset = null) {
    const isEdit = Boolean(booking);
    let data;
    try {
      data = await loadOptions();
    } catch (error) {
      if (disposed || error.name === 'AbortError') return;
      showToast(error.message || 'Không tải được danh sách lựa chọn.', 'error');
      return;
    }
    if (disposed) return;

    const customerOptions = data.customers
      .filter(customer => customer.isActive || customer.id === booking?.customerId)
      .map(customer => ({ value: customer.id, label: `${customer.name}${customer.phone ? ` · ${customer.phone}` : ''}` }));
    const packageOptions = data.packages
      .filter(packageItem => packageItem.isActive || packageItem.id === booking?.packageId)
      .map(packageItem => ({ value: packageItem.id, label: `${packageItem.name} · ${money(packageItem.price)}` }));
    const staffOptions = data.staff
      .filter(member => member.isActive || member.id === booking?.photographerId)
      .map(member => ({ value: member.id, label: `${member.name} · ${staffRoleLabel(member.role)}` }));
    const albumOptions = data.albums.map(album => ({ value: album.id, label: `${album.title}${album.clientName ? ` · ${album.clientName}` : ''}` }));
    const statusOptions = BOOKING_STATUSES.map(item => ({ value: item.key, label: item.label }));

    const modal = createModal(`
      <div class="dialog-card dialog-card--crm dialog-card--wide">
        <button class="dialog-close" type="button" data-close-dialog aria-label="Đóng">${icon('x')}</button>
        <header class="task-dialog__head">
          <span class="task-dialog__icon">${icon('calendar')}</span>
          <div>
            <p class="eyebrow">${isEdit ? `Cập nhật ${escapeHtml(booking.code)}` : 'Lịch chụp mới'}</p>
            <h2>${isEdit ? 'Sửa lịch chụp' : 'Thêm lịch chụp'}</h2>
          </div>
        </header>
        <form class="crm-form" data-booking-form novalidate>
          <div class="crm-form__grid crm-form__grid--three">
            <div class="field crm-form__span2">
              <label for="booking-customer">Khách hàng</label>
              <select id="booking-customer" name="customerId">${optionList(customerOptions, booking?.customerId || '', 'Khách lẻ / chưa chọn')}</select>
            </div>
            <div class="field">
              <label for="booking-status">Trạng thái</label>
              <select id="booking-status" name="status">${optionList(statusOptions, booking?.status || 'pending')}</select>
            </div>
            <div class="field crm-form__span2">
              <label for="booking-package">Gói dịch vụ</label>
              <select id="booking-package" name="packageId">${optionList(packageOptions, booking?.packageId || '', 'Chưa chọn gói')}</select>
            </div>
            <div class="field">
              <label for="booking-price">Giá (đ)</label>
              <input id="booking-price" name="price" type="number" min="0" step="1000" value="${Number(booking?.price || 0)}">
            </div>
            <div class="field">
              <label for="booking-date">Ngày chụp <span class="required">*</span></label>
              <input id="booking-date" name="date" type="date" value="${escapeAttr(booking?.date || preset?.date || '')}" required>
            </div>
            <div class="field">
              <label for="booking-time">Giờ bắt đầu</label>
              <input id="booking-time" name="startTime" type="time" value="${escapeAttr(booking?.startTime || preset?.startTime || '09:00')}">
            </div>
            <div class="field">
              <label for="booking-hours">Số giờ</label>
              <input id="booking-hours" name="hours" type="number" min="0" max="1000" step="0.5" value="${Number(booking?.hours ?? preset?.hours ?? 0)}">
            </div>
            <div class="field crm-form__span2">
              <label for="booking-location">Địa điểm</label>
              <input id="booking-location" name="location" type="text" maxlength="160" value="${escapeAttr(booking?.location || '')}" placeholder="Studio, Đà Lạt, biển Phú Quốc…">
            </div>
            <div class="field">
              <label for="booking-photographer">Người phụ trách</label>
              <select id="booking-photographer" name="photographerId">${optionList(staffOptions, booking?.photographerId || '', 'Chưa phân công')}</select>
            </div>
            ${data.albums.length ? `
              <div class="field crm-form__span2">
                <label for="booking-album">Album liên kết</label>
                <select id="booking-album" name="albumId">${optionList(albumOptions, booking?.albumId || '', 'Chưa liên kết album')}</select>
              </div>
            ` : ''}
            <div class="field crm-form__full">
              <label for="booking-notes">Ghi chú</label>
              <textarea id="booking-notes" name="notes" maxlength="2000" rows="3" placeholder="Yêu cầu của khách, concept, lưu ý di chuyển…">${escapeHtml(booking?.notes || '')}</textarea>
            </div>
          </div>
          ${isEdit && Array.isArray(booking.statusHistory) && booking.statusHistory.length ? `
            <section class="crm-detail__section crm-detail__section--compact">
              <h3>${icon('history')} Lịch sử trạng thái</h3>
              <ol class="crm-history">
                ${booking.statusHistory.slice().reverse().map(entry => `
                  <li><span>${escapeHtml(statusMeta(entry.status).label)}</span><small>${escapeHtml(entry.actorName || 'Studio')} · ${escapeHtml(formatTimestamp(entry.changedAt))}</small></li>
                `).join('')}
              </ol>
            </section>
          ` : ''}
          <div data-form-error></div>
          <div class="dialog-actions">
            <button class="btn btn--ghost" type="button" data-close-dialog>Huỷ</button>
            <button class="btn btn--primary" type="submit" data-submit>${icon('check')} ${isEdit ? 'Lưu thay đổi' : 'Thêm lịch chụp'}</button>
          </div>
        </form>
      </div>
    `, { className: 'dialog--crm dialog--wide' });

    const form = modal.dialog.querySelector('[data-booking-form]');
    const packageField = form.elements.namedItem('packageId');
    const priceField = form.elements.namedItem('price');
    const hoursField = form.elements.namedItem('hours');
    packageField.addEventListener('change', () => {
      const selected = data.packages.find(packageItem => packageItem.id === packageField.value);
      if (!selected) return;
      if (!Number(priceField.value || 0)) priceField.value = String(Number(selected.price || 0));
      if (!Number(hoursField.value || 0)) hoursField.value = String(Number(selected.hours || 0));
    });

    form.addEventListener('submit', async event => {
      event.preventDefault();
      clearFieldErrors(form);
      const errorRoot = form.querySelector('[data-form-error]');
      errorRoot.innerHTML = '';
      const dateField = form.elements.namedItem('date');
      if (!dateField.value) {
        fieldError(dateField, 'Vui lòng chọn ngày chụp.');
        return;
      }
      const payload = {
        customerId: form.elements.namedItem('customerId').value,
        packageId: form.elements.namedItem('packageId').value,
        date: dateField.value,
        startTime: form.elements.namedItem('startTime').value || '09:00',
        hours: Number(form.elements.namedItem('hours').value || 0),
        location: form.elements.namedItem('location').value.trim(),
        photographerId: form.elements.namedItem('photographerId').value,
        status: form.elements.namedItem('status').value,
        price: Number(form.elements.namedItem('price').value || 0),
        notes: form.elements.namedItem('notes').value.trim()
      };
      const albumField = form.elements.namedItem('albumId');
      if (albumField) payload.albumId = albumField.value;
      const submit = form.querySelector('[data-submit]');
      setButtonLoading(submit, true, 'Đang lưu…');
      try {
        if (isEdit) {
          await crmApi.updateBooking(booking.id, payload);
        } else {
          await crmApi.createBooking(payload);
        }
      } catch (error) {
        if (error.name === 'AbortError') return;
        setButtonLoading(submit, false);
        errorRoot.innerHTML = errorInline(error.message || 'Chưa lưu được lịch chụp.', error.code);
        return;
      }
      closeModalFrom(form);
      await reload(isEdit ? 'Đã cập nhật lịch chụp.' : 'Đã thêm lịch chụp.');
    });
  }

  async function changeStatus(booking, nextStatus) {
    if (!booking || nextStatus === booking.status) return;
    try {
      const updated = await crmApi.updateBookingStatus(booking.id, nextStatus);
      if (disposed) return;
      const index = bookings.findIndex(item => item.id === booking.id);
      if (index >= 0 && updated) bookings[index] = updated;
      paintStats();
      paintResults();
      showToast(`Đã chuyển ${booking.code} sang “${statusMeta(nextStatus).label}”.`, 'success');
    } catch (error) {
      if (disposed || error.name === 'AbortError') return;
      showToast(error.message || 'Chưa đổi được trạng thái lịch chụp.', 'error');
      paintResults();
    }
  }

  async function moveBooking(booking, date, startTime) {
    if (!booking) return;
    if (booking.date === date && (!startTime || booking.startTime === startTime)) return;
    const payload = { date };
    if (startTime) payload.startTime = startTime;
    try {
      const updated = await crmApi.updateBooking(booking.id, payload);
      if (disposed) return;
      const index = bookings.findIndex(item => item.id === booking.id);
      if (index >= 0 && updated) bookings[index] = updated;
      paintStats();
      paintResults();
      showToast(`Đã chuyển ${booking.code} sang ${formatDate(date)}${startTime ? ` · ${startTime}` : ''}.`, 'success');
    } catch (error) {
      if (disposed || error.name === 'AbortError') return;
      showToast(error.message || 'Chưa chuyển được lịch chụp.', 'error');
      paintResults();
    }
  }

  async function remove(booking) {
    const ok = await confirmDialog({
      title: 'Xoá lịch chụp?',
      message: `Lịch ${booking.code} sẽ bị xoá vĩnh viễn. Thẻ công việc đang liên kết sẽ được bỏ liên kết.`,
      confirmLabel: 'Xoá lịch chụp',
      danger: true
    });
    if (!ok || disposed) return;
    try {
      await crmApi.deleteBooking(booking.id);
      await reload('Đã xoá lịch chụp.');
    } catch (error) {
      if (disposed || error.name === 'AbortError') return;
      showToast(error.message || 'Chưa xoá được lịch chụp.', 'error');
    }
  }

  return {
    title: 'Lịch chụp',
    bodyClass: 'page-admin',
    html,
    mount(root) {
      teardown = mountAdminShell(root, session);
      const container = root.querySelector('[data-crm-page]');
      let searchTimer = null;

      container.addEventListener('click', event => {
        const viewBtn = event.target.closest('[data-cal-view]');
        if (viewBtn) {
          const next = viewBtn.dataset.calView;
          if (next === view) return;
          view = next;
          if (view !== 'list') cursor = new Date(cursor.getTime());
          repaint(`[data-cal-view="${view}"]`);
          return;
        }
        if (event.target.closest('[data-cal-prev]')) {
          shiftCalendar(-1);
          return;
        }
        if (event.target.closest('[data-cal-next]')) {
          shiftCalendar(1);
          return;
        }
        if (event.target.closest('[data-cal-today]')) {
          cursor = new Date();
          repaint('[data-cal-today]');
          return;
        }
        const add = event.target.closest('[data-cal-add]');
        if (add) {
          openForm(null, { date: add.dataset.calAdd });
          return;
        }
        const more = event.target.closest('[data-cal-more]');
        if (more) {
          cursor = parseDateKey(more.dataset.calMore);
          view = 'week';
          repaint('[data-cal-view="week"]');
          return;
        }
        const chip = event.target.closest('[data-cal-event]');
        if (chip) {
          const booking = findBooking(chip.dataset.calEvent);
          if (booking) openForm(booking);
          return;
        }

        const action = event.target.closest('[data-action]');
        if (action) {
          if (action.dataset.action === 'booking-create') {
            openForm();
            return;
          }
          if (action.dataset.action === 'booking-clear-filter') {
            query = '';
            status = 'all';
            const input = container.querySelector('[data-booking-search]');
            if (input) input.value = '';
            container.querySelectorAll('[data-booking-filter] .segmented__item').forEach(button => {
              const active = button.dataset.status === 'all';
              button.classList.toggle('is-active', active);
              button.setAttribute('aria-pressed', String(active));
            });
            paintResults();
            return;
          }
          if (action.dataset.action === 'booking-retry') {
            load();
            return;
          }
          const booking = findBooking(action.dataset.id);
          if (!booking) return;
          if (action.dataset.action === 'booking-edit') openForm(booking);
          if (action.dataset.action === 'booking-delete') remove(booking);
          return;
        }
        const sort = event.target.closest('[data-booking-sort]');
        if (sort) {
          const key = sort.dataset.bookingSort;
          if (sortKey === key) {
            sortDir = sortDir === 'asc' ? 'desc' : 'asc';
          } else {
            sortKey = key;
            sortDir = key === 'date' ? 'desc' : 'asc';
          }
          paintResults();
          return;
        }
        const filter = event.target.closest('[data-status]');
        if (filter) {
          status = filter.dataset.status;
          container.querySelectorAll('[data-booking-filter] .segmented__item').forEach(button => {
            const active = button === filter;
            button.classList.toggle('is-active', active);
            button.setAttribute('aria-pressed', String(active));
          });
          paintResults();
          return;
        }

        const weekColumn = event.target.closest('.cal-week__col');
        if (weekColumn) {
          const rect = weekColumn.getBoundingClientRect();
          openForm(null, {
            date: weekColumn.dataset.calDate,
            startTime: slotTime(event.clientY - rect.top)
          });
          return;
        }

        const dayCell = event.target.closest('.cal-day');
        if (dayCell) {
          openForm(null, { date: dayCell.dataset.calDate });
        }
      });

      container.addEventListener('input', event => {
        if (!event.target.matches('[data-booking-search]')) return;
        query = event.target.value;
        window.clearTimeout(searchTimer);
        searchTimer = window.setTimeout(paintResults, 140);
      });

      container.addEventListener('change', event => {
        const select = event.target.closest('[data-booking-status]');
        if (!select) return;
        const booking = findBooking(select.dataset.bookingStatus);
        if (booking) changeStatus(booking, select.value);
      });

      container.addEventListener('dragstart', event => {
        const chip = event.target.closest('[data-cal-event]');
        if (!chip) return;
        dragId = chip.dataset.calEvent;
        chip.classList.add('is-dragging');
        if (event.dataTransfer) {
          event.dataTransfer.effectAllowed = 'move';
          try {
            event.dataTransfer.setData('text/plain', dragId);
          } catch {
            /* Safari can reject custom payloads; the in-memory id is enough. */
          }
        }
      });

      container.addEventListener('dragend', event => {
        dragId = null;
        const chip = event.target.closest('[data-cal-event]');
        if (chip) chip.classList.remove('is-dragging');
        container.querySelectorAll('.is-dragging, .is-drop-target').forEach(node => {
          node.classList.remove('is-dragging', 'is-drop-target');
        });
      });

      container.addEventListener('dragover', event => {
        const target = event.target.closest('.cal-day, .cal-week__col');
        if (!target || !dragId) return;
        event.preventDefault();
        if (event.dataTransfer) event.dataTransfer.dropEffect = 'move';
        container.querySelectorAll('.is-drop-target').forEach(node => {
          if (node !== target) node.classList.remove('is-drop-target');
        });
        target.classList.add('is-drop-target');
      });

      container.addEventListener('dragleave', event => {
        const target = event.target.closest('.cal-day, .cal-week__col');
        if (!target) return;
        const next = event.relatedTarget;
        if (next && target.contains(next)) return;
        target.classList.remove('is-drop-target');
      });

      container.addEventListener('drop', event => {
        const target = event.target.closest('.cal-day, .cal-week__col');
        if (!target || !dragId) return;
        event.preventDefault();
        const booking = findBooking(dragId);
        const date = target.dataset.calDate;
        let startTime = null;
        if (target.classList.contains('cal-week__col')) {
          const rect = target.getBoundingClientRect();
          startTime = slotTime(event.clientY - rect.top);
        }
        dragId = null;
        target.classList.remove('is-drop-target');
        if (booking && date) moveBooking(booking, date, startTime);
      });

      load();
    },
    cleanup() {
      disposed = true;
      teardown();
    }
  };
}

// --------------------------------------------------------------------- audit

export function auditView({ session }) {
  let teardown = () => {};
  let disposed = false;
  let entries = [];
  let module = '';
  let loading = false;

  const html = crmPage({
    session,
    active: 'audit',
    eyebrow: 'Dấu vết hoạt động',
    title: 'Nhật ký studio',
    lead: 'Xem ai đã tạo, sửa, xoá hoặc đổi trạng thái dữ liệu quan trọng trong studio.',
    actions: `<button class="btn btn--ghost" type="button" data-action="audit-refresh">${icon('refresh')} Làm mới</button>`,
    body: `
      <section class="panel crm-toolbar" aria-label="Lọc nhật ký">
        <label class="crm-select">
          <span>${icon('filter')} Phân hệ</span>
          <select data-audit-module aria-label="Lọc nhật ký theo phân hệ">
            ${AUDIT_MODULES.map(item => `<option value="${item.key}">${escapeHtml(item.label)}</option>`).join('')}
          </select>
        </label>
        <span class="crm-toolbar__count" data-audit-count></span>
      </section>
      <section class="panel crm-list" data-audit-results aria-live="polite">${skeletonRows(5)}</section>
    `
  });

  const page = () => document.querySelector('[data-crm-page]');
  const resultsRoot = () => page()?.querySelector('[data-audit-results]');

  function itemMarkup(entry) {
    const details = Array.isArray(entry.details) ? entry.details.filter(Boolean) : [];
    const avatarClass = entry.actorRole === 'admin' ? 'avatar--ink' : 'avatar--olive';
    const moduleLabel = AUDIT_MODULES.find(item => item.key === entry.module)?.label || entry.module || 'Studio';
    return `
      <article class="audit-item">
        <span class="avatar ${avatarClass}" aria-hidden="true">${escapeHtml(initials(entry.actorName || 'Studio'))}</span>
        <div class="audit-item__body">
          <p class="audit-item__head">
            <strong>${escapeHtml(entry.actorName || 'Studio')}</strong>
            <span>${escapeHtml(entry.summary || 'đã cập nhật dữ liệu')}</span>
          </p>
          <p class="audit-item__meta">
            <span class="pill pill--neutral">${escapeHtml(moduleLabel)}</span>
            <span class="pill pill--draft">${escapeHtml(entry.action || 'update')}</span>
            <time datetime="${escapeAttr(entry.createdAt || '')}" title="${escapeAttr(formatTimestamp(entry.createdAt))}">${escapeHtml(formatRelativeTime(entry.createdAt))}</time>
            <span>${escapeHtml(formatTimestamp(entry.createdAt))}</span>
          </p>
          ${details.length ? `
            <ul class="audit-item__details">
              ${details.map(detail => `<li>${escapeHtml(detail)}</li>`).join('')}
            </ul>
          ` : ''}
        </div>
      </article>
    `;
  }

  function paint() {
    const root = resultsRoot();
    if (!root) return;
    const count = page()?.querySelector('[data-audit-count]');
    if (count) count.textContent = `${entries.length} hoạt động`;
    if (!entries.length) {
      root.innerHTML = emptyState({
        iconName: 'history',
        title: 'Chưa có hoạt động nào',
        message: 'Những thao tác quan trọng trong studio sẽ được ghi lại tại đây.',
        action: ''
      });
      return;
    }
    root.innerHTML = `
      <div class="crm-list__head">
        <span>${entries.length} hoạt động gần nhất</span>
        <span>Mới nhất ở trên</span>
      </div>
      <div class="audit-list">${entries.map(itemMarkup).join('')}</div>
    `;
  }

  async function load() {
    if (loading) return;
    loading = true;
    try {
      const result = await crmApi.audit({ limit: 250, module });
      if (disposed) return;
      entries = Array.isArray(result) ? result : [];
      paint();
    } catch (error) {
      if (disposed || error.name === 'AbortError') return;
      const root = resultsRoot();
      if (root) root.innerHTML = loadError(error, 'data-action="audit-refresh"');
    } finally {
      loading = false;
    }
  }

  return {
    title: 'Nhật ký studio',
    bodyClass: 'page-admin',
    html,
    mount(root) {
      teardown = mountAdminShell(root, session);
      const container = root.querySelector('[data-crm-page]');
      container.addEventListener('click', event => {
        if (event.target.closest('[data-action="audit-refresh"]')) load();
      });
      container.addEventListener('change', event => {
        if (!event.target.matches('[data-audit-module]')) return;
        module = event.target.value;
        load();
      });
      load();
    },
    cleanup() {
      disposed = true;
      teardown();
    }
  };
}
