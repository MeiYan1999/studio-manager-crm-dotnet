import { api } from './api.js';

const googleMark = `<svg aria-hidden="true" viewBox="0 0 24 24"><path fill="#4285f4" d="M21.6 12.2c0-.7-.1-1.4-.2-2H12v3.9h5.4a4.6 4.6 0 0 1-2 3v2.6h3.3c1.9-1.8 2.9-4.4 2.9-7.5Z"/><path fill="#34a853" d="M12 22c2.7 0 5-.9 6.7-2.3l-3.3-2.6c-.9.6-2.1 1-3.4 1-2.6 0-4.8-1.8-5.6-4.1H3v2.7A10 10 0 0 0 12 22Z"/><path fill="#fbbc05" d="M6.4 14a6 6 0 0 1 0-3.9V7.4H3a10 10 0 0 0 0 9.2L6.4 14Z"/><path fill="#ea4335" d="M12 5.9c1.5 0 2.8.5 3.8 1.5l2.9-2.8A9.7 9.7 0 0 0 3 7.4l3.4 2.7A6 6 0 0 1 12 5.9Z"/></svg>`;

const authErrors = {
  google_failed: 'Google chưa thể xác thực tài khoản. Vui lòng thử lại.',
  email_unverified: 'Tài khoản Google cần có email đã xác minh.',
  google_link_ambiguous: 'Email này đang thuộc nhiều studio. Hãy đăng nhập từ đường dẫn studio hiện có.',
  account_disabled: 'Tài khoản đã bị khóa. Vui lòng liên hệ quản trị viên.',
  google_registration_conflict: 'Tài khoản vừa được tạo ở một yêu cầu khác. Vui lòng đăng nhập lại.',
  invitation_pending_unavailable: 'Studio đã mời bạn nhưng hiện chưa còn slot hoặc chưa đủ gói. Vui lòng liên hệ quản trị viên studio.'
};
const escapeHtml = value => String(value ?? '').replace(/[&<>"']/g, character => ({ '&':'&amp;', '<':'&lt;', '>':'&gt;', '"':'&quot;', "'":'&#39;' })[character]);

export function platformView() {
  return {
    title: 'Phần mềm quản lý studio', bodyClass: 'page-platform',
    html: `<div class="platform"><header class="platform-nav"><a class="platform-logo" href="/">StudioCore<span>.</span></a><nav><a href="#tinh-nang">Tính năng</a><a href="#bat-dau">Đăng nhập</a></nav><a class="platform-button platform-button--small" href="#bat-dau">Bắt đầu miễn phí</a></header>
    <main><section class="platform-hero"><div><h1>Studio gọn trong một nơi.</h1><p>Quản lý khách hàng, lịch chụp và album ảnh để bạn dành nhiều thời gian hơn cho những bức hình đẹp.</p><div class="platform-actions"><a class="platform-button" href="#bat-dau">Đăng nhập với Google <span>→</span></a><a class="platform-link" href="#tinh-nang">Khám phá StudioCore ↓</a></div><p class="platform-note">Mỗi tài khoản Google mới nhận một không gian Free riêng. Chỉ sau khi vào studio, bạn mới chọn gói cần mở khóa.</p></div><div class="platform-visual"><div class="platform-visual__image"></div><div class="platform-visual__caption"><strong>Từ lịch chụp<br>đến album.</strong></div></div></section>
    <section class="platform-features" id="tinh-nang"><div><h2>Một không gian làm việc nhẹ nhàng hơn.</h2></div><div class="platform-feature-list"><article><img src="/assets/img/demo/thumbs/14.jpg" alt="Ảnh chân dung trong studio"><div><h3>Chăm sóc khách hàng</h3><p>Theo dõi thông tin khách, lịch chụp và hợp đồng theo quyền của gói.</p></div></article><article><img src="/assets/img/demo/thumbs/28.jpg" alt="Bộ ảnh khách hàng"><div><h3>Trao ảnh theo cách riêng</h3><p>Tạo album, chia sẻ liên kết và nhận lựa chọn ảnh từ khách hàng.</p></div></article><article><img src="/assets/img/demo/thumbs/15.jpg" alt="Buổi chụp ảnh tại studio"><div><h3>Vận hành studio</h3><p>Bắt đầu Free, sau đó mở khóa CRM, tài chính và đội ngũ ngay trong Cài đặt.</p></div></article></div></section>
    <section class="platform-auth" id="bat-dau"><div class="platform-auth__copy"><h2>Một tài khoản.<br>Một studio riêng.</h2><p>Google xác thực danh tính; StudioCore tự cấp tenant Free và đưa bạn vào bảng điều khiển. Không cần tạo mật khẩu mới.</p></div><div class="platform-auth__card" data-google-card><div class="platform-auth__mark" aria-hidden="true">SC</div><h2>Đăng nhập StudioCore</h2><p>Quản lý album và vận hành studio của bạn.</p><a class="platform-google" href="/api/auth/google/start" data-google-login>${googleMark}<span>Tiếp tục với Google</span></a><p class="platform-form-message" data-google-message role="status"></p><div class="platform-auth__meta"><span>Bảo vệ bởi Google OAuth 2.0</span><a href="/admin/login">Tài khoản cũ</a></div></div></section></main>
    <footer class="platform-footer"><strong>StudioCore.</strong><span>Không gian làm việc dành cho studio ảnh.</span><a href="#bat-dau">Đăng nhập</a></footer></div>`,
    mount(root) {
      const button = root.querySelector('[data-google-login]');
      const message = root.querySelector('[data-google-message]');
      const errorCode = new URLSearchParams(location.search).get('authError');
      if (errorCode) message.textContent = authErrors[errorCode] || 'Không thể đăng nhập Google. Vui lòng thử lại.';
      api('/api/auth/google/status').then(status => {
        if (status.enabled) return;
        button.removeAttribute('href'); button.setAttribute('aria-disabled', 'true');
        message.textContent = 'Đăng nhập Google đang được cấu hình. Vui lòng quay lại sau.';
      }).catch(() => {
        button.removeAttribute('href'); button.setAttribute('aria-disabled', 'true');
        message.textContent = 'Chưa tải được trạng thái đăng nhập. Vui lòng thử lại.';
      });
    }
  };
}

export function workspacePickerView() {
  return {
    title: 'Chọn studio', bodyClass: 'page-platform',
    html: `<main class="workspace-picker"><a class="platform-logo" href="/">StudioCore<span>.</span></a><section><h1>Chọn studio để tiếp tục</h1><p>Một tài khoản Google có thể làm việc trong nhiều studio. Mỗi workspace vẫn được cách ly dữ liệu và quyền truy cập.</p><div data-workspaces><p role="status">Đang tải studio…</p></div><a class="platform-link" href="/">← Về trang chủ</a></section></main>`,
    async mount(root) {
      const holder = root.querySelector('[data-workspaces]');
      try {
        const result = await api('/api/auth/workspaces');
        if (!result.workspaces?.length) {
          holder.innerHTML = '<p role="alert">Tài khoản chưa có studio khả dụng. Hãy đăng nhập Google lại để tạo workspace Free.</p><a class="platform-button" href="/api/auth/google/start">Đăng nhập Google</a>';
          return;
        }
        holder.innerHTML = result.workspaces.map(workspace => `<button type="button" class="workspace-card" data-tenant-id="${escapeHtml(workspace.tenantId)}"><span><strong>${escapeHtml(workspace.name)}</strong><small>${escapeHtml(workspace.role)} · ${workspace.slug ? '/' + escapeHtml(workspace.slug) : 'Chưa đặt đường dẫn'}</small></span><b>Tiếp tục →</b></button>`).join('');
        holder.addEventListener('click', async event => {
          const button = event.target.closest('[data-tenant-id]');
          if (!button) return;
          button.disabled = true;
          try {
            const selected = await api('/api/auth/workspaces/select', { method: 'POST', body: { tenantId: button.dataset.tenantId } });
            location.assign(selected.redirectUrl);
          } catch (error) {
            button.disabled = false;
            holder.insertAdjacentHTML('afterbegin', `<p role="alert">${escapeHtml(error.message)}</p>`);
          }
        });
      } catch {
        holder.innerHTML = '<p role="alert">Phiên Google đã hết hạn. Vui lòng đăng nhập lại.</p><a class="platform-button" href="/api/auth/google/start">Đăng nhập Google</a>';
      }
    }
  };
}
