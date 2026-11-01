import { api } from './api.js';

export function platformView() {
  return {
    title: 'Phần mềm quản lý studio', bodyClass: 'page-platform',
    html: `<div class="platform"><header class="platform-nav"><a class="platform-logo" href="/">StudioCore<span>.</span></a><nav><a href="#tinh-nang">Tính năng</a><a href="#goi-dich-vu">Gói dịch vụ</a><a href="#bat-dau">Bắt đầu</a></nav><a class="platform-button platform-button--small" href="#bat-dau">Tạo studio miễn phí</a></header>
    <main><section class="platform-hero"><div><p class="platform-kicker">PHẦN MỀM DÀNH CHO STUDIO ẢNH</p><h1>Mọi công việc của studio, trong một nơi.</h1><p>Quản lý khách hàng, lịch chụp và album ảnh để bạn dành nhiều thời gian hơn cho những bức hình đẹp.</p><div class="platform-actions"><a class="platform-button" href="#bat-dau">Bắt đầu miễn phí <span>→</span></a><a class="platform-link" href="#tinh-nang">Khám phá StudioCore ↓</a></div><p class="platform-note">Tạo không gian riêng cho studio của bạn với gói Free.</p></div><div class="platform-visual"><div class="platform-visual__image"></div><div class="platform-visual__caption"><span>STUDIOCORE</span><strong>Từ lịch chụp<br>đến album hoàn thiện.</strong></div></div></section>
    <section class="platform-features" id="tinh-nang"><div><p class="platform-kicker">MỘT KHÔNG GIAN LÀM VIỆC</p><h2>Nhẹ nhàng hơn trong từng công việc.</h2></div><div class="platform-feature-list"><article><span>01</span><div><h3>Chăm sóc khách hàng</h3><p>Theo dõi thông tin khách, lịch chụp và hợp đồng theo gói dịch vụ của bạn.</p></div></article><article><span>02</span><div><h3>Trao ảnh theo cách riêng</h3><p>Tạo album, chia sẻ liên kết và nhận lựa chọn ảnh từ khách hàng.</p></div></article><article><span>03</span><div><h3>Vận hành studio</h3><p>Quản lý công việc, doanh thu và đội ngũ ở những gói phù hợp.</p></div></article></div></section>
    <section class="platform-pricing" id="goi-dich-vu"><p class="platform-kicker">GÓI DỊCH VỤ</p><h2>Chọn cách phát triển phù hợp.</h2><p>Bắt đầu miễn phí và nâng cấp khi studio cần thêm công cụ.</p><div class="platform-plans"><article><h3>Free</h3><strong>Đang tải giá…</strong><p>Album và công cụ cơ bản cho một tài khoản.</p></article><article><h3>Album Pro</h3><strong>Đang tải giá…</strong><p>Mở rộng công cụ album và tên miền riêng.</p></article><article><h3>ERP Solo</h3><strong>Đang tải giá…</strong><p>Thêm CRM, hợp đồng và quản lý tài chính.</p></article><article><h3>ERP Studio</h3><strong>Đang tải giá…</strong><p>Quản lý đội ngũ và vận hành studio.</p></article></div></section>
    <section class="platform-signup" id="bat-dau"><div><p class="platform-kicker">BẮT ĐẦU VỚI STUDIOCORE</p><h2>Một không gian riêng cho studio của bạn.</h2><p>Tạo tài khoản Free để bắt đầu cấu hình website và quản lý album.</p><p class="platform-note">Đã có studio? Mở đường dẫn riêng của studio để đăng nhập.</p></div><form data-registration><h3>Tạo studio miễn phí</h3><label>Tên studio<input name="studioName" required minlength="2" maxlength="60" autocomplete="organization"></label><label>Định danh studio<input name="slug" required minlength="3" maxlength="63" pattern="[a-z0-9][a-z0-9-]*[a-z0-9]" placeholder="vi-du-studio"><small>Chữ thường không dấu, số và dấu gạch ngang.</small></label><label>Email liên hệ<input name="email" type="email" required maxlength="160" autocomplete="email"></label><label>Tên đăng nhập<input name="username" required minlength="3" maxlength="32" pattern="[a-zA-Z0-9._-]+" autocomplete="username"></label><label>Mật khẩu<input name="password" type="password" required minlength="8" maxlength="128" autocomplete="new-password"></label><p class="platform-form-message" data-registration-message role="status"></p><button class="platform-button" type="submit">Tạo studio <span>→</span></button></form></section></main><footer class="platform-footer"><strong>StudioCore.</strong><span>Không gian làm việc dành cho studio ảnh.</span><a href="/admin/login">Studio hiện có đăng nhập</a></footer></div>`,
    mount(root) {
      api('/api/public/plans').then(plans => {
        const cards = root.querySelectorAll('.platform-plans article');
        plans.forEach((plan, index) => { const price = cards[index]?.querySelector('strong'); if (price) price.innerHTML = `${new Intl.NumberFormat('vi-VN').format(plan.monthlyPrice)} đ${plan.monthlyPrice ? ' <small>/ tháng</small>' : ''}`; });
      }).catch(() => { root.querySelectorAll('.platform-plans strong').forEach(price => { price.textContent = 'Liên hệ để biết giá'; }); });
      const form = root.querySelector('[data-registration]');
      api('/api/onboarding/status').then(status => {
        if (status.registrationEnabled) return;
        form.querySelectorAll('input,button').forEach(control => { control.disabled = true; });
        const message = form.querySelector('[data-registration-message]');
        message.textContent = status.message || 'Đăng ký mới đang tạm đóng.';
        form.querySelector('button').textContent = 'Đăng ký đang tạm đóng';
      }).catch(() => {});
      form.addEventListener('submit', async event => {
        event.preventDefault();
        const button = form.querySelector('button');
        const message = form.querySelector('[data-registration-message]');
        button.disabled = true; message.textContent = 'Đang tạo studio…';
        try {
          const result = await api('/api/onboarding/register', { method: 'POST', body: Object.fromEntries(new FormData(form)) });
          location.assign(`${result.loginUrl}?registered=1&emailSent=${result.emailSent === false ? '0' : '1'}&identifier=${encodeURIComponent(form.elements.email.value.trim())}`);
        } catch (error) { message.textContent = error.message || 'Không tạo được studio. Vui lòng thử lại.'; button.disabled = false; }
      });
    }
  };
}
