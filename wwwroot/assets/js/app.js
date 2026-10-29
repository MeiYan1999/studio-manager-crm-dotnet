import { api, authApi } from './api.js';
import {
  PLACEHOLDER_IMAGE,
  icon,
  escapeHtml,
  escapeAttr,
  showToast
} from './ui.js';
import {
  loginView,
  dashboardView,
  albumsView,
  createView,
  albumDetailView,
  settingsView,
  changePasswordView,
  hasPermission
} from './admin.js';
import {
  customersView,
  packagesView,
  staffView,
  bookingsView,
  auditView
} from './crm.js';
import { ordersView, contractsView, reportsView } from './sales.js';
import { financeView, attendanceView, shiftsView, wardrobeView, notificationsView } from './operations.js';
import { boardView } from './board.js';
import { photoFilterView } from './tools.js';
import { publicAlbumView, notFoundAlbumView } from './album.js';

const APP_TITLE = 'Mây Album';
let websiteSettings = { studioName: 'Mây Studio', website: {} };
window.addEventListener('may:website', event => { websiteSettings = event.detail; });

const session = {
  authenticated: false,
  studioName: 'Mây Studio',
  user: null
};

let currentView = null;
let renderTicket = 0;

function normalizePath(pathname) {
  const path = pathname.replace(/\/+$/, '');
  return path || '/';
}

function parseRoute(pathname) {
  const path = normalizePath(pathname);

  if (path === '/') return { name: 'home', path };
  if (path === '/admin/login') return { name: 'login', path };
  if (path === '/admin' || path === '/admin/dashboard') return { name: 'dashboard', path: '/admin' };
  if (path === '/admin/albums') return { name: 'albums', path };
  if (path === '/admin/board') return { name: 'board', path };
  if (path === '/admin/create') return { name: 'create', path };
  if (path === '/admin/tools') return { name: 'tools', path };
  if (path === '/admin/customers') return { name: 'customers', path };
  if (path === '/admin/packages') return { name: 'packages', path };
  if (path === '/admin/staff') return { name: 'staff', path };
  if (path === '/admin/bookings') return { name: 'bookings', path };
  if (path === '/admin/orders') return { name: 'orders', path };
  if (path === '/admin/contracts') return { name: 'contracts', path };
  if (path === '/admin/reports') return { name: 'reports', path };
  if (path === '/admin/finance') return { name: 'finance', path };
  if (path === '/admin/attendance') return { name: 'attendance', path };
  if (path === '/admin/shifts') return { name: 'shifts', path };
  if (path === '/admin/wardrobe') return { name: 'wardrobe', path };
  if (path === '/admin/notifications') return { name: 'notifications', path };
  if (path === '/admin/audit') return { name: 'audit', path };
  if (path === '/admin/settings') return { name: 'settings', path };
  if (path === '/admin/change-password') return { name: 'change-password', path };

  const albumMatch = /^\/admin\/albums\/([^/]+)$/.exec(path);
  if (albumMatch) {
    return { name: 'album-detail', path, params: { id: decodeURIComponent(albumMatch[1]) } };
  }

  const publicMatch = /^\/a\/([^/]+)$/.exec(path);
  if (publicMatch) {
    return { name: 'public-album', path, params: { token: decodeURIComponent(publicMatch[1]) } };
  }

  return { name: 'not-found', path };
}

const ROUTE_PERMISSIONS = {
  dashboard: 'dashboard',
  board: 'board',
  albums: 'albums',
  create: 'albums',
  'album-detail': 'albums',
  tools: 'tools',
  customers: 'customers',
  packages: 'packages',
  staff: 'staff',
  bookings: 'bookings',
  orders: 'orders',
  contracts: 'contracts',
  reports: 'reports',
  finance: 'finance',
  attendance: 'attendance',
  shifts: 'attendance',
  wardrobe: 'wardrobe',
  notifications: 'notifications',
  audit: 'audit',
  settings: 'settings'
};

const PERMISSION_ROUTES = [
  { permission: 'dashboard', path: '/admin', label: 'Tổng quan', iconName: 'home' },
  { permission: 'board', path: '/admin/board', label: 'Bảng công việc', iconName: 'board' },
  { permission: 'albums', path: '/admin/albums', label: 'Album của bạn', iconName: 'albums' },
  { permission: 'tools', path: '/admin/tools', label: 'Lọc ảnh theo mã', iconName: 'filter' },
  { permission: 'customers', path: '/admin/customers', label: 'Khách hàng', iconName: 'users' },
  { permission: 'bookings', path: '/admin/bookings', label: 'Lịch chụp', iconName: 'calendar' },
  { permission: 'packages', path: '/admin/packages', label: 'Gói dịch vụ', iconName: 'tag' },
  { permission: 'staff', path: '/admin/staff', label: 'Nhân sự', iconName: 'camera' },
  { permission: 'orders', path: '/admin/orders', label: 'Đơn hàng', iconName: 'receipt' },
  { permission: 'contracts', path: '/admin/contracts', label: 'Hợp đồng', iconName: 'file' },
  { permission: 'reports', path: '/admin/reports', label: 'Báo cáo', iconName: 'chart' },
  { permission: 'finance', path: '/admin/finance', label: 'Tài chính & lương', iconName: 'wallet' },
  { permission: 'attendance', path: '/admin/attendance', label: 'Chấm công', iconName: 'clock' },
  { permission: 'wardrobe', path: '/admin/wardrobe', label: 'Tủ trang phục', iconName: 'tag' },
  { permission: 'notifications', path: '/admin/notifications', label: 'Thông báo', iconName: 'bell' },
  { permission: 'audit', path: '/admin/audit', label: 'Nhật ký studio', iconName: 'history' },
  { permission: 'settings', path: '/admin/settings', label: 'Cài đặt studio', iconName: 'settings' }
];

function isAdminRoute(route) {
  return Boolean(ROUTE_PERMISSIONS[route.name]) || route.name === 'change-password';
}

function safeNext(value) {
  if (!value || !value.startsWith('/admin') || value.startsWith('/admin/login')) return '/admin';
  if (value.startsWith('/admin/change-password')) return '/admin';
  return value;
}

function noAccessView({ session, route }) {
  const links = PERMISSION_ROUTES
    .filter(item => hasPermission(session, item.permission))
    .slice(0, 6)
    .map(item => `<a class="btn btn--ghost btn--compact" href="${item.path}">${icon(item.iconName)} ${item.label}</a>`)
    .join('');
  return {
    title: 'Chưa được cấp quyền',
    bodyClass: 'page-admin page-no-access',
    html: `
      <div class="fatal-page">
        <a class="wordmark" href="/">mây<span>.</span><small>album</small></a>
        <div class="fatal-page__card no-access-card">
          <span class="fatal-page__code">403</span>
          <h1>Trang này chưa được cấp cho bạn.</h1>
          <p>${session.user?.displayName ? `Chào ${session.user.displayName}, ` : ''}tài khoản của bạn không có quyền mở <code>${route.path.replaceAll('<', '&lt;')}</code>. Liên hệ quản trị viên studio nếu cần bổ sung quyền.</p>
          <div class="no-access-card__links">
            ${links || '<a class="btn btn--primary" href="/admin/change-password">Đổi mật khẩu</a>'}
          </div>
        </div>
      </div>
    `
  };
}

async function render(route) {
  const ticket = ++renderTicket;
  const app = document.getElementById('app');
  if (!app) return;

  if (currentView?.cleanup) {
    try {
      currentView.cleanup();
    } catch {
      // A view must never break navigation.
    }
  }
  currentView = null;
  document.body.classList.remove('nav-open', 'drawer-open');

  if (isAdminRoute(route) && !session.authenticated) {
    navigate('/admin/login?next=' + encodeURIComponent(route.path), { replace: true });
    return;
  }

  if (route.name === 'login') {
    const next = safeNext(new URLSearchParams(location.search).get('next'));
    if (session.authenticated) {
      navigate(session.user?.mustChangePassword ? '/admin/change-password' : next, { replace: true });
      return;
    }
  }

  if (session.authenticated && session.user?.mustChangePassword &&
      isAdminRoute(route) && route.name !== 'change-password') {
    const next = route.path.startsWith('/admin') && route.path !== '/admin/change-password'
      ? route.path
      : '/admin';
    navigate('/admin/change-password?next=' + encodeURIComponent(next), { replace: true });
    return;
  }

  const requiredPermission = ROUTE_PERMISSIONS[route.name];
  const denied = Boolean(requiredPermission) && !hasPermission(session, requiredPermission) ||
    (route.name === 'settings' && session.user?.role !== 'admin');

  let view;
  try {
    view = denied ? noAccessView({ session, route }) : buildView(route);
  } catch (error) {
    console.error(error);
    view = errorView('Không thể mở trang này.', route.path);
  }

  if (ticket !== renderTicket) return;

  document.title = view.title
    ? `${view.title} · ${APP_TITLE}`
    : `${APP_TITLE} — Album ảnh riêng tư`;

  document.body.className = view.bodyClass || 'page-default';
  document.body.dataset.route = route.name;
  document.documentElement.dataset.theme = view.name === 'public-album' ? 'album' : 'studio';

  app.innerHTML = view.html;
  app.classList.remove('is-entering');
  void app.offsetWidth;
  app.classList.add('is-entering');

  window.scrollTo({ top: 0, left: 0, behavior: 'instant' });
  currentView = view;

  try {
    await view.mount?.(app, { route, session });
  } catch (error) {
    if (error?.name === 'AbortError') return;
    console.error(error);
    if (ticket === renderTicket) {
      showToast('Có lỗi khi mở trang. Vui lòng thử lại.', 'error');
    }
  }

  if (ticket === renderTicket) {
    const heading = app.querySelector('h1');
    if (heading) {
      heading.setAttribute('tabindex', '-1');
      heading.focus({ preventScroll: true });
    }
  }
}

function buildView(route) {
  const context = { session: { ...session }, route };
  switch (route.name) {
    case 'home':
      return landingView(context);
    case 'login':
      return loginView({ ...context, websiteSettings });
    case 'dashboard':
      return dashboardView(context);
    case 'board':
      return boardView(context);
    case 'albums':
      return albumsView(context);
    case 'create':
      return createView(context);
    case 'album-detail':
      return albumDetailView({ ...context, albumId: route.params.id });
    case 'tools':
      return photoFilterView(context);
    case 'customers':
      return customersView(context);
    case 'packages':
      return packagesView(context);
    case 'staff':
      return staffView(context);
    case 'bookings':
      return bookingsView(context);
    case 'orders':
      return ordersView(context);
    case 'contracts':
      return contractsView(context);
    case 'reports':
      return reportsView(context);
    case 'finance':
      return financeView(context);
    case 'attendance':
      return attendanceView(context);
    case 'shifts':
      return shiftsView(context);
    case 'wardrobe':
      return wardrobeView(context);
    case 'notifications':
      return notificationsView(context);
    case 'audit':
      return auditView(context);
    case 'settings':
      return settingsView(context);
    case 'change-password':
      return changePasswordView(context);
    case 'public-album':
      return publicAlbumView({ token: route.params.token, route });
    case 'not-found':
      return notFoundAlbumView({ route, generic: true });
    default:
      return errorView('Trang không tồn tại.', route.path);
  }
}

function navigate(path, options = {}) {
  const target = new URL(path, location.origin);
  const nextPath = target.pathname + target.search + target.hash;
  if (options.replace) {
    history.replaceState({}, '', nextPath);
  } else {
    history.pushState({}, '', nextPath);
  }
  render(parseRoute(target.pathname));
}

function landingView() {
  const content = websiteSettings.website || {};
  const text = (key, fallback) => escapeHtml(content[key] ?? fallback);
  const brand = websiteSettings.logoUrl ? `<a href="/" aria-label="${escapeAttr(websiteSettings.studioName)}"><img src="${escapeAttr(websiteSettings.logoUrl)}" alt="${escapeAttr(websiteSettings.studioName)}" style="max-width:180px;height:56px;object-fit:contain"></a>` : `<a class="wordmark" href="/" aria-label="${escapeAttr(content.brandName ?? 'mây')} — trang chủ">${text('brandName', 'mây')}<span>.</span><small>${text('brandLabel', 'album')}</small></a>`;
  const html = `
    <div class="landing">
      <header class="site-nav">
        ${brand}
        <nav class="site-nav__links" aria-label="Điều hướng chính">
          <a href="#cach-hoat-dong">${text('howItWorksLabel', 'Cách hoạt động')}</a>
          <a href="#album-mau">${text('sampleAlbumsLabel', 'Album mẫu')}</a>
          <a href="#danh-cho-studio">${text('forStudioLabel', 'Dành cho studio')}</a>
        </nav>
        <div class="site-nav__actions">
          <a class="btn btn--ghost btn--compact" href="/admin/login">${icon('lock')} Studio</a>
          <a class="btn btn--primary btn--compact" href="/a/demo-ngay-chung-doi">${text('sampleAlbumsLabel', 'Album mẫu')} ${icon('arrowUpRight')}</a>
        </div>
        <button class="icon-btn site-nav__toggle" type="button" data-menu-toggle aria-label="Mở menu" aria-expanded="false">${icon('menu')}</button>
      </header>
      <div class="site-nav__drawer" data-mobile-menu>
        <a href="#cach-hoat-dong">${text('howItWorksLabel', 'Cách hoạt động')}</a>
        <a href="#album-mau">${text('sampleAlbumsLabel', 'Album mẫu')}</a>
        <a href="#danh-cho-studio">${text('forStudioLabel', 'Dành cho studio')}</a>
        <a href="/admin/login">Đăng nhập studio</a>
      </div>

      <main>
        <section class="landing-hero">
          <div class="landing-hero__copy">
            <p class="eyebrow"><span class="eyebrow__dot"></span>Album ảnh riêng tư</p>
            <h1>${!content.heroTitle || content.heroTitle === 'Mỗi khoảnh khắc, một lời nhớ.' ? 'Mỗi khoảnh khắc,<br>một <em>lời nhớ.</em>' : text('heroTitle', '')}</h1>
            <p class="landing-hero__lead">
              ${text('heroDescription', 'Mây gom những ngày đẹp nhất vào một album riêng. Bạn chỉ cần mở liên kết, thả tim những tấm ảnh mình yêu và gửi lại cho studio.')}
            </p>
            <div class="landing-hero__actions">
              <a class="btn btn--primary btn--lg" href="/a/demo-ngay-chung-doi">Mở album mẫu ${icon('arrowRight')}</a>
              <a class="btn btn--text btn--lg" href="#cach-hoat-dong">Tìm hiểu cách hoạt động ${icon('arrowUpRight')}</a>
            </div>
            <div class="landing-proof">
              <div class="avatar-stack" aria-hidden="true">
                <span class="avatar avatar--rose">L</span>
                <span class="avatar avatar--olive">M</span>
                <span class="avatar avatar--sand">H</span>
              </div>
              <p>Những album được trao đi<br><strong>bằng cả sự dịu dàng.</strong></p>
            </div>
          </div>
          <div class="landing-collage" aria-label="Ảnh cưới từ album mẫu">
            <div class="landing-collage__sun" aria-hidden="true"></div>
            <figure class="collage-photo collage-photo--main">
              <img src="/assets/img/demo/thumbs/07.jpg" alt="Khoảnh khắc bên biển trong album mẫu" fetchpriority="high">
            </figure>
            <figure class="collage-photo collage-photo--top">
              <img src="/assets/img/demo/thumbs/16.jpg" alt="Ảnh cưới trong vườn" loading="lazy">
            </figure>
            <figure class="collage-photo collage-photo--bottom">
              <img src="/assets/img/demo/thumbs/06.jpg" alt="Ảnh cưới giữa nắng sớm" loading="lazy">
            </figure>
            <div class="collage-note">
              <span class="collage-note__icon">${icon('heart')}</span>
              <div><strong>4 khoảnh khắc</strong><span>vừa được chọn</span></div>
            </div>
            <p class="collage-caption">Đà Lạt · 09.2026</p>
          </div>
        </section>

        <section class="landing-strip" aria-label="Điểm nổi bật">
          <span>${icon('camera')} Ảnh gốc chất lượng cao</span>
          <span>${icon('lock')} Liên kết riêng tư</span>
          <span>${icon('heart')} Chọn ảnh bằng một cái chạm</span>
          <span>${icon('download')} Tải về khi được bật</span>
        </section>

        <section class="landing-section landing-steps" id="cach-hoat-dong">
          <div class="section-heading">
            <p class="eyebrow">Một quy trình thật nhẹ</p>
            <h2>Từ Drive đến tay bạn,<br><em>chỉ trong vài bước.</em></h2>
            <p>Không app, không mật khẩu phức tạp. Chỉ một đường liên kết riêng và những tấm ảnh đang chờ bạn.</p>
          </div>
          <div class="step-grid">
            <article class="step-card">
              <span class="step-card__number">01</span>
              <span class="step-card__icon">${icon('drive')}</span>
              <h3>Tạo album từ Drive</h3>
              <p>Studio dán liên kết thư mục Google Drive. Mây tự đọc tên tệp và tạo mã hình cho từng ảnh.</p>
            </article>
            <article class="step-card step-card--accent">
              <span class="step-card__number">02</span>
              <span class="step-card__icon">${icon('link')}</span>
              <h3>Gửi một liên kết riêng</h3>
              <p>Mỗi album có một đường dẫn không thể đoán. Chỉ người nhận liên kết mới mở được album.</p>
            </article>
            <article class="step-card">
              <span class="step-card__number">03</span>
              <span class="step-card__icon">${icon('heart')}</span>
              <h3>Nhận lựa chọn của khách</h3>
              <p>Khách thả tim, tìm theo mã hình và gửi danh sách. Studio xem ngay trong bảng điều khiển.</p>
            </article>
          </div>
        </section>

        <section class="landing-section landing-demos" id="album-mau">
          <div class="section-heading section-heading--row">
            <div>
              <p class="eyebrow">Xem thử một album thật</p>
              <h2>Hai câu chuyện,<br><em>hai miền ký ức.</em></h2>
            </div>
            <p class="section-heading__aside">Mỗi album mẫu đã có sẵn lựa chọn để bạn hình dung trải nghiệm của khách hàng.</p>
          </div>
          <div class="demo-grid">
            <a class="demo-card" href="/a/demo-ngay-chung-doi">
              <div class="demo-card__media">
                <img src="/assets/img/demo/thumbs/06.jpg" alt="Album Ngày Mình Chung Đôi" loading="lazy">
                <span class="demo-card__badge">20 ảnh</span>
              </div>
              <div class="demo-card__body">
                <div>
                  <p class="demo-card__kicker">Đà Lạt · 20.09.2026</p>
                  <h3>Ngày Mình Chung Đôi</h3>
                  <p>Linh &amp; Minh</p>
                </div>
                <span class="demo-card__arrow">${icon('arrowUpRight')}</span>
              </div>
            </a>
            <a class="demo-card" href="/a/demo-bien-nho">
              <div class="demo-card__media demo-card__media--sea">
                <img src="/assets/img/demo/thumbs/07.jpg" alt="Album Biển Nhớ" loading="lazy">
                <span class="demo-card__badge">Đã gửi lựa chọn</span>
              </div>
              <div class="demo-card__body">
                <div>
                  <p class="demo-card__kicker">Phú Quốc · 14.06.2026</p>
                  <h3>Biển Nhớ</h3>
                  <p>Hà &amp; Nam</p>
                </div>
                <span class="demo-card__arrow">${icon('arrowUpRight')}</span>
              </div>
            </a>
          </div>
        </section>

        <section class="landing-studio" id="danh-cho-studio">
          <div class="landing-studio__inner">
            <div>
              <p class="eyebrow eyebrow--light">${text('forStudioLabel', 'Dành cho studio')}</p>
              <h2>Chiếc album đầu tiên<br>chỉ mất <em>vài phút.</em></h2>
              <p>Kết nối Google Drive, tạo album, gửi liên kết. Mọi lựa chọn của khách được lưu lại ngay khi họ thả tim.</p>
              <div class="landing-studio__actions">
                <a class="btn btn--cream btn--lg" href="/admin/login">Vào bảng điều khiển ${icon('arrowRight')}</a>
                <a class="btn btn--outline-light btn--lg" href="/admin/settings">Cài đặt Drive</a>
              </div>
            </div>
            <ol class="studio-facts">
              <li><strong>Mã hình tự động</strong><span>Đọc từ tên tệp, sắp xếp tự nhiên.</span></li>
              <li><strong>Đồng bộ một chạm</strong><span>Thêm hoặc bớt ảnh từ thư mục Drive.</span></li>
              <li><strong>Xuất CSV</strong><span>Danh sách ảnh khách chọn, kèm ghi chú.</span></li>
            </ol>
          </div>
          <span class="landing-studio__mark" aria-hidden="true">${text('brandName', 'mây')}.</span>
        </section>
      </main>

      <footer class="site-footer">
        <div class="site-footer__top">
          <div>
            ${brand}
            <p>${text('footerDescription', 'Album ảnh riêng tư, được làm bằng sự chăm chút.')}</p>
          </div>
          <div class="site-footer__cols">
            <div>
              <p class="site-footer__label">Khám phá</p>
              <a href="/a/demo-ngay-chung-doi">Ngày Mình Chung Đôi</a>
              <a href="/a/demo-bien-nho">Biển Nhớ</a>
            </div>
            <div>
              <p class="site-footer__label">Studio</p>
              <a href="/admin/login">Đăng nhập</a>
              <a href="/admin/settings">Cài đặt Google Drive</a>
            </div>
          </div>
        </div>
        <div class="site-footer__bottom">
          <span>© ${new Date().getFullYear()} ${escapeHtml(websiteSettings.studioName)}</span>
          <span>${icon('lock')} ${text('footerPrivacy', 'Album được chia sẻ riêng cho từng khách hàng.')}</span>
        </div>
      </footer>
    </div>
  `;

  return {
    title: '',
    bodyClass: 'page-landing',
    html,
    mount(root) {
      const toggle = root.querySelector('[data-menu-toggle]');
      const menu = root.querySelector('[data-mobile-menu]');
      toggle?.addEventListener('click', () => {
        const open = document.body.classList.toggle('nav-open');
        toggle.setAttribute('aria-expanded', String(open));
        toggle.innerHTML = open ? icon('x') : icon('menu');
      });
      menu?.querySelectorAll('a').forEach(link => {
        link.addEventListener('click', () => {
          document.body.classList.remove('nav-open');
          toggle?.setAttribute('aria-expanded', 'false');
          if (toggle) toggle.innerHTML = icon('menu');
        });
      });
    },
    cleanup() {
      document.body.classList.remove('nav-open');
    }
  };
}

function errorView(message, path) {
  return {
    title: 'Không tìm thấy trang',
    bodyClass: 'page-landing',
    html: `
      <div class="fatal-page">
        <a class="wordmark" href="/">mây<span>.</span><small>album</small></a>
        <div class="fatal-page__card">
          <span class="fatal-page__code">404</span>
          <h1>${message}</h1>
          <p>Đường dẫn <code>${path.replaceAll('<', '&lt;')}</code> không còn tồn tại hoặc đã được thay đổi.</p>
          <div class="dialog-actions">
            <a class="btn btn--primary" href="/">Về trang chủ ${icon('arrowRight')}</a>
            <a class="btn btn--ghost" href="/admin">Vào studio</a>
          </div>
        </div>
      </div>
    `
  };
}

window.addEventListener('popstate', () => {
  render(parseRoute(location.pathname));
});

window.addEventListener('may:navigate', event => {
  const path = event.detail?.path;
  if (!path) return;
  navigate(path, { replace: Boolean(event.detail.replace) });
});

window.addEventListener('may:session', event => {
  const next = event.detail || {};
  session.authenticated = Boolean(next.authenticated);
  session.studioName = next.studioName || session.studioName || 'Mây Studio';
  session.user = next.user || null;
});

let expiredNoticeShown = false;
window.addEventListener('may:unauthorized', () => {
  if (!session.authenticated) return;
  session.authenticated = false;
  session.user = null;
  if (expiredNoticeShown) return;
  expiredNoticeShown = true;
  window.setTimeout(() => { expiredNoticeShown = false; }, 2500);
  showToast('Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại.', 'info');
  if (parseRoute(location.pathname).name !== 'login') {
    navigate('/admin/login?next=' + encodeURIComponent(location.pathname), { replace: true });
  }
});

document.addEventListener('click', event => {
  if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
    return;
  }

  const link = event.target.closest('a[href]');
  if (!link) return;
  if (link.target && link.target !== '_self') return;
  if (link.hasAttribute('download') || link.dataset.external === 'true') return;

  const href = link.getAttribute('href');
  if (!href || href.startsWith('#') || href.startsWith('mailto:') || href.startsWith('tel:')) return;

  const url = new URL(link.href, location.origin);
  if (url.origin !== location.origin) return;
  if (url.pathname.startsWith('/api/') || url.pathname.startsWith('/assets/')) return;

  event.preventDefault();
  navigate(url.pathname + url.search + url.hash);
});

document.addEventListener('error', event => {
  const image = event.target;
  if (!(image instanceof HTMLImageElement)) return;
  if (image.dataset.fallbackApplied === '1') return;
  image.dataset.fallbackApplied = '1';
  image.classList.add('is-placeholder');
  image.src = PLACEHOLDER_IMAGE;
}, true);

async function boot() {
  try {
    websiteSettings = await api('/api/public/website');
  } catch {
    // Keep default copy if public settings are temporarily unavailable.
  }
  try {
    const state = await authApi.session();
    session.authenticated = Boolean(state?.authenticated);
    session.studioName = state?.studioName || 'Mây Studio';
    session.user = state?.user || null;
  } catch {
    // Public pages still work; admin routes will ask for a login.
    session.authenticated = false;
    session.user = null;
  }

  await render(parseRoute(location.pathname));
}

boot();
