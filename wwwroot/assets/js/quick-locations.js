import { api } from './api.js';
import { escapeHtml, escapeAttr } from './ui.js';

export async function mountQuickLocations(form, selector) {
  const input=form.querySelector(selector); if(!input)return;
  try {
    const status=await api('/api/saas/status');
    if(!form.isConnected)return;
    const chips=document.createElement('div'); chips.className='quick-location-chips';
    chips.innerHTML=(status.quickLocations||[]).map(v=>`<button type="button" data-location="${escapeAttr(v)}">${escapeHtml(v)}</button>`).join('');
    chips.addEventListener('click',e=>{const btn=e.target.closest('[data-location]');if(btn){input.value=btn.dataset.location;input.dispatchEvent(new Event('change',{bubbles:true}));}});
    input.after(chips);
  } catch { /* Free-form location input still works during API outages. */ }
}
