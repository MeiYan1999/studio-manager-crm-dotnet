import { api } from './api.js';
import {
  icon,
  escapeHtml,
  escapeAttr,
  stripDiacritics,
  formatDate,
  formatTimestamp,
  normalizeSearch,
  pluralPhotos,
  setButtonLoading,
  showToast,
  copyText
} from './ui.js';

const ACTIVE_BODY_CLASSES = ['drawer-open', 'lightbox-open'];

function photoRatio(photo) {
  if (!photo.width || !photo.height) return '';
  return `style="aspect-ratio:${photo.width}/${photo.height}"`;
}

function albumCover(album) {
  return album.coverUrl || album.photos?.[0]?.previewUrl || '';
}

function normalizeCodeSearch(value) {
  return stripDiacritics(normalizeSearch(value));
}

function publicWordmark(className = '') {
  return `<a class="wordmark ${className}" href="/" aria-label="Mây Album — trang chủ">mây<span>.</span><small>album</small></a>`;
}

function loadingView() {
  return `
    <div class="album-page album-page--loading">
      <header class="album-nav">
        ${publicWordmark()}
        <span class="album-nav__privacy">${icon('lock')} Album riêng tư</span>
        <span class="album-nav__loading">Đang mở album…</span>
      </header>
      <main class="album-loading">
        <div class="album-loading__hero">
          <div class="album-loading__copy">
            <div class="skeleton skeleton-line skeleton-line--xs"></div>
            <div class="skeleton skeleton-line skeleton-line--title"></div>
            <div class="skeleton skeleton-line skeleton-line--lg"></div>
            <div class="skeleton skeleton-line skeleton-line--sm"></div>
            <div class="skeleton skeleton-line skeleton-line--md"></div>
          </div>
          <div class="skeleton album-loading__cover"></div>
        </div>
        <div class="album-loading__grid">
          ${Array.from({ length: 6 }, (_, index) => `<div class="skeleton album-loading__photo" style="aspect-ratio:${index % 3 === 0 ? '4/5' : '4/3'}"></div>`).join('')}
        </div>
      </main>
    </div>
  `;
}

export function publicAlbumView({ token }) {
  let teardown = () => {};
  let disposed = false;

  return {
    title: 'Album',
    bodyClass: 'page-public',
    html: loadingView(),
    async mount(root) {
      try {
        const album = await api(`/api/public/albums/${encodeURIComponent(token)}`);
        if (disposed) return;
        root.innerHTML = albumPage(album);
        document.title = `${album.title} · ${album.studioName || 'Mây Album'}`;
        teardown = setupAlbum(root, album);
      } catch (error) {
        if (disposed || error.name === 'AbortError') return;
        root.innerHTML = publicErrorPage({
          title: 'Album chưa mở được',
          message: error.message || 'Album không tồn tại hoặc chưa được chia sẻ. Vui lòng xin liên kết mới từ studio.',
          code: error.code,
          action: 'home'
        });
        document.title = 'Album không khả dụng · Mây Album';
      }
    },
    cleanup() {
      disposed = true;
      teardown();
      ACTIVE_BODY_CLASSES.forEach(name => document.body.classList.remove(name));
    }
  };
}

export function notFoundAlbumView({ route, generic = false } = {}) {
  const message = generic
    ? 'Đường dẫn này không tồn tại hoặc đã được thay đổi. Có thể bạn đã gõ sai một ký tự trong liên kết.'
    : 'Album không tồn tại hoặc chưa được chia sẻ. Vui lòng liên hệ studio để nhận liên kết mới.';
  return {
    title: 'Không tìm thấy trang',
    bodyClass: 'page-public',
    html: publicErrorPage({
      title: generic ? 'Trang không tồn tại' : 'Album chưa mở được',
      message,
      code: generic ? 'not_found' : 'album_not_found',
      action: 'home',
      path: route?.path || ''
    })
  };
}

function publicErrorPage({ title, message, code = '', action = 'home', path = '' }) {
  return `
    <div class="album-page album-page--error">
      <header class="album-nav">
        ${publicWordmark()}
        <span class="album-nav__privacy">${icon('lock')} Album riêng tư</span>
        <a class="btn btn--ghost btn--compact" href="/">${icon('arrowLeft')} Trang chủ</a>
      </header>
      <main class="album-error">
        <div class="album-error__art" aria-hidden="true">
          <span class="album-error__frame">${icon('image')}</span>
          <span class="album-error__cloud"></span>
          <span class="album-error__star">${icon('sparkles')}</span>
        </div>
        <p class="eyebrow"><span class="eyebrow__dot"></span>${code === 'album_not_found' ? 'Liên kết riêng tư' : 'Mây Album'}</p>
        <h1>${escapeHtml(title)}</h1>
        <p class="album-error__message">${escapeHtml(message)}</p>
        ${path ? `<p class="album-error__path"><code>${escapeHtml(path)}</code></p>` : ''}
        <div class="album-error__actions">
          <a class="btn btn--primary" href="/">Về trang chủ ${icon('arrowRight')}</a>
          <a class="btn btn--ghost" href="/#album-mau">Xem album mẫu</a>
        </div>
        <p class="album-error__hint">${icon('mail')} Nếu bạn cho rằng đây là nhầm lẫn, hãy liên hệ trực tiếp studio đã gửi album.</p>
      </main>
      ${albumFooter({ studioName: 'Mây Studio', studioEmail: '', minimal: true })}
    </div>
  `;
}

function albumPage(album) {
  const photos = album.photos || [];
  const selectedCount = new Set(album.selectedPhotoIds || []).size;
  const cover = albumCover(album);
  const favorites = album.allowFavorites;
  const downloads = album.allowDownloads;
  const hasSelection = favorites;

  return `
    <div class="album-page">
      <header class="album-nav" data-album-nav>
        ${publicWordmark()}
        <p class="album-nav__privacy">${icon('lock')} <span>Album riêng tư</span></p>
        <div class="album-nav__actions">
          <span class="album-nav__studio">${escapeHtml(album.studioName || 'Mây Studio')}</span>
          ${hasSelection ? `
            <button class="btn btn--primary btn--compact album-nav__select" type="button" data-open-selection>
              ${icon('heart')}
              <span class="album-nav__select-label">Lựa chọn của bạn</span>
              <span class="album-nav__count" data-nav-count>${selectedCount}</span>
            </button>
          ` : `<span class="pill pill--viewonly">${icon('eye')} Chỉ để xem</span>`}
        </div>
      </header>

      <main>
        <section class="album-hero">
          <div class="album-hero__copy">
            <p class="eyebrow"><span class="eyebrow__dot"></span>Album ảnh riêng tư</p>
            <h1>${escapeHtml(album.title)}</h1>
            ${album.clientName ? `<p class="album-hero__client">Dành tặng ${escapeHtml(album.clientName)}</p>` : ''}
            <div class="album-hero__meta">
              ${album.eventDate ? `<span>${icon('calendar')} ${escapeHtml(formatDate(album.eventDate, true))}</span>` : ''}
              ${album.location ? `<span>${icon('mapPin')} ${escapeHtml(album.location)}</span>` : ''}
              <span>${icon('image')} ${pluralPhotos(photos.length)}</span>
            </div>
            ${album.description ? `<p class="album-hero__description">${escapeHtml(album.description)}</p>` : ''}
            <div class="album-hero__actions">
              ${hasSelection ? `
                <button class="btn btn--primary btn--lg" type="button" data-scroll-gallery>
                  ${icon('heart')} Bắt đầu chọn ảnh
                </button>
                <button class="btn btn--text btn--lg" type="button" data-open-selection>
                  Xem lựa chọn <span class="link-count">(<span data-hero-count>${selectedCount}</span>)</span>
                </button>
              ` : `
                <a class="btn btn--primary btn--lg" href="#gallery">${icon('eye')} Xem bộ ảnh</a>
                ${downloads ? `<a class="btn btn--text btn--lg" href="${escapeAttr(photos[0]?.downloadUrl || '#')}" target="_blank" rel="noopener">${icon('download')} Tải ảnh đầu tiên</a>` : ''}
              `}
            </div>
            <p class="album-hero__hint">
              ${hasSelection
                ? `${icon('heart')} Thả tim những ảnh bạn yêu. Gửi lại khi đã hài lòng.`
                : `${icon('lock')} Album chỉ để xem. Liên hệ studio nếu bạn cần thêm.`}
            </p>
          </div>
          <div class="album-hero__media">
            <div class="album-hero__arch">
              ${cover
                ? `<img src="${escapeAttr(cover)}" alt="Ảnh bìa album ${escapeAttr(album.title)}" fetchpriority="high">`
                : `<div class="cover-fallback cover-fallback--album">${icon('camera')}<span>Album ảnh</span></div>`}
            </div>
            <div class="album-hero__ticket">
              <span class="album-hero__ticket-icon">${icon('heart')}</span>
              <div>
                <strong data-ticket-count>${selectedCount}</strong>
                <span>khoảnh khắc<br>được yêu thích</span>
              </div>
            </div>
            <p class="album-hero__caption">
              <span>${escapeHtml(album.location || album.studioName || 'Mây Studio')}</span>
              <span>${album.eventDate ? escapeHtml(formatDate(album.eventDate)) : escapeHtml(album.studioName || '')}</span>
            </p>
          </div>
        </section>

        <div data-submission-slot>${submissionBanner(album, selectedCount)}</div>

        <section class="album-gallery-section" id="gallery">
          <div class="gallery-intro">
            <div>
              <p class="eyebrow">Bộ sưu tập</p>
              <h2>Những khoảnh khắc<br><em>của riêng bạn.</em></h2>
            </div>
            <div class="gallery-intro__aside">
              <p>${hasSelection
                ? 'Hãy chậm lại, xem từng tấm và giữ lại những gì khiến bạn mỉm cười.'
                : 'Một bộ ảnh được studio tuyển chọn và lưu giữ cho riêng bạn.'}</p>
              <span class="gallery-intro__note">${icon('search')} Tìm nhanh bằng mã hình in dưới mỗi ảnh</span>
            </div>
          </div>

          <div class="album-toolbar" data-toolbar>
            <div class="album-toolbar__inner">
              <div class="searchbox searchbox--album">
                <span class="searchbox__icon">${icon('search')}</span>
                <input type="search" data-album-search placeholder="Tìm theo mã hình…" aria-label="Tìm ảnh theo mã hình" autocomplete="off">
                <button class="searchbox__clear" type="button" data-album-search-clear aria-label="Xoá tìm kiếm" hidden>${icon('x')}</button>
              </div>
              ${hasSelection ? `
                <div class="segmented segmented--album" role="tablist" aria-label="Lọc ảnh">
                  <button class="segmented__item is-active" type="button" role="tab" aria-selected="true" data-album-filter="all">Tất cả <span>${photos.length}</span></button>
                  <button class="segmented__item" type="button" role="tab" aria-selected="false" data-album-filter="selected">${icon('heart')} Đã chọn <span data-filter-selected>${selectedCount}</span></button>
                </div>
                <button class="album-toolbar__selection" type="button" data-open-selection>
                  <span class="album-toolbar__selection-heart">${icon('heart')}</span>
                  <span><strong data-toolbar-count>${selectedCount}</strong> ảnh đã chọn</span>
                  ${icon('chevronRight')}
                </button>
              ` : ''}
            </div>
          </div>

          <div class="gallery-meta">
            <span><span data-visible-count>${pluralPhotos(photos.length)}</span></span>
            <span>${hasSelection ? `${icon('heart')} Nhấn vào ảnh để xem lớn, thả tim để chọn` : `${icon('zoomIn')} Nhấn vào ảnh để xem lớn`}</span>
          </div>

          <div class="album-gallery" data-gallery>
            ${photos.map((photo, index) => photoTile(photo, index, favorites, downloads)).join('')}
          </div>
          <div class="gallery-empty" data-gallery-empty hidden></div>
        </section>

        <section class="album-signoff">
          <span class="album-signoff__mark">${icon('camera')}</span>
          <p class="eyebrow">Từ ${escapeHtml(album.studioName || 'Mây Studio')}</p>
          <h2>Cảm ơn bạn đã<br><em>giữ lại cùng chúng tôi.</em></h2>
          <p>${hasSelection
            ? 'Khi bạn gửi lựa chọn, studio sẽ nhận được danh sách ảnh yêu thích và bắt đầu chuẩn bị cho bước tiếp theo.'
            : 'Nếu cần thêm ảnh hoặc một bản in, hãy nhắn cho studio qua email bên dưới.'}</p>
          ${album.studioEmail ? `<a class="text-link text-link--large" href="mailto:${escapeAttr(album.studioEmail)}">${icon('mail')} ${escapeHtml(album.studioEmail)}</a>` : ''}
        </section>
      </main>

      ${albumFooter({ studioName: album.studioName, studioEmail: album.studioEmail })}

      ${hasSelection ? `
        <button class="selection-fab" type="button" data-open-selection data-selection-fab hidden>
          <span class="selection-fab__heart">${icon('heart')}</span>
          <span><strong data-fab-count>${selectedCount}</strong> ảnh đã chọn</span>
          ${icon('arrowRight')}
        </button>
      ` : ''}

      ${hasSelection ? selectionDrawer(album, selectedCount) : ''}
      <div class="album-live" data-live aria-live="polite" aria-atomic="true"></div>
    </div>
  `;
}

function photoTile(photo, index, favorites, downloads) {
  const selected = Boolean(photo.isSelected);
  return `
    <figure class="photo-tile ${selected ? 'is-selected' : ''}" data-photo-id="${escapeAttr(photo.id)}" data-photo-index="${index}">
      <div class="photo-tile__media">
        <button class="photo-tile__open" type="button" data-open-photo aria-label="Xem ảnh ${escapeAttr(photo.code)}">
          <img src="${escapeAttr(photo.thumbUrl)}" alt="Ảnh ${escapeAttr(photo.code)}" loading="lazy" width="${photo.width || ''}" height="${photo.height || ''}" ${photoRatio(photo)}>
          <span class="photo-tile__zoom">${icon('zoomIn')}</span>
        </button>
        ${favorites ? `
          <button class="photo-tile__heart ${selected ? 'is-active' : ''}" type="button" data-favorite-id="${escapeAttr(photo.id)}" aria-label="${selected ? 'Bỏ chọn' : 'Chọn'} ảnh ${escapeAttr(photo.code)}" aria-pressed="${selected}">
            ${icon('heart')}
          </button>
        ` : ''}
        <span class="photo-tile__badge">${icon('check')} <span>Đã chọn</span></span>
      </div>
      <figcaption class="photo-tile__caption">
        <span class="photo-tile__code">${escapeHtml(photo.code)}</span>
        <span class="photo-tile__caption-end">
          ${downloads && photo.downloadUrl ? `<a class="photo-tile__download" href="${escapeAttr(photo.downloadUrl)}" target="_blank" rel="noopener" aria-label="Tải ảnh ${escapeAttr(photo.code)}" title="Tải ảnh gốc" download>${icon('download')}</a>` : ''}
          <span class="photo-tile__number">${String(index + 1).padStart(2, '0')}</span>
        </span>
      </figcaption>
    </figure>
  `;
}

function submissionBanner(album, selectedCount) {
  if (!album.submittedAt) return '';
  return `
    <section class="submitted-banner" data-submitted-banner>
      <span class="submitted-banner__icon">${icon('checkCircle')}</span>
      <div class="submitted-banner__copy">
        <p class="eyebrow">Lựa chọn đã được gửi</p>
        <h3>${pluralPhotos(selectedCount)} · ${escapeHtml(formatTimestamp(album.submittedAt))}</h3>
        ${album.clientNote ? `<blockquote>“${escapeHtml(album.clientNote)}”</blockquote>` : ''}
      </div>
      <button class="btn btn--cream btn--compact" type="button" data-open-selection>${icon('heart')} Xem lựa chọn</button>
    </section>
  `;
}

function selectionDrawer(album, selectedCount) {
  return `
    <div class="selection-drawer" data-drawer aria-hidden="true">
      <div class="selection-drawer__scrim" data-close-drawer></div>
      <aside class="selection-drawer__panel" role="dialog" aria-modal="true" aria-labelledby="selection-title" tabindex="-1">
        <button class="dialog-close selection-drawer__close" type="button" data-close-drawer aria-label="Đóng">${icon('x')}</button>
        <header class="selection-drawer__head">
          <p class="eyebrow">Album riêng tư</p>
          <h2 id="selection-title">Lựa chọn của bạn</h2>
          <p class="selection-drawer__lead"><span data-drawer-count>${selectedCount}</span> khoảnh khắc đang được giữ lại.</p>
        </header>

        <div class="selection-drawer__body" data-drawer-body>
          <div class="drawer-selection-list" data-drawer-list></div>
          <div class="drawer-empty" data-drawer-empty hidden>
            <span class="drawer-empty__icon">${icon('heart')}</span>
            <h3>Chưa có ảnh nào được chọn</h3>
            <p>Quay lại bộ ảnh và thả tim những tấm bạn yêu thích nhất.</p>
            <button class="btn btn--ghost btn--compact" type="button" data-close-drawer>Tiếp tục xem ảnh</button>
          </div>
          <div class="drawer-note">
            <div class="drawer-note__head">
              <label for="selection-note">Lời nhắn gửi studio <span>(không bắt buộc)</span></label>
              <span data-note-count>${(album.clientNote || '').length} / 600</span>
            </div>
            <textarea id="selection-note" data-note maxlength="600" rows="3" placeholder="Ví dụ: Tụi mình thích nhất những tấm có nắng chiều ạ…">${escapeHtml(album.clientNote || '')}</textarea>
          </div>
          <div data-drawer-error></div>
        </div>

        <footer class="selection-drawer__foot" data-drawer-foot>
          <p>${icon('lock')} Studio sẽ nhận được danh sách ngay sau khi bạn gửi.</p>
          <button class="btn btn--ghost btn--full" type="button" data-copy-selected-codes ${selectedCount ? '' : 'disabled'}>${icon('copy')} Sao chép mã ảnh đã chọn</button>
          <button class="btn btn--primary btn--lg btn--full" type="button" data-submit-selection>
            ${icon('send')} Gửi lựa chọn cho studio
          </button>
          <span class="selection-drawer__secondary">Bạn có thể quay lại chỉnh sửa sau khi gửi.</span>
        </footer>

        <div class="drawer-success" data-drawer-success hidden>
          <span class="drawer-success__icon">${icon('check')}</span>
          <p class="eyebrow">Đã gửi thành công</p>
          <h2>Cảm ơn bạn<br><em>rất nhiều.</em></h2>
          <p>Studio đã nhận được <strong data-success-count>0</strong> ảnh yêu thích của bạn.</p>
          <p class="drawer-success__time">${icon('clock')} <span data-success-time></span></p>
          <blockquote data-success-note hidden></blockquote>
          <div class="drawer-success__actions">
            <button class="btn btn--ghost btn--full" type="button" data-copy-selected-codes>${icon('copy')} Sao chép mã ảnh đã chọn</button>
            <button class="btn btn--primary btn--full" type="button" data-close-drawer>${icon('image')} Xem lại album</button>
            <button class="btn btn--ghost btn--full" type="button" data-edit-selection>Chỉnh sửa lựa chọn</button>
          </div>
        </div>
      </aside>
    </div>
  `;
}

function albumFooter({ studioName, studioEmail = '', minimal = false }) {
  return `
    <footer class="album-footer">
      <div class="album-footer__inner">
        ${publicWordmark('wordmark--light')}
        <p class="album-footer__note">${minimal ? 'Một sản phẩm của Mây Album.' : `Album được chia sẻ riêng cho khách hàng của ${escapeHtml(studioName || 'Mây Studio')}.`}</p>
        <div class="album-footer__links">
          ${studioEmail ? `<a href="mailto:${escapeAttr(studioEmail)}">${escapeHtml(studioEmail)}</a>` : ''}
          <span>${icon('lock')} Liên kết riêng tư</span>
        </div>
      </div>
      <div class="album-footer__bottom">
        <span>© ${new Date().getFullYear()} ${escapeHtml(studioName || 'Mây Studio')}</span>
        <span>Made with ${icon('heart')} by Mây Album</span>
      </div>
    </footer>
  `;
}

// ---------------------------------------------------------------- interactions

function setupAlbum(root, album) {
  const photos = album.photos || [];
  const photoById = new Map(photos.map(photo => [photo.id, photo]));
  const gallery = root.querySelector('[data-gallery]');
  const drawer = root.querySelector('[data-drawer]');
  const drawerPanel = drawer?.querySelector('.selection-drawer__panel');
  const drawerList = root.querySelector('[data-drawer-list]');
  const drawerEmpty = root.querySelector('[data-drawer-empty]');
  const drawerBody = root.querySelector('[data-drawer-body]');
  const drawerFoot = root.querySelector('[data-drawer-foot]');
  const drawerSuccess = root.querySelector('[data-drawer-success]');
  const noteField = root.querySelector('[data-note]');
  const noteCount = root.querySelector('[data-note-count]');
  const submitButton = root.querySelector('[data-submit-selection]');
  const drawerError = root.querySelector('[data-drawer-error]');
  const search = root.querySelector('[data-album-search]');
  const clearSearchButton = root.querySelector('[data-album-search-clear]');
  const live = root.querySelector('[data-live]');
  const bannerSlot = root.querySelector('[data-submission-slot]');
  const favorites = Boolean(album.allowFavorites);

  const state = {
    selected: new Set(album.selectedPhotoIds || []),
    filter: 'all',
    query: '',
    note: album.clientNote || '',
    submittedAt: album.submittedAt || null,
    submittedCount: album.selectedCount ?? new Set(album.selectedPhotoIds || []).size,
    dirty: false,
    submitting: false,
    busy: new Set(),
    drawerOpen: false,
    success: false,
    lightbox: null
  };

  let lastDrawerTrigger = null;
  let lastLightboxTrigger = null;
  let active = true;

  const selectedPhotos = () => photos.filter(photo => state.selected.has(photo.id));

  const visiblePhotos = () => {
    const needle = normalizeCodeSearch(state.query);
    return photos.filter(photo => {
      if (state.filter === 'selected' && !state.selected.has(photo.id)) return false;
      if (!needle) return true;
      return normalizeCodeSearch(`${photo.code} ${photo.fileName}`).includes(needle);
    });
  };

  const announce = message => {
    if (!live) return;
    live.textContent = '';
    window.setTimeout(() => {
      if (active) live.textContent = message;
    }, 30);
  };

  const setCount = (selector, value) => {
    root.querySelectorAll(selector).forEach(element => {
      element.textContent = numberLabel(value);
    });
  };

  const updateBanner = () => {
    if (!bannerSlot) return;
    if (!state.submittedAt) {
      bannerSlot.innerHTML = '';
      return;
    }

    const count = state.dirty ? state.selected.size : state.submittedCount;
    bannerSlot.innerHTML = `
      <section class="submitted-banner ${state.dirty ? 'submitted-banner--dirty' : ''}" data-submitted-banner>
        <span class="submitted-banner__icon">${icon(state.dirty ? 'alertCircle' : 'checkCircle')}</span>
        <div class="submitted-banner__copy">
          <p class="eyebrow">${state.dirty ? 'Có thay đổi chưa gửi lại' : 'Lựa chọn đã được gửi'}</p>
          <h3>${pluralPhotos(count)}${state.dirty ? ' · đang chờ cập nhật' : ` · ${escapeHtml(formatTimestamp(state.submittedAt))}`}</h3>
          ${state.dirty
            ? '<p>Bạn vừa thay đổi lựa chọn. Hãy gửi lại để studio nhận được ghi chú và thời điểm mới nhất.</p>'
            : state.note ? `<blockquote>“${escapeHtml(state.note)}”</blockquote>` : ''}
        </div>
        <button class="btn ${state.dirty ? 'btn--primary' : 'btn--cream'} btn--compact" type="button" data-open-selection>
          ${icon(state.dirty ? 'send' : 'heart')} ${state.dirty ? 'Gửi lại lựa chọn' : 'Xem lựa chọn'}
        </button>
      </section>
    `;
  };

  const updateTileStates = () => {
    gallery.querySelectorAll('[data-photo-id]').forEach(tile => {
      const id = tile.dataset.photoId;
      const isSelected = state.selected.has(id);
      tile.classList.toggle('is-selected', isSelected);
      const heart = tile.querySelector('[data-favorite-id]');
      if (heart) {
        heart.classList.toggle('is-active', isSelected);
        heart.setAttribute('aria-pressed', String(isSelected));
        heart.setAttribute('aria-label', `${isSelected ? 'Bỏ chọn' : 'Chọn'} ảnh ${tile.querySelector('.photo-tile__code')?.textContent || ''}`);
        heart.disabled = state.busy.has(id);
      }
    });
  };

  const applyFilter = () => {
    const visible = visiblePhotos();
    const ids = new Set(visible.map(photo => photo.id));
    gallery.querySelectorAll('[data-photo-id]').forEach(tile => {
      tile.hidden = !ids.has(tile.dataset.photoId);
    });

    root.querySelectorAll('[data-album-filter]').forEach(button => {
      const isActive = button.dataset.albumFilter === state.filter;
      button.classList.toggle('is-active', isActive);
      button.setAttribute('aria-selected', String(isActive));
    });

    const scopeCount = state.filter === 'selected' ? state.selected.size : photos.length;
    const scopeLabel = state.filter === 'selected' ? 'ảnh đã chọn' : 'ảnh';
    root.querySelectorAll('[data-visible-count]').forEach(element => {
      element.textContent = visible.length === scopeCount
        ? (state.filter === 'selected' ? `${numberLabel(scopeCount)} ${scopeLabel}` : pluralPhotos(scopeCount))
        : `${numberLabel(visible.length)} / ${numberLabel(scopeCount)} ${scopeLabel}`;
    });

    if (clearSearchButton) clearSearchButton.hidden = !state.query.length;

    const empty = root.querySelector('[data-gallery-empty]');
    if (!empty) return;
    if (visible.length) {
      empty.hidden = true;
      empty.innerHTML = '';
      return;
    }

    empty.hidden = false;
    empty.innerHTML = `
      <span class="gallery-empty__icon">${icon(state.filter === 'selected' && !state.query ? 'heart' : 'search')}</span>
      <h3>${state.filter === 'selected' && !state.query ? 'Bạn chưa chọn ảnh nào' : 'Không tìm thấy ảnh phù hợp'}</h3>
      <p>${state.query
        ? `Không có ảnh nào khớp với mã “${escapeHtml(state.query)}”. Thử một mã khác nhé.`
        : 'Hãy thả tim những ảnh bạn yêu thích, chúng sẽ xuất hiện ở đây.'}</p>
      <button class="btn btn--ghost btn--compact" type="button" data-clear-album-filter>Xem tất cả ảnh</button>
    `;
  };

  const renderDrawerList = () => {
    if (!drawerList) return;
    const chosen = selectedPhotos();
    root.querySelectorAll('[data-copy-selected-codes]').forEach(button => {
      button.disabled = chosen.length === 0 || state.busy.size > 0;
    });
    drawerList.innerHTML = chosen.map(photo => `
      <article class="drawer-item">
        <img src="${escapeAttr(photo.thumbUrl)}" alt="${escapeAttr(photo.code)}" loading="lazy">
        <div class="drawer-item__copy">
          <strong>${escapeHtml(photo.code)}</strong>
          <span>${escapeHtml(photo.fileName || 'Ảnh đã chọn')}</span>
        </div>
        <button class="icon-btn drawer-item__remove" type="button" data-remove-selection="${escapeAttr(photo.id)}" aria-label="Bỏ chọn ảnh ${escapeAttr(photo.code)}" ${state.busy.has(photo.id) ? 'disabled' : ''}>${icon('x')}</button>
      </article>
    `).join('');

    drawerEmpty.hidden = chosen.length > 0;
    drawerList.hidden = chosen.length === 0;
    setCount('[data-drawer-count]', chosen.length);
    setCount('[data-selected-count]', chosen.length);
    setCount('[data-nav-count]', chosen.length);
    setCount('[data-hero-count]', chosen.length);
    setCount('[data-toolbar-count]', chosen.length);
    setCount('[data-ticket-count]', chosen.length);
    setCount('[data-filter-selected]', chosen.length);
    setCount('[data-fab-count]', chosen.length);
    setCount('[data-success-count]', chosen.length);

    const fab = root.querySelector('[data-selection-fab]');
    if (fab) fab.hidden = chosen.length === 0 || Boolean(state.submittedAt);
    if (submitButton) {
      submitButton.disabled = chosen.length === 0 || state.submitting || state.busy.size > 0;
    }
    const noteWrap = root.querySelector('.drawer-note');
    if (noteWrap) noteWrap.hidden = chosen.length === 0;
  };

  const syncLightboxFavorite = () => {
    const lightbox = state.lightbox;
    if (!lightbox) return;
    const photo = visiblePhotos()[lightbox.index];
    if (!photo) return;
    const heartButton = lightbox.element.querySelector('[data-lightbox-favorite]');
    if (!heartButton) return;
    const isSelected = state.selected.has(photo.id);
    heartButton.classList.toggle('is-active', isSelected);
    heartButton.setAttribute('aria-pressed', String(isSelected));
    heartButton.disabled = state.busy.has(photo.id);
    heartButton.innerHTML = `${icon('heart')} <span>${isSelected ? 'Đã chọn' : 'Chọn ảnh này'}</span>`;
  };

  const updateUi = () => {
    updateTileStates();
    applyFilter();
    renderDrawerList();
    updateBanner();
    syncLightboxFavorite();
  };

  // ------------------------------------------------------------ drawer

  const openDrawer = trigger => {
    if (!drawer || !favorites) return;
    if (state.lightbox) closeLightbox();
    lastDrawerTrigger = trigger instanceof HTMLElement ? trigger : document.activeElement;
    state.drawerOpen = true;
    state.success = false;
    drawerPanel?.classList.remove('is-success');
    if (drawerSuccess) drawerSuccess.hidden = true;
    if (drawerBody) drawerBody.hidden = false;
    if (drawerFoot) drawerFoot.hidden = false;
    if (drawerError) drawerError.innerHTML = '';
    renderDrawerList();
    drawer.classList.add('is-open');
    drawer.setAttribute('aria-hidden', 'false');
    document.body.classList.add('drawer-open');
    window.requestAnimationFrame(() => drawerPanel?.focus());
  };

  const closeDrawer = () => {
    if (!drawer || !state.drawerOpen) return;
    state.drawerOpen = false;
    drawer.classList.remove('is-open');
    drawer.setAttribute('aria-hidden', 'true');
    document.body.classList.remove('drawer-open');
    if (lastDrawerTrigger instanceof HTMLElement && lastDrawerTrigger.isConnected) {
      lastDrawerTrigger.focus({ preventScroll: true });
    }
  };

  const showDrawerSuccess = result => {
    state.success = true;
    drawerPanel?.classList.add('is-success');
    if (drawerBody) drawerBody.hidden = true;
    if (drawerFoot) drawerFoot.hidden = true;
    if (drawerSuccess) drawerSuccess.hidden = false;
    const time = drawerSuccess?.querySelector('[data-success-time]');
    if (time) time.textContent = formatTimestamp(result.submittedAt);
    const successCount = drawerSuccess?.querySelector('[data-success-count]');
    if (successCount) successCount.textContent = numberLabel(result.selectedCount);
    const note = drawerSuccess?.querySelector('[data-success-note]');
    if (note) {
      note.hidden = !result.clientNote;
      note.textContent = result.clientNote ? `“${result.clientNote}”` : '';
    }
    const fab = root.querySelector('[data-selection-fab]');
    if (fab) fab.hidden = true;
    drawerSuccess?.querySelector('[data-close-drawer]')?.focus();
  };

  const setDrawerError = message => {
    if (!drawerError) return;
    drawerError.innerHTML = message
      ? `<div class="inline-alert inline-alert--error" role="alert">${icon('alertCircle')}<p>${escapeHtml(message)}</p></div>`
      : '';
  };

  const submitSelection = async () => {
    if (!favorites || state.submitting || state.selected.size === 0) return;
    state.submitting = true;
    setDrawerError('');
    setButtonLoading(submitButton, true, 'Đang gửi lựa chọn…');
    renderDrawerList();
    try {
      const result = await api(`/api/public/albums/${encodeURIComponent(album.shareToken)}/submit`, {
        method: 'POST',
        body: { note: state.note.trim() }
      });
      if (!active) return;
      state.submittedAt = result.submittedAt;
      state.submittedCount = result.selectedCount;
      state.note = result.clientNote || '';
      state.dirty = false;
      state.submitting = false;
      setButtonLoading(submitButton, false);
      showDrawerSuccess(result);
      updateBanner();
      announce('Đã gửi lựa chọn cho studio.');
      showToast('Studio đã nhận được lựa chọn của bạn.', 'success');
    } catch (error) {
      if (!active || error.name === 'AbortError') return;
      state.submitting = false;
      setButtonLoading(submitButton, false);
      setDrawerError(error.message || 'Chưa gửi được lựa chọn. Vui lòng thử lại.');
      renderDrawerList();
    }
  };

  // ------------------------------------------------------------ favorites

  const toggleFavorite = async photoId => {
    if (!favorites || state.busy.has(photoId)) return;
    const photo = photoById.get(photoId);
    if (!photo) return;
    const wasSelected = state.selected.has(photoId);
    state.busy.add(photoId);
    if (wasSelected) state.selected.delete(photoId);
    else state.selected.add(photoId);
    updateUi();
    const tile = gallery.querySelector(`[data-photo-id="${CSS.escape(photoId)}"]`);
    tile?.classList.add('is-pulsing');
    window.setTimeout(() => tile?.classList.remove('is-pulsing'), 420);

    try {
      await api(`/api/public/albums/${encodeURIComponent(album.shareToken)}/favorites/toggle`, {
        method: 'POST',
        body: { photoId }
      });
      if (!active) return;
      if (state.submittedAt && !state.dirty) {
        state.dirty = true;
        updateBanner();
      }
      announce(`${wasSelected ? 'Đã bỏ chọn' : 'Đã chọn'} ảnh ${photo.code}. ${state.selected.size} ảnh trong lựa chọn.`);
    } catch (error) {
      if (!active || error.name === 'AbortError') return;
      if (wasSelected) state.selected.add(photoId);
      else state.selected.delete(photoId);
      showToast(error.message || 'Chưa cập nhật được lựa chọn.', 'error');
    } finally {
      state.busy.delete(photoId);
      if (active) updateUi();
    }
  };

  // ------------------------------------------------------------ lightbox

  const updateLightbox = () => {
    const lightbox = state.lightbox;
    if (!lightbox) return;
    const list = visiblePhotos();
    if (!list.length) {
      closeLightbox();
      return;
    }
    if (lightbox.index >= list.length) lightbox.index = 0;
    const photo = list[lightbox.index];
    if (!photo) return;
    lightbox.list = list;

    const image = lightbox.element.querySelector('[data-lightbox-image]');
    const wrap = lightbox.element.querySelector('[data-lightbox-media]');
    if (image && image.dataset.currentId !== photo.id) {
      image.dataset.currentId = photo.id;
      wrap?.classList.add('is-loading');
      image.classList.remove('is-ready');
      image.src = photo.previewUrl || photo.thumbUrl;
      image.alt = `Ảnh ${photo.code}`;
      if (image.complete) {
        wrap?.classList.remove('is-loading');
        image.classList.add('is-ready');
      }
    }

    const counter = lightbox.element.querySelector('[data-lightbox-counter]');
    if (counter) counter.textContent = `${String(lightbox.index + 1).padStart(2, '0')} / ${String(list.length).padStart(2, '0')}`;
    const code = lightbox.element.querySelector('[data-lightbox-code]');
    if (code) code.textContent = photo.code;
    const meta = lightbox.element.querySelector('[data-lightbox-meta]');
    if (meta) {
      meta.textContent = [
        photo.width && photo.height ? `${numberLabel(photo.width)} × ${numberLabel(photo.height)} px` : '',
        photo.fileName || ''
      ].filter(Boolean).join(' · ');
    }

    const download = lightbox.element.querySelector('[data-lightbox-download]');
    if (download) {
      const allowed = Boolean(album.allowDownloads && photo.downloadUrl);
      download.hidden = !allowed;
      if (allowed) download.href = photo.downloadUrl;
    }

    const previousButton = lightbox.element.querySelector('[data-lightbox-prev]');
    const nextButton = lightbox.element.querySelector('[data-lightbox-next]');
    if (previousButton) previousButton.disabled = list.length < 2;
    if (nextButton) nextButton.disabled = list.length < 2;
    syncLightboxFavorite();

    const nextPhoto = list[(lightbox.index + 1) % list.length];
    if (nextPhoto && list.length > 1) {
      const preload = new Image();
      preload.src = nextPhoto.previewUrl || nextPhoto.thumbUrl;
    }
  };

  const closeLightbox = () => {
    const lightbox = state.lightbox;
    if (!lightbox) return;
    state.lightbox = null;
    document.removeEventListener('keydown', lightbox.onKeydown);
    lightbox.element.classList.remove('is-open');
    document.body.classList.remove('lightbox-open');
    window.setTimeout(() => lightbox.element.remove(), 220);
    if (lastLightboxTrigger instanceof HTMLElement && lastLightboxTrigger.isConnected) {
      lastLightboxTrigger.focus({ preventScroll: true });
    }
  };

  const moveLightbox = delta => {
    const lightbox = state.lightbox;
    if (!lightbox) return;
    const list = visiblePhotos();
    if (list.length < 2) return;
    lightbox.index = (lightbox.index + delta + list.length) % list.length;
    updateLightbox();
  };

  const openLightbox = (startPhotoId, trigger) => {
    if (state.drawerOpen) closeDrawer();
    const list = visiblePhotos();
    if (!list.length) return;
    let index = list.findIndex(photo => photo.id === startPhotoId);
    if (index < 0) index = 0;

    const element = document.createElement('div');
    element.className = 'album-lightbox';
    element.setAttribute('role', 'dialog');
    element.setAttribute('aria-modal', 'true');
    element.setAttribute('aria-label', 'Xem ảnh lớn');
    element.innerHTML = `
      <div class="album-lightbox__top">
        ${publicWordmark('wordmark--light wordmark--small')}
        <span class="album-lightbox__counter" data-lightbox-counter></span>
        <button class="album-lightbox__close" type="button" data-close-lightbox aria-label="Đóng">${icon('x')}</button>
      </div>
      <div class="album-lightbox__stage">
        <button class="album-lightbox__nav album-lightbox__nav--prev" type="button" data-lightbox-prev aria-label="Ảnh trước">${icon('arrowLeft')}</button>
        <div class="album-lightbox__media" data-lightbox-media>
          <span class="album-lightbox__loader">${icon('loader', 'spin')}</span>
          <img data-lightbox-image src="" alt="">
        </div>
        <button class="album-lightbox__nav album-lightbox__nav--next" type="button" data-lightbox-next aria-label="Ảnh sau">${icon('arrowRight')}</button>
      </div>
      <div class="album-lightbox__bottom">
        <div class="album-lightbox__caption">
          <p class="eyebrow">Mã hình</p>
          <h2 data-lightbox-code></h2>
          <p data-lightbox-meta></p>
        </div>
        <div class="album-lightbox__actions">
          ${favorites ? `
            <button class="btn btn--ghost-light btn--compact" type="button" data-lightbox-favorite aria-pressed="false">${icon('heart')} <span>Chọn ảnh này</span></button>
          ` : ''}
          <a class="btn btn--cream btn--compact" data-lightbox-download href="#" target="_blank" rel="noopener">${icon('download')} Tải ảnh gốc</a>
        </div>
      </div>
    `;

    document.body.append(element);
    document.body.classList.add('lightbox-open');
    lastLightboxTrigger = trigger instanceof HTMLElement ? trigger : document.activeElement;

    const image = element.querySelector('[data-lightbox-image]');
    const wrap = element.querySelector('[data-lightbox-media]');
    image.addEventListener('load', () => {
      wrap.classList.remove('is-loading');
      image.classList.add('is-ready');
    });
    image.addEventListener('error', () => {
      wrap.classList.remove('is-loading');
    });

    const onKeydown = event => {
      if (event.key === 'Escape') {
        event.preventDefault();
        closeLightbox();
      } else if (event.key === 'ArrowLeft') {
        event.preventDefault();
        moveLightbox(-1);
      } else if (event.key === 'ArrowRight') {
        event.preventDefault();
        moveLightbox(1);
      }
    };

    const lightbox = {
      element,
      index,
      list,
      onKeydown,
      close: closeLightbox
    };
    state.lightbox = lightbox;

    element.querySelector('[data-close-lightbox]')?.addEventListener('click', closeLightbox);
    element.querySelector('[data-lightbox-prev]')?.addEventListener('click', () => moveLightbox(-1));
    element.querySelector('[data-lightbox-next]')?.addEventListener('click', () => moveLightbox(1));
    element.querySelector('[data-lightbox-favorite]')?.addEventListener('click', () => {
      const photo = visiblePhotos()[lightbox.index];
      if (photo) toggleFavorite(photo.id);
    });
    element.addEventListener('click', event => {
      if (event.target === element) closeLightbox();
    });
    document.addEventListener('keydown', onKeydown);
    updateLightbox();
    window.requestAnimationFrame(() => element.classList.add('is-open'));
    element.querySelector('[data-close-lightbox]')?.focus({ preventScroll: true });
  };

  // ------------------------------------------------------------ events

  const onClick = event => {
    const copyButton = event.target.closest('[data-copy-selected-codes]');
    if (copyButton) {
      const codes = selectedPhotos()
        .map(photo => photo.code)
        .sort((a, b) => a.localeCompare(b, 'vi', { numeric: true }));
      if (codes.length) {
        copyText(codes.join(',')).then(ok => {
          showToast(ok ? `Đã sao chép ${codes.length} mã ảnh.` : 'Không thể sao chép mã ảnh. Hãy thử lại.', ok ? 'success' : 'error');
        });
      }
      return;
    }
    const openSelection = event.target.closest('[data-open-selection]');
    if (openSelection) {
      openDrawer(openSelection);
      return;
    }

    if (event.target.closest('[data-close-drawer]')) {
      closeDrawer();
      return;
    }

    if (event.target.closest('[data-edit-selection]')) {
      state.success = false;
      drawerPanel?.classList.remove('is-success');
      if (drawerSuccess) drawerSuccess.hidden = true;
      if (drawerBody) drawerBody.hidden = false;
      if (drawerFoot) drawerFoot.hidden = false;
      renderDrawerList();
      drawerBody?.scrollIntoView({ block: 'start' });
      return;
    }

    if (event.target.closest('[data-submit-selection]')) {
      submitSelection();
      return;
    }

    if (event.target.closest('[data-scroll-gallery]')) {
      document.getElementById('gallery')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
      return;
    }

    const removeButton = event.target.closest('[data-remove-selection]');
    if (removeButton) {
      toggleFavorite(removeButton.dataset.removeSelection);
      return;
    }

    const heart = event.target.closest('[data-favorite-id]');
    if (heart) {
      toggleFavorite(heart.dataset.favoriteId);
      return;
    }

    const filterButton = event.target.closest('[data-album-filter]');
    if (filterButton) {
      state.filter = filterButton.dataset.albumFilter;
      applyFilter();
      return;
    }

    if (event.target.closest('[data-clear-album-filter]')) {
      state.filter = 'all';
      state.query = '';
      if (search) search.value = '';
      applyFilter();
      return;
    }

    if (event.target.closest('[data-album-search-clear]')) {
      state.query = '';
      if (search) {
        search.value = '';
        search.focus();
      }
      applyFilter();
      return;
    }

    const openPhoto = event.target.closest('[data-open-photo]');
    if (openPhoto) {
      const tile = openPhoto.closest('[data-photo-id]');
      if (tile) openLightbox(tile.dataset.photoId, openPhoto);
      return;
    }
  };

  const onInput = event => {
    if (event.target === search) {
      state.query = event.target.value;
      applyFilter();
      return;
    }

    if (event.target === noteField) {
      state.note = event.target.value;
      if (noteCount) noteCount.textContent = `${event.target.value.length} / 600`;
    }
  };

  const onKeydown = event => {
    if (event.key !== 'Escape') return;
    if (state.lightbox) return;
    if (state.drawerOpen) {
      event.preventDefault();
      closeDrawer();
    }
  };

  root.addEventListener('click', onClick);
  root.addEventListener('input', onInput);
  document.addEventListener('keydown', onKeydown);
  updateUi();

  return () => {
    active = false;
    root.removeEventListener('click', onClick);
    root.removeEventListener('input', onInput);
    document.removeEventListener('keydown', onKeydown);
    if (state.lightbox) {
      document.removeEventListener('keydown', state.lightbox.onKeydown);
      state.lightbox.element.remove();
      state.lightbox = null;
    }
    ACTIVE_BODY_CLASSES.forEach(name => document.body.classList.remove(name));
    document.title = 'Mây Album';
  };
}

function numberLabel(value) {
  return Number(value || 0).toLocaleString('vi-VN');
}

