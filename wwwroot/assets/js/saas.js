import { api } from './api.js';
import { escapeHtml as h, escapeAttr as a, showToast, createModal, confirmDialog } from './ui.js';
import { tenantPath, tenantPrefix } from './tenant.js';

// Prices are populated from the server plan catalog before rendering.
const plans = [
  { key:'Free', name:'FREE', label:'Gói Free - Miễn Phí', price:0, badge:'Trọn đời', features:[
    '1 / 1 user', 'Album không giới hạn', 'Lọc ảnh PC: 5 album/tháng', 'Khách chọn ảnh & ghi chú', 'Tải ảnh & sao chép mã', 'Đường dẫn /s/{slug} & subdomain'], locks:['Tên miền riêng · PRO', 'Sao lưu & phục hồi · PRO', 'Hợp đồng, CRM & Sổ quỹ · SOLO', 'Phân quyền nhân viên · STUDIO'] },
  { key:'AlbumPro', name:'PHOTOGRAPHER', label:'ALBUM PRO', features:[
    '1 tài khoản', 'Toàn bộ tính năng Free', 'Lọc ảnh PC không giới hạn trên máy tính', 'Tên miền riêng qua CNAME', 'Sao Lưu & Phục Hồi'], locks:['Hợp đồng, CRM & Sổ quỹ · SOLO', 'Phân quyền nhân viên · STUDIO'] },
  { key:'ErpSolo', name:'FREELANCER', label:'ERP SOLO', badge:'BÁN CHẠY NHẤT', features:[
    '1 tài khoản', 'Toàn bộ tính năng Album Pro', 'Quản lý Hợp đồng, CRM khách hàng', 'Sổ quỹ thu chi và công nợ'], locks:['Phân quyền 5 nhân viên · STUDIO'] },
  { key:'ErpStudio', name:'STUDIO', label:'ERP STUDIO', badge:'ĐẲNG CẤP NHẤT', features:[
    '5 tài khoản nhân viên qua Email', 'Toàn bộ tính năng ERP Solo', 'Phân quyền Admin, Manager, Sale', 'Phân công ê-kíp & tính lương tháng'], locks:[] }
];
const money = n => new Intl.NumberFormat('vi-VN').format(n) + ' đ';
const badge = key => ({ Free:'FREE', AlbumPro:'PRO', ErpSolo:'SOLO', ErpStudio:'STUDIO' }[key] || 'FREE');
let currentStatus;
export function getSaasStatus() { return currentStatus; }
export async function loadSaasIdentity(root) {
  try {
    currentStatus = await api('/api/saas/status');
    const target = root.querySelector('[data-saas-sidebar]');
    if (target) target.innerHTML = `<span class="saas-badge">${badge(currentStatus.currentPlan)}</span><span>${h(currentStatus.customDomainVerified ? currentStatus.customDomain : 'Chưa cài đặt tên miền')}</span>`;
    root.querySelectorAll('[data-plan-feature]').forEach(link => {
      const locked = currentStatus.features[link.dataset.planFeature] === false;
      link.dataset.planLocked = String(locked);
      link.querySelector('[data-plan-lock]')?.remove();
      if (locked) {
        const label = document.createElement('small');
        label.dataset.planLock = '';
        label.className = 'admin-nav__plan';
        label.textContent = link.dataset.planLabel;
        label.title = `Cần gói ${link.dataset.planLabel} trở lên`;
        link.append(label);
      }
    });
  } catch { /* Existing navigation remains usable if the status API fails. */ }
}

export function mountSaasSettings(holder) {
  let disposed = false, cycle = 'Monthly', status, domains, preferences, history, invitations, active = location.hash === '#domains' ? 'domains' : 'brand';
  let stream, polling, timer;
  const legacy = holder.querySelector('.settings-layout');
  if (!legacy) return () => {};
  legacy.dataset.saasLegacy = '';
  const accounts = legacy.querySelector('[data-accounts-panel]');
  const importPanel = legacy.querySelector('[data-crm-import]');
  const accountsWrap = document.createElement('div'); accountsWrap.dataset.saasAccounts = ''; accountsWrap.hidden = true;
  holder.append(accountsWrap); if (accounts) accountsWrap.append(accounts);
  const container = document.createElement('div'); container.dataset.saasContent = ''; container.hidden = true;
  const nav = document.createElement('nav'); nav.className = 'saas-tabs'; nav.setAttribute('aria-label','Cài đặt studio');
  holder.prepend(nav); holder.append(container);
  const tabs = [ ['brand','Thương Hiệu & Kết Nối'], ['services','Loại Dịch Vụ & Màu Sắc'], ['members','Thành Viên & Phân Quyền'], ['backup','Sao Lưu & Phục Hồi'], ['messages','Tin nhắn Chia sẻ và Lời chào'], ['templates','Mẫu Hợp Đồng'], ['domains','Tên Miền & Gói Dịch Vụ'] ];
  function show() {
    nav.innerHTML = tabs.map(([key,label]) => `<button type="button" data-saas-tab="${key}" aria-selected="${active===key}">${label} ${key==='services' ? `<span class="saas-badge">${preferences?.serviceTypes?.length || 0}</span>` : key==='members' && status?.currentPlan!=='ErpStudio' ? '<span class="saas-badge">STUDIO</span>' : key==='backup' && status?.currentPlan==='Free' ? '<span class="saas-badge">PRO</span>' : key==='messages' ? '<span class="saas-badge">Mới</span>' : key==='templates' ? '<span class="saas-badge">Chuẩn</span>' : ''}</button>`).join('');
    legacy.hidden = active !== 'brand'; accountsWrap.hidden = active !== 'members'; container.hidden = ['brand','members'].includes(active);
    if (!status) { container.innerHTML = '<p role="status">Đang tải cài đặt…</p>'; return; }
    if (active==='brand') return;
    if (active==='members') {
      accountsWrap.querySelector('[data-slot-notice]')?.remove();
      const text = document.createElement('p'); text.dataset.slotNotice = ''; text.className='saas-notice';
      text.textContent = `Số user đã kích hoạt: ${status.activeUsers} / ${status.maxUsers} user. ${status.activeUsers>=status.maxUsers ? 'Đã hết slot user.' : ''} ${status.currentPlan!=='ErpStudio' ? 'Nâng cấp ERP STUDIO để thêm thành viên & phân quyền.' : ''}`;
      accountsWrap.prepend(text);
      accountsWrap.querySelector('[data-invite-panel]')?.remove();
      const invite=document.createElement('section');invite.className='saas-panel';invite.dataset.invitePanel='';
      invite.innerHTML=`<h2>Mời nhân viên qua Email</h2>${status.currentPlan!=='ErpStudio' ? '<p>Nâng cấp ERP STUDIO để mời tối đa 5 tài khoản nhân viên.</p>' : `<form data-invite-form class="saas-inline"><label class="field">Email<input type="email" name="email" required maxlength="160"></label><label class="field">Vai trò<select name="role"><option value="employee">Nhân viên</option><option value="manager">Manager</option><option value="sale">Sale</option><option value="admin">Admin</option></select></label><button class="btn btn--primary" ${invitations.emailConfigured?'':'disabled'}>Gửi lời mời</button></form>${invitations.emailConfigured?'':'<p>Quản trị nền tảng chưa cấu hình email SMTP.</p>'}<ul>${invitations.invitations.filter(i=>!i.acceptedAt&&new Date(i.expiresAt)>new Date()).map(i=>`<li>${h(i.email)} · ${h(i.role)} <button type="button" class="btn btn--ghost" data-revoke-invite="${a(i.id)}">Thu hồi</button></li>`).join('')}</ul>`}`;
      accountsWrap.append(invite); return;
    }
    if (active==='services') {
      container.innerHTML=`<section class="saas-panel"><h2>Loại Dịch Vụ & Màu Sắc</h2><form data-services-form><div data-service-rows>${(preferences.serviceTypes||[]).map(serviceRow).join('')}</div><div class="saas-inline"><button type="button" class="btn btn--ghost" data-add-service>Thêm loại dịch vụ</button><button class="btn btn--primary">Lưu thay đổi</button></div></form></section>`; return;
    }
    if (active==='backup') {
      container.innerHTML=`<section class="saas-panel"><h2>Sao Lưu & Phục Hồi</h2><p>Sao lưu metadata album, lựa chọn của khách, bảng giá và concept. Tệp ảnh gốc tiếp tục nằm trong Google Drive/kho ảnh hiện tại.</p>${status.features.Backup ? `<a class="btn btn--primary" href="${tenantPath('/api/saas/backup/albums')}" download="studiocore-albums.json">Tải bản sao lưu</a><label class="field">Phục hồi bản sao lưu<input type="file" accept=".json,application/json" data-restore></label><p>Phục hồi cập nhật các mã trùng và bổ sung mã mới, không xóa album ngoài bản sao lưu.</p>` : '<p class="saas-notice">Nâng cấp từ Album Pro để sử dụng Sao Lưu & Phục Hồi.</p><button class="btn btn--primary" data-go-pricing>Xem Gói Nâng Cấp</button>'}</section>`;
      if (importPanel && status.features.Crm) container.append(importPanel); return;
    }
    if (active==='messages' || active==='templates') {
      const templates = active==='templates';
      container.innerHTML=`<section class="saas-panel"><h2>${templates ? 'Mẫu Hợp Đồng' : 'Tin nhắn Chia sẻ và Lời chào'}</h2>${templates && !status.features.Crm ? '<p class="saas-notice">Mẫu hợp đồng cần gói ERP SOLO trở lên.</p>' : `<form data-preferences-form>${templates ? `<label class="field">Nội dung mẫu hợp đồng<textarea name="contractTemplate" rows="12" maxlength="20000">${h(preferences.contractTemplate||'')}</textarea></label>` : `<label class="field">Tin nhắn chia sẻ album<textarea name="shareMessage" rows="4" maxlength="3000">${h(preferences.shareMessage||'')}</textarea></label><label class="field">Lời chào<textarea name="greeting" rows="4" maxlength="3000">${h(preferences.greeting||'')}</textarea></label>`}<button class="btn btn--primary">Lưu thay đổi</button></form>`}</section>`; return;
    }
    container.innerHTML=renderCommercial();
  }
  function serviceRow(item={name:'',color:'#42665b'}) { return `<div class="saas-service-row"><input type="text" name="serviceName" aria-label="Tên loại dịch vụ" maxlength="80" required value="${a(item.name)}"><input type="color" name="serviceColor" aria-label="Màu loại dịch vụ" value="${a(item.color)}"><button type="button" class="btn btn--ghost" data-remove-service aria-label="Xóa loại dịch vụ">×</button></div>`; }
  function renderCommercial() {
    const percent=Math.min(100, Math.round(status.activeUsers/status.maxUsers*100));
    const free=status.currentPlan==='Free';
    return `<section class="saas-panel saas-plan-summary"><div><h2>Gói Dịch Vụ Hiện Tại <span class="saas-badge">${badge(status.currentPlan)}</span></h2><dl><dt>Mã định danh Studio</dt><dd><code>${h(status.studioCode)}</code></dd><dt>Thời Hạn Gói</dt><dd>${status.subscriptionExpiresAt ? new Date(status.subscriptionExpiresAt).toLocaleDateString('vi-VN') : free ? 'Trọn đời / Vô thời hạn' : status.isTrialGift ? 'ERP STUDIO (Trải nghiệm quà tặng)' : 'Vô thời hạn'}</dd><dt>Lọc ảnh PC tháng này</dt><dd>${status.currentMonthPcFilterUsed} / ${status.monthlyPcFilterQuota===-1 ? 'Không giới hạn' : status.monthlyPcFilterQuota} album</dd></dl></div><div><h3>Thành Viên Ê-kíp & User</h3><p>Số user đã kích hoạt: ${status.activeUsers} / ${status.maxUsers} user (${percent}%)</p><progress max="${status.maxUsers}" value="${status.activeUsers}" aria-label="Số tài khoản đã kích hoạt"></progress><p>${status.activeUsers>=status.maxUsers ? '<span class="saas-badge">Đã hết slot user</span>' : 'Còn slot user'}</p></div></section>
    <section class="saas-panel" id="saas-pricing"><h2>Nâng Cấp Hoặc Chuyển Đổi Gói Dịch Vụ</h2><div class="saas-cycle"><button class="btn btn--ghost" data-cycle="Monthly" aria-pressed="${cycle==='Monthly'}">Thanh toán Tháng</button><button class="btn btn--ghost" data-cycle="Yearly" aria-pressed="${cycle==='Yearly'}">Thanh toán Năm</button></div>${cycle==='Yearly' ? '<p>Giá năm bằng 12 tháng, không áp dụng giảm giá.</p>' : ''}<div class="saas-pricing">${plans.map(p=>`<article class="saas-price">${p.badge ? `<span class="saas-badge">${p.badge}</span>` : ''}<h3>${p.name}</h3><strong>${money((cycle==='Yearly'?p.yearlyPrice:p.price))}</strong><span>/ ${cycle==='Yearly'?'năm':'tháng'}</span><ul>${p.features.map(f=>`<li>✓ ${h(f)}</li>`).join('')}${p.locks.map(f=>`<li data-locked>Khóa: ${h(f)}</li>`).join('')}</ul><button type="button" class="btn btn--primary" data-checkout="${p.key}" ${p.key==='Free' || plans.findIndex(v=>v.key===status.currentPlan)>plans.indexOf(p) || !history.configured ? 'disabled' : ''}>${p.key===status.currentPlan ? (p.key==='Free'?'Đang sử dụng':'Gia Hạn '+p.label) : 'Nâng Cấp '+p.label}</button></article>`).join('')}</div><p class="saas-notice">Kích Hoạt Tức Thì Qua VietQR · Tự động đối soát SePay${history.configured?'':' · Quản trị nền tảng chưa cấu hình thanh toán.'}</p><p>Các kết nối AI, video, Calendar và Google Sheets cần được triển khai/kết nối dịch vụ trước khi sử dụng. Bảng trên thể hiện quyền theo gói.</p></section>
    <section class="saas-panel"><h2>Lịch Sử Thanh Toán Mua Gói & Nâng Cấp</h2><p>Tự động đối soát SePay</p><div class="saas-history">${history.transactions.length ? `<table><thead><tr><th>Thời gian</th><th>Gói / mã</th><th>Số tiền</th><th>Trạng thái</th></tr></thead><tbody>${history.transactions.map(t=>`<tr><td>${new Date(t.createdAt).toLocaleString('vi-VN')}</td><td>${h(t.plan)}<br><code>${h(t.paymentCode)}</code></td><td>${money(t.amount)}</td><td>${({Paid:'Đã thanh toán',Pending:'Chờ thanh toán',Cancelled:'Đã hủy',ManualReview:'Cần đối soát thủ công'})[t.status]||h(t.status)}</td></tr>`).join('')}</tbody></table>`:'<p>Chưa có giao dịch mua gói.</p>'}</div></section>
    <section class="saas-panel" id="saas-domains"><h2>Tên Miền / Đường Dẫn Website Studio</h2><form data-domains-form><div class="saas-domain-grid"><div><label class="field">Tên Định Danh Studio (Slug)<div class="saas-inline"><input name="slug" value="${a(domains.slug||'')}" placeholder="lumi-studio" maxlength="63" pattern="[a-z0-9-]+"><span>.${h(domains.baseDomain)}</span></div></label><p data-slug-status aria-live="polite"></p><p>1. ĐƯỜNG DẪN TIÊU CHUẨN</p><div class="saas-url"><span>${h(domains.standardUrl||'Lưu slug để tạo đường dẫn')}</span><button type="button" class="btn btn--ghost btn--compact" data-copy="${a(domains.standardUrl)}">Sao chép</button></div><p>2. SUBDOMAIN MẶC ĐỊNH</p><div class="saas-url"><span>${h(domains.subdomainUrl||'Lưu slug để tạo subdomain')}</span><button type="button" class="btn btn--ghost btn--compact" data-copy="${a(domains.subdomainUrl)}">Sao chép</button></div></div><div><h3>Tên Miền Riêng Của Studio <span class="saas-badge">TỪ GÓI ALBUM PRO</span></h3><label class="field">Tên miền riêng<input name="customDomain" value="${a(domains.customDomain||'')}" ${free?'disabled':''} placeholder="${free?'Nâng cấp từ Gói Album Pro để gắn tên miền riêng':'gallery.clientstudio.com'}"></label>${free?'<p class="saas-notice">Nâng cấp để gắn tên miền của bạn. <button type="button" class="btn btn--ghost" data-go-pricing>Xem Gói Nâng Cấp</button></p>':`<p>${domains.customDomainVerified?'Đã xác minh tên miền':'Chưa xác minh tên miền'}</p><button type="button" class="btn btn--ghost" data-verify-domain>Xác minh DNS</button>`}<div class="saas-history"><table><thead><tr><th>LOẠI (TYPE)</th><th>HOST / NAME</th><th>GIÁ TRỊ (TARGET)</th></tr></thead><tbody><tr><td>CNAME</td><td>gallery (hoặc tên miền con)</td><td>${h(domains.cnameTarget)}</td></tr><tr><td>TXT</td><td>${h(domains.verificationName||'_studiocore.gallery')}</td><td>${h(domains.domainVerificationToken)}</td></tr></tbody></table></div></div></div><button class="btn btn--primary" type="submit">Lưu tên miền & đường dẫn</button></form></section>
    <section class="saas-panel"><h2>Gợi Ý Địa Điểm & Phòng Chụp Studio</h2><div class="saas-tags">${(status.quickLocations||[]).map((v,i)=>`<button type="button" data-delete-location="${i}" aria-label="Xóa ${a(v)}">${h(v)} ×</button>`).join('')}</div><form data-location-form class="saas-inline"><input name="location" maxlength="80" required aria-label="Địa điểm mới" placeholder="Nhập địa điểm hoặc phòng chụp"><button class="btn btn--primary">Thêm địa điểm</button><button type="button" class="btn btn--ghost" data-reset-locations>Khôi phục mặc định</button></form></section>`;
  }
  async function load() {
    try { let catalog; [status,domains,preferences,history,invitations,catalog]=await Promise.all([...['/status','/domains','/preferences','/billing/history','/invitations'].map(p=>api('/api/saas'+p)),api('/api/public/plans')]); for (const plan of plans) { const server = catalog.find(item=>item.key===plan.key); if (!server) throw new Error('Không tải đủ thông tin các gói dịch vụ.'); plan.price=server.monthlyPrice; plan.yearlyPrice=server.yearlyPrice; } if (!disposed) { currentStatus=status; show(); } }
    catch(error) { if(!disposed) { container.hidden=false; container.innerHTML=`<p role="alert">${h(error.message)}</p><button type="button" class="btn btn--ghost" data-reload-saas>Thử lại</button>`; } }
  }
  async function saveLocations(locations) { await api('/api/saas/locations',{method:'PUT',body:{locations}}); await load(); showToast('Đã lưu địa điểm.','success'); }
  async function checkout(plan) {
    const order=await api('/api/saas/billing/checkout',{method:'POST',body:{plan,billingCycle:cycle}});
    const stop=()=>{clearInterval(polling);stream?.close();};
    const modal=createModal(`<div class="dialog-card saas-payment"><button class="dialog-close" data-close-dialog aria-label="Đóng">×</button><h2>Kích Hoạt Tức Thì Qua VietQR</h2><p>${h(order.plan)} · ${money(order.amount)}</p><img src="${a(order.qrUrl)}" alt="Mã VietQR thanh toán gói"><p>${h(order.bank)} · ${h(order.accountNumber)} · ${h(order.accountName)}</p><p>Nội dung chuyển khoản:<br><code>${h(order.description)}</code></p><p data-payment-status role="status">Đang chờ đối soát SePay…</p><p>Mã QR có hiệu lực đến ${new Date(order.expiresAt).toLocaleTimeString('vi-VN')}.</p></div>`,{className:'dialog--crm',onClose:stop});
    let busy=false;
    const check=async()=>{if(busy||!modal.dialog?.isConnected)return;busy=true;try {const result=await api('/api/saas/billing/history'); const tx=result.transactions.find(t=>t.id===order.id);if(tx?.status==='Paid'||tx?.status==='ManualReview') {stop(); const node=modal.dialog.querySelector('[data-payment-status]');if(node)node.textContent=tx.status==='Paid'?'Đã thanh toán. Gói đã được kích hoạt.':'Đã nhận tiền; cần quản trị đối soát do gói đã thay đổi.';await load();showToast(tx.status==='Paid'?'Đã kích hoạt gói dịch vụ.':'Đã nhận tiền, cần đối soát.','success');} else if(Date.now()>=Date.parse(order.expiresAt)) {stop(); const node=modal.dialog.querySelector('[data-payment-status]');if(node)node.textContent='Mã thanh toán đã hết thời gian chờ. Nếu bạn đã chuyển khoản, hãy kiểm tra lịch sử thanh toán hoặc liên hệ hỗ trợ; khoản chuyển muộn vẫn được đối soát.';}}catch {if(Date.now()>=Date.parse(order.expiresAt)){stop();const node=modal.dialog.querySelector('[data-payment-status]');if(node)node.textContent='Đã hết thời gian chờ. Vui lòng kiểm tra lịch sử thanh toán hoặc liên hệ hỗ trợ.';}}finally{busy=false;}};
    stream?.close();stream=new EventSource(tenantPath('/api/saas/billing/events'));stream.addEventListener('subscription',check);
    polling=setInterval(check,4000);
    check();

  }
  const onClick=async event=>{
    const el=event.target.closest('button'); if(!el)return;
    try {
      if(el.dataset.saasTab){active=el.dataset.saasTab;show();}
      else if(el.hasAttribute('data-go-pricing')) {active='domains';show();container.querySelector('#saas-pricing')?.scrollIntoView({behavior:'smooth'});}
      else if(el.dataset.cycle){cycle=el.dataset.cycle;show();}
      else if(el.dataset.checkout){el.disabled=true;await checkout(el.dataset.checkout);el.disabled=false;}
      else if(el.hasAttribute('data-copy')){await navigator.clipboard.writeText(el.dataset.copy);showToast('Đã sao chép.','success');}
      else if(el.hasAttribute('data-delete-location')){await saveLocations(status.quickLocations.filter((_,i)=>i!==Number(el.dataset.deleteLocation)));}
      else if(el.hasAttribute('data-reset-locations')) await saveLocations(['Phòng Studio A','Phòng Studio B','Ngoại cảnh','Phim trường']);
      else if(el.hasAttribute('data-verify-domain')){el.disabled=true;const result=await api('/api/saas/domains/verify',{method:'POST'});showToast(result.message,result.verified?'success':'info');await load();}
      else if(el.hasAttribute('data-add-service')) container.querySelector('[data-service-rows]').insertAdjacentHTML('beforeend',serviceRow());
      else if(el.hasAttribute('data-remove-service')) el.closest('.saas-service-row').remove();
      else if(el.dataset.revokeInvite) {await api('/api/saas/invitations/'+encodeURIComponent(el.dataset.revokeInvite),{method:'DELETE'});await load();}
      else if(el.hasAttribute('data-reload-saas')) await load();
    } catch(error){el.disabled=false;showToast(error.message,'error');}
  };
  const onSubmit=async event=>{
    const form=event.target;
    if(!form.matches('[data-location-form],[data-domains-form],[data-services-form],[data-preferences-form],[data-invite-form]'))return;
    event.preventDefault(); event.stopPropagation(); const button=form.querySelector('[type=submit]')||form.querySelector('button:not([type=button])');if(button)button.disabled=true;
    try {
      if(form.matches('[data-invite-form]')){await api('/api/saas/invitations',{method:'POST',body:Object.fromEntries(new FormData(form))});showToast('Đã gửi lời mời qua email.','success');await load();}
      else if(form.matches('[data-location-form]'))await saveLocations([...status.quickLocations,form.elements.location.value.trim()]);
      else if(form.matches('[data-domains-form]')){
        const result=await api('/api/saas/domains',{method:'PUT',body:{slug:form.elements.slug.value,customDomain:status.currentPlan==='Free'?null:form.elements.customDomain.value}});
        showToast('Đã lưu đường dẫn & tên miền.','success');
        if(tenantPrefix&&result.slug&&tenantPrefix!=='/s/'+result.slug){location.assign('/s/'+result.slug+'/admin/settings#domains');return;}await load();
      } else {
        const body=form.matches('[data-services-form]')?{serviceTypes:[...form.querySelectorAll('.saas-service-row')].map(r=>({name:r.querySelector('[name=serviceName]').value.trim(),color:r.querySelector('[name=serviceColor]').value}))}:Object.fromEntries(new FormData(form));
        await api('/api/saas/preferences',{method:'PUT',body});showToast('Đã lưu cài đặt.','success');await load();
      }
    }catch(error){showToast(error.message,'error');}finally{if(button)button.disabled=false;}
  };
  const onInput=event=>{
    if(event.target.name!=='slug')return;clearTimeout(timer);const value=event.target.value;
    timer=setTimeout(async()=>{const node=container.querySelector('[data-slug-status]');try{const result=await api('/api/saas/domains/slug-available?slug='+encodeURIComponent(value));if(node&&container.querySelector('[name=slug]')?.value===value)node.textContent=result.available?'Tên định danh có thể sử dụng.':'Tên định danh đã có studio sử dụng.';}catch(error){if(node)node.textContent=error.message;}},350);
  };
  const onChange=async event=>{
    if(!event.target.matches('[data-restore]'))return;const file=event.target.files[0];if(!file)return;
    try{if(file.size>20*1024*1024)throw new Error('Tệp tối đa 20 MB.');const data=JSON.parse(await file.text());if(!await confirmDialog({title:'Phục hồi bản sao lưu?',message:'Các album trùng mã sẽ được cập nhật. Những album khác được giữ nguyên.',confirmLabel:'Phục hồi'}))return;await api('/api/saas/backup/albums/restore',{method:'POST',body:data});showToast('Đã phục hồi metadata album.','success');}catch(error){showToast(error.message,'error');}
  };
  holder.addEventListener('click',onClick);holder.addEventListener('submit',onSubmit);holder.addEventListener('input',onInput);holder.addEventListener('change',onChange);
  show();load();
  return ()=>{disposed=true;clearTimeout(timer);clearInterval(polling);stream?.close();holder.removeEventListener('click',onClick);holder.removeEventListener('submit',onSubmit);holder.removeEventListener('input',onInput);holder.removeEventListener('change',onChange);};
}

export function acceptInvitationView() {
  return {
    title:'Nhận lời mời studio', bodyClass:'page-login',
    html:`<main class="fatal-page"><div class="saas-panel"><h1>Tham gia studio</h1><form data-accept-invite><label class="field">Tên hiển thị<input name="displayName" required maxlength="60"></label><label class="field">Tên đăng nhập<input name="username" required pattern="[a-z0-9._-]{3,32}" minlength="3" maxlength="32" autocomplete="username"></label><label class="field">Mật khẩu mới<input type="password" name="password" required minlength="8" maxlength="128" autocomplete="new-password"></label><p>Tên đăng nhập gồm 3–32 chữ thường, số, dấu chấm, gạch ngang hoặc gạch dưới. Mật khẩu tối thiểu 8 ký tự.</p><button class="btn btn--primary">Tạo tài khoản & tham gia</button><p data-invite-error role="alert"></p></form></div></main>`,
    mount(root){const form=root.querySelector('form');form.addEventListener('submit',async e=>{e.preventDefault();const button=form.querySelector('button');button.disabled=true;try{await api('/api/saas/invitations/accept',{method:'POST',body:{...Object.fromEntries(new FormData(form)),token:new URLSearchParams(location.search).get('token')||''}});location.replace(tenantPath('/admin'));}catch(error){form.querySelector('[data-invite-error]').textContent=error.message;button.disabled=false;}});}
  };
}

function accountPage(title, lead, content) {
  return `<main class="fatal-page"><div class="saas-panel" style="max-width:560px"><a class="wordmark" href="/">StudioCore<span>.</span></a><h1>${title}</h1><p>${lead}</p>${content}<p><a href="/admin/login">← Quay lại đăng nhập</a></p></div></main>`;
}

export function verifyEmailView() {
  const token = new URLSearchParams(location.search).get('token') || '';
  const identifier = new URLSearchParams(location.search).get('identifier') || '';
  return {
    title:'Xác thực email', bodyClass:'page-login',
    html:accountPage('Xác thực email', token ? 'StudioCore đang kiểm tra liên kết của bạn.' : 'Nhập email hoặc tên đăng nhập để nhận liên kết xác thực mới.', token
      ? '<p data-account-message role="status">Đang xác thực…</p>'
      : `<form data-resend-verification><label class="field">Email hoặc tên đăng nhập<input name="identifier" value="${identifier.replaceAll('&','&amp;').replaceAll('"','&quot;').replaceAll('<','&lt;')}" required minlength="3" maxlength="160" autocomplete="username"></label><button class="btn btn--primary">Gửi liên kết xác thực</button><p data-account-message role="status"></p></form>`),
    async mount(root){
      const message=root.querySelector('[data-account-message]');
      if(token){try{await api('/api/auth/verify-email',{method:'POST',body:{token}});message.textContent='Email đã được xác thực. Bạn có thể đăng nhập ngay.';}catch(error){message.textContent=error.message;}}
      else root.querySelector('form').addEventListener('submit',async event=>{event.preventDefault();const button=event.currentTarget.querySelector('button');button.disabled=true;try{await api('/api/auth/verification-email',{method:'POST',body:Object.fromEntries(new FormData(event.currentTarget))});message.textContent='Nếu tài khoản cần xác thực, StudioCore đã gửi một email mới.';}catch(error){message.textContent=error.message;}finally{button.disabled=false;}});
    }
  };
}

export function forgotPasswordView() {
  return {
    title:'Quên mật khẩu', bodyClass:'page-login',
    html:accountPage('Quên mật khẩu', 'Nhập email hoặc tên đăng nhập. Vì lý do bảo mật, StudioCore luôn trả cùng một thông báo.', '<form data-forgot-password><label class="field">Email hoặc tên đăng nhập<input name="identifier" required minlength="3" maxlength="160" autocomplete="username" autofocus></label><button class="btn btn--primary">Gửi liên kết đặt lại mật khẩu</button><p data-account-message role="status"></p></form>'),
    mount(root){const form=root.querySelector('form');form.addEventListener('submit',async event=>{event.preventDefault();const button=form.querySelector('button');const message=form.querySelector('[data-account-message]');button.disabled=true;try{await api('/api/auth/forgot-password',{method:'POST',body:Object.fromEntries(new FormData(form))});message.textContent='Nếu tài khoản tồn tại, liên kết đặt lại mật khẩu đã được gửi và có hiệu lực 30 phút.';}catch(error){message.textContent=error.message;}finally{button.disabled=false;}});}
  };
}

export function resetPasswordView() {
  const token = new URLSearchParams(location.search).get('token') || '';
  return {
    title:'Đặt lại mật khẩu', bodyClass:'page-login',
    html:accountPage('Đặt lại mật khẩu', 'Mật khẩu mới cần từ 8 đến 128 ký tự. Tất cả phiên đăng nhập cũ sẽ bị thu hồi.', '<form data-reset-password><label class="field">Mật khẩu mới<input type="password" name="newPassword" required minlength="8" maxlength="128" autocomplete="new-password" autofocus></label><label class="field">Nhập lại mật khẩu<input type="password" name="confirmPassword" required minlength="8" maxlength="128" autocomplete="new-password"></label><button class="btn btn--primary">Lưu mật khẩu mới</button><p data-account-message role="status"></p></form>'),
    mount(root){const form=root.querySelector('form');form.addEventListener('submit',async event=>{event.preventDefault();const data=Object.fromEntries(new FormData(form));const message=form.querySelector('[data-account-message]');if(!token){message.textContent='Liên kết không có token đặt lại mật khẩu.';return;}if(data.newPassword!==data.confirmPassword){message.textContent='Hai mật khẩu chưa trùng nhau.';return;}const button=form.querySelector('button');button.disabled=true;try{await api('/api/auth/reset-password',{method:'POST',body:{token,newPassword:data.newPassword}});message.textContent='Đã đổi mật khẩu và thu hồi các phiên cũ. Bạn có thể đăng nhập bằng mật khẩu mới.';form.reset();}catch(error){message.textContent=error.message;}finally{button.disabled=false;}});}
  };
}
