import { api } from './api.js';
import { escapeHtml, escapeAttr, createModal, showToast } from './ui.js';

export function mountPrices(root) {
  let disposed = false;
  const controller = new AbortController();
  const load = async () => {
    try {
      const groups = await api('/api/public/prices', { signal: controller.signal });
      if (disposed) return;
      root.innerHTML = `<nav class="price-tabs" aria-label="Nhóm bảng giá">${groups.map(g => `<a href="#price-${escapeAttr(g.id)}">${escapeHtml(g.title)}</a>`).join('')}</nav>${groups.map(g => `<section class="price-group" id="price-${escapeAttr(g.id)}"><header><h3>${escapeHtml(g.title)}</h3><p>${escapeHtml(g.description)}</p></header><div class="price-grid">${g.items.map(item => `<article class="price-card"><h4>${escapeHtml(item.name)}</h4>${item.subtitle ? `<p class="price-subtitle">${escapeHtml(item.subtitle)}</p>` : ''}<div class="price-value"><strong>Giá gói chụp</strong><p>${escapeHtml(item.priceText).replaceAll('\n', '<br>')}</p></div><ul>${item.benefits.split('\n').filter(Boolean).map(line => `<li>${escapeHtml(line)}</li>`).join('')}</ul></article>`).join('')}</div>${g.items.length ? '' : '<p>Đang cập nhật bảng giá.</p>'}</section>`).join('')}`;
      if (location.hash === '#bang-gia') root.closest('section')?.scrollIntoView({block:'start'});
    } catch (error) {
      if (!disposed && error.name !== 'AbortError') root.textContent = 'Chưa tải được bảng giá. Vui lòng thử lại sau.';
    }
  };
  load();
  return () => { disposed = true; controller.abort(); };
}

export function mountPriceEditor(root) {
  let disposed = false;
  let groups = [];
  let modal;
  const load = async () => {
    try {
      groups = await api('/api/admin/price-catalog');
      if (disposed) return;
      root.innerHTML = `<div class="price-admin-heading"><div><h2>Bảng giá trên website</h2><p>Chọn Sửa để cập nhật tên, giá, quyền lợi hoặc ẩn gói trên website.</p></div><a class="btn btn--ghost btn--compact" href="/#bang-gia" target="_blank" rel="noopener">Xem bảng giá</a></div>${groups.map(g => `<details class="price-admin-group"><summary>${escapeHtml(g.title)} (${g.items.length} gói)</summary><div>${g.items.map(i => `<div class="price-admin-row"><span><strong>${escapeHtml(i.name)}</strong><small>${escapeHtml(i.priceText.replaceAll('\n', ' · '))} ${i.visible ? '' : ' · Đang ẩn'}</small></span><button class="btn btn--ghost btn--compact" type="button" data-price-edit="${escapeAttr(i.id)}" data-price-group="${escapeAttr(g.id)}">Sửa</button></div>`).join('')}</div></details>`).join('')}`;
    } catch (error) { if (!disposed) root.textContent = 'Không tải được bảng giá: ' + error.message; }
  };
  const click = event => {
    const button = event.target.closest('[data-price-edit]');
    if (!button) return;
    const item = groups.find(g => g.id === button.dataset.priceGroup)?.items.find(i => i.id === button.dataset.priceEdit);
    if (!item) return;
    modal = createModal(`<div class="dialog-card dialog-card--crm"><button class="dialog-close" type="button" data-close-dialog aria-label="Đóng">×</button><h2>Sửa bảng giá website</h2><form class="crm-form" data-price-form><div class="field"><label for="price-edit-name">Tên gói</label><input id="price-edit-name" name="name" maxlength="120" required value="${escapeAttr(item.name)}"></div><div class="field"><label for="price-edit-subtitle">Mô tả ngắn</label><textarea id="price-edit-subtitle" name="subtitle" maxlength="500" rows="2">${escapeHtml(item.subtitle)}</textarea></div><div class="field"><label for="price-edit-price">Giá hiển thị</label><textarea id="price-edit-price" name="priceText" maxlength="500" rows="3">${escapeHtml(item.priceText)}</textarea></div><div class="field"><label for="price-edit-benefits">Quyền lợi (mỗi dòng một mục)</label><textarea id="price-edit-benefits" name="benefits" maxlength="4000" rows="8">${escapeHtml(item.benefits)}</textarea></div><label><input type="checkbox" name="visible" ${item.visible ? 'checked' : ''}> Hiển thị trên website</label><p data-price-error role="alert"></p><div class="dialog-actions"><button class="btn btn--ghost" type="button" data-close-dialog>Huỷ</button><button class="btn btn--primary" type="submit">Lưu thay đổi</button></div></form></div>`, {className:'dialog--crm'});
    modal.dialog.querySelector('form').addEventListener('submit', async event => {
      event.preventDefault(); const form = event.currentTarget; const save = form.querySelector('[type=submit]'); save.disabled = true;
      try {
        await api(`/api/admin/price-catalog/${encodeURIComponent(button.dataset.priceGroup)}/${encodeURIComponent(item.id)}`, {method:'PUT', body:{name:form.elements.name.value, subtitle:form.elements.subtitle.value, priceText:form.elements.priceText.value, benefits:form.elements.benefits.value, visible:form.elements.visible.checked}});
        modal.close(); modal = null; await load(); showToast('Đã cập nhật bảng giá.', 'success');
      } catch (error) { form.querySelector('[data-price-error]').textContent = error.message; save.disabled = false; }
    });
  };
  root.addEventListener('click', click); load();
  return () => { disposed = true; modal?.close(); root.removeEventListener('click', click); };
}
