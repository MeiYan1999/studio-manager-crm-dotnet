import { api } from './api.js';
import { icon, escapeHtml, escapeAttr, createModal } from './ui.js';

const tones = ['#b9b39b', '#758f7c', '#a4b59c', '#b7a1af', '#c6afa0'];

export function mountConceptCards(holder) {
  const controller = new AbortController();
  let disposed = false;
  const load = async () => {
    holder.innerHTML = '<p class="concept-loading" role="status">Đang tải concept…</p>';
    try {
      const categories = await api('/api/public/concepts', { signal: controller.signal });
      if (disposed) return;
      holder.innerHTML = categories.map((item, index) => `
        <a class="concept-card ${item.coverUrl ? '' : 'concept-card--empty'}" href="/concepts/${encodeURIComponent(item.id)}" style="--concept-tone:${tones[index % tones.length]}">
          ${item.coverUrl
            ? `<img src="${escapeAttr(item.coverUrl)}" alt="${escapeAttr(item.title)}" loading="lazy" width="640" height="853">`
            : `<span class="concept-card__art" aria-hidden="true">${icon(index === 0 ? 'users' : index === 1 ? 'sparkles' : index === 4 ? 'camera' : 'heart')}</span>`}
          <div class="concept-card__copy"><span class="concept-card__label">Concept</span>
            <h3>${escapeHtml(item.title)}</h3><span class="concept-card__description">${escapeHtml(item.description)}</span>
            <span class="concept-card__link">${item.photoCount ? 'Xem chi tiết' : 'Bộ ảnh đang được cập nhật'} ${icon('arrowRight')}</span>
          </div>
        </a>`).join('');
      if (['#concepts', '#album-mau'].includes(location.hash)) holder.closest('section')?.scrollIntoView({ block: 'start', behavior: 'instant' });
    } catch (error) {
      if (disposed || error.name === 'AbortError') return;
      holder.innerHTML = '<p class="concept-loading">Chưa tải được concept. <button class="btn btn--ghost btn--compact" type="button" data-concept-retry>Thử lại</button></p>';
    }
  };
  const retry = event => { if (event.target.closest('[data-concept-retry]')) load(); };
  holder.addEventListener('click', retry);
  load();
  return () => { disposed = true; controller.abort(); holder.removeEventListener('click', retry); };
}

export function conceptGalleryView({ category, brand, studioName }) {
  const controller = new AbortController();
  let disposed = false;
  let modal = null;
  let photos = [];
  let teardown = () => {};
  return {
    title: 'Concept', bodyClass: 'page-landing',
    html: `<div class="landing"><header class="site-nav concept-nav">${brand}<a class="btn btn--ghost btn--compact concept-back" href="/#concepts">${icon('arrowLeft')} Tất cả concept</a></header>
      <main class="concept-gallery"><header class="concept-gallery__heading"><p class="eyebrow">Bộ sưu tập concept</p><h1 data-concept-title>Khám phá concept</h1><p data-concept-description></p></header>
      <div data-concept-gallery role="status">Đang tải bộ ảnh…</div></main></div>`,
    mount(root) {
      const holder = root.querySelector('[data-concept-gallery]');
      const openPhoto = index => {
        modal?.close();
        modal = createModal(`<div class="concept-lightbox"><button class="dialog-close" type="button" data-close-dialog aria-label="Đóng ảnh">${icon('x')}</button>
          <img data-concept-large alt=""><div class="concept-lightbox__controls"><button class="icon-btn" data-photo-previous aria-label="Ảnh trước">${icon('arrowLeft')}</button><span data-photo-counter aria-live="polite"></span><button class="icon-btn" data-photo-next aria-label="Ảnh tiếp theo">${icon('arrowRight')}</button></div></div>`, { className: 'dialog--concept' });
        modal.dialog.setAttribute('aria-label', 'Xem ảnh concept');
        const paint = () => {
          const photo = photos[index];
          const image = modal.dialog.querySelector('[data-concept-large]');
          image.src = photo.url; image.alt = photo.alt;
          modal.dialog.querySelector('[data-photo-counter]').textContent = `${index + 1} / ${photos.length}`;
          modal.dialog.querySelectorAll('[data-photo-previous], [data-photo-next]').forEach(button => { button.disabled = photos.length < 2; });
        };
        const move = delta => { index = (index + delta + photos.length) % photos.length; paint(); };
        modal.dialog.querySelector('[data-photo-previous]').addEventListener('click', () => move(-1));
        modal.dialog.querySelector('[data-photo-next]').addEventListener('click', () => move(1));
        modal.dialog.addEventListener('keydown', event => {
          if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') { event.preventDefault(); move(event.key === 'ArrowLeft' ? -1 : 1); }
        });
        paint();
      };
      const load = async () => {
        holder.innerHTML = '<p class="concept-loading" role="status">Đang tải bộ ảnh…</p>';
        try {
          const result = await api(`/api/public/concepts/${encodeURIComponent(category)}`, { signal: controller.signal });
          if (disposed) return;
          root.querySelector('[data-concept-title]').textContent = result.title;
          root.querySelector('[data-concept-description]').textContent = result.description;
          document.title = `${result.title} · ${studioName || 'StudioCore'}`;
          photos = [];
          holder.removeAttribute('role');
          holder.innerHTML = result.concepts.length ? result.concepts.map(concept => `<section class="concept-collection">
            <header><h2>${escapeHtml(concept.name)}</h2>${concept.description ? `<p>${escapeHtml(concept.description)}</p>` : ''}<span>${concept.photos.length} ảnh</span></header>
            <div class="concept-photo-grid">${concept.photos.map(photo => {
              const index = photos.push(photo) - 1;
              return `<button class="concept-photo" type="button" data-concept-photo="${index}" aria-label="Xem ${escapeAttr(photo.alt)}"><img src="${escapeAttr(photo.thumbnailUrl)}" alt="${escapeAttr(photo.alt)}" loading="lazy" width="640" height="800">${icon('zoomIn')}</button>`;
            }).join('')}</div></section>`).join('')
            : '<div class="concept-empty">' + icon('camera') + '<h2>Bộ ảnh đang được cập nhật</h2><p>Studio đang chuẩn bị những concept mới. Bạn hãy quay lại sau nhé.</p><a class="btn btn--ghost" href="/#concepts">Khám phá concept khác</a></div>';
        } catch (error) {
          if (disposed || error.name === 'AbortError') return;
          holder.innerHTML = `<div class="concept-empty"><h2>${error.status === 404 ? 'Không tìm thấy nhóm concept' : 'Chưa tải được bộ ảnh'}</h2><button class="btn btn--ghost" type="button" data-concept-retry>Thử lại</button><a class="btn btn--text" href="/#concepts">Về trang chủ</a></div>`;
        }
      };
      const click = event => {
        const photo = event.target.closest('[data-concept-photo]');
        if (photo) openPhoto(Number(photo.dataset.conceptPhoto));
        if (event.target.closest('[data-concept-retry]')) load();
      };
      holder.addEventListener('click', click);
      teardown = () => holder.removeEventListener('click', click);
      load();
    },
    cleanup() { disposed = true; controller.abort(); modal?.close(); teardown(); }
  };
}
