import { api, crmApi, operationsApi } from './api.js';
import { mountAdminShell, hasPermission } from './admin.js';
import {
  icon,
  escapeHtml,
  escapeAttr,
  stripDiacritics,
  formatDate,
  showToast,
  setButtonLoading,
  createModal,
  confirmDialog,
  emptyState,
  errorInline,
  initials,
  downloadFile
} from './ui.js';
import {
  crmPage,
  metric,
  skeletonRows,
  optionList,
  loadError,
  money,
  fieldError,
  clearFieldErrors,
  closeModalFrom
} from './crm.js';

// --------------------------------------------------------------- shared bits

const EXPENSE_CATEGORIES = [
  'Thuê studio', 'Nhân sự', 'Quảng cáo', 'Đồ nghề & thiết bị',
  'Điện nước', 'Vận chuyển', 'Thuế & phí', 'Khác'
];

const INCOME_CATEGORIES = [
  'Bán gói dịch vụ', 'Cho thuê studio', 'Bán váy & phụ kiện',
  'Dịch vụ chỉnh sửa', 'Hoàn tiền', 'Thu khác'
];

const OUTFIT_CATEGORIES = [
  'Váy cưới', 'Váy dạ hội', 'Vest & Suit', 'Áo dài', 'Hanbok',
  'Thời trang', 'Trẻ em', 'Phụ kiện', 'Khác'
];

const SHIFT_LABELS = {
  ca1: 'Ca 1 (7:30–17:30)',
  normal: 'Ca thường (8:30–18:30)',
  ca2: 'Ca 2 (9:00–19:00)'
};

const SHIFT_SHORT = {
  ca1: 'Ca 1',
  normal: 'Ca thường',
  ca2: 'Ca 2'
};

const STAFF_ROLE_LABELS = {
  photographer: 'Nhiếp ảnh',
  editor: 'Hậu kỳ',
  assistant: 'Trợ lý',
  makeup: 'Trang điểm',
  manager: 'Quản lý',
  other: 'Khác'
};

const SHIFT_OPTIONS = [
  { value: '', label: 'Theo ca hồ sơ' },
  { value: 'ca1', label: SHIFT_LABELS.ca1 },
  { value: 'normal', label: SHIFT_LABELS.normal },
  { value: 'ca2', label: SHIFT_LABELS.ca2 },
  { value: 'off', label: 'Nghỉ (không xếp ca)' }
];

const ATTENDANCE_STATUS = {
  ontime: { label: 'Đúng giờ', tone: 'live', iconName: 'checkCircle' },
  late: { label: 'Đi muộn', tone: 'warn', iconName: 'clock' },
  early: { label: 'Về sớm', tone: 'draft', iconName: 'clock' },
  late_early: { label: 'Muộn & về sớm', tone: 'warn', iconName: 'clock' },
  open: { label: 'Chưa tan ca', tone: 'neutral', iconName: 'clock' },
  missing_in: { label: 'Thiếu giờ vào', tone: 'unpaid', iconName: 'alertCircle' }
};

const NOTIFICATION_TYPES = {
  booking: { label: 'Lịch chụp', iconName: 'calendar', tone: 'clay' },
  payment: { label: 'Thanh toán', iconName: 'wallet', tone: 'olive' },
  contract: { label: 'Hợp đồng', iconName: 'file', tone: 'sand' },
  wardrobe: { label: 'Trang phục', iconName: 'tag', tone: 'rose' },
  payroll: { label: 'Bảng lương', iconName: 'credit', tone: 'olive' },
  system: { label: 'Hệ thống', iconName: 'sparkles', tone: 'clay' }
};

function currentMonth() {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}`;
}

function todayInput() {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
}

function isoToDate(value) {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(String(value || ''));
  if (!match) return new Date();
  return new Date(Number(match[1]), Number(match[2]) - 1, Number(match[3]));
}

function dateToIso(date) {
  return `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
}

function addIsoDays(value, days) {
  const date = isoToDate(value);
  date.setDate(date.getDate() + days);
  return dateToIso(date);
}

function isoWeekStart(value) {
  const date = isoToDate(value);
  const offset = (date.getDay() + 6) % 7;
  date.setDate(date.getDate() - offset);
  return dateToIso(date);
}

function weekLabel(value) {
  const start = isoToDate(value);
  const end = isoToDate(addIsoDays(value, 6));
  const pad = number => String(number).padStart(2, '0');
  return `${pad(start.getDate())}/${pad(start.getMonth() + 1)} – ${pad(end.getDate())}/${pad(end.getMonth() + 1)}/${end.getFullYear()}`;
}

function monthLabel(key) {
  if (!key || !/^\d{4}-\d{2}$/.test(key)) return 'Tháng này';
  return `Tháng ${Number(key.slice(5, 7))}/${key.slice(0, 4)}`;
}

function dateOrDash(value) {
  if (!value) return '—';
  const text = String(value);
  if (/^\d{4}-\d{2}-\d{2}$/.test(text)) return formatDate(text) || '—';
  const date = new Date(text);
  if (Number.isNaN(date.getTime())) return formatDate(text) || '—';
  return `${String(date.getDate()).padStart(2, '0')}.${String(date.getMonth() + 1).padStart(2, '0')}.${date.getFullYear()}`;
}

function timeAgo(value) {
  if (!value) return '';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '';
  const minutes = Math.round((Date.now() - date.getTime()) / 60000);
  if (minutes < 1) return 'Vừa xong';
  if (minutes < 60) return `${minutes} phút trước`;
  const hours = Math.round(minutes / 60);
  if (hours < 24) return `${hours} giờ trước`;
  const days = Math.round(hours / 24);
  if (days < 7) return `${days} ngày trước`;
  return dateOrDash(value);
}

function hoursText(value) {
  const amount = Number(value || 0);
  return `${amount.toLocaleString('vi-VN', { maximumFractionDigits: 2 })} giờ`;
}

function minutesText(value) {
  const amount = Number(value || 0);
  if (!amount) return '';
  return `${amount} phút`;
}

function matches(query, values) {
  const needle = stripDiacritics(query).trim();
  if (!needle) return true;
  return values.some(value => stripDiacritics(String(value ?? '')).includes(needle));
}

function sumBy(list, selector) {
  return list.reduce((total, item) => total + Number(selector(item) || 0), 0);
}

function pill(meta) {
  return `<span class="pill pill--${meta.tone}">${icon(meta.iconName)}${escapeHtml(meta.label)}</span>`;
}

function attendancePill(status) {
  return pill(ATTENDANCE_STATUS[status] || ATTENDANCE_STATUS.ontime);
}

function payrollPill(paid) {
  return paid
    ? `<span class="pill pill--paid">${icon('checkCircle')}Đã chi</span>`
    : `<span class="pill pill--unpaid">${icon('clock')}Chưa chi</span>`;
}

function categoryOptions(list, selected) {
  const values = list.slice();
  if (selected && !values.includes(selected)) values.unshift(selected);
  return values.map(value => `<option value="${escapeAttr(value)}" ${value === selected ? 'selected' : ''}>${escapeHtml(value)}</option>`).join('');
}

// ------------------------------------------------------------------- finance

export function financeView({ session }) {
  let teardown = () => {};
  let disposed = false;
  let tab = 'overview';
  let month = currentMonth();
  let overview = null;
  let entries = [];
  let payrolls = [];
  let kindFilter = 'all';
  let loading = false;

  const canManage = hasPermission(session, 'finance');

  const html = crmPage({
    session,
    active: 'finance',
    eyebrow: 'Vận hành studio',
    title: 'Tài chính & lương',
    lead: 'Theo dõi doanh thu, chi phí, công nợ và bảng lương của studio theo từng tháng.',
    actions: `
      <label class="ops-month" aria-label="Chọn tháng tài chính">
        <span>${icon('calendar')}</span>
        <input type="month" data-fin-month value="${escapeAttr(month)}">
      </label>
      ${canManage ? `<button class="btn btn--ghost btn--compact" type="button" data-fin-export>${icon('download')} Xuất Excel</button>` : ''}
    `,
    body: `
      <nav class="ops-tabs" role="tablist" aria-label="Khu vực tài chính">
        <button class="ops-tab is-active" type="button" role="tab" aria-selected="true" data-fin-tab="overview">${icon('chart')} Tổng quan</button>
        <button class="ops-tab" type="button" role="tab" aria-selected="false" data-fin-tab="entries">${icon('receipt')} Thu chi</button>
        <button class="ops-tab" type="button" role="tab" aria-selected="false" data-fin-tab="payrolls">${icon('users')} Bảng lương</button>
      </nav>
      <div data-fin-body aria-live="polite">${skeletonRows(4)}</div>
    `
  });

  function overviewMarkup() {
    const o = overview;
    if (!o) return skeletonRows(4);
    const maxExpense = Math.max(1, ...o.expenseBreakdown.map(item => Number(item.amount || 0)));
    const unpaid = Math.max(0, Number(o.payrollTotal || 0) - Number(o.payrollPaid || 0));
    return `
      <section class="crm-metrics" aria-label="Số liệu tài chính">
        ${metric({
          label: 'Doanh thu',
          value: money(o.totalRevenue),
          note: `Đơn ${money(o.orderRevenue)} · HĐ ${money(o.contractRevenue)}${Number(o.otherIncome || 0) ? ` · Thu khác ${money(o.otherIncome)}` : ''}`,
          tone: 'clay',
          iconName: 'chart'
        })}
        ${metric({
          label: 'Chi phí',
          value: money(o.expenseTotal),
          note: o.expenseBreakdown.length ? `${o.expenseBreakdown.length} danh mục chi` : 'Chưa ghi chi phí',
          tone: 'sand',
          iconName: 'receipt'
        })}
        ${metric({
          label: 'Lương tháng',
          value: money(o.payrollTotal),
          note: o.unpaidPayrollCount ? `${o.unpaidPayrollCount} nhân sự chưa chi` : 'Đã chi đủ',
          tone: 'rose',
          iconName: 'users'
        })}
        ${metric({
          label: 'Lợi nhuận',
          value: money(o.profit),
          note: 'Doanh thu − chi phí − lương',
          tone: 'olive',
          iconName: 'wallet'
        })}
      </section>

      <div class="ops-grid">
        <section class="panel ops-panel" aria-label="Công nợ và nghĩa vụ">
          <header class="panel__head">
            <p class="eyebrow">Công nợ</p>
            <h2>Nghĩa vụ trong tháng</h2>
          </header>
          <ul class="ops-stat-list">
            <li><span>${icon('receipt')} Công nợ đơn hàng</span><strong>${money(o.outstandingDebt)}</strong></li>
            <li><span>${icon('file')} Công nợ hợp đồng</span><strong>${money(o.contractOutstanding)}</strong></li>
            <li><span>${icon('users')} Lương chưa chi</span><strong class="${unpaid > 0 ? 'is-debt' : ''}">${money(unpaid)}</strong></li>
            <li><span>${icon('clock')} Hợp đồng chờ ký</span><strong>${Number(o.pendingContractCount || 0)}</strong></li>
          </ul>
        </section>

        <section class="panel ops-panel" aria-label="Cơ cấu chi phí">
          <header class="panel__head">
            <p class="eyebrow">Chi phí</p>
            <h2>Cơ cấu chi phí</h2>
          </header>
          ${o.expenseBreakdown.length ? `
            <ul class="ops-bars">
              ${o.expenseBreakdown.map(item => `
                <li>
                  <div class="ops-bars__head">
                    <span>${escapeHtml(item.category)}</span>
                    <strong>${money(item.amount)}</strong>
                  </div>
                  <span class="ops-bars__track"><span style="width:${Math.max(4, Math.round(Number(item.amount || 0) / maxExpense * 100))}%"></span></span>
                </li>
              `).join('')}
            </ul>
          ` : `<p class="ops-muted">${icon('receipt')} Chưa ghi khoản chi nào trong ${escapeHtml(monthLabel(o.month))}.</p>`}
        </section>
      </div>

      <section class="panel ops-panel" aria-label="Bảng lương tạm tính">
        <header class="panel__head panel__head--row">
          <div>
            <p class="eyebrow">${escapeHtml(monthLabel(o.month))}</p>
            <h2>Bảng lương tạm tính</h2>
          </div>
          <button class="btn btn--ghost btn--compact" type="button" data-fin-goto="payrolls">Xem bảng lương ${icon('arrowRight')}</button>
        </header>
        ${payrollTable(o.payrollBreakdown, false)}
      </section>

      <section class="panel ops-panel" aria-label="Thu chi gần đây">
        <header class="panel__head panel__head--row">
          <div>
            <p class="eyebrow">Sổ thu chi</p>
            <h2>Ghi nhận gần đây</h2>
          </div>
          <button class="btn btn--text btn--compact" type="button" data-fin-goto="entries">Xem sổ thu chi ${icon('arrowRight')}</button>
        </header>
        ${o.recentEntries.length ? `
          <div class="ops-list">
            ${o.recentEntries.map(entry => entryRow(entry, false)).join('')}
          </div>
        ` : `<p class="ops-muted">${icon('receipt')} Chưa có khoản thu chi nào.</p>`}
      </section>
    `;
  }

  function entryRow(entry, withActions = true) {
    const isIncome = entry.kind === 'income';
    return `
      <article class="ops-row ${isIncome ? 'ops-row--income' : 'ops-row--expense'}" data-finance-row="${escapeAttr(entry.id)}">
        <span class="ops-date-chip">
          <strong>${escapeHtml((entry.date || '').slice(8, 10) || '--')}</strong>
          <small>${escapeHtml(`Th.${Number((entry.date || '').slice(5, 7)) || ''}`)}</small>
        </span>
        <div class="ops-row__main">
          <p class="ops-row__title">
            <span class="fin-kind ${isIncome ? 'fin-kind--income' : 'fin-kind--expense'}">${icon(isIncome ? 'arrowDown' : 'arrowUp')}${isIncome ? 'Thu' : 'Chi'}</span>
            <strong>${escapeHtml(entry.category)}</strong>
            <span class="ops-row__amount ${isIncome ? 'is-income' : 'is-debt'}">${isIncome ? '+' : '−'}${money(entry.amount)}</span>
          </p>
          <p class="ops-row__meta">
            <span>${icon('calendar')}${escapeHtml(dateOrDash(entry.date))}</span>
            ${entry.createdByName ? `<span>${icon('user')}${escapeHtml(entry.createdByName)}</span>` : ''}
            ${entry.photos?.length ? `<span>${icon('image')}${entry.photos.length} ảnh</span>` : ''}
          </p>
          ${entry.note ? `<p class="ops-row__note">${escapeHtml(entry.note)}</p>` : ''}
          ${entry.photos?.length ? `
            <div class="fin-photos">
              ${entry.photos.map(photo => `
                <a class="fin-photo" href="${escapeAttr(photo.url)}" target="_blank" rel="noopener" title="${escapeAttr(photo.caption || photo.name)}">
                  <img src="${escapeAttr(photo.url)}" alt="${escapeAttr(photo.caption || photo.name)}" loading="lazy">
                  <span>${escapeHtml(photo.caption || photo.name)}</span>
                </a>
              `).join('')}
            </div>
          ` : ''}
        </div>
        ${withActions ? `
          <div class="ops-row__actions">
            <button class="icon-btn" type="button" data-finance-edit="${escapeAttr(entry.id)}" aria-label="Sửa khoản ${isIncome ? 'thu' : 'chi'} ${escapeAttr(entry.category)}" title="Sửa">${icon('edit')}</button>
            <button class="icon-btn" type="button" data-finance-delete="${escapeAttr(entry.id)}" aria-label="Xoá khoản ${isIncome ? 'thu' : 'chi'} ${escapeAttr(entry.category)}" title="Xoá">${icon('trash')}</button>
          </div>
        ` : ''}
      </article>
    `;
  }

  function payrollTable(list, withActions) {
    if (!list || !list.length) {
      return `<p class="ops-muted">${icon('users')} Chưa có dữ liệu lương. Bấm “Tính bảng lương” để tổng hợp từ chấm công và doanh thu.</p>`;
    }
    return `
      <div class="table-wrap">
        <table class="ops-table">
          <thead>
            <tr>
              <th>Nhân sự</th>
              <th>Lương cơ bản</th>
              <th>Giờ</th>
              <th>Hoa hồng</th>
              <th>Tổng</th>
              <th>Trạng thái</th>
              ${withActions ? '<th class="ops-table__actions">Thao tác</th>' : ''}
            </tr>
          </thead>
          <tbody>
            ${list.map(item => `
              <tr>
                <td><strong>${escapeHtml(item.staffName)}</strong></td>
                <td>${money(item.base)}</td>
                <td>${hoursText(item.hours)}</td>
                <td>${money(item.commission)}</td>
                <td><strong>${money(item.total)}</strong></td>
                <td>${payrollPill(item.paid)}</td>
                ${withActions ? `
                  <td class="ops-table__actions">
                    <button class="btn btn--ghost btn--compact" type="button" data-payroll-toggle="${escapeAttr(item.staffId)}" data-paid="${item.paid ? 'true' : 'false'}">
                      ${icon(item.paid ? 'refresh' : 'checkCircle')} ${item.paid ? 'Bỏ đánh dấu' : 'Đánh dấu đã chi'}
                    </button>
                  </td>
                ` : ''}
              </tr>
            `).join('')}
          </tbody>
        </table>
      </div>
    `;
  }

  function filteredEntries() {
    return entries.filter(entry => kindFilter === 'all' || entry.kind === kindFilter);
  }

  function entriesMarkup() {
    const list = filteredEntries();
    const incomeTotal = sumBy(entries.filter(entry => entry.kind === 'income'), entry => entry.amount);
    const expenseTotal = sumBy(entries.filter(entry => entry.kind === 'expense'), entry => entry.amount);
    const filters = [
      { key: 'all', label: 'Tất cả' },
      { key: 'income', label: 'Thu' },
      { key: 'expense', label: 'Chi' }
    ];
    return `
      <section class="ops-summary-bar" aria-label="Tóm tắt thu chi">
        <div><span>${icon('arrowDown')} Thu trong tháng</span><strong class="is-income">${money(incomeTotal)}</strong></div>
        <div><span>${icon('arrowUp')} Chi trong tháng</span><strong class="is-debt">${money(expenseTotal)}</strong></div>
        <div><span>${icon('wallet')} Chênh lệch</span><strong>${money(incomeTotal - expenseTotal)}</strong></div>
      </section>
      <section class="crm-toolbar" aria-label="Bộ lọc thu chi">
        <div class="ops-segmented" role="group" aria-label="Lọc loại thu chi">
          ${filters.map(item => `
            <button class="ops-segmented__item ${kindFilter === item.key ? 'is-active' : ''}" type="button" data-fin-kind="${item.key}" aria-pressed="${kindFilter === item.key ? 'true' : 'false'}">${item.label}</button>
          `).join('')}
        </div>
        <span class="crm-toolbar__count">${list.length} khoản trong ${escapeHtml(monthLabel(month))}</span>
        <span class="ops-toolbar__spacer"></span>
        ${canManage ? `<button class="btn btn--ghost btn--compact" type="button" data-fin-export>${icon('download')} Xuất Excel</button>` : ''}
        ${canManage ? `<button class="btn btn--primary btn--compact" type="button" data-finance-create>${icon('plus')} Thêm thu chi</button>` : ''}
      </section>
      <section class="panel crm-list ops-list-panel" aria-label="Danh sách thu chi">
        ${list.length
          ? `<div class="ops-list">${list.map(entry => entryRow(entry, canManage)).join('')}</div>`
          : emptyState({
            iconName: 'receipt',
            title: kindFilter === 'income' ? 'Chưa có khoản thu nào' : kindFilter === 'expense' ? 'Chưa có khoản chi nào' : 'Sổ thu chi đang trống',
            message: `Ghi lại các khoản thu và chi của ${monthLabel(month).toLowerCase()} để theo dõi lợi nhuận chính xác.`,
            action: canManage ? '<button class="btn btn--primary" type="button" data-finance-create>Thêm khoản đầu tiên</button>' : ''
          })}
      </section>
    `;
  }

  function payrollsMarkup() {
    const total = sumBy(payrolls, item => item.total);
    const paid = sumBy(payrolls.filter(item => item.paid), item => item.total);
    return `
      <section class="ops-summary-bar" aria-label="Tóm tắt bảng lương">
        <div><span>${icon('calendar')} Kỳ lương</span><strong>${escapeHtml(monthLabel(month))}</strong></div>
        <div><span>${icon('users')} Nhân sự</span><strong>${payrolls.length}</strong></div>
        <div><span>${icon('wallet')} Tổng lương</span><strong>${money(total)}</strong></div>
        <div><span>${icon('checkCircle')} Đã chi</span><strong>${money(paid)}</strong></div>
        ${canManage ? `<button class="btn btn--primary btn--compact" type="button" data-payroll-compute>${icon('refreshCw')} Tính bảng lương</button>` : ''}
      </section>
      <section class="panel ops-panel">
        ${payrollTable(payrolls, canManage)}
      </section>
    `;
  }

  function render() {
    const body = document.querySelector('[data-fin-body]');
    if (!body) return;
    body.innerHTML = tab === 'overview'
      ? overviewMarkup()
      : tab === 'entries'
        ? entriesMarkup()
        : payrollsMarkup();
    document.querySelectorAll('[data-fin-tab]').forEach(button => {
      const active = button.dataset.finTab === tab;
      button.classList.toggle('is-active', active);
      button.setAttribute('aria-selected', active ? 'true' : 'false');
    });
  }

  async function load() {
    if (loading || disposed) return;
    loading = true;
    const body = document.querySelector('[data-fin-body]');
    if (body) body.innerHTML = skeletonRows(4);
    try {
      const [overviewData, entryData, payrollData] = await Promise.all([
        operationsApi.financeOverview(month),
        operationsApi.financeEntries(month, 'all'),
        operationsApi.payrolls(month)
      ]);
      if (disposed) return;
      overview = overviewData;
      entries = Array.isArray(entryData) ? entryData : [];
      payrolls = Array.isArray(payrollData) ? payrollData : [];
      render();
    } catch (error) {
      if (disposed) return;
      if (body) body.innerHTML = loadError(error, 'data-fin-retry');
    } finally {
      loading = false;
    }
  }

  function openEntryForm(entry = null) {
    const isEdit = Boolean(entry);
    let savedEntryId = entry?.id || null;
    let saving = false;
    let selectedKind = entry?.kind === 'income' ? 'income' : 'expense';
    let existingPhotos = (entry?.photos || []).map(photo => ({ ...photo }));
    const removedPhotoIds = new Set();
    const pendingFiles = [];
    const previewUrls = new Map();

    const modal = createModal(`
      <div class="dialog-card dialog-card--crm">
        <button class="dialog-close" type="button" data-close-dialog aria-label="Đóng">${icon('x')}</button>
        <header class="task-dialog__head">
          <span class="task-dialog__icon">${icon('receipt')}</span>
          <div>
            <p class="eyebrow">${isEdit ? 'Cập nhật sổ thu chi' : 'Sổ thu chi mới'}</p>
            <h2>${isEdit ? 'Sửa khoản thu chi' : 'Thêm khoản thu chi'}</h2>
          </div>
        </header>
        <form class="crm-form" data-finance-form novalidate>
          <div class="fin-kind-toggle" role="group" aria-label="Loại giao dịch">
            <button class="fin-kind-toggle__btn ${selectedKind === 'expense' ? 'is-active' : ''}" type="button" data-kind-choice="expense" aria-pressed="${selectedKind === 'expense' ? 'true' : 'false'}">${icon('arrowUp')} Khoản chi</button>
            <button class="fin-kind-toggle__btn ${selectedKind === 'income' ? 'is-active' : ''}" type="button" data-kind-choice="income" aria-pressed="${selectedKind === 'income' ? 'true' : 'false'}">${icon('arrowDown')} Khoản thu</button>
          </div>
          <div class="crm-form__grid">
            <div class="field">
              <label for="finance-date">Ngày <span class="required">*</span></label>
              <input id="finance-date" name="date" type="date" value="${escapeAttr(entry?.date || todayInput())}" required>
            </div>
            <div class="field">
              <label for="finance-amount">Số tiền (đ) <span class="required">*</span></label>
              <input id="finance-amount" name="amount" type="number" min="1" step="1000" value="${Number(entry?.amount || 0)}" required>
            </div>
            <div class="field crm-form__full">
              <label for="finance-category">Danh mục</label>
              <input id="finance-category" name="category" type="text" maxlength="60" list="finance-expense-categories" value="${escapeAttr(entry?.category || 'Khác')}" placeholder="Ví dụ: Thuê studio">
              <datalist id="finance-expense-categories">
                ${EXPENSE_CATEGORIES.map(category => `<option value="${escapeAttr(category)}"></option>`).join('')}
              </datalist>
              <datalist id="finance-income-categories">
                ${INCOME_CATEGORIES.map(category => `<option value="${escapeAttr(category)}"></option>`).join('')}
              </datalist>
            </div>
            <div class="field crm-form__full">
              <label for="finance-note">Ghi chú</label>
              <textarea id="finance-note" name="note" maxlength="500" rows="2" placeholder="Nội dung thu chi, người nhận…">${escapeHtml(entry?.note || '')}</textarea>
            </div>
            <div class="field crm-form__full">
              <label>Ảnh chứng từ</label>
              <div class="fin-upload">
                <label class="btn btn--ghost btn--compact" for="finance-photo-input">${icon('camera')} Chọn ảnh</label>
                <input id="finance-photo-input" type="file" accept="image/jpeg,image/png,image/webp" multiple hidden>
                <p class="fin-upload__hint">JPG, PNG hoặc WebP · tối đa 5 MB mỗi ảnh · tối đa 6 ảnh cho mỗi khoản.</p>
              </div>
              <div class="fin-photo-edit" data-photo-list></div>
            </div>
          </div>
          <div data-form-error></div>
          <div class="dialog-actions">
            <button class="btn btn--ghost" type="button" data-close-dialog>Huỷ</button>
            <button class="btn btn--primary" type="submit" data-submit>${icon('check')} ${isEdit ? 'Lưu thay đổi' : 'Ghi vào sổ'}</button>
          </div>
        </form>
      </div>
    `, {
      className: 'dialog--crm',
      onClose: () => previewUrls.forEach(url => URL.revokeObjectURL(url))
    });

    const form = modal.dialog.querySelector('[data-finance-form]');
    const categoryInput = form.elements.namedItem('category');
    const photoInput = form.querySelector('#finance-photo-input');
    const photoList = form.querySelector('[data-photo-list]');
    const kindButtons = [...form.querySelectorAll('[data-kind-choice]')];

    function syncKindUi() {
      const isIncome = selectedKind === 'income';
      categoryInput.setAttribute('list', isIncome ? 'finance-income-categories' : 'finance-expense-categories');
      if (!categoryInput.value.trim()) {
        categoryInput.value = isIncome ? 'Thu khác' : 'Khác';
      }
      kindButtons.forEach(button => {
        const active = button.dataset.kindChoice === selectedKind;
        button.classList.toggle('is-active', active);
        button.setAttribute('aria-pressed', active ? 'true' : 'false');
      });
    }

    function photoCount() {
      return existingPhotos.length - removedPhotoIds.size + pendingFiles.length;
    }

    function renderPhotos() {
      const cards = [];
      existingPhotos.forEach(photo => {
        if (removedPhotoIds.has(photo.id)) return;
        cards.push(`
          <div class="fin-photo-card" data-existing-photo="${escapeAttr(photo.id)}">
            <a href="${escapeAttr(photo.url)}" target="_blank" rel="noopener">
              <img src="${escapeAttr(photo.url)}" alt="${escapeAttr(photo.caption || photo.name)}" loading="lazy">
            </a>
            <input type="text" maxlength="120" placeholder="Ghi chú ảnh…" value="${escapeAttr(photo.caption || '')}" data-photo-caption="${escapeAttr(photo.id)}" aria-label="Ghi chú ảnh ${escapeAttr(photo.name)}">
            <button class="icon-btn" type="button" data-photo-remove="${escapeAttr(photo.id)}" aria-label="Bỏ ảnh ${escapeAttr(photo.name)}" title="Bỏ ảnh">${icon('trash')}</button>
          </div>
        `);
      });
      pendingFiles.forEach(item => {
        cards.push(`
          <div class="fin-photo-card" data-pending-photo="${escapeAttr(item.id)}">
            <img src="${escapeAttr(item.preview)}" alt="${escapeAttr(item.file.name)}" loading="lazy">
            <input type="text" maxlength="120" placeholder="Ghi chú ảnh…" value="${escapeAttr(item.caption)}" data-pending-caption="${escapeAttr(item.id)}" aria-label="Ghi chú ảnh ${escapeAttr(item.file.name)}">
            <button class="icon-btn" type="button" data-pending-remove="${escapeAttr(item.id)}" aria-label="Bỏ ảnh ${escapeAttr(item.file.name)}" title="Bỏ ảnh">${icon('x')}</button>
          </div>
        `);
      });
      photoList.innerHTML = cards.length
        ? cards.join('')
        : '<p class="fin-photo-empty">Chưa gắn ảnh chứng từ nào.</p>';
    }

    function addFiles(fileList) {
      const allowed = ['image/jpeg', 'image/png', 'image/webp'];
      for (const file of fileList) {
        if (photoCount() >= 6) {
          showToast('Mỗi khoản thu chi chỉ gắn tối đa 6 ảnh.', 'error');
          break;
        }
        if (!allowed.includes(file.type)) {
          showToast(`${file.name}: chỉ nhận ảnh JPG, PNG hoặc WebP.`, 'error');
          continue;
        }
        if (file.size > 5 * 1024 * 1024) {
          showToast(`${file.name}: ảnh vượt quá 5 MB.`, 'error');
          continue;
        }
        const preview = URL.createObjectURL(file);
        const id = `pending-${Date.now()}-${Math.random().toString(16).slice(2, 8)}`;
        previewUrls.set(id, preview);
        pendingFiles.push({ id, file, caption: '', preview });
      }
      renderPhotos();
    }

    kindButtons.forEach(button => {
      button.addEventListener('click', () => {
        selectedKind = button.dataset.kindChoice === 'income' ? 'income' : 'expense';
        syncKindUi();
      });
    });

    photoInput.addEventListener('change', () => {
      addFiles([...photoInput.files]);
      photoInput.value = '';
    });

    photoList.addEventListener('click', event => {
      const existingRemove = event.target.closest('[data-photo-remove]');
      if (existingRemove) {
        removedPhotoIds.add(existingRemove.dataset.photoRemove);
        renderPhotos();
        return;
      }
      const pendingRemove = event.target.closest('[data-pending-remove]');
      if (pendingRemove) {
        const index = pendingFiles.findIndex(item => item.id === pendingRemove.dataset.pendingRemove);
        if (index >= 0) {
          const [removed] = pendingFiles.splice(index, 1);
          const url = previewUrls.get(removed.id);
          if (url) URL.revokeObjectURL(url);
          previewUrls.delete(removed.id);
        }
        renderPhotos();
      }
    });

    photoList.addEventListener('input', event => {
      const existingCaption = event.target.closest('[data-photo-caption]');
      if (existingCaption) {
        const photo = existingPhotos.find(item => item.id === existingCaption.dataset.photoCaption);
        if (photo) photo.caption = existingCaption.value;
        return;
      }
      const pendingCaption = event.target.closest('[data-pending-caption]');
      if (pendingCaption) {
        const item = pendingFiles.find(entryFile => entryFile.id === pendingCaption.dataset.pendingCaption);
        if (item) item.caption = pendingCaption.value;
      }
    });

    syncKindUi();
    renderPhotos();

    form.addEventListener('submit', async event => {
      event.preventDefault();
      if (saving) return;
      clearFieldErrors(form);
      const errorRoot = form.querySelector('[data-form-error]');
      errorRoot.innerHTML = '';
      const dateField = form.elements.namedItem('date');
      const amountField = form.elements.namedItem('amount');
      const amount = Number(amountField.value || 0);
      if (!dateField.value) {
        fieldError(dateField, 'Chọn ngày ghi sổ.');
        return;
      }
      if (!amount || amount <= 0) {
        fieldError(amountField, 'Số tiền phải lớn hơn 0.');
        return;
      }

      const isIncome = selectedKind === 'income';
      const payload = {
        kind: selectedKind,
        date: dateField.value,
        amount,
        category: categoryInput.value.trim() || (isIncome ? 'Thu khác' : 'Khác'),
        note: form.elements.namedItem('note').value.trim()
      };
      const submit = form.querySelector('[data-submit]');
      saving = true;
      form.inert = true;
      setButtonLoading(submit, true, 'Đang lưu…');
      let saved;
      try {
        if (savedEntryId) {
          payload.photos = existingPhotos
            .filter(photo => !removedPhotoIds.has(photo.id))
            .map(photo => ({ id: photo.id, caption: photo.caption || '' }));
          saved = await operationsApi.updateFinanceEntry(savedEntryId, payload);
        } else {
          saved = await operationsApi.createFinanceEntry(payload);
        }
        savedEntryId = saved.id;
        existingPhotos = saved.photos.map(photo => ({ ...photo }));
      } catch (error) {
        saving = false;
        form.inert = false;
        if (error.name === 'AbortError') return;
        setButtonLoading(submit, false);
        errorRoot.innerHTML = errorInline(error.message || 'Chưa lưu được khoản thu chi.', error.code);
        return;
      }

      let uploadFailed = false;
      const entryId = savedEntryId;
      try {
        for (const photoId of [...removedPhotoIds]) {
          await operationsApi.deleteFinancePhoto(entryId, photoId);
          removedPhotoIds.delete(photoId);
          existingPhotos = existingPhotos.filter(photo => photo.id !== photoId);
        }
        while (pendingFiles.length) {
          const item = pendingFiles[0];
          const photo = await operationsApi.uploadFinancePhoto(entryId, item.file, item.caption);
          existingPhotos.push(photo);
          pendingFiles.shift();
          URL.revokeObjectURL(item.preview);
          previewUrls.delete(item.id);
        }
      } catch (error) {
        uploadFailed = true;
        errorRoot.innerHTML = errorInline(error.message || 'Khoản đã lưu nhưng ảnh chưa tải lên được.', error.code);
      }

      saving = false;
      form.inert = false;
      if (uploadFailed) {
        renderPhotos();
        setButtonLoading(submit, false);
        showToast('Đã lưu khoản thu chi, nhưng một số ảnh chưa tải lên được.', 'error');
        load();
        return;
      }

      closeModalFrom(form);
      showToast(isEdit ? 'Đã cập nhật khoản thu chi.' : 'Đã ghi khoản thu chi mới.');
      load();
    });
  }

  async function deleteEntry(id) {
    const entry = entries.find(item => item.id === id);
    const label = entry?.kind === 'income' ? 'khoản thu' : 'khoản chi';
    const confirmed = await confirmDialog({
      title: `Xoá ${label}?`,
      message: `${entry ? money(entry.amount) : ''} sẽ bị xoá khỏi sổ thu chi và báo cáo lợi nhuận.`,
      confirmLabel: 'Xoá khoản này',
      danger: true
    });
    if (!confirmed) return;
    try {
      await operationsApi.deleteFinanceEntry(id);
      showToast(`Đã xoá ${label}.`);
      load();
    } catch (error) {
      showToast(error.message || 'Chưa xoá được khoản thu chi.', 'error');
    }
  }

  async function exportExcel(button) {
    setButtonLoading(button, true, 'Đang xuất…');
    try {
      const url = tab === 'entries'
        ? operationsApi.exportFinanceEntriesUrl(month, kindFilter)
        : operationsApi.exportFinanceWorkbookUrl(month);
      const fileName = await downloadFile(url, `tai-chinh-${month || 'thang'}.xlsx`);
      showToast(`Đã xuất ${fileName}.`);
    } catch (error) {
      showToast(error.message || 'Chưa xuất được tệp Excel.', 'error');
    } finally {
      setButtonLoading(button, false);
    }
  }

  async function computePayrolls() {
    const confirmed = await confirmDialog({
      title: `Tính bảng lương ${monthLabel(month)}?`,
      message: 'Bảng lương sẽ được tính lại từ lương cơ bản, giờ chấm công và doanh thu đơn hàng của từng nhân sự. Trạng thái đã chi được giữ nguyên.',
      confirmLabel: 'Tính bảng lương'
    });
    if (!confirmed) return;
    try {
      const result = await operationsApi.computePayrolls(month);
      showToast(`Đã tính lương ${monthLabel(month)} cho ${result.count} nhân sự.`);
      load();
    } catch (error) {
      showToast(error.message || 'Chưa tính được bảng lương.', 'error');
    }
  }

  async function togglePayroll(button) {
    const staffId = button.dataset.payrollToggle;
    const payroll = payrolls.find(item => item.staffId === staffId);
    if (!payroll) return;
    const next = !payroll.paid;
    setButtonLoading(button, true, 'Đang lưu…');
    try {
      await operationsApi.togglePayrollPaid(payroll.id, next);
      payroll.paid = next;
      showToast(next ? `Đã đánh dấu chi lương cho ${payroll.staffName}.` : `Đã bỏ đánh dấu chi lương cho ${payroll.staffName}.`);
      render();
    } catch (error) {
      setButtonLoading(button, false);
      showToast(error.message || 'Chưa cập nhật được trạng thái lương.', 'error');
    }
  }

  return {
    title: 'Tài chính & lương',
    bodyClass: 'page-admin',
    html,
    async mount(root) {
      teardown = mountAdminShell(root, session);
      const page = root.querySelector('[data-crm-page]');
      page.addEventListener('click', event => {
        const tabButton = event.target.closest('[data-fin-tab]');
        if (tabButton) {
          tab = tabButton.dataset.finTab;
          render();
          return;
        }

        if (event.target.closest('[data-fin-retry]')) {
          load();
          return;
        }

        if (event.target.closest('[data-fin-export]')) {
          exportExcel(event.target.closest('[data-fin-export]'));
          return;
        }

        if (event.target.closest('[data-finance-create]')) {
          openEntryForm();
          return;
        }

        const kindButton = event.target.closest('[data-fin-kind]');
        if (kindButton) {
          kindFilter = kindButton.dataset.finKind || 'all';
          render();
          return;
        }

        const financeEdit = event.target.closest('[data-finance-edit]');
        if (financeEdit) {
          const entry = entries.find(item => item.id === financeEdit.dataset.financeEdit);
          if (entry) openEntryForm(entry);
          return;
        }

        const financeDelete = event.target.closest('[data-finance-delete]');
        if (financeDelete) {
          deleteEntry(financeDelete.dataset.financeDelete);
          return;
        }

        if (event.target.closest('[data-payroll-compute]')) {
          computePayrolls();
          return;
        }

        const payrollToggle = event.target.closest('[data-payroll-toggle]');
        if (payrollToggle) {
          togglePayroll(payrollToggle);
          return;
        }

        const goto = event.target.closest('[data-fin-goto]');
        if (goto) {
          tab = goto.dataset.finGoto;
          render();
        }
      });

      page.addEventListener('change', event => {
        if (event.target.matches('[data-fin-month]')) {
          month = event.target.value || currentMonth();
          load();
        }
      });

      await load();
    },
    cleanup() {
      disposed = true;
      teardown();
    }
  };
}

// ---------------------------------------------------------------- attendance

export function attendanceView({ session }) {
  let teardown = () => {};
  let disposed = false;
  let tab = 'day';
  let date = todayInput();
  let month = date.slice(0, 7);
  let dayRecords = [];
  let monthRows = [];
  let staff = [];
  let staffPromise = null;
  let loading = false;

  const canManage = hasPermission(session, 'attendance');

  const html = crmPage({
    session,
    active: 'attendance',
    eyebrow: 'Vận hành studio',
    title: 'Chấm công',
    lead: 'Ghi nhận giờ vào – ra, đối chiếu ca làm việc và tổng hợp ngày công cho bảng lương.',
    body: `
      <nav class="ops-tabs" role="tablist" aria-label="Khu vực chấm công">
        <button class="ops-tab is-active" type="button" role="tab" aria-selected="true" data-att-tab="day">${icon('calendar')} Theo ngày</button>
        <button class="ops-tab" type="button" role="tab" aria-selected="false" data-att-tab="month">${icon('chart')} Tổng hợp tháng</button>
      </nav>
      <div data-att-body aria-live="polite">${skeletonRows(4)}</div>
    `
  });

  function ensureStaff() {
    if (!staffPromise) {
      staffPromise = crmApi.staff()
        .then(list => {
          staff = Array.isArray(list) ? list : [];
          return staff;
        })
        .catch(error => {
          staffPromise = null;
          throw error;
        });
    }
    return staffPromise;
  }

  function dayMarkup() {
    const totalHours = sumBy(dayRecords, record => record.hours);
    return `
      <section class="crm-toolbar" aria-label="Chọn ngày chấm công">
        <label class="ops-date" aria-label="Chọn ngày chấm công">
          <span>${icon('calendar')}</span>
          <input type="date" data-att-date value="${escapeAttr(date)}">
        </label>
        <span class="crm-toolbar__count">${dayRecords.length} bản ghi</span>
        <span class="ops-toolbar__spacer"></span>
        ${canManage ? `<button class="btn btn--primary btn--compact" type="button" data-att-create>${icon('plus')} Chấm công</button>` : ''}
      </section>

      <section class="crm-metrics" aria-label="Số liệu chấm công trong ngày">
        ${metric({
          label: 'Có mặt',
          value: String(dayRecords.filter(record => record.in).length),
          note: `${staff.filter(member => member.isActive).length} nhân sự đang làm`,
          tone: 'olive',
          iconName: 'users'
        })}
        ${metric({
          label: 'Đang trong ca',
          value: String(dayRecords.filter(record => record.status === 'open').length),
          note: 'chưa ghi giờ ra',
          tone: 'sand',
          iconName: 'clock'
        })}
        ${metric({
          label: 'Đi muộn',
          value: String(dayRecords.filter(record => Number(record.lateMinutes) > 0).length),
          note: 'so với giờ vào của ca',
          tone: 'rose',
          iconName: 'alertCircle'
        })}
        ${metric({
          label: 'Tổng giờ',
          value: hoursText(totalHours),
          note: `ngày ${dateOrDash(date)}`,
          tone: 'clay',
          iconName: 'chart'
        })}
      </section>

      <section class="panel crm-list ops-list-panel" aria-label="Bản ghi chấm công">
        ${dayRecords.length ? `
          <div class="ops-list">
            ${dayRecords.map(record => attendanceRow(record)).join('')}
          </div>
        ` : emptyState({
          iconName: 'clock',
          title: 'Chưa có chấm công',
          message: `Chưa ai được ghi nhận giờ vào – ra trong ngày ${dateOrDash(date)}.`,
          action: canManage ? '<button class="btn btn--primary" type="button" data-att-create>Chấm công đầu tiên</button>' : ''
        })}
      </section>
    `;
  }

  function attendanceRow(record) {
    const member = staff.find(item => item.id === record.staffId);
    const shift = SHIFT_LABELS[record.shift] || SHIFT_LABELS[member?.shift] || 'Theo ca';
    const meta = [
      shift,
      record.in && record.out ? `${record.in} – ${record.out}` : record.in ? `Vào ${record.in}` : '',
      record.hours ? hoursText(record.hours) : '',
      record.lateMinutes ? `Muộn ${minutesText(record.lateMinutes)}` : '',
      record.earlyMinutes ? `Sớm ${minutesText(record.earlyMinutes)}` : ''
    ].filter(Boolean);
    return `
      <article class="ops-row" data-att-row="${escapeAttr(record.id)}">
        <span class="avatar avatar--olive" aria-hidden="true">${escapeHtml(initials(record.staffName))}</span>
        <div class="ops-row__main">
          <p class="ops-row__title">
            <strong>${escapeHtml(record.staffName)}</strong>
            ${attendancePill(record.status)}
          </p>
          <p class="ops-row__meta">
            ${meta.map(value => `<span>${escapeHtml(value)}</span>`).join('<span aria-hidden="true">·</span>')}
          </p>
          ${record.note ? `<p class="ops-row__note">${escapeHtml(record.note)}</p>` : ''}
        </div>
        ${canManage ? `
          <div class="ops-row__actions">
            <button class="icon-btn" type="button" data-att-edit="${escapeAttr(record.id)}" aria-label="Sửa chấm công của ${escapeAttr(record.staffName)}" title="Sửa">${icon('edit')}</button>
            <button class="icon-btn" type="button" data-att-delete="${escapeAttr(record.id)}" aria-label="Xoá chấm công của ${escapeAttr(record.staffName)}" title="Xoá">${icon('trash')}</button>
          </div>
        ` : ''}
      </article>
    `;
  }

  function monthMarkup() {
    const days = sumBy(monthRows, row => row.daysWorked);
    const hours = sumBy(monthRows, row => row.hours);
    const late = sumBy(monthRows, row => row.lateCount);
    const missingOut = sumBy(monthRows, row => row.missingOutCount);
    return `
      <section class="crm-toolbar" aria-label="Chọn tháng chấm công">
        <label class="ops-month" aria-label="Chọn tháng tổng hợp">
          <span>${icon('calendar')}</span>
          <input type="month" data-att-month value="${escapeAttr(month)}">
        </label>
        <span class="crm-toolbar__count">${monthRows.length} nhân sự · ${escapeHtml(monthLabel(month))}</span>
      </section>

      <section class="crm-metrics" aria-label="Số liệu chấm công trong tháng">
        ${metric({ label: 'Ngày công', value: String(days), note: 'tổng số lượt chấm công', tone: 'olive', iconName: 'calendar' })}
        ${metric({ label: 'Tổng giờ', value: hoursText(hours), note: 'đã làm tròn 15 phút', tone: 'clay', iconName: 'chart' })}
        ${metric({ label: 'Lượt đi muộn', value: String(late), note: 'cần nhắc nhở', tone: 'rose', iconName: 'alertCircle' })}
        ${metric({ label: 'Thiếu giờ ra', value: String(missingOut), note: 'chưa tan ca', tone: 'sand', iconName: 'clock' })}
      </section>

      <section class="panel ops-panel" aria-label="Bảng tổng hợp chấm công">
        ${monthRows.length ? `
          <div class="table-wrap">
            <table class="ops-table">
              <thead>
                <tr>
                  <th>Nhân sự</th>
                  <th>Ca làm</th>
                  <th>Ngày công</th>
                  <th>Giờ</th>
                  <th>Đi muộn</th>
                  <th>Về sớm</th>
                  <th>Thiếu giờ ra</th>
                </tr>
              </thead>
              <tbody>
                ${monthRows.map(row => `
                  <tr>
                    <td><strong>${escapeHtml(row.staffName)}</strong></td>
                    <td>${escapeHtml(SHIFT_SHORT[row.shift] || 'Ca thường')}</td>
                    <td>${Number(row.daysWorked || 0)}</td>
                    <td>${hoursText(row.hours)}</td>
                    <td class="${row.lateCount ? 'is-debt' : ''}">${Number(row.lateCount || 0)}</td>
                    <td class="${row.earlyCount ? 'is-debt' : ''}">${Number(row.earlyCount || 0)}</td>
                    <td class="${row.missingOutCount ? 'is-debt' : ''}">${Number(row.missingOutCount || 0)}</td>
                  </tr>
                `).join('')}
              </tbody>
            </table>
          </div>
        ` : emptyState({
          iconName: 'clock',
          title: 'Chưa có dữ liệu tháng',
          message: `Chưa có bản ghi chấm công nào trong ${monthLabel(month).toLowerCase()}.`
        })}
      </section>
    `;
  }

  function render() {
    const body = document.querySelector('[data-att-body]');
    if (!body) return;
    body.innerHTML = tab === 'day' ? dayMarkup() : monthMarkup();
    document.querySelectorAll('[data-att-tab]').forEach(button => {
      const active = button.dataset.attTab === tab;
      button.classList.toggle('is-active', active);
      button.setAttribute('aria-selected', active ? 'true' : 'false');
    });
  }

  async function load() {
    if (loading || disposed) return;
    loading = true;
    const body = document.querySelector('[data-att-body]');
    if (body) body.innerHTML = skeletonRows(4);
    try {
      if (tab === 'day') {
        const [records] = await Promise.all([operationsApi.attendance(date), ensureStaff()]);
        if (disposed) return;
        dayRecords = Array.isArray(records) ? records : [];
      } else {
        const rows = await operationsApi.attendanceMonth(month);
        if (disposed) return;
        monthRows = Array.isArray(rows) ? rows : [];
      }
      render();
    } catch (error) {
      if (disposed) return;
      if (body) body.innerHTML = loadError(error, 'data-att-retry');
    } finally {
      loading = false;
    }
  }

  async function openAttendanceForm(record = null) {
    const isEdit = Boolean(record);
    try {
      await ensureStaff();
    } catch (error) {
      showToast(error.message || 'Không tải được danh sách nhân sự.', 'error');
      return;
    }
    if (disposed) return;
    const activeStaff = staff.filter(member => member.isActive || member.id === record?.staffId);
    const staffOptions = activeStaff.map(member => ({
      value: member.id,
      label: `${member.name} · ${SHIFT_SHORT[member.shift] || 'Ca thường'}`
    }));
    const modal = createModal(`
      <div class="dialog-card dialog-card--crm">
        <button class="dialog-close" type="button" data-close-dialog aria-label="Đóng">${icon('x')}</button>
        <header class="task-dialog__head">
          <span class="task-dialog__icon">${icon('clock')}</span>
          <div>
            <p class="eyebrow">${isEdit ? 'Điều chỉnh chấm công' : 'Chấm công mới'}</p>
            <h2>${isEdit ? 'Sửa giờ vào – ra' : 'Ghi nhận chấm công'}</h2>
          </div>
        </header>
        <form class="crm-form" data-att-form novalidate>
          <div class="crm-form__grid">
            <div class="field crm-form__full">
              <label for="att-staff">Nhân sự <span class="required">*</span></label>
              ${isEdit
                ? `<input id="att-staff" type="text" value="${escapeAttr(record.staffName)}" disabled>`
                : `<select id="att-staff" name="staffId">${optionList(staffOptions, '', 'Chọn nhân sự')}</select>`}
            </div>
            <div class="field">
              <label for="att-date">Ngày <span class="required">*</span></label>
              <input id="att-date" name="date" type="date" value="${escapeAttr(record?.date || date)}" ${isEdit ? 'disabled' : ''} required>
            </div>
            <div class="field">
              <label for="att-in">Giờ vào</label>
              <input id="att-in" name="in" type="time" value="${escapeAttr(record?.in || '')}">
            </div>
            <div class="field">
              <label for="att-out">Giờ ra</label>
              <input id="att-out" name="out" type="time" value="${escapeAttr(record?.out || '')}">
            </div>
            <div class="field crm-form__full">
              <label for="att-note">Ghi chú</label>
              <textarea id="att-note" name="note" maxlength="500" rows="2" placeholder="Đi muộn, về sớm, lý do…">${escapeHtml(record?.note || '')}</textarea>
            </div>
          </div>
          <div data-form-error></div>
          <div class="dialog-actions">
            <button class="btn btn--ghost" type="button" data-close-dialog>Huỷ</button>
            <button class="btn btn--primary" type="submit" data-submit>${icon('check')} ${isEdit ? 'Lưu thay đổi' : 'Lưu chấm công'}</button>
          </div>
        </form>
      </div>
    `, { className: 'dialog--crm' });

    const form = modal.dialog.querySelector('[data-att-form]');
    form.addEventListener('submit', async event => {
      event.preventDefault();
      clearFieldErrors(form);
      const errorRoot = form.querySelector('[data-form-error]');
      errorRoot.innerHTML = '';
      const staffField = form.elements.namedItem('staffId');
      const dateField = form.elements.namedItem('date');
      const inField = form.elements.namedItem('in');
      const outField = form.elements.namedItem('out');
      if (!isEdit && !staffField.value) {
        fieldError(staffField, 'Chọn nhân sự cần chấm công.');
        return;
      }
      if (!isEdit && !dateField.value) {
        fieldError(dateField, 'Chọn ngày chấm công.');
        return;
      }
      if (!inField.value && !outField.value) {
        fieldError(inField, 'Nhập ít nhất giờ vào hoặc giờ ra.');
        return;
      }
      if (inField.value && outField.value && outField.value <= inField.value) {
        fieldError(outField, 'Giờ ra phải sau giờ vào.');
        return;
      }
      const submit = form.querySelector('[data-submit]');
      setButtonLoading(submit, true, 'Đang lưu…');
      try {
        if (isEdit) {
          await operationsApi.updateAttendance(record.id, {
            in: inField.value,
            out: outField.value,
            note: form.elements.namedItem('note').value.trim()
          });
        } else {
          await operationsApi.createAttendance({
            staffId: staffField.value,
            date: dateField.value,
            in: inField.value,
            out: outField.value,
            note: form.elements.namedItem('note').value.trim()
          });
        }
      } catch (error) {
        if (error.name === 'AbortError') return;
        setButtonLoading(submit, false);
        errorRoot.innerHTML = errorInline(error.message || 'Chưa lưu được chấm công.', error.code);
        return;
      }
      closeModalFrom(form);
      showToast(isEdit ? 'Đã cập nhật chấm công.' : 'Đã ghi nhận chấm công.');
      load();
    });
  }

  async function deleteAttendance(id) {
    const record = dayRecords.find(item => item.id === id);
    const confirmed = await confirmDialog({
      title: 'Xoá bản ghi chấm công?',
      message: `${record ? `${record.staffName} ngày ${dateOrDash(record.date)}` : 'Bản ghi này'} sẽ bị xoá khỏi bảng công và bảng lương.`,
      confirmLabel: 'Xoá chấm công',
      danger: true
    });
    if (!confirmed) return;
    try {
      await operationsApi.deleteAttendance(id);
      showToast('Đã xoá bản ghi chấm công.');
      load();
    } catch (error) {
      showToast(error.message || 'Chưa xoá được chấm công.', 'error');
    }
  }

  return {
    title: 'Chấm công',
    bodyClass: 'page-admin',
    html,
    async mount(root) {
      teardown = mountAdminShell(root, session);
      const page = root.querySelector('[data-crm-page]');
      page.addEventListener('click', event => {
        const tabButton = event.target.closest('[data-att-tab]');
        if (tabButton) {
          tab = tabButton.dataset.attTab;
          load();
          return;
        }

        if (event.target.closest('[data-att-retry]')) {
          load();
          return;
        }

        if (event.target.closest('[data-att-create]')) {
          openAttendanceForm();
          return;
        }

        const edit = event.target.closest('[data-att-edit]');
        if (edit) {
          const record = dayRecords.find(item => item.id === edit.dataset.attEdit);
          if (record) openAttendanceForm(record);
          return;
        }

        const remove = event.target.closest('[data-att-delete]');
        if (remove) {
          deleteAttendance(remove.dataset.attDelete);
        }
      });

      page.addEventListener('change', event => {
        if (event.target.matches('[data-att-date]')) {
          date = event.target.value || todayInput();
          load();
        }
        if (event.target.matches('[data-att-month]')) {
          month = event.target.value || currentMonth();
          load();
        }
      });

      await load();
    },
    cleanup() {
      disposed = true;
      teardown();
    }
  };
}

// ------------------------------------------------------------- shift planner

export function shiftsView({ session }) {
  let teardown = () => {};
  let disposed = false;
  let week = isoWeekStart(todayInput());
  let data = { weekStart: week, weekEnd: '', label: '', days: [], staff: [] };
  let loading = false;

  const canManage = hasPermission(session, 'attendance');

  const html = crmPage({
    session,
    active: 'shifts',
    eyebrow: 'Vận hành studio',
    title: 'Chia ca làm việc',
    lead: 'Xếp ca cho từng nhân sự theo tuần, ghi chú ngày nghỉ và đối chiếu giờ chấm công với ca đã phân.',
    body: '<div data-shift-body aria-live="polite">' + skeletonRows(4) + '</div>'
  });

  function weekMarkup() {
    const isCurrent = data.weekStart === isoWeekStart(todayInput());
    const assignedCells = data.staff.reduce(
      (total, member) => total + (member.assignments?.length || 0),
      0
    );
    return `
      <section class="crm-toolbar shift-toolbar" aria-label="Chọn tuần chia ca">
        <div class="shift-weeknav">
          <button class="icon-btn" type="button" data-shift-prev aria-label="Tuần trước" title="Tuần trước">${icon('arrowLeft')}</button>
          <div class="shift-weeknav__label">
            <strong>${escapeHtml(data.label || weekLabel(week))}</strong>
            <span>${isCurrent ? 'Tuần này' : 'Lịch chia ca'}</span>
          </div>
          <button class="icon-btn" type="button" data-shift-next aria-label="Tuần sau" title="Tuần sau">${icon('arrowRight')}</button>
          <button class="btn btn--ghost btn--compact" type="button" data-shift-today ${isCurrent ? 'disabled' : ''}>Hôm nay</button>
        </div>
        <span class="ops-toolbar__spacer"></span>
        ${canManage ? `
          <button class="btn btn--ghost btn--compact" type="button" data-shift-fill>${icon('calendar')} Điền ca mặc định</button>
          <button class="btn btn--ghost btn--compact shift-danger" type="button" data-shift-clear>${icon('trash')} Xoá lịch tuần</button>
        ` : ''}
      </section>

      <section class="panel shift-panel" aria-label="Lịch chia ca theo tuần">
        <div class="shift-scroll">
          <table class="shift-table">
            <thead>
              <tr>
                <th scope="col" class="shift-table__staffhead">
                  <span class="shift-table__eyebrow">Nhân sự</span>
                  <strong>${data.staff.length} thành viên</strong>
                </th>
                ${data.days.map(day => `
                  <th scope="col" class="shift-day ${day.isToday ? 'is-today' : ''}">
                    <span class="shift-day__weekday">${escapeHtml(day.weekday)}</span>
                    <strong class="shift-day__date">${escapeHtml(day.shortDate)}</strong>
                    ${day.isToday ? '<span class="shift-day__today">Hôm nay</span>' : ''}
                  </th>
                `).join('')}
              </tr>
            </thead>
            <tbody>
              ${data.staff.length
                ? data.staff.map(member => staffRow(member)).join('')
                : `<tr><td class="shift-empty" colspan="8">${emptyState({
                    iconName: 'users',
                    title: 'Chưa có nhân sự',
                    message: 'Thêm nhân sự trong mục Nhân sự để bắt đầu chia ca.'
                  })}</td></tr>`}
            </tbody>
            <tfoot>
              <tr>
                <th scope="row" class="shift-foot__staff">
                  <span>${data.staff.length} nhân sự</span>
                  <small>Tổng ${assignedCells} ô đã phân</small>
                </th>
                ${data.days.map(day => `
                  <td class="shift-foot ${day.isToday ? 'is-today' : ''}">
                    <strong>${day.scheduled} ca${day.off ? ` · ${day.off} nghỉ` : ''}</strong>
                    <span>${escapeHtml(day.window || 'Chưa xếp ca')}</span>
                  </td>
                `).join('')}
              </tr>
            </tfoot>
          </table>
        </div>
      </section>
    `;
  }

  function staffRow(member) {
    const assignments = new Map((member.assignments || []).map(item => [item.date, item]));
    return `
      <tr class="shift-row">
        <th scope="row" class="shift-staff">
          <span class="avatar avatar--olive" aria-hidden="true">${escapeHtml(initials(member.name))}</span>
          <span class="shift-staff__main">
            <strong>${escapeHtml(member.name)}</strong>
            <small>${escapeHtml(STAFF_ROLE_LABELS[member.role] || 'Nhân sự')} · ${escapeHtml(SHIFT_SHORT[member.shift] || 'Ca thường')}</small>
          </span>
        </th>
        ${data.days.map(day => shiftCell(member, day, assignments.get(day.date))).join('')}
      </tr>
    `;
  }

  function shiftCell(member, day, assignment) {
    const shift = assignment?.shift || '';
    const isOff = shift === 'off';
    const tone = !assignment ? 'empty' : isOff ? 'off' : (shift || member.shift || 'normal');
    const label = !assignment
      ? ''
      : isOff
        ? 'Nghỉ'
        : SHIFT_SHORT[shift] || SHIFT_SHORT[member.shift] || 'Ca thường';
    const hint = !assignment
      ? ''
      : isOff
        ? 'Ngày nghỉ'
        : shift === ''
          ? 'Theo ca hồ sơ'
          : 'Đã phân ca';
    const description = assignment
      ? `${member.name} · ${day.weekday} ${day.shortDate}: ${label}${assignment.note ? ` (${assignment.note})` : ''}`
      : `Thêm ca cho ${member.name} ngày ${day.weekday} ${day.shortDate}`;
    return `
      <td class="shift-cell ${day.isToday ? 'is-today' : ''}">
        <button class="shift-cell__btn shift-cell__btn--${tone} ${assignment ? 'has-shift' : 'is-empty'}"
          type="button"
          data-shift-cell
          data-staff-id="${escapeAttr(member.id)}"
          data-date="${escapeAttr(day.date)}"
          aria-label="${escapeAttr(description)}"
          title="${escapeAttr(description)}">
          ${assignment ? `
            <span class="shift-pill">${escapeHtml(label)}</span>
            ${assignment.note
              ? `<span class="shift-cell__note">${escapeHtml(assignment.note)}</span>`
              : `<span class="shift-cell__hint">${escapeHtml(hint)}</span>`}
          ` : `
            <span class="shift-cell__add">${icon('plus')}<span>Thêm ca</span></span>
          `}
        </button>
      </td>
    `;
  }

  function render() {
    const body = document.querySelector('[data-shift-body]');
    if (!body) return;
    body.innerHTML = weekMarkup();
  }

  async function load() {
    if (loading || disposed) return;
    loading = true;
    const body = document.querySelector('[data-shift-body]');
    if (body) body.innerHTML = skeletonRows(4);
    try {
      const payload = await operationsApi.shifts(week);
      if (disposed) return;
      data = payload && Array.isArray(payload.days)
        ? payload
        : { weekStart: week, weekEnd: '', label: weekLabel(week), days: [], staff: [] };
      week = data.weekStart || week;
      render();
    } catch (error) {
      if (disposed) return;
      if (body) body.innerHTML = loadError(error, 'data-shift-retry');
    } finally {
      loading = false;
    }
  }

  async function openShiftForm(member, day, assignment) {
    const profileLabel = SHIFT_LABELS[member.shift] || SHIFT_LABELS.normal;
    const options = SHIFT_OPTIONS.map(option => {
      const label = option.value === ''
        ? `Theo ca hồ sơ — ${profileLabel}`
        : option.label;
      return `<option value="${escapeAttr(option.value)}" ${(assignment?.shift || '') === option.value ? 'selected' : ''}>${escapeHtml(label)}</option>`;
    }).join('');

    const modal = createModal(`
      <div class="dialog-card dialog-card--crm">
        <button class="dialog-close" type="button" data-close-dialog aria-label="Đóng">${icon('x')}</button>
        <header class="task-dialog__head">
          <span class="task-dialog__icon">${icon('calendar')}</span>
          <div>
            <p class="eyebrow">Chia ca làm việc</p>
            <h2>${escapeHtml(member.name)} · ${escapeHtml(day.weekday)} ${escapeHtml(day.shortDate)}</h2>
          </div>
        </header>
        <form class="crm-form" data-shift-form novalidate>
          <div class="field">
            <label for="shift-kind">Ca làm việc</label>
            <select id="shift-kind" name="shift">${options}</select>
          </div>
          <div class="field">
            <label for="shift-note">Ghi chú</label>
            <textarea id="shift-note" name="note" maxlength="300" rows="2" placeholder="Ví dụ: đổi ca, chụp ngoài studio…">${escapeHtml(assignment?.note || '')}</textarea>
          </div>
          <div data-form-error></div>
          <div class="dialog-actions">
            ${assignment ? `<button class="btn btn--ghost shift-danger" type="button" data-shift-remove>${icon('trash')} Bỏ phân ca</button>` : ''}
            <span class="ops-toolbar__spacer"></span>
            <button class="btn btn--ghost" type="button" data-close-dialog>Huỷ</button>
            <button class="btn btn--primary" type="submit" data-submit>${icon('check')} Lưu phân ca</button>
          </div>
        </form>
      </div>
    `, { className: 'dialog--crm' });

    const form = modal.dialog.querySelector('[data-shift-form]');
    form.addEventListener('submit', async event => {
      event.preventDefault();
      clearFieldErrors(form);
      const errorRoot = form.querySelector('[data-form-error]');
      errorRoot.innerHTML = '';
      const submit = form.querySelector('[data-submit]');
      setButtonLoading(submit, true, 'Đang lưu…');
      try {
        await operationsApi.saveShift({
          staffId: member.id,
          date: day.date,
          shift: form.elements.namedItem('shift').value,
          note: form.elements.namedItem('note').value.trim()
        });
      } catch (error) {
        if (error.name === 'AbortError') return;
        setButtonLoading(submit, false);
        errorRoot.innerHTML = errorInline(error.message || 'Chưa lưu được phân ca.', error.code);
        return;
      }
      closeModalFrom(form);
      showToast('Đã lưu phân ca.');
      load();
    });

    const removeButton = form.querySelector('[data-shift-remove]');
    if (removeButton) {
      removeButton.addEventListener('click', async () => {
        const confirmed = await confirmDialog({
          title: 'Bỏ phân ca này?',
          message: `${member.name} sẽ trở về trạng thái chưa xếp ca trong ${day.weekday} ${day.shortDate}.`,
          confirmLabel: 'Bỏ phân ca',
          danger: true
        });
        if (!confirmed) return;
        setButtonLoading(removeButton, true, 'Đang xoá…');
        try {
          await operationsApi.deleteShift(member.id, day.date);
        } catch (error) {
          if (error.name === 'AbortError') return;
          setButtonLoading(removeButton, false);
          showToast(error.message || 'Chưa bỏ được phân ca.', 'error');
          return;
        }
        closeModalFrom(form);
        showToast('Đã bỏ phân ca.');
        load();
      });
    }
  }

  async function fillWeek() {
    const confirmed = await confirmDialog({
      title: 'Điền ca mặc định cả tuần?',
      message: `Các ô còn trống trong tuần ${data.label} sẽ được điền theo ca hồ sơ của từng nhân sự. Những ô đã phân ca hoặc đánh dấu nghỉ vẫn được giữ nguyên.`,
      confirmLabel: 'Điền ca mặc định'
    });
    if (!confirmed) return;
    try {
      data = await operationsApi.fillShifts(week);
      week = data.weekStart || week;
      render();
      showToast('Đã điền ca mặc định cho tuần.');
    } catch (error) {
      showToast(error.message || 'Chưa điền được ca mặc định.', 'error');
    }
  }

  async function clearWeek() {
    const confirmed = await confirmDialog({
      title: 'Xoá lịch chia ca tuần này?',
      message: `Toàn bộ phân ca trong tuần ${data.label} sẽ bị xoá. Giờ chấm công và các dữ liệu khác không bị ảnh hưởng.`,
      confirmLabel: 'Xoá lịch tuần',
      danger: true
    });
    if (!confirmed) return;
    try {
      data = await operationsApi.clearShifts(week);
      week = data.weekStart || week;
      render();
      showToast('Đã xoá lịch chia ca trong tuần.');
    } catch (error) {
      showToast(error.message || 'Chưa xoá được lịch tuần.', 'error');
    }
  }

  return {
    title: 'Chia ca làm việc',
    bodyClass: 'page-admin',
    html,
    async mount(root) {
      teardown = mountAdminShell(root, session);
      const page = root.querySelector('[data-crm-page]');
      page.addEventListener('click', event => {
        if (event.target.closest('[data-shift-retry]')) {
          load();
          return;
        }

        if (event.target.closest('[data-shift-prev]')) {
          week = addIsoDays(week, -7);
          load();
          return;
        }

        if (event.target.closest('[data-shift-next]')) {
          week = addIsoDays(week, 7);
          load();
          return;
        }

        if (event.target.closest('[data-shift-today]')) {
          week = isoWeekStart(todayInput());
          load();
          return;
        }

        if (event.target.closest('[data-shift-fill]')) {
          fillWeek();
          return;
        }

        if (event.target.closest('[data-shift-clear]')) {
          clearWeek();
          return;
        }

        const cell = event.target.closest('[data-shift-cell]');
        if (cell) {
          const member = data.staff.find(item => item.id === cell.dataset.staffId);
          const day = data.days.find(item => item.date === cell.dataset.date);
          if (!member || !day) return;
          const assignment = (member.assignments || []).find(item => item.date === day.date) || null;
          openShiftForm(member, day, assignment);
        }
      });

      await load();
    },
    cleanup() {
      disposed = true;
      teardown();
    }
  };
}

// ------------------------------------------------------------------ wardrobe

export function wardrobeView({ session, websiteConcepts = false }) {
  let teardown = () => {};
  let disposed = false;
  let tab = websiteConcepts ? 'concepts' : 'outfits';
  let query = '';
  let category = 'all';
  let loanFilter = 'all';
  let outfits = [];
  let concepts = [];
  let websiteAlbums = [];
  let websiteCategories = [];
  const canPublishConcepts = session?.user?.role === 'admin';
  let loans = [];
  let optionsPromise = null;
  let loading = false;

  const canManage = hasPermission(session, 'wardrobe');

  const html = crmPage({
    session,
    active: websiteConcepts ? 'website-concepts' : 'wardrobe',
    eyebrow: 'Vận hành studio',
    title: websiteConcepts ? 'Bộ ảnh website' : 'Tủ trang phục',
    lead: websiteConcepts ? 'Thêm hoặc sửa concept, chọn nhóm và album rồi bật hiển thị trên website.' : 'Quản lý trang phục, concept và phiếu cho thuê của studio trong một nơi.',
    body: `
      <nav class="ops-tabs" role="tablist" aria-label="Khu vực tủ trang phục">
        <button class="ops-tab is-active" type="button" role="tab" aria-selected="true" data-wardrobe-tab="outfits">${icon('tag')} Trang phục</button>
        <button class="ops-tab" type="button" role="tab" aria-selected="false" data-wardrobe-tab="concepts">${icon('palette')} Concept</button>
        <button class="ops-tab" type="button" role="tab" aria-selected="false" data-wardrobe-tab="loans">${icon('share')} Cho thuê</button>
      </nav>
      <div data-wardrobe-body aria-live="polite">${skeletonRows(4)}</div>
    `
  });

  function loadOptions() {
    if (!optionsPromise) {
      optionsPromise = Promise.all([crmApi.customers(), crmApi.staff(), crmApi.bookings()])
        .then(([customers, staff, bookings]) => ({
          customers: Array.isArray(customers) ? customers : [],
          staff: Array.isArray(staff) ? staff : [],
          bookings: Array.isArray(bookings) ? bookings : []
        }))
        .catch(error => {
          optionsPromise = null;
          throw error;
        });
    }
    return optionsPromise;
  }

  function outfitCard(outfit) {
    const available = Math.max(0, Number(outfit.quantity || 0) - Number(outfit.loanedQuantity || 0));
    const details = [outfit.size, outfit.color].filter(Boolean).join(' · ');
    return `
      <article class="ops-card" data-outfit-card="${escapeAttr(outfit.id)}">
        <div class="ops-card__head">
          <span class="ops-card__icon ops-card__icon--clay">${icon('tag')}</span>
          <div class="ops-card__title">
            <h3>${escapeHtml(outfit.name)}</h3>
            <p>${escapeHtml(outfit.category || 'Khác')}${details ? ` · ${escapeHtml(details)}` : ''}</p>
          </div>
          <span class="pill pill--${available > 0 ? 'live' : 'unpaid'}">${available > 0 ? `${available} sẵn` : 'Hết sẵn'}</span>
        </div>
        <div class="ops-card__stats">
          <span><strong>${Number(outfit.quantity || 0)}</strong><small>Tổng</small></span>
          <span><strong>${Number(outfit.loanedQuantity || 0)}</strong><small>Đang thuê</small></span>
          <span><strong>${available}</strong><small>Sẵn sàng</small></span>
        </div>
        ${outfit.note ? `<p class="ops-card__note">${escapeHtml(outfit.note)}</p>` : ''}
        ${canManage ? `
          <div class="ops-card__actions">
            <button class="btn btn--ghost btn--compact" type="button" data-outfit-edit="${escapeAttr(outfit.id)}">${icon('edit')} Sửa</button>
            <button class="btn btn--ghost btn--compact" type="button" data-outfit-delete="${escapeAttr(outfit.id)}">${icon('trash')} Xoá</button>
          </div>
        ` : ''}
      </article>
    `;
  }

  function conceptCard(concept) {
    return `
      <article class="ops-card" data-concept-card="${escapeAttr(concept.id)}">
        <div class="ops-card__head">
          <span class="ops-card__icon ops-card__icon--rose">${icon('palette')}</span>
          <div class="ops-card__title">
            <h3>${escapeHtml(concept.name)}</h3>
            <p>${money(concept.fee)}</p>
          </div>
          ${concept.status === 'maintenance'
            ? `<span class="pill pill--warn">${icon('alertTriangle')}Bảo trì</span>`
            : `<span class="pill pill--live">${icon('checkCircle')}Sẵn sàng</span>`}
        </div>
        ${concept.description ? `<p class="ops-card__note">${escapeHtml(concept.description)}</p>` : ''}
        ${canPublishConcepts ? `<p class="ops-card__note">Website: ${concept.isPublished ? 'Đã bật hiển thị' : 'Chưa công khai'}${concept.category ? ` · ${escapeHtml(websiteCategories.find(item => item.id === concept.category)?.title || concept.category)}` : ''}</p>` : ''}
        ${canManage ? `
          <div class="ops-card__actions">
            ${canPublishConcepts && concept.isPublished && concept.category ? `<a class="btn btn--ghost btn--compact" href="/concepts/${encodeURIComponent(concept.category)}" target="_blank" rel="noopener">${icon('eye')} Xem trên website</a>` : ''}
            <button class="btn btn--ghost btn--compact" type="button" data-concept-edit="${escapeAttr(concept.id)}">${icon('edit')} Sửa</button>
            <button class="btn btn--ghost btn--compact" type="button" data-concept-delete="${escapeAttr(concept.id)}">${icon('trash')} Xoá</button>
          </div>
        ` : ''}
      </article>
    `;
  }

  function filteredOutfits() {
    return outfits
      .filter(outfit => category === 'all' || outfit.category === category)
      .filter(outfit => matches(query, [outfit.name, outfit.category, outfit.size, outfit.color, outfit.note]));
  }

  function filteredConcepts() {
    return concepts.filter(concept => matches(query, [concept.name, concept.description, concept.status]));
  }

  function filteredLoans() {
    return loans
      .filter(loan => {
        if (loanFilter === 'open') return !loan.isReturned;
        if (loanFilter === 'returned') return loan.isReturned;
        if (loanFilter === 'overdue') return loan.isOverdue;
        return true;
      })
      .filter(loan => matches(query, [loan.outfitName, loan.customerName, loan.staffName, loan.bookingCode, loan.notes]));
  }

  function outfitsMarkup() {
    const list = filteredOutfits();
    const totalPieces = sumBy(outfits, outfit => outfit.quantity);
    const loaned = sumBy(outfits, outfit => outfit.loanedQuantity);
    return `
      <section class="crm-toolbar" aria-label="Bộ lọc trang phục">
        <label class="crm-search">
          <span>${icon('search')}</span>
          <input type="search" data-wardrobe-search value="${escapeAttr(query)}" placeholder="Tìm trang phục, size, màu…" aria-label="Tìm trang phục" autocomplete="off">
        </label>
        <select class="select" data-outfit-category aria-label="Lọc danh mục trang phục">
          <option value="all">Mọi danh mục</option>
          ${OUTFIT_CATEGORIES.map(value => `<option value="${escapeAttr(value)}" ${value === category ? 'selected' : ''}>${escapeHtml(value)}</option>`).join('')}
        </select>
        <span class="crm-toolbar__count">${list.length} / ${outfits.length} mẫu</span>
        <span class="ops-toolbar__spacer"></span>
        ${canManage ? `<button class="btn btn--primary btn--compact" type="button" data-outfit-create>${icon('plus')} Thêm trang phục</button>` : ''}
      </section>

      <section class="crm-metrics" aria-label="Số liệu tủ trang phục">
        ${metric({ label: 'Tổng món', value: String(totalPieces), note: `${outfits.length} mẫu trang phục`, tone: 'clay', iconName: 'tag' })}
        ${metric({ label: 'Đang cho thuê', value: String(loaned), note: `${loans.filter(loan => !loan.isReturned).length} phiếu chưa trả`, tone: 'rose', iconName: 'share' })}
        ${metric({ label: 'Sẵn sàng', value: String(Math.max(0, totalPieces - loaned)), note: 'có thể cho khách mượn', tone: 'olive', iconName: 'checkCircle' })}
      </section>

      <section class="ops-card-grid" aria-label="Danh sách trang phục">
        ${list.length ? list.map(outfitCard).join('') : emptyState({
          iconName: 'tag',
          title: outfits.length ? 'Không có mẫu phù hợp' : 'Tủ trang phục đang trống',
          message: outfits.length ? 'Thử đổi từ khoá hoặc danh mục.' : 'Thêm trang phục đầu tiên để quản lý cho thuê và tồn kho.',
          action: canManage && !outfits.length ? '<button class="btn btn--primary" type="button" data-outfit-create>Thêm trang phục</button>' : ''
        })}
      </section>
    `;
  }

  function conceptsMarkup() {
    const list = filteredConcepts();
    return `
      <section class="crm-toolbar" aria-label="Bộ lọc concept">
        <label class="crm-search">
          <span>${icon('search')}</span>
          <input type="search" data-wardrobe-search value="${escapeAttr(query)}" placeholder="Tìm concept…" aria-label="Tìm concept" autocomplete="off">
        </label>
        <span class="crm-toolbar__count">${list.length} / ${concepts.length} concept</span>
        <span class="ops-toolbar__spacer"></span>
        ${canManage ? `<button class="btn btn--primary btn--compact" type="button" data-concept-create>${icon('plus')} Thêm concept</button>` : ''}
      </section>

      <section class="crm-metrics" aria-label="Số liệu concept">
        ${metric({ label: 'Tổng concept', value: String(concepts.length), note: 'ý tưởng chụp đang có', tone: 'clay', iconName: 'palette' })}
        ${metric({ label: 'Sẵn sàng', value: String(concepts.filter(concept => concept.status !== 'maintenance').length), note: 'có thể nhận lịch', tone: 'olive', iconName: 'checkCircle' })}
        ${metric({ label: 'Đang bảo trì', value: String(concepts.filter(concept => concept.status === 'maintenance').length), note: 'cần cập nhật', tone: 'rose', iconName: 'alertTriangle' })}
      </section>

      <section class="ops-card-grid" aria-label="Danh sách concept">
        ${list.length ? list.map(conceptCard).join('') : emptyState({
          iconName: 'palette',
          title: concepts.length ? 'Không có concept phù hợp' : 'Chưa có concept nào',
          message: concepts.length ? 'Thử từ khoá khác.' : 'Thêm concept để gợi ý ý tưởng chụp cho khách.',
          action: canManage && !concepts.length ? '<button class="btn btn--primary" type="button" data-concept-create>Thêm concept</button>' : ''
        })}
      </section>
    `;
  }

  function loanRow(loan) {
    const borrower = loan.borrowerType === 'staff'
      ? (loan.staffName || 'Nhân sự')
      : (loan.customerName || 'Khách lẻ');
    const status = loan.isReturned
      ? `<span class="pill pill--live">${icon('checkCircle')}Đã trả</span>`
      : loan.isOverdue
        ? `<span class="pill pill--unpaid">${icon('alertTriangle')}Quá hạn</span>`
        : `<span class="pill pill--neutral">${icon('clock')}Đang thuê</span>`;
    return `
      <article class="ops-row" data-loan-row="${escapeAttr(loan.id)}">
        <span class="ops-card__icon ops-card__icon--sand" aria-hidden="true">${icon('tag')}</span>
        <div class="ops-row__main">
          <p class="ops-row__title">
            <strong>${escapeHtml(loan.outfitName)} × ${Number(loan.quantity || 0)}</strong>
            ${status}
          </p>
          <p class="ops-row__meta">
            <span>${icon('user')}${escapeHtml(borrower)}</span>
            <span>${icon('calendar')}${escapeHtml(dateOrDash(loan.borrowDate))}${loan.dueDate ? ` → ${escapeHtml(dateOrDash(loan.dueDate))}` : ''}</span>
            ${loan.bookingCode ? `<span>${icon('camera')}${escapeHtml(loan.bookingCode)}</span>` : ''}
            ${loan.deposit ? `<span>${icon('wallet')}Cọc ${money(loan.deposit)}</span>` : ''}
          </p>
          ${loan.notes ? `<p class="ops-row__note">${escapeHtml(loan.notes)}</p>` : ''}
        </div>
        ${canManage ? `
          <div class="ops-row__actions">
            ${!loan.isReturned ? `<button class="btn btn--ghost btn--compact" type="button" data-loan-return="${escapeAttr(loan.id)}">${icon('check')} Nhận lại</button>` : ''}
            <button class="icon-btn" type="button" data-loan-edit="${escapeAttr(loan.id)}" aria-label="Sửa phiếu cho thuê" title="Sửa">${icon('edit')}</button>
            <button class="icon-btn" type="button" data-loan-delete="${escapeAttr(loan.id)}" aria-label="Xoá phiếu cho thuê" title="Xoá">${icon('trash')}</button>
          </div>
        ` : ''}
      </article>
    `;
  }

  function loansMarkup() {
    const list = filteredLoans();
    const open = loans.filter(loan => !loan.isReturned);
    const overdue = loans.filter(loan => loan.isOverdue);
    return `
      <section class="crm-toolbar" aria-label="Bộ lọc phiếu cho thuê">
        <label class="crm-search">
          <span>${icon('search')}</span>
          <input type="search" data-wardrobe-search value="${escapeAttr(query)}" placeholder="Tìm trang phục, khách, mã lịch…" aria-label="Tìm phiếu cho thuê" autocomplete="off">
        </label>
        <select class="select" data-loan-filter aria-label="Lọc trạng thái cho thuê">
          <option value="all" ${loanFilter === 'all' ? 'selected' : ''}>Mọi phiếu</option>
          <option value="open" ${loanFilter === 'open' ? 'selected' : ''}>Đang thuê</option>
          <option value="overdue" ${loanFilter === 'overdue' ? 'selected' : ''}>Quá hạn</option>
          <option value="returned" ${loanFilter === 'returned' ? 'selected' : ''}>Đã trả</option>
        </select>
        <span class="crm-toolbar__count">${list.length} / ${loans.length} phiếu</span>
        <span class="ops-toolbar__spacer"></span>
        ${canManage ? `<button class="btn btn--primary btn--compact" type="button" data-loan-create>${icon('plus')} Cho thuê</button>` : ''}
      </section>

      <section class="crm-metrics" aria-label="Số liệu cho thuê">
        ${metric({ label: 'Đang cho thuê', value: String(open.length), note: `${open.reduce((sum, loan) => sum + Number(loan.quantity || 0), 0)} món bên ngoài`, tone: 'clay', iconName: 'share' })}
        ${metric({ label: 'Quá hạn', value: String(overdue.length), note: 'cần liên hệ khách', tone: 'rose', iconName: 'alertTriangle' })}
        ${metric({ label: 'Đã trả', value: String(loans.length - open.length), note: 'phiếu đã hoàn tất', tone: 'olive', iconName: 'checkCircle' })}
      </section>

      <section class="panel crm-list ops-list-panel" aria-label="Danh sách phiếu cho thuê">
        ${list.length ? `<div class="ops-list">${list.map(loanRow).join('')}</div>` : emptyState({
          iconName: 'share',
          title: loans.length ? 'Không có phiếu phù hợp' : 'Chưa có phiếu cho thuê',
          message: loans.length ? 'Thử đổi bộ lọc hoặc từ khoá.' : 'Tạo phiếu cho thuê để theo dõi trang phục đang ở ngoài studio.',
          action: canManage && !loans.length ? '<button class="btn btn--primary" type="button" data-loan-create>Cho thuê trang phục</button>' : ''
        })}
      </section>
    `;
  }

  function render() {
    const body = document.querySelector('[data-wardrobe-body]');
    if (!body) return;
    body.innerHTML = tab === 'outfits'
      ? outfitsMarkup()
      : tab === 'concepts'
        ? conceptsMarkup()
        : loansMarkup();
    document.querySelectorAll('[data-wardrobe-tab]').forEach(button => {
      const active = button.dataset.wardrobeTab === tab;
      button.classList.toggle('is-active', active);
      button.setAttribute('aria-selected', active ? 'true' : 'false');
    });
  }

  async function load() {
    if (loading || disposed) return;
    loading = true;
    const body = document.querySelector('[data-wardrobe-body]');
    if (body) body.innerHTML = skeletonRows(4);
    try {
      const [outfitData, conceptData, loanData, albumData, categoryData] = await Promise.all([
        operationsApi.outfits(),
        operationsApi.concepts(),
        operationsApi.loans(),
        canPublishConcepts ? api('/api/admin/albums') : Promise.resolve([]),
        canPublishConcepts ? api('/api/public/concepts') : Promise.resolve([])
      ]);
      if (disposed) return;
      outfits = Array.isArray(outfitData) ? outfitData : [];
      concepts = Array.isArray(conceptData) ? conceptData : [];
      websiteAlbums = Array.isArray(albumData) ? albumData.filter(album => album.isPublished && album.photoCount > 0) : [];
      websiteCategories = Array.isArray(categoryData) ? categoryData : [];
      loans = Array.isArray(loanData) ? loanData : [];
      render();
    } catch (error) {
      if (disposed) return;
      if (body) body.innerHTML = loadError(error, 'data-wardrobe-retry');
    } finally {
      loading = false;
    }
  }

  function openOutfitForm(outfit = null) {
    const isEdit = Boolean(outfit);
    const modal = createModal(`
      <div class="dialog-card dialog-card--crm">
        <button class="dialog-close" type="button" data-close-dialog aria-label="Đóng">${icon('x')}</button>
        <header class="task-dialog__head">
          <span class="task-dialog__icon">${icon('tag')}</span>
          <div>
            <p class="eyebrow">${isEdit ? 'Cập nhật tủ đồ' : 'Trang phục mới'}</p>
            <h2>${isEdit ? 'Sửa trang phục' : 'Thêm trang phục'}</h2>
          </div>
        </header>
        <form class="crm-form" data-outfit-form novalidate>
          <div class="crm-form__grid">
            <div class="field crm-form__full">
              <label for="outfit-name">Tên trang phục <span class="required">*</span></label>
              <input id="outfit-name" name="name" type="text" maxlength="120" value="${escapeAttr(outfit?.name || '')}" placeholder="Ví dụ: Váy cưới ren trắng" required autofocus>
            </div>
            <div class="field">
              <label for="outfit-category">Danh mục</label>
              <select id="outfit-category" name="category">${categoryOptions(OUTFIT_CATEGORIES, outfit?.category || 'Váy cưới')}</select>
            </div>
            <div class="field">
              <label for="outfit-quantity">Số lượng</label>
              <input id="outfit-quantity" name="quantity" type="number" min="0" max="10000" step="1" value="${Number(outfit?.quantity ?? 1)}">
            </div>
            <div class="field">
              <label for="outfit-size">Size</label>
              <input id="outfit-size" name="size" type="text" maxlength="40" value="${escapeAttr(outfit?.size || '')}" placeholder="S, M, L…">
            </div>
            <div class="field">
              <label for="outfit-color">Màu sắc</label>
              <input id="outfit-color" name="color" type="text" maxlength="60" value="${escapeAttr(outfit?.color || '')}" placeholder="Trắng ngà, đỏ rượu…">
            </div>
            <div class="field crm-form__full">
              <label for="outfit-note">Ghi chú</label>
              <textarea id="outfit-note" name="note" maxlength="1000" rows="2" placeholder="Chất liệu, phụ kiện đi kèm, tình trạng…">${escapeHtml(outfit?.note || '')}</textarea>
            </div>
          </div>
          <div data-form-error></div>
          <div class="dialog-actions">
            <button class="btn btn--ghost" type="button" data-close-dialog>Huỷ</button>
            <button class="btn btn--primary" type="submit" data-submit>${icon('check')} ${isEdit ? 'Lưu thay đổi' : 'Thêm trang phục'}</button>
          </div>
        </form>
      </div>
    `, { className: 'dialog--crm' });

    const form = modal.dialog.querySelector('[data-outfit-form]');
    form.addEventListener('submit', async event => {
      event.preventDefault();
      clearFieldErrors(form);
      const errorRoot = form.querySelector('[data-form-error]');
      errorRoot.innerHTML = '';
      const nameField = form.elements.namedItem('name');
      if (!nameField.value.trim()) {
        fieldError(nameField, 'Tên trang phục không được để trống.');
        return;
      }
      const payload = {
        name: nameField.value.trim(),
        category: form.elements.namedItem('category').value,
        size: form.elements.namedItem('size').value.trim(),
        color: form.elements.namedItem('color').value.trim(),
        quantity: Number(form.elements.namedItem('quantity').value || 0),
        note: form.elements.namedItem('note').value.trim()
      };
      const submit = form.querySelector('[data-submit]');
      setButtonLoading(submit, true, 'Đang lưu…');
      try {
        if (isEdit) {
          await operationsApi.updateOutfit(outfit.id, payload);
        } else {
          await operationsApi.createOutfit(payload);
        }
      } catch (error) {
        if (error.name === 'AbortError') return;
        setButtonLoading(submit, false);
        errorRoot.innerHTML = errorInline(error.message || 'Chưa lưu được trang phục.', error.code);
        return;
      }
      closeModalFrom(form);
      showToast(isEdit ? 'Đã cập nhật trang phục.' : 'Đã thêm trang phục.');
      load();
    });
  }

  async function deleteOutfit(id) {
    const outfit = outfits.find(item => item.id === id);
    const confirmed = await confirmDialog({
      title: 'Xoá trang phục?',
      message: `“${outfit?.name || 'Trang phục'}” sẽ bị xoá khỏi tủ đồ. Không thể xoá mẫu đã có lịch sử cho thuê.`,
      confirmLabel: 'Xoá trang phục',
      danger: true
    });
    if (!confirmed) return;
    try {
      await operationsApi.deleteOutfit(id);
      showToast('Đã xoá trang phục.');
      load();
    } catch (error) {
      showToast(error.message || 'Chưa xoá được trang phục.', 'error');
    }
  }

  function openConceptForm(concept = null) {
    const isEdit = Boolean(concept);
    const modal = createModal(`
      <div class="dialog-card dialog-card--crm">
        <button class="dialog-close" type="button" data-close-dialog aria-label="Đóng">${icon('x')}</button>
        <header class="task-dialog__head">
          <span class="task-dialog__icon">${icon('palette')}</span>
          <div>
            <p class="eyebrow">${isEdit ? 'Cập nhật concept' : 'Concept mới'}</p>
            <h2>${isEdit ? 'Sửa concept' : 'Thêm concept'}</h2>
          </div>
        </header>
        <form class="crm-form" data-concept-form novalidate>
          <div class="crm-form__grid">
            <div class="field crm-form__full">
              <label for="concept-name">Tên concept <span class="required">*</span></label>
              <input id="concept-name" name="name" type="text" maxlength="120" value="${escapeAttr(concept?.name || '')}" placeholder="Ví dụ: Mộc mạc Đà Lạt" required autofocus>
            </div>
            <div class="field">
              <label for="concept-fee">Phí concept (đ)</label>
              <input id="concept-fee" name="fee" type="number" min="0" step="1000" value="${Number(concept?.fee || 0)}">
            </div>
            <div class="field">
              <label for="concept-status">Trạng thái</label>
              <select id="concept-status" name="status">
                <option value="ready" ${(concept?.status || 'ready') === 'ready' ? 'selected' : ''}>Sẵn sàng</option>
                <option value="maintenance" ${concept?.status === 'maintenance' ? 'selected' : ''}>Đang bảo trì</option>
              </select>
            </div>
            <div class="field crm-form__full">
              <label for="concept-description">Mô tả</label>
              <textarea id="concept-description" name="description" maxlength="2000" rows="3" placeholder="Bối cảnh, đạo cụ, tông màu…">${escapeHtml(concept?.description || '')}</textarea>
            </div>
            ${canPublishConcepts ? `
            <div class="field crm-form__full">
              <label for="concept-category">Nhóm concept trên trang chủ</label>
              <select id="concept-category" name="category">${optionList(websiteCategories.map(item => ({ value: item.id, label: item.title })), concept?.category || '', 'Chưa phân nhóm')}</select>
            </div>
            <div class="field crm-form__full">
              <label for="concept-album">Album ảnh của concept</label>
              <select id="concept-album" name="albumId">${optionList(websiteAlbums.map(album => ({ value: album.id, label: album.title })), concept?.albumId || '', 'Chọn album đã chia sẻ và có ảnh')}</select>
              <p class="field__hint">Tạo hoặc nhập album từ Google Drive trong mục Album trước. Ảnh bìa album sẽ dùng làm ảnh đại diện cho nhóm concept.</p>
            </div>
            <label class="check-field crm-form__full"><input type="checkbox" name="isPublished" ${concept?.isPublished ? 'checked' : ''}><span><strong>Hiển thị bộ ảnh trên website</strong><small>Tên, mô tả concept và ảnh trong album đã chọn sẽ được công khai cho mọi khách truy cập. Thông tin khách hàng, ghi chú và lựa chọn ảnh không được đưa vào gallery.</small></span></label>
            ` : ''}
          </div>
          <div data-form-error></div>
          <div class="dialog-actions">
            <button class="btn btn--ghost" type="button" data-close-dialog>Huỷ</button>
            <button class="btn btn--primary" type="submit" data-submit>${icon('check')} ${isEdit ? 'Lưu thay đổi' : 'Thêm concept'}</button>
          </div>
        </form>
      </div>
    `, { className: 'dialog--crm' });

    const form = modal.dialog.querySelector('[data-concept-form]');
    form.addEventListener('submit', async event => {
      event.preventDefault();
      clearFieldErrors(form);
      const errorRoot = form.querySelector('[data-form-error]');
      errorRoot.innerHTML = '';
      const nameField = form.elements.namedItem('name');
      if (!nameField.value.trim()) {
        fieldError(nameField, 'Tên concept không được để trống.');
        return;
      }
      const payload = {
        name: nameField.value.trim(),
        fee: Number(form.elements.namedItem('fee').value || 0),
        status: form.elements.namedItem('status').value,
        description: form.elements.namedItem('description').value.trim()
      };
      if (canPublishConcepts) {
        payload.category = form.elements.namedItem('category').value;
        payload.albumId = form.elements.namedItem('albumId').value;
        payload.isPublished = form.elements.namedItem('isPublished').checked;
      }
      const submit = form.querySelector('[data-submit]');
      setButtonLoading(submit, true, 'Đang lưu…');
      try {
        if (isEdit) {
          await operationsApi.updateConcept(concept.id, payload);
        } else {
          await operationsApi.createConcept(payload);
        }
      } catch (error) {
        if (error.name === 'AbortError') return;
        setButtonLoading(submit, false);
        errorRoot.innerHTML = errorInline(error.message || 'Chưa lưu được concept.', error.code);
        return;
      }
      closeModalFrom(form);
      showToast(isEdit ? 'Đã cập nhật concept.' : 'Đã thêm concept.');
      load();
    });
  }

  async function deleteConcept(id) {
    const concept = concepts.find(item => item.id === id);
    const confirmed = await confirmDialog({
      title: 'Xoá concept?',
      message: `“${concept?.name || 'Concept'}” sẽ bị xoá khỏi danh sách gợi ý.`,
      confirmLabel: 'Xoá concept',
      danger: true
    });
    if (!confirmed) return;
    try {
      await operationsApi.deleteConcept(id);
      showToast('Đã xoá concept.');
      load();
    } catch (error) {
      showToast(error.message || 'Chưa xoá được concept.', 'error');
    }
  }

  async function openLoanForm(loan = null) {
    let options;
    try {
      options = await loadOptions();
    } catch (error) {
      showToast(error.message || 'Không tải được danh sách khách và nhân sự.', 'error');
      return;
    }
    if (disposed) return;
    const isEdit = Boolean(loan);
    const customerOptions = options.customers.map(customer => ({
      value: customer.id,
      label: `${customer.name}${customer.phone ? ` · ${customer.phone}` : ''}`
    }));
    const staffOptions = options.staff
      .filter(member => member.isActive || member.id === loan?.staffId)
      .map(member => ({ value: member.id, label: member.name }));
    const bookingOptions = options.bookings.map(booking => ({
      value: booking.id,
      label: `${booking.code}${booking.customerName ? ` · ${booking.customerName}` : ''}${booking.date ? ` · ${dateOrDash(booking.date)}` : ''}`
    }));
    const outfitOptions = outfits.map(outfit => {
      const available = Math.max(0, Number(outfit.quantity || 0) - Number(outfit.loanedQuantity || 0));
      return {
        value: outfit.id,
        label: `${outfit.name} · còn ${available}`
      };
    });
    const borrowerType = loan?.borrowerType || 'customer';

    const modal = createModal(`
      <div class="dialog-card dialog-card--crm dialog-card--wide">
        <button class="dialog-close" type="button" data-close-dialog aria-label="Đóng">${icon('x')}</button>
        <header class="task-dialog__head">
          <span class="task-dialog__icon">${icon('share')}</span>
          <div>
            <p class="eyebrow">${isEdit ? 'Cập nhật phiếu' : 'Phiếu cho thuê mới'}</p>
            <h2>${isEdit ? 'Sửa phiếu cho thuê' : 'Cho thuê trang phục'}</h2>
          </div>
        </header>
        <form class="crm-form" data-loan-form novalidate>
          <div class="crm-form__grid">
            <div class="field crm-form__full">
              <label for="loan-outfit">Trang phục <span class="required">*</span></label>
              <select id="loan-outfit" name="outfitId">${optionList(outfitOptions, loan?.outfitId || '', 'Chọn trang phục')}</select>
            </div>
            <div class="field">
              <label for="loan-quantity">Số lượng</label>
              <input id="loan-quantity" name="quantity" type="number" min="1" max="10000" step="1" value="${Number(loan?.quantity ?? 1)}">
            </div>
            <div class="field">
              <label for="loan-borrower">Người mượn</label>
              <select id="loan-borrower" name="borrowerType">
                <option value="customer" ${borrowerType === 'customer' ? 'selected' : ''}>Khách hàng</option>
                <option value="staff" ${borrowerType === 'staff' ? 'selected' : ''}>Nhân sự</option>
              </select>
            </div>
            <div class="field crm-form__full" data-borrower-customer ${borrowerType === 'staff' ? 'hidden' : ''}>
              <label for="loan-customer">Khách hàng</label>
              <select id="loan-customer" name="customerId">${optionList(customerOptions, loan?.customerId || '', 'Khách lẻ')}</select>
            </div>
            <div class="field crm-form__full" data-borrower-staff ${borrowerType === 'staff' ? '' : 'hidden'}>
              <label for="loan-staff">Nhân sự</label>
              <select id="loan-staff" name="staffId">${optionList(staffOptions, loan?.staffId || '', 'Chọn nhân sự')}</select>
            </div>
            <div class="field crm-form__full">
              <label for="loan-booking">Lịch chụp liên quan</label>
              <select id="loan-booking" name="bookingId">${optionList(bookingOptions, loan?.bookingId || '', 'Không gắn lịch chụp')}</select>
            </div>
            <div class="field">
              <label for="loan-borrow-date">Ngày cho thuê <span class="required">*</span></label>
              <input id="loan-borrow-date" name="borrowDate" type="date" value="${escapeAttr(loan?.borrowDate || todayInput())}" required>
            </div>
            <div class="field">
              <label for="loan-due-date">Hạn trả</label>
              <input id="loan-due-date" name="dueDate" type="date" value="${escapeAttr(loan?.dueDate || '')}">
            </div>
            <div class="field">
              <label for="loan-deposit">Tiền cọc (đ)</label>
              <input id="loan-deposit" name="deposit" type="number" min="0" step="1000" value="${Number(loan?.deposit || 0)}">
            </div>
            <div class="field crm-form__full">
              <label for="loan-notes">Ghi chú</label>
              <textarea id="loan-notes" name="notes" maxlength="1000" rows="2" placeholder="Tình trạng trang phục, phụ kiện đi kèm…">${escapeHtml(loan?.notes || '')}</textarea>
            </div>
          </div>
          <div data-form-error></div>
          <div class="dialog-actions">
            <button class="btn btn--ghost" type="button" data-close-dialog>Huỷ</button>
            <button class="btn btn--primary" type="submit" data-submit>${icon('check')} ${isEdit ? 'Lưu thay đổi' : 'Tạo phiếu cho thuê'}</button>
          </div>
        </form>
      </div>
    `, { className: 'dialog--crm dialog--wide' });

    const form = modal.dialog.querySelector('[data-loan-form]');
    const borrowerField = form.elements.namedItem('borrowerType');
    const customerField = form.querySelector('[data-borrower-customer]');
    const staffFieldBlock = form.querySelector('[data-borrower-staff]');
    const syncBorrower = () => {
      const isStaff = borrowerField.value === 'staff';
      customerField.hidden = isStaff;
      staffFieldBlock.hidden = !isStaff;
    };
    borrowerField.addEventListener('change', syncBorrower);

    form.addEventListener('submit', async event => {
      event.preventDefault();
      clearFieldErrors(form);
      const errorRoot = form.querySelector('[data-form-error]');
      errorRoot.innerHTML = '';
      const outfitField = form.elements.namedItem('outfitId');
      const quantityField = form.elements.namedItem('quantity');
      const borrowField = form.elements.namedItem('borrowDate');
      const dueField = form.elements.namedItem('dueDate');
      const quantity = Number(quantityField.value || 0);
      if (!outfitField.value) {
        fieldError(outfitField, 'Chọn trang phục cho thuê.');
        return;
      }
      if (!quantity || quantity < 1) {
        fieldError(quantityField, 'Số lượng phải từ 1 trở lên.');
        return;
      }
      if (!borrowField.value) {
        fieldError(borrowField, 'Chọn ngày cho thuê.');
        return;
      }
      if (dueField.value && dueField.value < borrowField.value) {
        fieldError(dueField, 'Hạn trả phải sau ngày cho thuê.');
        return;
      }
      const type = borrowerField.value;
      const payload = {
        outfitId: outfitField.value,
        quantity,
        borrowerType: type,
        customerId: type === 'customer' ? form.elements.namedItem('customerId').value : '',
        staffId: type === 'staff' ? form.elements.namedItem('staffId').value : '',
        bookingId: form.elements.namedItem('bookingId').value,
        borrowDate: borrowField.value,
        dueDate: dueField.value,
        deposit: Number(form.elements.namedItem('deposit').value || 0),
        notes: form.elements.namedItem('notes').value.trim()
      };
      if (type === 'staff' && !payload.staffId) {
        fieldError(form.elements.namedItem('staffId'), 'Chọn nhân sự mượn trang phục.');
        return;
      }
      const submit = form.querySelector('[data-submit]');
      setButtonLoading(submit, true, 'Đang lưu…');
      try {
        if (isEdit) {
          await operationsApi.updateLoan(loan.id, payload);
        } else {
          await operationsApi.createLoan(payload);
        }
      } catch (error) {
        if (error.name === 'AbortError') return;
        setButtonLoading(submit, false);
        errorRoot.innerHTML = errorInline(error.message || 'Chưa lưu được phiếu cho thuê.', error.code);
        return;
      }
      closeModalFrom(form);
      showToast(isEdit ? 'Đã cập nhật phiếu cho thuê.' : 'Đã tạo phiếu cho thuê.');
      load();
    });
  }

  async function returnLoan(id) {
    const loan = loans.find(item => item.id === id);
    const confirmed = await confirmDialog({
      title: 'Nhận lại trang phục?',
      message: `${loan ? `${loan.outfitName} × ${loan.quantity}` : 'Trang phục'} sẽ được đánh dấu đã trả trong hôm nay.`,
      confirmLabel: 'Đã nhận lại'
    });
    if (!confirmed) return;
    try {
      await operationsApi.returnLoan(id, todayInput());
      showToast('Đã nhận lại trang phục.');
      load();
    } catch (error) {
      showToast(error.message || 'Chưa cập nhật được phiếu cho thuê.', 'error');
    }
  }

  async function deleteLoan(id) {
    const loan = loans.find(item => item.id === id);
    const confirmed = await confirmDialog({
      title: 'Xoá phiếu cho thuê?',
      message: `${loan ? `${loan.outfitName} × ${loan.quantity}` : 'Phiếu này'} sẽ bị xoá khỏi lịch sử tủ đồ.`,
      confirmLabel: 'Xoá phiếu',
      danger: true
    });
    if (!confirmed) return;
    try {
      await operationsApi.deleteLoan(id);
      showToast('Đã xoá phiếu cho thuê.');
      load();
    } catch (error) {
      showToast(error.message || 'Chưa xoá được phiếu cho thuê.', 'error');
    }
  }

  return {
    title: websiteConcepts ? 'Bộ ảnh website' : 'Tủ trang phục',
    bodyClass: 'page-admin',
    html,
    async mount(root) {
      teardown = mountAdminShell(root, session);
      const page = root.querySelector('[data-crm-page]');
      page.addEventListener('click', event => {
        const tabButton = event.target.closest('[data-wardrobe-tab]');
        if (tabButton) {
          tab = tabButton.dataset.wardrobeTab;
          query = '';
          render();
          return;
        }

        if (event.target.closest('[data-wardrobe-retry]')) {
          load();
          return;
        }

        if (event.target.closest('[data-outfit-create]')) {
          openOutfitForm();
          return;
        }

        const outfitEdit = event.target.closest('[data-outfit-edit]');
        if (outfitEdit) {
          const outfit = outfits.find(item => item.id === outfitEdit.dataset.outfitEdit);
          if (outfit) openOutfitForm(outfit);
          return;
        }

        const outfitDelete = event.target.closest('[data-outfit-delete]');
        if (outfitDelete) {
          deleteOutfit(outfitDelete.dataset.outfitDelete);
          return;
        }

        if (event.target.closest('[data-concept-create]')) {
          openConceptForm();
          return;
        }

        const conceptEdit = event.target.closest('[data-concept-edit]');
        if (conceptEdit) {
          const concept = concepts.find(item => item.id === conceptEdit.dataset.conceptEdit);
          if (concept) openConceptForm(concept);
          return;
        }

        const conceptDelete = event.target.closest('[data-concept-delete]');
        if (conceptDelete) {
          deleteConcept(conceptDelete.dataset.conceptDelete);
          return;
        }

        if (event.target.closest('[data-loan-create]')) {
          openLoanForm();
          return;
        }

        const loanEdit = event.target.closest('[data-loan-edit]');
        if (loanEdit) {
          const loan = loans.find(item => item.id === loanEdit.dataset.loanEdit);
          if (loan) openLoanForm(loan);
          return;
        }

        const loanReturn = event.target.closest('[data-loan-return]');
        if (loanReturn) {
          returnLoan(loanReturn.dataset.loanReturn);
          return;
        }

        const loanDelete = event.target.closest('[data-loan-delete]');
        if (loanDelete) {
          deleteLoan(loanDelete.dataset.loanDelete);
        }
      });

      page.addEventListener('input', event => {
        if (event.target.matches('[data-wardrobe-search]')) {
          query = event.target.value;
          const body = document.querySelector('[data-wardrobe-body]');
          if (!body) return;
          const caret = event.target.selectionStart;
          render();
          const next = body.querySelector('[data-wardrobe-search]');
          if (next) {
            next.focus();
            next.setSelectionRange(caret, caret);
          }
        }
      });

      page.addEventListener('change', event => {
        if (event.target.matches('[data-outfit-category]')) {
          category = event.target.value;
          render();
        }
        if (event.target.matches('[data-loan-filter]')) {
          loanFilter = event.target.value;
          render();
        }
      });

      await load();
    },
    cleanup() {
      disposed = true;
      teardown();
    }
  };
}

// ------------------------------------------------------------- notifications

export function notificationsView({ session }) {
  let teardown = () => {};
  let disposed = false;
  let tab = 'inbox';
  let filter = 'all';
  let notifications = [];
  let settings = null;
  let loading = false;

  const canManage = hasPermission(session, 'notifications');

  const html = crmPage({
    session,
    active: 'notifications',
    eyebrow: 'Vận hành studio',
    title: 'Thông báo',
    lead: 'Theo dõi lịch chụp, thanh toán, hợp đồng, tủ đồ và bảng lương trong một hộp thư.',
    body: `
      <nav class="ops-tabs" role="tablist" aria-label="Khu vực thông báo">
        <button class="ops-tab is-active" type="button" role="tab" aria-selected="true" data-notif-tab="inbox">${icon('mail')} Hộp thư</button>
        <button class="ops-tab" type="button" role="tab" aria-selected="false" data-notif-tab="settings">${icon('settings')} Cài đặt</button>
      </nav>
      <div data-notif-body aria-live="polite">${skeletonRows(4)}</div>
    `
  });

  function inboxMarkup() {
    const unread = notifications.filter(item => !item.isRead);
    const list = filter === 'unread' ? unread : notifications;
    return `
      <section class="crm-toolbar" aria-label="Bộ lọc thông báo">
        <div class="ops-segmented" role="group" aria-label="Lọc thông báo">
          <button class="ops-segmented__item ${filter === 'all' ? 'is-active' : ''}" type="button" data-notif-filter="all">Tất cả (${notifications.length})</button>
          <button class="ops-segmented__item ${filter === 'unread' ? 'is-active' : ''}" type="button" data-notif-filter="unread">Chưa đọc (${unread.length})</button>
        </div>
        <span class="ops-toolbar__spacer"></span>
        ${canManage && unread.length ? `<button class="btn btn--ghost btn--compact" type="button" data-notif-read-all>${icon('checkCircle')} Đánh dấu đã đọc tất cả</button>` : ''}
      </section>

      <section class="crm-metrics" aria-label="Số liệu thông báo">
        ${metric({ label: 'Chưa đọc', value: String(unread.length), note: 'cần xem', tone: 'rose', iconName: 'mail' })}
        ${metric({ label: 'Hôm nay', value: String(notifications.filter(item => String(item.createdAt || '').slice(0, 10) === todayInput()).length), note: 'thông báo mới', tone: 'clay', iconName: 'calendar' })}
        ${metric({ label: 'Tổng cộng', value: String(notifications.length), note: 'trong hộp thư', tone: 'olive', iconName: 'chart' })}
      </section>

      <section class="panel ops-panel ops-notif-panel" aria-label="Danh sách thông báo">
        ${list.length ? `
          <div class="ops-notif-list">
            ${list.map(notificationRow).join('')}
          </div>
        ` : emptyState({
          iconName: 'mail',
          title: filter === 'unread' ? 'Không còn thông báo chưa đọc' : 'Hộp thư đang trống',
          message: filter === 'unread' ? 'Bạn đã xem hết thông báo mới.' : 'Thông báo về lịch chụp, thanh toán và tủ đồ sẽ xuất hiện ở đây.'
        })}
      </section>
    `;
  }

  function notificationRow(notification) {
    const meta = NOTIFICATION_TYPES[notification.type] || NOTIFICATION_TYPES.system;
    return `
      <article class="ops-notif ${notification.isRead ? '' : 'is-unread'}" data-notif-row="${escapeAttr(notification.id)}">
        <span class="ops-notif__icon ops-notif__icon--${meta.tone}">${icon(meta.iconName)}</span>
        <div class="ops-notif__main">
          <p class="ops-notif__title">
            ${notification.link
              ? `<a href="${escapeAttr(notification.link)}" data-notif-link="${escapeAttr(notification.id)}">${escapeHtml(notification.title)}</a>`
              : `<strong>${escapeHtml(notification.title)}</strong>`}
            ${notification.isRead ? '' : '<span class="ops-notif__dot" aria-label="Chưa đọc"></span>'}
          </p>
          ${notification.body ? `<p class="ops-notif__body">${escapeHtml(notification.body)}</p>` : ''}
          <p class="ops-notif__meta">${escapeHtml(meta.label)} · ${escapeHtml(timeAgo(notification.createdAt))}</p>
        </div>
        ${canManage ? `
          <div class="ops-notif__actions">
            ${notification.isRead ? '' : `<button class="icon-btn" type="button" data-notif-read="${escapeAttr(notification.id)}" aria-label="Đánh dấu đã đọc" title="Đánh dấu đã đọc">${icon('check')}</button>`}
            <button class="icon-btn" type="button" data-notif-delete="${escapeAttr(notification.id)}" aria-label="Xoá thông báo" title="Xoá">${icon('trash')}</button>
          </div>
        ` : ''}
      </article>
    `;
  }

  function settingsMarkup() {
    if (!settings) return skeletonRows(3);
    const rows = [
      { key: 'master', label: 'Bật thông báo', hint: 'Công tắc tổng cho toàn bộ hộp thư.', iconName: 'bell' },
      { key: 'booking', label: 'Lịch chụp', hint: 'Lịch mới, đổi trạng thái lịch.', iconName: 'calendar' },
      { key: 'payment', label: 'Thanh toán', hint: 'Khi ghi nhận một khoản thu.', iconName: 'wallet' },
      { key: 'contract', label: 'Hợp đồng', hint: 'Hợp đồng mới hoặc thay đổi.', iconName: 'file' },
      { key: 'wardrobe', label: 'Trang phục', hint: 'Cho thuê và nhận lại trang phục.', iconName: 'tag' },
      { key: 'payroll', label: 'Bảng lương', hint: 'Nhắc chi lương và quá hạn.', iconName: 'credit' }
    ];
    return `
      <section class="panel ops-panel ops-settings-panel">
        <header class="panel__head">
          <p class="eyebrow">Hộp thư</p>
          <h2>Cài đặt thông báo</h2>
        </header>
        <form data-notif-settings-form>
          <div class="ops-toggle-list">
            ${rows.map(row => `
              <label class="ops-toggle ${row.key === 'master' ? 'ops-toggle--master' : ''}">
                <span class="ops-toggle__icon">${icon(row.iconName)}</span>
                <span class="ops-toggle__copy">
                  <strong>${escapeHtml(row.label)}</strong>
                  <small>${escapeHtml(row.hint)}</small>
                </span>
                <input type="checkbox" data-notif-setting="${row.key}" ${settings[row.key] ? 'checked' : ''} ${row.key !== 'master' && !settings.master ? 'disabled' : ''}>
                <span class="ops-toggle__track" aria-hidden="true"><span></span></span>
              </label>
            `).join('')}
          </div>
          <div class="dialog-actions ops-settings-actions">
            <button class="btn btn--primary" type="submit" data-submit>${icon('check')} Lưu cài đặt</button>
          </div>
        </form>
      </section>
    `;
  }

  function render() {
    const body = document.querySelector('[data-notif-body]');
    if (!body) return;
    body.innerHTML = tab === 'inbox' ? inboxMarkup() : settingsMarkup();
    document.querySelectorAll('[data-notif-tab]').forEach(button => {
      const active = button.dataset.notifTab === tab;
      button.classList.toggle('is-active', active);
      button.setAttribute('aria-selected', active ? 'true' : 'false');
    });
  }

  async function load() {
    if (loading || disposed) return;
    loading = true;
    const body = document.querySelector('[data-notif-body]');
    if (body) body.innerHTML = skeletonRows(4);
    try {
      const [list, config] = await Promise.all([
        operationsApi.notifications(),
        operationsApi.notificationSettings()
      ]);
      if (disposed) return;
      notifications = Array.isArray(list) ? list : [];
      settings = config;
      render();
    } catch (error) {
      if (disposed) return;
      if (body) body.innerHTML = loadError(error, 'data-notif-retry');
    } finally {
      loading = false;
    }
  }

  async function markRead(id) {
    try {
      const updated = await operationsApi.markNotificationRead(id);
      const index = notifications.findIndex(item => item.id === id);
      if (index >= 0) notifications[index] = updated;
      render();
    } catch (error) {
      showToast(error.message || 'Chưa đánh dấu được thông báo.', 'error');
    }
  }

  async function markAllRead() {
    try {
      const result = await operationsApi.markAllNotificationsRead();
      notifications = notifications.map(item => ({ ...item, isRead: true }));
      showToast(`Đã đánh dấu ${Number(result.updated || 0)} thông báo là đã đọc.`);
      render();
    } catch (error) {
      showToast(error.message || 'Chưa đánh dấu được thông báo.', 'error');
    }
  }

  async function deleteNotification(id) {
    const confirmed = await confirmDialog({
      title: 'Xoá thông báo?',
      message: 'Thông báo này sẽ bị xoá khỏi hộp thư.',
      confirmLabel: 'Xoá thông báo',
      danger: true
    });
    if (!confirmed) return;
    try {
      await operationsApi.deleteNotification(id);
      notifications = notifications.filter(item => item.id !== id);
      showToast('Đã xoá thông báo.');
      render();
    } catch (error) {
      showToast(error.message || 'Chưa xoá được thông báo.', 'error');
    }
  }

  async function saveSettings(form) {
    const payload = {};
    ['master', 'booking', 'payment', 'contract', 'wardrobe', 'payroll'].forEach(key => {
      payload[key] = Boolean(form.querySelector(`[data-notif-setting="${key}"]`)?.checked);
    });
    const submit = form.querySelector('[data-submit]');
    setButtonLoading(submit, true, 'Đang lưu…');
    try {
      settings = await operationsApi.updateNotificationSettings(payload);
      showToast('Đã lưu cài đặt thông báo.');
      render();
    } catch (error) {
      setButtonLoading(submit, false);
      showToast(error.message || 'Chưa lưu được cài đặt.', 'error');
    }
  }

  return {
    title: 'Thông báo',
    bodyClass: 'page-admin',
    html,
    async mount(root) {
      teardown = mountAdminShell(root, session);
      const page = root.querySelector('[data-crm-page]');
      page.addEventListener('click', event => {
        const tabButton = event.target.closest('[data-notif-tab]');
        if (tabButton) {
          tab = tabButton.dataset.notifTab;
          render();
          return;
        }

        if (event.target.closest('[data-notif-retry]')) {
          load();
          return;
        }

        const filterButton = event.target.closest('[data-notif-filter]');
        if (filterButton) {
          filter = filterButton.dataset.notifFilter;
          render();
          return;
        }

        if (event.target.closest('[data-notif-read-all]')) {
          markAllRead();
          return;
        }

        const readButton = event.target.closest('[data-notif-read]');
        if (readButton) {
          markRead(readButton.dataset.notifRead);
          return;
        }

        const deleteButton = event.target.closest('[data-notif-delete]');
        if (deleteButton) {
          deleteNotification(deleteButton.dataset.notifDelete);
          return;
        }

        const link = event.target.closest('[data-notif-link]');
        if (link) {
          const notification = notifications.find(item => item.id === link.dataset.notifLink);
          if (notification && !notification.isRead) {
            operationsApi.markNotificationRead(notification.id).catch(() => {});
          }
        }
      });

      page.addEventListener('change', event => {
        if (event.target.matches('[data-notif-setting="master"]')) {
          const enabled = event.target.checked;
          page.querySelectorAll('[data-notif-setting]').forEach(input => {
            if (input.dataset.notifSetting !== 'master') input.disabled = !enabled;
          });
        }
      });

      page.addEventListener('submit', event => {
        const form = event.target.closest('[data-notif-settings-form]');
        if (!form) return;
        event.preventDefault();
        saveSettings(form);
      });

      await load();
    },
    cleanup() {
      disposed = true;
      teardown();
    }
  };
}
