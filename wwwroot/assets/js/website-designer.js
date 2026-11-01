import { api, settingsApi } from './api.js';
import { adminShell, mountAdminShell } from './admin.js';
import { escapeAttr, escapeHtml, icon, showToast } from './ui.js';

const SECTIONS = [
  { key: 'hero', title: 'Ảnh bìa giới thiệu', description: 'Ảnh mở đầu, tiêu đề và nút chính', flag: 'showHero' },
  { key: 'highlights', title: 'Điểm nổi bật', description: 'Bốn cam kết ngắn của studio', flag: 'showHighlights' },
  { key: 'howItWorks', title: 'Cách hoạt động', description: 'Quy trình từ album đến lựa chọn ảnh', flag: 'showHowItWorks' },
  { key: 'pricing', title: 'Bảng giá dịch vụ', description: 'Các nhóm và gói chụp đang công khai', flag: 'showPricing' },
  { key: 'concepts', title: 'Bộ ảnh theo concept', description: 'Những bộ ảnh đã bật hiển thị website', flag: 'showConcepts' },
  { key: 'studio', title: 'Dành cho studio', description: 'Lời mời đăng nhập và giới thiệu công cụ', flag: 'showStudio' }
];

const defaults = Object.fromEntries(SECTIONS.map(item => [item.flag, true]));

function normalizeWebsite(website = {}) {
  const known = new Set(SECTIONS.map(item => item.key));
  const order = Array.isArray(website.sectionOrder) ? website.sectionOrder.filter(key => known.has(key)) : [];
  SECTIONS.forEach(item => { if (!order.includes(item.key)) order.push(item.key); });
  return { ...defaults, ...website, sectionOrder: order };
}

function sectionRows(state) {
  return state.sectionOrder.map((key, index) => {
    const item = SECTIONS.find(entry => entry.key === key);
    return `<li class="wb-order-row" draggable="true" data-section-key="${key}">
      <span class="wb-order-row__grip" aria-hidden="true">${icon('gripVertical')}</span>
      <span class="wb-order-row__index">${index + 1}</span>
      <span class="wb-order-row__copy"><strong>${escapeHtml(item.title)}</strong><small>${escapeHtml(item.description)}</small></span>
      <span class="wb-order-row__moves">
        <button type="button" class="icon-btn" data-move="up" aria-label="Đưa ${escapeAttr(item.title)} lên" ${index === 0 ? 'disabled' : ''}>${icon('arrowUp')}</button>
        <button type="button" class="icon-btn" data-move="down" aria-label="Đưa ${escapeAttr(item.title)} xuống" ${index === state.sectionOrder.length - 1 ? 'disabled' : ''}>${icon('arrowDown')}</button>
      </span>
      <label class="wb-switch"><input type="checkbox" name="${item.flag}" ${state[item.flag] !== false ? 'checked' : ''}><span aria-hidden="true"></span><span class="sr-only">Hiển thị ${escapeHtml(item.title)}</span></label>
    </li>`;
  }).join('');
}

function preview(state, settings) {
  const visible = key => {
    const item = SECTIONS.find(entry => entry.key === key);
    return item && state[item.flag] !== false;
  };
  const brand = state.brandName || settings.studioName || 'StudioCore';
  const label = state.brandLabel || 'album';
  const image = settings.homeImageUrl || '/assets/img/demo/12.jpg';
  const blocks = state.sectionOrder.filter(visible).map(key => {
    if (key === 'hero') return `<section class="wb-preview-hero" style="--preview-image:url('${escapeAttr(image)}')"><div class="wb-preview-hero__shade"></div><div class="wb-preview-hero__copy"><p>${escapeHtml(state.heroTagline || 'Lưu giữ những điều thương mến.')}</p><h2>${escapeHtml(state.heroTitle || 'Mỗi khoảnh khắc, một lời nhớ.')}</h2><a href="${escapeAttr(state.heroCtaUrl || '#concepts')}" tabindex="-1">${escapeHtml(state.heroCtaLabel || 'Khám phá concept')}</a></div></section>`;
    if (key === 'highlights') return `<section class="wb-preview-strip"><span>Ảnh gốc chất lượng cao</span><span>Liên kết riêng tư</span><span>Chọn ảnh nhanh</span></section>`;
    if (key === 'howItWorks') return `<section class="wb-preview-section"><h3>Cách hoạt động</h3><div class="wb-preview-steps"><span>Tạo album</span><span>Gửi liên kết</span><span>Nhận lựa chọn</span></div></section>`;
    if (key === 'pricing') return `<section class="wb-preview-section wb-preview-price"><h3>Bảng giá</h3><div><span>Gia đình</span><span>Trẻ em</span><span>Sơ sinh</span></div></section>`;
    if (key === 'concepts') return `<section class="wb-preview-section"><h3>Concept nổi bật</h3><div class="wb-preview-concepts"><span></span><span></span><span></span></div></section>`;
    return `<section class="wb-preview-studio"><h3>Dành cho studio</h3><p>Kết nối album, gửi liên kết và nhận lựa chọn ảnh.</p></section>`;
  }).join('');
  return `<div class="wb-preview-site"><header><strong>${escapeHtml(brand)}<i>.</i></strong><small>${escapeHtml(label)}</small><nav>Trang chủ&nbsp;&nbsp;&nbsp; Bảng giá</nav></header>${blocks || '<div class="wb-preview-empty">Hãy bật ít nhất một mục để xem trước website.</div>'}</div>`;
}

function editor(state, settings) {
  return `<form class="wb-editor" data-wb-form>
    <section class="wb-panel">
      <header><div><h2>Bố cục &amp; sắp xếp vị trí</h2><p>Kéo thả hoặc dùng nút mũi tên để đổi thứ tự hiển thị.</p></div><span class="wb-status">Tự do kéo đổi</span></header>
      <ol class="wb-order" data-section-order>${sectionRows(state)}</ol>
    </section>
    <section class="wb-panel">
      <header><div><h2>Thương hiệu góc trên web</h2><p>Chỉnh chữ thương hiệu xuất hiện ở đầu website.</p></div></header>
      <div class="wb-fields wb-fields--two">
        <label>Tên thương hiệu<input name="brandName" maxlength="60" value="${escapeAttr(state.brandName || 'StudioCore')}"></label>
        <label>Tiêu đề phụ<input name="brandLabel" maxlength="30" value="${escapeAttr(state.brandLabel || 'album')}"></label>
      </div>
    </section>
    <section class="wb-panel">
      <header><div><h2>Ảnh bìa hero giới thiệu</h2><p>Ảnh mở màn và thông điệp chính của studio.</p></div></header>
      <img class="wb-hero-thumb" data-wb-image src="${escapeAttr(settings.homeImageUrl || '/assets/img/demo/12.jpg')}" alt="Ảnh bìa hiện tại">
      <div class="wb-image-actions"><label class="btn btn--ghost btn--compact">${icon('image')} Đổi ảnh<input type="file" accept="image/png,image/jpeg,image/webp" data-wb-image-upload hidden></label><button type="button" class="btn btn--text btn--compact" data-wb-image-reset ${settings.homeImageUrl ? '' : 'hidden'}>Dùng ảnh mặc định</button></div>
      <div class="wb-fields">
        <label>Tiêu đề chính<input name="heroTitle" maxlength="160" value="${escapeAttr(state.heroTitle || '')}"></label>
        <label>Dòng cảm xúc<input name="heroTagline" maxlength="160" value="${escapeAttr(state.heroTagline || '')}"></label>
        <label>Mô tả<textarea name="heroDescription" maxlength="1200" rows="3">${escapeHtml(state.heroDescription || '')}</textarea></label>
        <div class="wb-fields wb-fields--two"><label>Nhãn nút chính<input name="heroCtaLabel" maxlength="60" value="${escapeAttr(state.heroCtaLabel || '')}"></label><label>Liên kết nút<input name="heroCtaUrl" maxlength="500" value="${escapeAttr(state.heroCtaUrl || '#concepts')}"></label></div>
      </div>
    </section>
  </form>`;
}

export function websiteDesignerView({ session }) {
  const html = adminShell({
    active: 'website-designer', session,
    content: `<div class="admin-page__inner wb-page" data-wb-page><header class="wb-title"><div><h1>Trình thiết kế website studio</h1><p>Sắp xếp nội dung, chỉnh thương hiệu và xem kết quả ngay trước khi lưu.</p></div><div><a class="btn btn--ghost" href="/" target="_blank" rel="noopener">Xem website ${icon('arrowUpRight')}</a><button class="btn btn--primary" type="button" data-wb-save>${icon('check')} Lưu thay đổi</button></div></header><div class="wb-loading">Đang tải cấu hình website…</div></div>`
  });
  let cleanupShell = () => {};
  let disposed = false;
  return {
    title: 'Thiết kế website', bodyClass: 'page-admin page-website-builder', html,
    async mount(root) {
      cleanupShell = mountAdminShell(root, session);
      const page = root.querySelector('[data-wb-page]');
      let settings;
      let state;
      let mode = 'desktop';
      const renderWorkspace = () => {
        page.querySelector('.wb-workspace')?.remove();
        page.querySelector('.wb-loading')?.remove();
        page.insertAdjacentHTML('beforeend', `<div class="wb-workspace"><div class="wb-editor-column">${editor(state, settings)}</div><aside class="wb-preview-column"><div class="wb-preview-toolbar"><div role="group" aria-label="Kích thước xem trước"><button type="button" data-preview-mode="desktop" class="${mode === 'desktop' ? 'is-active' : ''}">${icon('hardDrive')} Máy tính</button><button type="button" data-preview-mode="mobile" class="${mode === 'mobile' ? 'is-active' : ''}">${icon('phone')} Điện thoại</button></div><span><i></i>Xem trước thời gian thực</span></div><div class="wb-browser wb-browser--${mode}"><div class="wb-browser__bar"><i></i><i></i><i></i><span>${escapeHtml(location.host)}/</span></div><div class="wb-preview-frame" data-wb-preview>${preview(state, settings)}</div></div></aside></div>`);
      };
      const syncState = () => {
        const form = page.querySelector('[data-wb-form]');
        if (!form) return;
        const data = new FormData(form);
        ['brandName','brandLabel','heroTitle','heroTagline','heroDescription','heroCtaLabel','heroCtaUrl'].forEach(key => { state[key] = String(data.get(key) || '').trim(); });
        SECTIONS.forEach(item => { state[item.flag] = Boolean(form.querySelector(`[name="${item.flag}"]`)?.checked); });
        page.querySelector('[data-wb-preview]').innerHTML = preview(state, settings);
      };
      try {
        settings = await api('/api/admin/settings');
        if (disposed) return;
        state = normalizeWebsite(settings.website);
        renderWorkspace();
      } catch (error) {
        page.querySelector('.wb-loading').textContent = error.message || 'Không tải được cấu hình website.';
        return;
      }
      page.addEventListener('input', syncState);
      page.addEventListener('change', syncState);
      page.addEventListener('click', async event => {
        const modeButton = event.target.closest('[data-preview-mode]');
        if (modeButton) { mode = modeButton.dataset.previewMode; syncState(); renderWorkspace(); return; }
        const move = event.target.closest('[data-move]');
        if (move) {
          const key = move.closest('[data-section-key]').dataset.sectionKey;
          const from = state.sectionOrder.indexOf(key);
          const to = move.dataset.move === 'up' ? from - 1 : from + 1;
          if (to >= 0 && to < state.sectionOrder.length) [state.sectionOrder[from], state.sectionOrder[to]] = [state.sectionOrder[to], state.sectionOrder[from]];
          syncState(); renderWorkspace(); return;
        }
        const reset = event.target.closest('[data-wb-image-reset]');
        if (reset) {
          settings = await settingsApi.removeHomeImage();
          settings.website = state;
          renderWorkspace(); showToast('Đã dùng lại ảnh bìa mặc định.', 'success'); return;
        }
        const save = event.target.closest('[data-wb-save]');
        if (save) {
          syncState(); save.disabled = true;
          try {
            settings = await api('/api/admin/settings', { method: 'PUT', body: { website: state, studioName: settings.studioName, contactEmail: settings.contactEmail, googleApiKey: null, clearGoogleApiKey: false } });
            state = normalizeWebsite(settings.website);
            window.dispatchEvent(new CustomEvent('may:website', { detail: settings }));
            showToast('Đã lưu thiết kế website.', 'success');
          } catch (error) { showToast(error.message || 'Chưa lưu được thiết kế.', 'error'); }
          finally { save.disabled = false; }
        }
      });
      page.addEventListener('change', async event => {
        if (!event.target.matches('[data-wb-image-upload]')) return;
        const file = event.target.files?.[0];
        if (!file) return;
        try { settings = await settingsApi.uploadHomeImage(file); settings.website = state; renderWorkspace(); showToast('Đã cập nhật ảnh bìa.', 'success'); }
        catch (error) { showToast(error.message || 'Chưa tải được ảnh.', 'error'); }
      });
      let dragged = '';
      page.addEventListener('dragstart', event => { const row = event.target.closest('[data-section-key]'); if (row) { dragged = row.dataset.sectionKey; row.classList.add('is-dragging'); } });
      page.addEventListener('dragend', event => { event.target.closest('[data-section-key]')?.classList.remove('is-dragging'); dragged = ''; });
      page.addEventListener('dragover', event => { if (dragged && event.target.closest('[data-section-key]')) event.preventDefault(); });
      page.addEventListener('drop', event => { const row = event.target.closest('[data-section-key]'); if (!row || !dragged) return; event.preventDefault(); const target = row.dataset.sectionKey; const order = state.sectionOrder.filter(key => key !== dragged); order.splice(order.indexOf(target), 0, dragged); state.sectionOrder = order; renderWorkspace(); });
    },
    cleanup() { disposed = true; cleanupShell(); }
  };
}
