import { crmApi, salesApi } from './api.js';
import { mountAdminShell, pageHead, hasPermission } from './admin.js';
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
  downloadFile
} from './ui.js';
import {
  crmPage,
  metric,
  skeletonRows,
  optionList,
  loadError,
  money
} from './crm.js';

// --------------------------------------------------------------- shared bits

const ORDER_STATUSES = [
  { key: 'draft', label: 'Nháp', tone: 'draft', iconName: 'edit' },
  { key: 'confirmed', label: 'Đã xác nhận', tone: 'live', iconName: 'checkCircle' },
  { key: 'completed', label: 'Hoàn thành', tone: 'olive', iconName: 'check' },
  { key: 'cancelled', label: 'Đã huỷ', tone: 'neutral', iconName: 'x' }
];

const PAYMENT_STATUSES = {
  paid: { label: 'Đã thanh toán', tone: 'paid', iconName: 'checkCircle' },
  deposit: { label: 'Đã cọc', tone: 'deposit', iconName: 'wallet' },
  unpaid: { label: 'Chưa thanh toán', tone: 'unpaid', iconName: 'clock' }
};

const PAYMENT_METHODS = [
  { key: 'transfer', label: 'Chuyển khoản' },
  { key: 'cash', label: 'Tiền mặt' },
  { key: 'card', label: 'Thẻ' },
  { key: 'other', label: 'Khác' }
];

const CONTRACT_STATUSES = [
  { key: 'draft', label: 'Nháp', tone: 'draft', iconName: 'edit' },
  { key: 'pending', label: 'Chờ ký', tone: 'warn', iconName: 'clock' },
  { key: 'signed', label: 'Đã ký', tone: 'live', iconName: 'checkCircle' },
  { key: 'completed', label: 'Hoàn thành', tone: 'olive', iconName: 'check' },
  { key: 'cancelled', label: 'Đã huỷ', tone: 'neutral', iconName: 'x' }
];

const PRESETS = [
  { key: '50/50', label: 'Đặt cọc 50% · còn lại 50%' },
  { key: '30/40/30', label: '30% · 40% · 30%' },
  { key: '40/40/20', label: '40% · 40% · 20%' },
  { key: '30/70', label: 'Đặt cọc 30% · còn lại 70%' }
];

function statusOf(list, key) {
  return list.find(item => item.key === key) || list[0];
}

function pill(meta) {
  return `<span class="pill pill--${meta.tone}">${icon(meta.iconName)}${escapeHtml(meta.label)}</span>`;
}

function orderStatusPill(status) {
  return pill(statusOf(ORDER_STATUSES, status));
}

function paymentStatusPill(status) {
  return pill(PAYMENT_STATUSES[status] || PAYMENT_STATUSES.unpaid);
}

function contractStatusPill(status) {
  return pill(statusOf(CONTRACT_STATUSES, status));
}

function contractStatusLabel(status) {
  return statusOf(CONTRACT_STATUSES, status).label;
}

function methodLabel(method) {
  return PAYMENT_METHODS.find(item => item.key === method)?.label || 'Khác';
}

function matches(query, values) {
  const needle = stripDiacritics(query).trim();
  if (!needle) return true;
  return values.some(value => stripDiacritics(String(value ?? '')).includes(needle));
}

function dateOrDash(value) {
  if (!value) return '—';
  const text = String(value);
  if (/^\d{4}-\d{2}-\d{2}$/.test(text)) return formatDate(text) || '—';
  const date = new Date(text);
  if (Number.isNaN(date.getTime())) return formatDate(text) || '—';
  return `${String(date.getDate()).padStart(2, '0')}.${String(date.getMonth() + 1).padStart(2, '0')}.${date.getFullYear()}`;
}

function todayInput() {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-${String(now.getDate()).padStart(2, '0')}`;
}

function monthStartInput() {
  const now = new Date();
  return `${now.getFullYear()}-${String(now.getMonth() + 1).padStart(2, '0')}-01`;
}

function sumBy(list, selector) {
  return list.reduce((total, item) => total + Number(selector(item) || 0), 0);
}

function reportPresetFromSchedule(schedule, total) {
  if (!Array.isArray(schedule) || !schedule.length || !total) return '30/40/30';
  const key = schedule
    .map(item => Math.round(Number(item.amount || 0) / total * 100))
    .join('/');
  return PRESETS.some(preset => preset.key === key) ? key : '30/40/30';
}

// ------------------------------------------------------------------- orders

export function ordersView({ session }) {
  let teardown = () => {};
  let disposed = false;
  let orders = [];
  let query = '';
  let status = 'all';
  let payment = 'all';
  let optionsPromise = null;

  const canCreate = hasPermission(session, 'orders');

  const html = crmPage({
    session,
    active: 'orders',
    eyebrow: 'Kinh doanh',
    title: 'Đơn hàng',
    lead: 'Theo dõi giá trị đơn, các khoản đã thu và công nợ còn lại.',
    actions: canCreate
      ? `<button class="btn btn--primary" type="button" data-order-create>${icon('plus')} Tạo đơn hàng</button>`
      : '',
    body: `
      <section class="crm-metrics" data-order-metrics aria-label="Số liệu đơn hàng">
        ${metric({ label: 'Đang tải', value: '—', note: 'Đơn hàng', tone: 'clay', iconName: 'receipt' })}
      </section>
      <section class="crm-toolbar" aria-label="Bộ lọc đơn hàng">
        <label class="crm-search">
          <span>${icon('search')}</span>
          <input type="search" data-order-search placeholder="Tìm mã đơn, khách hàng…" aria-label="Tìm đơn hàng" autocomplete="off">
        </label>
        <select class="select" data-order-status aria-label="Lọc trạng thái đơn">
          <option value="all">Mọi trạng thái</option>
          ${ORDER_STATUSES.map(item => `<option value="${item.key}">${item.label}</option>`).join('')}
        </select>
        <select class="select" data-order-payment aria-label="Lọc tình trạng thanh toán">
          <option value="all">Mọi thanh toán</option>
          <option value="paid">Đã thanh toán</option>
          <option value="deposit">Đã cọc</option>
          <option value="unpaid">Chưa thanh toán</option>
        </select>
        <span class="crm-toolbar__count" data-order-count></span>
      </section>
      <section class="panel crm-list sales-list" data-order-results aria-live="polite">
        ${skeletonRows(4)}
      </section>
    `
  });

  function filtered() {
    return orders
      .filter(order => status === 'all' || order.status === status)
      .filter(order => payment === 'all' || order.paymentStatus === payment)
      .filter(order => matches(query, [order.code, order.customerName, order.customerPhone, order.bookingCode]))
      .sort((a, b) => String(b.createdAt).localeCompare(String(a.createdAt)));
  }

  function metricsHtml() {
    const active = orders.filter(order => order.status !== 'cancelled');
    return [
      metric({
        label: 'Giá trị đơn hàng',
        value: money(sumBy(active, order => order.total)),
        note: `${active.length} đơn đang theo dõi`,
        tone: 'clay',
        iconName: 'receipt'
      }),
      metric({
        label: 'Đã thu',
        value: money(sumBy(active, order => order.paidAmount)),
        note: `${active.filter(order => order.paymentStatus === 'paid').length} đơn đã thanh toán đủ`,
        tone: 'olive',
        iconName: 'wallet'
      }),
      metric({
        label: 'Còn phải thu',
        value: money(sumBy(active, order => order.remaining)),
        note: `${active.filter(order => order.paymentStatus === 'unpaid').length} đơn chưa thanh toán`,
        tone: 'sand',
        iconName: 'clock'
      }),
      metric({
        label: 'Đang mở',
        value: String(active.filter(order => order.status !== 'completed').length),
        note: `${orders.filter(order => order.status === 'completed').length} đơn hoàn thành`,
        tone: 'rose',
        iconName: 'chart'
      })
    ].join('');
  }

  function rowHtml(order) {
    return `
      <article class="sales-row" data-order-row="${escapeAttr(order.id)}">
        <button class="sales-row__open" type="button" data-order-open="${escapeAttr(order.id)}">
          <span class="sales-row__code">${escapeHtml(order.code)}</span>
          <span class="sales-row__main">
            <strong>${escapeHtml(order.customerName || 'Khách lẻ')}</strong>
            <small>${escapeHtml(dateOrDash(order.createdAt))}${order.bookingCode ? ` · ${escapeHtml(order.bookingCode)}` : ''}</small>
          </span>
          <span class="sales-row__amount">
            <strong>${money(order.total)}</strong>
            <small>Đã thu ${money(order.paidAmount)}</small>
          </span>
          <span class="sales-row__pills">
            ${orderStatusPill(order.status)}
            ${paymentStatusPill(order.paymentStatus)}
          </span>
        </button>
        <div class="sales-row__actions">
          <button class="icon-btn" type="button" data-order-invoice="${escapeAttr(order.id)}" aria-label="In hoá đơn ${escapeAttr(order.code)}" title="In hoá đơn">${icon('printer')}</button>
        </div>
      </article>
    `;
  }

  function renderRows() {
    const results = document.querySelector('[data-order-results]');
    if (!results) return;
    const list = filtered();
    results.innerHTML = list.length
      ? list.map(rowHtml).join('')
      : emptyState({
        iconName: 'receipt',
        title: orders.length ? 'Không có đơn phù hợp' : 'Chưa có đơn hàng nào',
        message: orders.length
          ? 'Thử đổi từ khoá hoặc bộ lọc.'
          : 'Tạo đơn hàng đầu tiên để bắt đầu theo dõi doanh thu và công nợ.',
        action: canCreate && !orders.length
          ? '<button class="btn btn--primary" type="button" data-order-create>Tạo đơn hàng</button>'
          : ''
      });

    const metrics = document.querySelector('[data-order-metrics]');
    if (metrics) metrics.innerHTML = metricsHtml();
    const count = document.querySelector('[data-order-count]');
    if (count) count.textContent = `${list.length} / ${orders.length} đơn`;
  }

  async function loadOptions() {
    if (!optionsPromise) {
      optionsPromise = Promise.all([crmApi.customers(), crmApi.bookings()])
        .then(([customers, bookings]) => ({ customers, bookings }))
        .catch(error => {
          optionsPromise = null;
          throw error;
        });
    }
    return optionsPromise;
  }

  async function openDetail(id) {
    try {
      const detail = await salesApi.order(id);
      if (disposed) return;
      const order = detail.order;
      const payments = detail.payments || [];
      const modal = createModal(`
        <div class="dialog-card dialog-card--crm dialog-card--wide">
          <button class="dialog-close" type="button" data-close-dialog aria-label="Đóng">${icon('x')}</button>
          <header class="task-dialog__head">
            <span class="task-dialog__icon">${icon('receipt')}</span>
            <div>
              <p class="eyebrow">Đơn hàng</p>
              <h2>${escapeHtml(order.code)}</h2>
            </div>
          </header>
          <div class="sales-detail__pills">
            ${orderStatusPill(order.status)}
            ${paymentStatusPill(order.paymentStatus)}
            ${order.bookingCode ? `<span class="pill pill--neutral">${icon('calendar')}${escapeHtml(order.bookingCode)}</span>` : ''}
          </div>
          <div class="sales-detail__summary">
            <div><span>Khách hàng</span><strong>${escapeHtml(order.customerName || 'Khách lẻ')}</strong></div>
            <div><span>Ngày tạo</span><strong>${escapeHtml(dateOrDash(order.createdAt))}</strong></div>
            <div><span>Tổng tiền</span><strong>${money(order.total)}</strong></div>
            <div><span>Đã thu</span><strong>${money(order.paidAmount)}</strong></div>
            <div><span>Còn lại</span><strong class="${order.remaining > 0 ? 'is-debt' : ''}">${money(order.remaining)}</strong></div>
          </div>
          <div class="sales-detail__section">
            <h3>Hạng mục</h3>
            <div class="table-wrap">
              <table class="sales-table">
                <thead><tr><th>Hạng mục</th><th>Số lượng</th><th>Đơn giá</th><th>Thành tiền</th></tr></thead>
                <tbody>
                  ${order.items.map(item => `
                    <tr>
                      <td>${escapeHtml(item.name)}</td>
                      <td>${item.quantity.toLocaleString('vi-VN')}</td>
                      <td>${money(item.unitPrice)}</td>
                      <td>${money(item.amount)}</td>
                    </tr>
                  `).join('')}
                </tbody>
                <tfoot>
                  <tr><td colspan="3">Tạm tính</td><td>${money(order.subtotal)}</td></tr>
                  ${order.discount > 0 ? `<tr><td colspan="3">Giảm giá</td><td>−${money(order.discount)}</td></tr>` : ''}
                  <tr><td colspan="3">Tổng cộng</td><td><strong>${money(order.total)}</strong></td></tr>
                </tfoot>
              </table>
            </div>
          </div>
          <div class="sales-detail__section">
            <div class="sales-detail__section-head">
              <h3>Thanh toán</h3>
              ${canCreate ? `<button class="btn btn--ghost btn--compact" type="button" data-payment-add>${icon('plus')} Ghi thanh toán</button>` : ''}
            </div>
            ${payments.length ? `
              <ul class="sales-payments">
                ${payments.map(item => `
                  <li>
                    <span class="sales-payments__icon">${icon('wallet')}</span>
                    <div>
                      <strong>${money(item.amount)}</strong>
                      <small>${escapeHtml(item.code)} · ${escapeHtml(methodLabel(item.method))} · ${escapeHtml(dateOrDash(item.paidAt))}</small>
                      ${item.note ? `<small>${escapeHtml(item.note)}</small>` : ''}
                    </div>
                    ${canCreate ? `<button class="icon-btn" type="button" data-payment-delete="${escapeAttr(item.id)}" aria-label="Xoá thanh toán ${escapeAttr(item.code)}">${icon('trash')}</button>` : ''}
                  </li>
                `).join('')}
              </ul>
            ` : '<p class="sales-muted">Chưa ghi nhận khoản thanh toán nào.</p>'}
          </div>
          ${order.notes ? `
            <div class="sales-detail__section">
              <h3>Ghi chú</h3>
              <p class="sales-note">${escapeHtml(order.notes)}</p>
            </div>
          ` : ''}
          <div class="dialog-actions sales-detail__actions">
            <button class="btn btn--ghost" type="button" data-order-invoice="${escapeAttr(order.id)}">${icon('printer')} In hoá đơn</button>
            ${canCreate ? `<button class="btn btn--ghost" type="button" data-order-edit="${escapeAttr(order.id)}">${icon('edit')} Sửa</button>` : ''}
            ${canCreate ? `<button class="btn btn--danger" type="button" data-order-delete="${escapeAttr(order.id)}">${icon('trash')} Xoá</button>` : ''}
          </div>
        </div>
      `, { className: 'dialog--crm dialog--wide' });

      modal.dialog.addEventListener('click', async event => {
        const addButton = event.target.closest('[data-payment-add]');
        if (addButton) {
          modal.close();
          openPaymentForm(order);
          return;
        }

        const deleteButton = event.target.closest('[data-payment-delete]');
        if (deleteButton) {
          const paymentId = deleteButton.dataset.paymentDelete;
          const confirmed = await confirmDialog({
            title: 'Xoá khoản thanh toán?',
            message: 'Khoản thanh toán sẽ bị xoá khỏi đơn hàng và báo cáo doanh thu.',
            confirmLabel: 'Xoá thanh toán',
            danger: true
          });
          if (!confirmed) return;
          try {
            await salesApi.deletePayment(order.id, paymentId);
            showToast('Đã xoá khoản thanh toán.');
            modal.close();
            await reload();
            openDetail(order.id);
          } catch (error) {
            showToast(error.message || 'Chưa xoá được khoản thanh toán.', 'error');
          }
          return;
        }

        const editButton = event.target.closest('[data-order-edit]');
        if (editButton) {
          modal.close();
          openForm(order);
          return;
        }

        const invoiceButton = event.target.closest('[data-order-invoice]');
        if (invoiceButton) {
          printInvoice(order.id);
          return;
        }

        const deleteOrderButton = event.target.closest('[data-order-delete]');
        if (deleteOrderButton) {
          const confirmed = await confirmDialog({
            title: `Xoá đơn ${order.code}?`,
            message: 'Đơn hàng sẽ bị xoá vĩnh viễn. Chỉ xoá được khi chưa có thanh toán.',
            confirmLabel: 'Xoá đơn hàng',
            danger: true
          });
          if (!confirmed) return;
          try {
            await salesApi.deleteOrder(order.id);
            showToast('Đã xoá đơn hàng.');
            modal.close();
            await reload();
          } catch (error) {
            showToast(error.message || 'Chưa xoá được đơn hàng.', 'error');
          }
        }
      });
    } catch (error) {
      showToast(error.message || 'Không tải được đơn hàng.', 'error');
    }
  }

  async function openForm(order = null) {
    let options;
    try {
      options = await loadOptions();
    } catch (error) {
      showToast(error.message || 'Không tải được danh sách khách hàng, lịch chụp.', 'error');
      return;
    }

    const isEdit = Boolean(order);
    const items = order?.items?.length
      ? order.items.map(item => ({ name: item.name, quantity: item.quantity, unitPrice: item.unitPrice }))
      : [{ name: '', quantity: 1, unitPrice: 0 }];
    const customerOptions = options.customers.map(customer => ({
      value: customer.id,
      label: `${customer.name}${customer.phone ? ` · ${customer.phone}` : ''}`
    }));
    const bookingOptions = options.bookings.map(booking => ({
      value: booking.id,
      label: `${booking.code} · ${booking.customerName || 'Khách'} · ${dateOrDash(booking.date)}`
    }));

    const itemRow = (item, index) => `
      <div class="sales-item" data-item-row>
        <div class="field sales-item__name">
          <label class="sr-only" for="order-item-name-${index}">Hạng mục ${index + 1}</label>
          <input id="order-item-name-${index}" type="text" maxlength="160" value="${escapeAttr(item.name || '')}" placeholder="Tên hạng mục" data-item-name>
        </div>
        <div class="field">
          <label class="sr-only" for="order-item-qty-${index}">Số lượng</label>
          <input id="order-item-qty-${index}" type="number" min="1" step="1" value="${escapeAttr(item.quantity ?? 1)}" data-item-qty aria-label="Số lượng">
        </div>
        <div class="field">
          <label class="sr-only" for="order-item-price-${index}">Đơn giá</label>
          <input id="order-item-price-${index}" type="number" min="0" step="1000" value="${escapeAttr(item.unitPrice ?? 0)}" data-item-price aria-label="Đơn giá">
        </div>
        <button class="icon-btn" type="button" data-item-remove aria-label="Xoá hạng mục">${icon('trash')}</button>
      </div>
    `;

    const modal = createModal(`
      <div class="dialog-card dialog-card--crm dialog-card--wide">
        <button class="dialog-close" type="button" data-close-dialog aria-label="Đóng">${icon('x')}</button>
        <header class="task-dialog__head">
          <span class="task-dialog__icon">${icon(isEdit ? 'edit' : 'receipt')}</span>
          <div>
            <p class="eyebrow">${isEdit ? 'Cập nhật đơn' : 'Đơn hàng mới'}</p>
            <h2>${isEdit ? `Sửa đơn ${escapeHtml(order.code)}` : 'Tạo đơn hàng'}</h2>
          </div>
        </header>
        <form class="crm-form" data-order-form novalidate>
          <div class="crm-form__grid">
            <div class="field">
              <label for="order-customer">Khách hàng <span class="required">*</span></label>
              <select id="order-customer" name="customerId" required>
                ${optionList(customerOptions, order?.customerId || '', 'Chọn khách hàng')}
              </select>
            </div>
            <div class="field">
              <label for="order-booking">Lịch chụp</label>
              <select id="order-booking" name="bookingId">
                ${optionList(bookingOptions, order?.bookingId || '', 'Không gắn lịch chụp')}
              </select>
            </div>
            <div class="field">
              <label for="order-status">Trạng thái</label>
              <select id="order-status" name="status">
                ${ORDER_STATUSES.map(item => `<option value="${item.key}" ${(order?.status || 'confirmed') === item.key ? 'selected' : ''}>${item.label}</option>`).join('')}
              </select>
            </div>
            <div class="field">
              <label for="order-discount">Giảm giá (đ)</label>
              <input id="order-discount" name="discount" type="number" min="0" step="1000" value="${escapeAttr(order?.discount ?? 0)}">
            </div>
          </div>

          <div class="sales-items" data-items>
            ${items.map(itemRow).join('')}
          </div>
          <button class="btn btn--ghost btn--compact" type="button" data-item-add>${icon('plus')} Thêm hạng mục</button>

          <div class="sales-form-total">
            <span>Tổng cộng</span>
            <strong data-order-total>${money(order?.total ?? 0)}</strong>
          </div>

          <div class="field crm-form__full">
            <label for="order-notes">Ghi chú</label>
            <textarea id="order-notes" name="notes" rows="3" maxlength="2000" placeholder="Yêu cầu riêng, thoả thuận thêm…">${escapeHtml(order?.notes || '')}</textarea>
          </div>

          <div data-form-error></div>
          <div class="dialog-actions">
            <button class="btn btn--ghost" type="button" data-close-dialog>Huỷ</button>
            <button class="btn btn--primary" type="submit" data-submit>${icon('check')} ${isEdit ? 'Lưu thay đổi' : 'Tạo đơn hàng'}</button>
          </div>
        </form>
      </div>
    `, { className: 'dialog--crm dialog--wide' });

    const form = modal.dialog.querySelector('[data-order-form]');
    const itemsHolder = form.querySelector('[data-items]');
    const discountInput = form.querySelector('[name="discount"]');
    const totalHolder = form.querySelector('[data-order-total]');

    function recalc() {
      const rows = [...itemsHolder.querySelectorAll('[data-item-row]')];
      const subtotal = rows.reduce((sum, row) => {
        const qty = Number(row.querySelector('[data-item-qty]').value || 0);
        const price = Number(row.querySelector('[data-item-price]').value || 0);
        return sum + qty * price;
      }, 0);
      const discount = Number(discountInput.value || 0);
      totalHolder.textContent = money(Math.max(0, subtotal - discount));
    }

    form.addEventListener('input', event => {
      if (event.target.matches('[data-item-qty], [data-item-price], [name="discount"]')) recalc();
    });

    form.addEventListener('click', event => {
      if (event.target.closest('[data-item-add]')) {
        const holder = document.createElement('div');
        holder.innerHTML = itemRow({ name: '', quantity: 1, unitPrice: 0 }, itemsHolder.children.length);
        itemsHolder.append(holder.firstElementChild);
        recalc();
        itemsHolder.querySelector('[data-item-row]:last-child [data-item-name]')?.focus();
        return;
      }

      const remove = event.target.closest('[data-item-remove]');
      if (remove) {
        if (itemsHolder.children.length === 1) {
          showToast('Đơn hàng cần ít nhất một hạng mục.', 'error');
          return;
        }
        remove.closest('[data-item-row]').remove();
        recalc();
      }
    });

    form.addEventListener('submit', async event => {
      event.preventDefault();
      const submit = form.querySelector('[data-submit]');
      const errorHolder = form.querySelector('[data-form-error]');
      errorHolder.innerHTML = '';

      const customerId = form.querySelector('[name="customerId"]').value;
      if (!customerId) {
        errorHolder.innerHTML = errorInline('Vui lòng chọn khách hàng.');
        form.querySelector('[name="customerId"]').focus();
        return;
      }

      const payloadItems = [...itemsHolder.querySelectorAll('[data-item-row]')]
        .map(row => ({
          name: row.querySelector('[data-item-name]').value.trim(),
          quantity: Number(row.querySelector('[data-item-qty]').value || 0),
          unitPrice: Number(row.querySelector('[data-item-price]').value || 0)
        }))
        .filter(item => item.name.length > 0);

      if (!payloadItems.length) {
        errorHolder.innerHTML = errorInline('Thêm ít nhất một hạng mục có tên.');
        return;
      }

      const body = {
        customerId,
        bookingId: form.querySelector('[name="bookingId"]').value,
        items: payloadItems,
        discount: Number(form.querySelector('[name="discount"]').value || 0),
        status: form.querySelector('[name="status"]').value,
        notes: form.querySelector('[name="notes"]').value
      };

      setButtonLoading(submit, true, isEdit ? 'Đang lưu…' : 'Đang tạo…');
      try {
        if (isEdit) {
          await salesApi.updateOrder(order.id, body);
          showToast('Đã cập nhật đơn hàng.');
        } else {
          await salesApi.createOrder(body);
          showToast('Đã tạo đơn hàng.');
        }
        modal.close();
        await reload();
      } catch (error) {
        errorHolder.innerHTML = errorInline(error.message || 'Chưa lưu được đơn hàng.', error.code);
      } finally {
        setButtonLoading(submit, false);
      }
    });

    recalc();
  }

  function openPaymentForm(order) {
    const amount = order.remaining > 0 ? order.remaining : order.total;
    const modal = createModal(`
      <div class="dialog-card dialog-card--crm">
        <button class="dialog-close" type="button" data-close-dialog aria-label="Đóng">${icon('x')}</button>
        <header class="task-dialog__head">
          <span class="task-dialog__icon">${icon('wallet')}</span>
          <div>
            <p class="eyebrow">Thanh toán</p>
            <h2>Ghi thanh toán · ${escapeHtml(order.code)}</h2>
          </div>
        </header>
        <form class="crm-form" data-payment-form novalidate>
          <div class="crm-form__grid">
            <div class="field">
              <label for="payment-amount">Số tiền (đ) <span class="required">*</span></label>
              <input id="payment-amount" name="amount" type="number" min="1" step="1000" value="${escapeAttr(Math.round(amount))}" required autofocus>
            </div>
            <div class="field">
              <label for="payment-method">Hình thức</label>
              <select id="payment-method" name="method">
                ${PAYMENT_METHODS.map(item => `<option value="${item.key}" ${item.key === 'transfer' ? 'selected' : ''}>${item.label}</option>`).join('')}
              </select>
            </div>
            <div class="field">
              <label for="payment-date">Ngày thu</label>
              <input id="payment-date" name="paidAt" type="date" value="${todayInput()}">
            </div>
            <div class="field crm-form__full">
              <label for="payment-note">Ghi chú</label>
              <input id="payment-note" name="note" type="text" maxlength="500" placeholder="Mã giao dịch, nội dung chuyển khoản…">
            </div>
          </div>
          <p class="sales-muted">Còn lại trước khi thu: <strong>${money(order.remaining)}</strong></p>
          <div data-form-error></div>
          <div class="dialog-actions">
            <button class="btn btn--ghost" type="button" data-close-dialog>Huỷ</button>
            <button class="btn btn--primary" type="submit" data-submit>${icon('check')} Ghi thanh toán</button>
          </div>
        </form>
      </div>
    `, { className: 'dialog--crm' });

    const form = modal.dialog.querySelector('[data-payment-form]');
    form.addEventListener('submit', async event => {
      event.preventDefault();
      const submit = form.querySelector('[data-submit]');
      const errorHolder = form.querySelector('[data-form-error]');
      const payload = {
        amount: Number(form.querySelector('[name="amount"]').value || 0),
        method: form.querySelector('[name="method"]').value,
        paidAt: form.querySelector('[name="paidAt"]').value,
        note: form.querySelector('[name="note"]').value
      };
      if (payload.amount <= 0) {
        errorHolder.innerHTML = errorInline('Số tiền phải lớn hơn 0.');
        return;
      }

      setButtonLoading(submit, true, 'Đang ghi…');
      try {
        await salesApi.recordPayment(order.id, payload);
        showToast('Đã ghi nhận thanh toán.');
        modal.close();
        await reload();
        openDetail(order.id);
      } catch (error) {
        errorHolder.innerHTML = errorInline(error.message || 'Chưa ghi được thanh toán.', error.code);
      } finally {
        setButtonLoading(submit, false);
      }
    });
  }

  async function printInvoice(id) {
    try {
      const invoice = await salesApi.invoice(id);
      const rows = invoice.items.map(item => `
        <tr>
          <td>${escapeHtml(item.name)}</td>
          <td class="num">${item.quantity.toLocaleString('vi-VN')}</td>
          <td class="num">${money(item.unitPrice)}</td>
          <td class="num">${money(item.amount)}</td>
        </tr>
      `).join('');
      openPrintWindow(`Hoá đơn ${invoice.code}`, `
        <header class="print-head">
          <div>
            <p class="print-kicker">HOÁ ĐƠN THANH TOÁN</p>
            <h1>${escapeHtml(invoice.studioName || 'Mây Studio')}</h1>
            <p>${escapeHtml(invoice.studioEmail || '')}</p>
          </div>
          <div class="print-meta">
            <strong>${escapeHtml(invoice.code)}</strong>
            <span>${escapeHtml(dateOrDash(invoice.createdAt))}</span>
          </div>
        </header>
        <section class="print-customer">
          <h2>Khách hàng</h2>
          <p><strong>${escapeHtml(invoice.customerName || 'Khách lẻ')}</strong></p>
          ${invoice.customerPhone ? `<p>${escapeHtml(invoice.customerPhone)}</p>` : ''}
          ${invoice.customerAddress ? `<p>${escapeHtml(invoice.customerAddress)}</p>` : ''}
        </section>
        <table class="print-table">
          <thead><tr><th>Hạng mục</th><th class="num">SL</th><th class="num">Đơn giá</th><th class="num">Thành tiền</th></tr></thead>
          <tbody>${rows}</tbody>
        </table>
        <section class="print-totals">
          <p><span>Tạm tính</span><strong>${money(invoice.subtotal)}</strong></p>
          ${invoice.discount > 0 ? `<p><span>Giảm giá</span><strong>−${money(invoice.discount)}</strong></p>` : ''}
          <p class="grand"><span>Tổng cộng</span><strong>${money(invoice.total)}</strong></p>
          <p><span>Đã thanh toán</span><strong>${money(invoice.paidAmount)}</strong></p>
          <p><span>Còn lại</span><strong>${money(invoice.remaining)}</strong></p>
        </section>
        ${invoice.notes ? `<section class="print-note"><h2>Ghi chú</h2><p>${escapeHtml(invoice.notes)}</p></section>` : ''}
        <footer class="print-sign">
          <div><span>Khách hàng</span><small>Ký, ghi rõ họ tên</small></div>
          <div><span>${escapeHtml(invoice.studioName || 'Studio')}</span><small>Ký, đóng dấu</small></div>
        </footer>
      `);
    } catch (error) {
      showToast(error.message || 'Không tải được hoá đơn.', 'error');
    }
  }

  async function reload() {
    try {
      orders = await salesApi.orders();
      if (!disposed) renderRows();
    } catch (error) {
      if (disposed) return;
      const results = document.querySelector('[data-order-results]');
      if (results) results.innerHTML = loadError(error, 'data-order-retry');
    }
  }

  return {
    title: 'Đơn hàng',
    bodyClass: 'page-admin',
    html,
    async mount(root) {
      teardown = mountAdminShell(root, session);
      const page = root.querySelector('[data-crm-page]');

      page.addEventListener('click', event => {
        if (event.target.closest('[data-order-create]')) {
          openForm();
          return;
        }

        if (event.target.closest('[data-order-retry]')) {
          reload();
          return;
        }

        const invoice = event.target.closest('[data-order-invoice]');
        if (invoice) {
          event.preventDefault();
          printInvoice(invoice.dataset.orderInvoice);
          return;
        }

        const open = event.target.closest('[data-order-open]');
        if (open) {
          openDetail(open.dataset.orderOpen);
        }
      });

      page.addEventListener('input', event => {
        if (event.target.matches('[data-order-search]')) {
          query = event.target.value;
          renderRows();
        }
      });

      page.addEventListener('change', event => {
        if (event.target.matches('[data-order-status]')) {
          status = event.target.value;
          renderRows();
        }
        if (event.target.matches('[data-order-payment]')) {
          payment = event.target.value;
          renderRows();
        }
      });

      await reload();
    },
    cleanup() {
      disposed = true;
      teardown();
    }
  };
}

// ---------------------------------------------------------------- contracts

export function contractsView({ session }) {
  let teardown = () => {};
  let disposed = false;
  let contracts = [];
  let query = '';
  let status = 'all';
  let optionsPromise = null;

  const canCreate = hasPermission(session, 'contracts');

  const html = crmPage({
    session,
    active: 'contracts',
    eyebrow: 'Kinh doanh',
    title: 'Hợp đồng',
    lead: 'Lịch thanh toán theo từng mốc, điều khoản và tiến độ thu tiền.',
    actions: `
      <button class="btn btn--ghost btn--compact" type="button" data-contract-export>${icon('download')} Xuất Excel</button>
      ${canCreate
        ? `<button class="btn btn--primary" type="button" data-contract-create>${icon('plus')} Tạo hợp đồng</button>`
        : ''}
    `,
    body: `
      <section class="crm-metrics" data-contract-metrics aria-label="Số liệu hợp đồng">
        ${metric({ label: 'Đang tải', value: '—', note: 'Hợp đồng', tone: 'clay', iconName: 'file' })}
      </section>
      <section class="crm-toolbar" aria-label="Bộ lọc hợp đồng">
        <label class="crm-search">
          <span>${icon('search')}</span>
          <input type="search" data-contract-search placeholder="Tìm mã hợp đồng, khách hàng, dịch vụ…" aria-label="Tìm hợp đồng" autocomplete="off">
        </label>
        <select class="select" data-contract-status aria-label="Lọc trạng thái hợp đồng">
          <option value="all">Mọi trạng thái</option>
          ${CONTRACT_STATUSES.map(item => `<option value="${item.key}">${item.label}</option>`).join('')}
        </select>
        <span class="crm-toolbar__count" data-contract-count></span>
      </section>
      <section class="panel crm-list sales-list" data-contract-results aria-live="polite">
        ${skeletonRows(4)}
      </section>
    `
  });

  function filtered() {
    return contracts
      .filter(contract => status === 'all' || contract.status === status)
      .filter(contract => matches(query, [contract.code, contract.customerName, contract.service, contract.bookingCode]))
      .sort((a, b) => String(b.createdAt).localeCompare(String(a.createdAt)));
  }

  function metricsHtml() {
    const active = contracts.filter(contract => contract.status !== 'cancelled');
    return [
      metric({
        label: 'Giá trị hợp đồng',
        value: money(sumBy(active, contract => contract.total)),
        note: `${active.length} hợp đồng`,
        tone: 'clay',
        iconName: 'file'
      }),
      metric({
        label: 'Đã thu theo lịch',
        value: money(sumBy(active, contract => contract.paidAmount)),
        note: `${active.filter(contract => contract.status === 'signed').length} hợp đồng đã ký`,
        tone: 'olive',
        iconName: 'wallet'
      }),
      metric({
        label: 'Còn lại',
        value: money(sumBy(active, contract => contract.remaining)),
        note: 'Theo các mốc chưa thanh toán',
        tone: 'sand',
        iconName: 'clock'
      }),
      metric({
        label: 'Chờ ký',
        value: String(active.filter(contract => ['draft', 'pending'].includes(contract.status)).length),
        note: `${contracts.filter(contract => contract.status === 'completed').length} đã hoàn thành`,
        tone: 'rose',
        iconName: 'edit'
      })
    ].join('');
  }

  function rowHtml(contract) {
    return `
      <article class="sales-row" data-contract-row="${escapeAttr(contract.id)}">
        <button class="sales-row__open" type="button" data-contract-open="${escapeAttr(contract.id)}">
          <span class="sales-row__code">${escapeHtml(contract.code)}</span>
          <span class="sales-row__main">
            <strong>${escapeHtml(contract.customerName || 'Khách lẻ')}</strong>
            <small>${escapeHtml(contract.service || 'Dịch vụ studio')}${contract.bookingCode ? ` · ${escapeHtml(contract.bookingCode)}` : ''}</small>
          </span>
          <span class="sales-row__amount">
            <strong>${money(contract.total)}</strong>
            <small>Đã thu ${money(contract.paidAmount)} · ${contract.progressPercent}%</small>
          </span>
          <span class="sales-row__pills">${contractStatusPill(contract.status)}</span>
        </button>
        <div class="sales-row__actions">
          <button class="icon-btn" type="button" data-contract-print="${escapeAttr(contract.id)}" aria-label="In hợp đồng ${escapeAttr(contract.code)}" title="In hợp đồng">${icon('printer')}</button>
        </div>
      </article>
    `;
  }

  function renderRows() {
    const results = document.querySelector('[data-contract-results]');
    if (!results) return;
    const list = filtered();
    results.innerHTML = list.length
      ? list.map(rowHtml).join('')
      : emptyState({
        iconName: 'file',
        title: contracts.length ? 'Không có hợp đồng phù hợp' : 'Chưa có hợp đồng nào',
        message: contracts.length
          ? 'Thử đổi từ khoá hoặc bộ lọc.'
          : 'Tạo hợp đồng để tự sinh lịch thanh toán và điều khoản.',
        action: canCreate && !contracts.length
          ? '<button class="btn btn--primary" type="button" data-contract-create>Tạo hợp đồng</button>'
          : ''
      });

    const metrics = document.querySelector('[data-contract-metrics]');
    if (metrics) metrics.innerHTML = metricsHtml();
    const count = document.querySelector('[data-contract-count]');
    if (count) count.textContent = `${list.length} / ${contracts.length} hợp đồng`;
  }

  async function loadOptions() {
    if (!optionsPromise) {
      optionsPromise = Promise.all([crmApi.customers(), crmApi.bookings()])
        .then(([customers, bookings]) => ({ customers, bookings }))
        .catch(error => {
          optionsPromise = null;
          throw error;
        });
    }
    return optionsPromise;
  }

  async function openDetail(id) {
    try {
      const contract = await salesApi.contract(id);
      if (disposed) return;
      const modal = createModal(`
        <div class="dialog-card dialog-card--crm dialog-card--wide">
          <button class="dialog-close" type="button" data-close-dialog aria-label="Đóng">${icon('x')}</button>
          <header class="task-dialog__head">
            <span class="task-dialog__icon">${icon('file')}</span>
            <div>
              <p class="eyebrow">Hợp đồng</p>
              <h2>${escapeHtml(contract.code)}</h2>
            </div>
          </header>
          <div class="sales-detail__pills">
            ${contractStatusPill(contract.status)}
            ${contract.bookingCode ? `<span class="pill pill--neutral">${icon('calendar')}${escapeHtml(contract.bookingCode)}</span>` : ''}
          </div>
          <div class="sales-detail__summary">
            <div><span>Khách hàng</span><strong>${escapeHtml(contract.customerName || 'Khách lẻ')}</strong></div>
            <div><span>Dịch vụ</span><strong>${escapeHtml(contract.service || '—')}</strong></div>
            <div><span>Giá trị</span><strong>${money(contract.total)}</strong></div>
            <div><span>Đã thu</span><strong>${money(contract.paidAmount)}</strong></div>
            <div><span>Còn lại</span><strong class="${contract.remaining > 0 ? 'is-debt' : ''}">${money(contract.remaining)}</strong></div>
          </div>
          <div class="sales-progress" aria-label="Tiến độ thanh toán ${contract.progressPercent}%">
            <span style="width:${contract.progressPercent}%"></span>
          </div>
          <div class="sales-detail__section">
            <h3>Lịch thanh toán</h3>
            <div class="table-wrap">
              <table class="sales-table sales-table--schedule">
                <thead><tr><th>Mốc</th><th>Đến hạn</th><th>Số tiền</th><th>Trạng thái</th><th></th></tr></thead>
                <tbody>
                  ${contract.schedule.map((item, index) => `
                    <tr class="${item.paid ? 'is-paid' : item.isOverdue ? 'is-overdue' : ''}">
                      <td>${escapeHtml(item.label)}</td>
                      <td>${escapeHtml(dateOrDash(item.dueDate))}${item.isOverdue ? ' · quá hạn' : ''}</td>
                      <td>${money(item.amount)}</td>
                      <td>${item.paid
                        ? `<span class="pill pill--paid">${icon('checkCircle')}Đã thu${item.paidAt ? ` ${escapeHtml(dateOrDash(item.paidAt))}` : ''}</span>`
                        : `<span class="pill pill--unpaid">${icon('clock')}Chưa thu</span>`}</td>
                      <td>
                        ${canCreate ? `<button class="btn btn--ghost btn--compact" type="button" data-toggle-index="${index}" data-paid="${item.paid ? 'false' : 'true'}">${item.paid ? 'Bỏ đánh dấu' : 'Đánh dấu đã thu'}</button>` : ''}
                      </td>
                    </tr>
                  `).join('')}
                </tbody>
              </table>
            </div>
          </div>
          ${contract.terms ? `
            <div class="sales-detail__section">
              <h3>Điều khoản</h3>
              <p class="sales-note sales-note--terms">${escapeHtml(contract.terms)}</p>
            </div>
          ` : ''}
          <div class="dialog-actions sales-detail__actions">
            <button class="btn btn--ghost" type="button" data-contract-print="${escapeAttr(contract.id)}">${icon('printer')} In hợp đồng</button>
            ${canCreate ? `<button class="btn btn--ghost" type="button" data-contract-edit="${escapeAttr(contract.id)}">${icon('edit')} Sửa</button>` : ''}
            ${canCreate ? `<button class="btn btn--danger" type="button" data-contract-delete="${escapeAttr(contract.id)}">${icon('trash')} Xoá</button>` : ''}
          </div>
        </div>
      `, { className: 'dialog--crm dialog--wide' });

      modal.dialog.addEventListener('click', async event => {
        const toggle = event.target.closest('[data-toggle-index]');
        if (toggle) {
          const index = Number(toggle.dataset.toggleIndex);
          const paid = toggle.dataset.paid === 'true';
          setButtonLoading(toggle, true, 'Đang lưu…');
          try {
            await salesApi.toggleSchedule(contract.id, index, paid);
            showToast(paid ? 'Đã đánh dấu mốc thanh toán.' : 'Đã bỏ đánh dấu mốc thanh toán.');
            modal.close();
            await reload();
            openDetail(contract.id);
          } catch (error) {
            showToast(error.message || 'Chưa cập nhật được lịch thanh toán.', 'error');
          } finally {
            setButtonLoading(toggle, false);
          }
          return;
        }

        if (event.target.closest('[data-contract-print]')) {
          printContract(contract.id);
          return;
        }

        const editButton = event.target.closest('[data-contract-edit]');
        if (editButton) {
          modal.close();
          openForm(contract);
          return;
        }

        const deleteButton = event.target.closest('[data-contract-delete]');
        if (deleteButton) {
          const confirmed = await confirmDialog({
            title: `Xoá hợp đồng ${contract.code}?`,
            message: 'Hợp đồng và lịch thanh toán sẽ bị xoá vĩnh viễn.',
            confirmLabel: 'Xoá hợp đồng',
            danger: true
          });
          if (!confirmed) return;
          try {
            await salesApi.deleteContract(contract.id);
            showToast('Đã xoá hợp đồng.');
            modal.close();
            await reload();
          } catch (error) {
            showToast(error.message || 'Chưa xoá được hợp đồng.', 'error');
          }
        }
      });
    } catch (error) {
      showToast(error.message || 'Không tải được hợp đồng.', 'error');
    }
  }

  async function openForm(contract = null) {
    let options;
    try {
      options = await loadOptions();
    } catch (error) {
      showToast(error.message || 'Không tải được danh sách khách hàng, lịch chụp.', 'error');
      return;
    }

    const isEdit = Boolean(contract);
    const customerOptions = options.customers.map(customer => ({
      value: customer.id,
      label: `${customer.name}${customer.phone ? ` · ${customer.phone}` : ''}`
    }));
    const bookingOptions = options.bookings.map(booking => ({
      value: booking.id,
      label: `${booking.code} · ${booking.customerName || 'Khách'} · ${dateOrDash(booking.date)}`
    }));
    const preset = contract
      ? reportPresetFromSchedule(contract.schedule, contract.total)
      : '30/40/30';

    const modal = createModal(`
      <div class="dialog-card dialog-card--crm dialog-card--wide">
        <button class="dialog-close" type="button" data-close-dialog aria-label="Đóng">${icon('x')}</button>
        <header class="task-dialog__head">
          <span class="task-dialog__icon">${icon(isEdit ? 'edit' : 'file')}</span>
          <div>
            <p class="eyebrow">${isEdit ? 'Cập nhật hợp đồng' : 'Hợp đồng mới'}</p>
            <h2>${isEdit ? `Sửa ${escapeHtml(contract.code)}` : 'Tạo hợp đồng'}</h2>
          </div>
        </header>
        <form class="crm-form" data-contract-form novalidate>
          <div class="crm-form__grid">
            <div class="field">
              <label for="contract-customer">Khách hàng <span class="required">*</span></label>
              <select id="contract-customer" name="customerId" required>
                ${optionList(customerOptions, contract?.customerId || '', 'Chọn khách hàng')}
              </select>
            </div>
            <div class="field">
              <label for="contract-booking">Lịch chụp</label>
              <select id="contract-booking" name="bookingId">
                ${optionList(bookingOptions, contract?.bookingId || '', 'Không gắn lịch chụp')}
              </select>
            </div>
            <div class="field">
              <label for="contract-service">Dịch vụ</label>
              <input id="contract-service" name="service" type="text" maxlength="160" value="${escapeAttr(contract?.service || '')}" placeholder="Ví dụ: Cưới — Sweet Moments">
            </div>
            <div class="field">
              <label for="contract-total">Giá trị hợp đồng (đ) <span class="required">*</span></label>
              <input id="contract-total" name="total" type="number" min="1" step="1000" value="${escapeAttr(contract?.total ?? 0)}" required>
            </div>
            <div class="field">
              <label for="contract-preset">Tỷ lệ thanh toán</label>
              <select id="contract-preset" name="preset">
                ${PRESETS.map(item => `<option value="${item.key}" ${item.key === preset ? 'selected' : ''}>${item.label}</option>`).join('')}
              </select>
            </div>
            <div class="field">
              <label for="contract-status">Trạng thái</label>
              <select id="contract-status" name="status">
                ${CONTRACT_STATUSES.map(item => `<option value="${item.key}" ${(contract?.status || 'pending') === item.key ? 'selected' : ''}>${item.label}</option>`).join('')}
              </select>
            </div>
            <div class="field crm-form__full">
              <label for="contract-terms">Điều khoản</label>
              <textarea id="contract-terms" name="terms" rows="6" maxlength="8000" placeholder="Để trống để studio tự sinh điều khoản mẫu.">${escapeHtml(contract?.terms || '')}</textarea>
            </div>
          </div>
          <div data-form-error></div>
          <div class="dialog-actions">
            <button class="btn btn--ghost" type="button" data-close-dialog>Huỷ</button>
            <button class="btn btn--primary" type="submit" data-submit>${icon('check')} ${isEdit ? 'Lưu thay đổi' : 'Tạo hợp đồng'}</button>
          </div>
        </form>
      </div>
    `, { className: 'dialog--crm dialog--wide' });

    const form = modal.dialog.querySelector('[data-contract-form]');
    form.querySelector('[name="bookingId"]').addEventListener('change', event => {
      const booking = options.bookings.find(item => item.id === event.target.value);
      if (!booking) return;
      const service = form.querySelector('[name="service"]');
      const total = form.querySelector('[name="total"]');
      if (!service.value.trim()) service.value = booking.packageName || '';
      if (Number(total.value || 0) <= 0 && booking.price > 0) total.value = Math.round(booking.price);
    });

    form.addEventListener('submit', async event => {
      event.preventDefault();
      const submit = form.querySelector('[data-submit]');
      const errorHolder = form.querySelector('[data-form-error]');
      errorHolder.innerHTML = '';
      const customerId = form.querySelector('[name="customerId"]').value;
      if (!customerId) {
        errorHolder.innerHTML = errorInline('Vui lòng chọn khách hàng.');
        return;
      }

      const total = Number(form.querySelector('[name="total"]').value || 0);
      if (total <= 0) {
        errorHolder.innerHTML = errorInline('Giá trị hợp đồng phải lớn hơn 0.');
        return;
      }

      const presetValue = form.querySelector('[name="preset"]').value;
      const body = {
        customerId,
        bookingId: form.querySelector('[name="bookingId"]').value,
        service: form.querySelector('[name="service"]').value,
        terms: form.querySelector('[name="terms"]').value,
        status: form.querySelector('[name="status"]').value
      };

      if (isEdit) {
        // Chỉ gửi trường thực sự thay đổi để không ghi đè lịch thanh toán
        // tuỳ chỉnh của hợp đồng đã nhập từ CRM.
        if (total !== Number(contract.total)) body.total = total;
        if (presetValue !== preset) body.preset = presetValue;
      } else {
        body.total = total;
        body.preset = presetValue;
      }

      setButtonLoading(submit, true, isEdit ? 'Đang lưu…' : 'Đang tạo…');
      try {
        if (isEdit) {
          await salesApi.updateContract(contract.id, body);
          showToast('Đã cập nhật hợp đồng.');
        } else {
          await salesApi.createContract(body);
          showToast('Đã tạo hợp đồng.');
        }
        modal.close();
        await reload();
      } catch (error) {
        errorHolder.innerHTML = errorInline(error.message || 'Chưa lưu được hợp đồng.', error.code);
      } finally {
        setButtonLoading(submit, false);
      }
    });
  }

  async function printContract(id) {
    try {
      const contract = await salesApi.contract(id);
      const rows = contract.schedule.map(item => `
        <tr class="${item.paid ? 'is-paid' : ''}">
          <td>${escapeHtml(item.label)}</td>
          <td>${escapeHtml(dateOrDash(item.dueDate))}</td>
          <td class="num">${money(item.amount)}</td>
          <td>${item.paid ? 'Đã thanh toán' : 'Chưa thanh toán'}</td>
        </tr>
      `).join('');

      openPrintWindow(`Hợp đồng ${contract.code}`, `
        <header class="print-head">
          <div>
            <p class="print-kicker">HỢP ĐỒNG DỊCH VỤ</p>
            <h1>${escapeHtml(contract.code)}</h1>
          </div>
          <div class="print-meta">
            <strong>${escapeHtml(contractStatusLabel(contract.status))}</strong>
            <span>${escapeHtml(dateOrDash(contract.createdAt))}</span>
          </div>
        </header>
        <section class="print-customer">
          <h2>Khách hàng</h2>
          <p><strong>${escapeHtml(contract.customerName || 'Khách lẻ')}</strong></p>
          ${contract.customerPhone ? `<p>${escapeHtml(contract.customerPhone)}</p>` : ''}
        </section>
        <section class="print-customer">
          <h2>Dịch vụ</h2>
          <p><strong>${escapeHtml(contract.service || 'Dịch vụ studio')}</strong></p>
          <p>Giá trị hợp đồng: <strong>${money(contract.total)}</strong></p>
        </section>
        <table class="print-table">
          <thead><tr><th>Mốc thanh toán</th><th>Đến hạn</th><th class="num">Số tiền</th><th>Trạng thái</th></tr></thead>
          <tbody>${rows}</tbody>
        </table>
        <section class="print-totals">
          <p><span>Đã thu</span><strong>${money(contract.paidAmount)}</strong></p>
          <p class="grand"><span>Còn lại</span><strong>${money(contract.remaining)}</strong></p>
        </section>
        ${contract.terms ? `<section class="print-note"><h2>Điều khoản</h2><p class="pre">${escapeHtml(contract.terms)}</p></section>` : ''}
        <footer class="print-sign">
          <div><span>Khách hàng</span><small>Ký, ghi rõ họ tên</small></div>
          <div><span>Đại diện studio</span><small>Ký, đóng dấu</small></div>
        </footer>
      `);
    } catch (error) {
      showToast(error.message || 'Không tải được hợp đồng.', 'error');
    }
  }

  async function reload() {
    try {
      contracts = await salesApi.contracts();
      if (!disposed) renderRows();
    } catch (error) {
      if (disposed) return;
      const results = document.querySelector('[data-contract-results]');
      if (results) results.innerHTML = loadError(error, 'data-contract-retry');
    }
  }

  async function exportContracts(button) {
    setButtonLoading(button, true, 'Đang xuất…');
    try {
      const fileName = await downloadFile(
        salesApi.exportContractsUrl(status, query),
        'hop-dong.xlsx'
      );
      showToast(`Đã xuất ${fileName}.`);
    } catch (error) {
      showToast(error.message || 'Chưa xuất được danh sách hợp đồng.', 'error');
    } finally {
      setButtonLoading(button, false);
    }
  }

  return {
    title: 'Hợp đồng',
    bodyClass: 'page-admin',
    html,
    async mount(root) {
      teardown = mountAdminShell(root, session);
      const page = root.querySelector('[data-crm-page]');

      page.addEventListener('click', event => {
        if (event.target.closest('[data-contract-create]')) {
          openForm();
          return;
        }

        if (event.target.closest('[data-contract-export]')) {
          exportContracts(event.target.closest('[data-contract-export]'));
          return;
        }

        if (event.target.closest('[data-contract-retry]')) {
          reload();
          return;
        }

        const print = event.target.closest('[data-contract-print]');
        if (print) {
          event.preventDefault();
          printContract(print.dataset.contractPrint);
          return;
        }

        const open = event.target.closest('[data-contract-open]');
        if (open) {
          openDetail(open.dataset.contractOpen);
        }
      });

      page.addEventListener('input', event => {
        if (event.target.matches('[data-contract-search]')) {
          query = event.target.value;
          renderRows();
        }
      });

      page.addEventListener('change', event => {
        if (event.target.matches('[data-contract-status]')) {
          status = event.target.value;
          renderRows();
        }
      });

      await reload();
    },
    cleanup() {
      disposed = true;
      teardown();
    }
  };
}

// ------------------------------------------------------------------ reports

export function reportsView({ session }) {
  let teardown = () => {};
  let disposed = false;
  let from = monthStartInput();
  let to = todayInput();
  let report = null;

  const html = crmPage({
    session,
    active: 'reports',
    eyebrow: 'Kinh doanh',
    title: 'Báo cáo',
    lead: 'Doanh thu theo đơn hàng và hợp đồng, công nợ và khách hàng nổi bật.',
    actions: `<button class="btn btn--ghost" type="button" data-report-print>${icon('printer')} In báo cáo</button>`,
    body: `
      <section class="crm-toolbar sales-report-toolbar" aria-label="Khoảng thời gian báo cáo">
        <label class="sales-date-field">
          <span>Từ ngày</span>
          <input type="date" data-report-from value="${from}">
        </label>
        <label class="sales-date-field">
          <span>Đến ngày</span>
          <input type="date" data-report-to value="${to}">
        </label>
        <button class="btn btn--primary btn--compact" type="button" data-report-apply>${icon('chart')} Xem báo cáo</button>
        <div class="spacer"></div>
        <span class="crm-toolbar__count" data-report-range></span>
      </section>
      <section data-report-results aria-live="polite">
        ${skeletonRows(3)}
      </section>
    `
  });

  function renderReport(data) {
    report = data;
    const holder = document.querySelector('[data-report-results]');
    if (!holder) return;
    const maxMonth = Math.max(...(data.months || []).map(item => item.total), 1);
    const months = data.months || [];
    const methods = data.methods || [];
    const topCustomers = data.topCustomers || [];

    holder.innerHTML = `
      <section class="crm-metrics" aria-label="Số liệu báo cáo">
        ${metric({
          label: 'Tổng doanh thu',
          value: money(data.totalRevenue),
          note: 'Đơn hàng + hợp đồng trong kỳ',
          tone: 'clay',
          iconName: 'chart'
        })}
        ${metric({
          label: 'Doanh thu đơn hàng',
          value: money(data.orderRevenue),
          note: `${data.orderCount} đơn tạo trong kỳ`,
          tone: 'olive',
          iconName: 'receipt'
        })}
        ${metric({
          label: 'Doanh thu hợp đồng',
          value: money(data.contractRevenue),
          note: `${data.contractCount} hợp đồng đang theo dõi`,
          tone: 'sand',
          iconName: 'file'
        })}
        ${metric({
          label: 'Công nợ còn lại',
          value: money(Number(data.orderDebt || 0) + Number(data.contractDebt || 0)),
          note: `${money(data.orderDebt)} đơn · ${money(data.contractDebt)} hợp đồng`,
          tone: 'rose',
          iconName: 'clock'
        })}
      </section>

      <p class="sales-muted sales-report-note">${icon('alertCircle')} Doanh thu đơn hàng tính theo ngày thu tiền; doanh thu hợp đồng tính theo các mốc đã đánh dấu. Hai nguồn được hiển thị riêng để tránh trùng lặp.</p>

      <div class="sales-report-grid">
        <section class="panel panel--pad sales-report-card">
          <header class="panel__head panel__head--row">
            <div>
              <p class="eyebrow">Theo tháng</p>
              <h2>Doanh thu 6 tháng gần nhất</h2>
            </div>
          </header>
          ${months.length ? `
            <div class="sales-bars">
              ${months.map(item => `
                <div class="sales-bar">
                  <div class="sales-bar__track"><span style="height:${Math.max(2, Math.round(item.total / maxMonth * 100))}%"></span></div>
                  <strong>${money(item.total)}</strong>
                  <small>${escapeHtml(item.month)}</small>
                </div>
              `).join('')}
            </div>
          ` : '<p class="sales-muted">Chưa có dữ liệu trong khoảng thời gian này.</p>'}
        </section>

        <section class="panel panel--pad sales-report-card">
          <header class="panel__head panel__head--row">
            <div>
              <p class="eyebrow">Hình thức thu</p>
              <h2>Thanh toán theo phương thức</h2>
            </div>
          </header>
          ${methods.length ? `
            <ul class="sales-breakdown">
              ${methods.map(item => `
                <li>
                  <span>${icon('wallet')}${escapeHtml(methodLabel(item.method))}</span>
                  <strong>${money(item.amount)}</strong>
                  <small>${item.count} giao dịch</small>
                </li>
              `).join('')}
            </ul>
          ` : '<p class="sales-muted">Chưa có thanh toán nào trong kỳ.</p>'}
        </section>

        <section class="panel panel--pad sales-report-card">
          <header class="panel__head panel__head--row">
            <div>
              <p class="eyebrow">Khách hàng</p>
              <h2>Doanh thu nổi bật</h2>
            </div>
          </header>
          ${topCustomers.length ? `
            <ol class="sales-top-customers">
              ${topCustomers.map((item, index) => `
                <li>
                  <span class="sales-top-customers__rank">${index + 1}</span>
                  <div>
                    <strong>${escapeHtml(item.customerName)}</strong>
                    <small>${item.orderCount} đơn hàng</small>
                  </div>
                  <span>${money(item.revenue)}</span>
                </li>
              `).join('')}
            </ol>
          ` : '<p class="sales-muted">Chưa có doanh thu theo khách hàng trong kỳ.</p>'}
        </section>

        <section class="panel panel--pad sales-report-card">
          <header class="panel__head panel__head--row">
            <div>
              <p class="eyebrow">Tình trạng</p>
              <h2>Tóm tắt công nợ</h2>
            </div>
          </header>
          <ul class="sales-breakdown">
            <li><span>Đơn đã thanh toán đủ</span><strong>${data.paidOrderCount}</strong><small>${money(data.orderRevenue)} doanh thu</small></li>
            <li><span>Đơn chưa thanh toán</span><strong>${data.unpaidOrderCount}</strong><small>${money(data.orderDebt)} còn lại</small></li>
            <li><span>Hợp đồng chờ ký</span><strong>${data.pendingContractCount}</strong><small>${money(data.contractDebt)} còn lại</small></li>
          </ul>
        </section>
      </div>
    `;

    const range = document.querySelector('[data-report-range]');
    if (range) range.textContent = `${dateOrDash(data.from)} → ${dateOrDash(data.to)}`;
  }

  async function loadReport() {
    const holder = document.querySelector('[data-report-results]');
    if (holder) holder.innerHTML = skeletonRows(3);
    try {
      const data = await salesApi.reportSummary({ from, to });
      if (!disposed) renderReport(data);
    } catch (error) {
      if (disposed) return;
      if (holder) holder.innerHTML = loadError(error, 'data-report-retry');
    }
  }

  function printReport() {
    if (!report) {
      showToast('Chưa có dữ liệu báo cáo để in.', 'error');
      return;
    }
    const months = report.months || [];
    const methods = report.methods || [];
    const topCustomers = report.topCustomers || [];
    openPrintWindow('Báo cáo doanh thu', `
      <header class="print-head">
        <div>
          <p class="print-kicker">BÁO CÁO DOANH THU</p>
          <h1>${escapeHtml(session.studioName || 'Mây Studio')}</h1>
          <p>Từ ${escapeHtml(dateOrDash(report.from))} đến ${escapeHtml(dateOrDash(report.to))}</p>
        </div>
      </header>
      <section class="print-totals">
        <p><span>Tổng doanh thu</span><strong>${money(report.totalRevenue)}</strong></p>
        <p><span>Doanh thu đơn hàng</span><strong>${money(report.orderRevenue)}</strong></p>
        <p><span>Doanh thu hợp đồng</span><strong>${money(report.contractRevenue)}</strong></p>
        <p><span>Công nợ đơn hàng</span><strong>${money(report.orderDebt)}</strong></p>
        <p><span>Công nợ hợp đồng</span><strong>${money(report.contractDebt)}</strong></p>
      </section>
      <table class="print-table">
        <thead><tr><th>Tháng</th><th class="num">Đơn hàng</th><th class="num">Hợp đồng</th><th class="num">Tổng</th></tr></thead>
        <tbody>
          ${months.map(item => `
            <tr><td>${escapeHtml(item.month)}</td><td class="num">${money(item.orderRevenue)}</td><td class="num">${money(item.contractRevenue)}</td><td class="num">${money(item.total)}</td></tr>
          `).join('')}
        </tbody>
      </table>
      ${methods.length ? `
        <section class="print-note"><h2>Thanh toán theo phương thức</h2>
          ${methods.map(item => `<p>${escapeHtml(methodLabel(item.method))}: <strong>${money(item.amount)}</strong> (${item.count} giao dịch)</p>`).join('')}
        </section>
      ` : ''}
      ${topCustomers.length ? `
        <section class="print-note"><h2>Khách hàng nổi bật</h2>
          ${topCustomers.map(item => `<p>${escapeHtml(item.customerName)}: <strong>${money(item.revenue)}</strong></p>`).join('')}
        </section>
      ` : ''}
    `);
  }

  return {
    title: 'Báo cáo',
    bodyClass: 'page-admin',
    html,
    async mount(root) {
      teardown = mountAdminShell(root, session);
      const page = root.querySelector('[data-crm-page]');

      page.addEventListener('click', event => {
        if (event.target.closest('[data-report-apply]')) {
          from = page.querySelector('[data-report-from]').value || from;
          to = page.querySelector('[data-report-to]').value || to;
          loadReport();
          return;
        }

        if (event.target.closest('[data-report-retry]')) {
          loadReport();
          return;
        }

        if (event.target.closest('[data-report-print]')) {
          printReport();
        }
      });

      await loadReport();
    },
    cleanup() {
      disposed = true;
      teardown();
    }
  };
}

// ------------------------------------------------------------------- print

function openPrintWindow(title, bodyHtml) {
  const win = window.open('', '_blank', 'width=960,height=760');
  if (!win) {
    showToast('Trình duyệt đã chặn cửa sổ in. Hãy cho phép pop-up rồi thử lại.', 'error');
    return;
  }

  win.document.write(`<!doctype html>
<html lang="vi">
<head>
  <meta charset="utf-8">
  <title>${escapeHtml(title)}</title>
  <style>
    @page { size: A4; margin: 18mm; }
    * { box-sizing: border-box; }
    body { margin: 0; color: #2f2a24; font-family: "Be Vietnam Pro", "Segoe UI", sans-serif; font-size: 12px; line-height: 1.55; }
    h1 { margin: 0; font-family: "Lora", Georgia, serif; font-size: 24px; }
    h2 { margin: 0 0 6px; font-size: 12px; text-transform: uppercase; letter-spacing: .08em; color: #8a6f5a; }
    p { margin: 0 0 4px; }
    .print-head { display: flex; justify-content: space-between; gap: 24px; padding-bottom: 16px; border-bottom: 2px solid #c97b5a; margin-bottom: 20px; }
    .print-kicker { margin: 0 0 4px; font-size: 10px; letter-spacing: .18em; color: #8a6f5a; }
    .print-meta { text-align: right; }
    .print-meta strong { display: block; font-size: 18px; }
    .print-meta span { color: #6f6459; }
    .print-customer { margin-bottom: 14px; }
    .print-table { width: 100%; border-collapse: collapse; margin: 12px 0 18px; }
    .print-table th, .print-table td { padding: 8px 10px; border-bottom: 1px solid #e6ded4; text-align: left; vertical-align: top; }
    .print-table th { background: #f7f2eb; font-size: 10px; text-transform: uppercase; letter-spacing: .06em; }
    .print-table .num { text-align: right; white-space: nowrap; }
    .print-totals { margin-left: auto; width: 300px; }
    .print-totals p { display: flex; justify-content: space-between; gap: 16px; padding: 5px 0; border-bottom: 1px dashed #e6ded4; }
    .print-totals .grand { font-size: 15px; border-bottom: 2px solid #c97b5a; }
    .print-note { margin-top: 16px; }
    .print-note .pre { white-space: pre-wrap; }
    .print-sign { display: flex; justify-content: space-between; gap: 40px; margin-top: 48px; text-align: center; }
    .print-sign div { flex: 1; }
    .print-sign span { display: block; font-weight: 600; }
    .print-sign small { color: #8a7f72; font-style: italic; }
    .is-paid { color: #4f6b45; }
  </style>
</head>
<body>${bodyHtml}</body>
</html>`);
  win.document.close();
  win.focus();
  window.setTimeout(() => {
    try {
      win.print();
    } catch {
      // Người dùng vẫn có thể in thủ công từ cửa sổ vừa mở.
    }
  }, 350);
}
