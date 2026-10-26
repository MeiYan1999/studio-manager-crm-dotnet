export class ApiError extends Error {
  constructor(message, status, code) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
  }
}

export async function api(path, options = {}) {
  const init = {
    method: options.method || 'GET',
    credentials: 'same-origin',
    headers: {
      Accept: 'application/json',
      ...(options.headers || {})
    },
    signal: options.signal
  };

  if (options.body !== undefined) {
    init.headers['Content-Type'] = 'application/json';
    init.body = JSON.stringify(options.body);
  }

  let response;
  try {
    response = await fetch(path, init);
  } catch (error) {
    if (error.name === 'AbortError') throw error;
    throw new ApiError('Không thể kết nối máy chủ. Vui lòng kiểm tra mạng và thử lại.', 0, 'network_error');
  }

  const contentType = response.headers.get('content-type') || '';
  let payload = null;
  if (contentType.includes('application/json')) {
    payload = await response.json().catch(() => null);
  } else if (response.status !== 204) {
    payload = await response.text().catch(() => null);
  }

  if (!response.ok) {
    if (response.status === 401 && !path.includes('/api/auth/login')) {
      window.dispatchEvent(new CustomEvent('may:unauthorized'));
    }

    const message = payload && typeof payload === 'object'
      ? payload.error || 'Yêu cầu chưa thực hiện được.'
      : 'Yêu cầu chưa thực hiện được.';
    throw new ApiError(message, response.status, payload?.code);
  }

  return payload;
}

async function uploadForm(path, form) {
  let response;
  try {
    response = await fetch(path, {
      method: 'POST',
      credentials: 'same-origin',
      headers: { Accept: 'application/json' },
      body: form
    });
  } catch (error) {
    if (error.name === 'AbortError') throw error;
    throw new ApiError('Không thể kết nối máy chủ. Vui lòng kiểm tra mạng và thử lại.', 0, 'network_error');
  }

  const contentType = response.headers.get('content-type') || '';
  const payload = contentType.includes('application/json')
    ? await response.json().catch(() => null)
    : null;

  if (!response.ok) {
    if (response.status === 401) {
      window.dispatchEvent(new CustomEvent('may:unauthorized'));
    }
    const message = payload && typeof payload === 'object'
      ? payload.error || 'Tệp chưa tải lên được.'
      : 'Tệp chưa tải lên được.';
    throw new ApiError(message, response.status, payload?.code);
  }

  return payload;
}

export const authApi = {
  session: () => api('/api/auth/session'),
  login: (username, password) => api('/api/auth/login', {
    method: 'POST',
    body: { username, password }
  }),
  changePassword: (currentPassword, newPassword) => api('/api/auth/change-password', {
    method: 'POST',
    body: { currentPassword, newPassword }
  }),
  logout: () => api('/api/auth/logout', { method: 'POST' })
};

export const crmApi = {
  summary: () => api('/api/admin/crm/summary'),
  audit: (params = {}) => {
    const query = new URLSearchParams();
    Object.entries(params).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== '') query.set(key, value);
    });
    const suffix = query.toString();
    return api(`/api/admin/crm/audit${suffix ? `?${suffix}` : ''}`);
  },
  importBackup: (json, { dryRun = true, replaceExisting = false } = {}) => api('/api/admin/crm/import', {
    method: 'POST',
    body: { json, dryRun, replaceExisting }
  }),
  customers: () => api('/api/admin/customers'),
  customer: id => api(`/api/admin/customers/${encodeURIComponent(id)}`),
  createCustomer: body => api('/api/admin/customers', { method: 'POST', body }),
  updateCustomer: (id, body) => api(`/api/admin/customers/${encodeURIComponent(id)}`, { method: 'PUT', body }),
  deleteCustomer: id => api(`/api/admin/customers/${encodeURIComponent(id)}`, { method: 'DELETE' }),
  packages: () => api('/api/admin/packages'),
  createPackage: body => api('/api/admin/packages', { method: 'POST', body }),
  updatePackage: (id, body) => api(`/api/admin/packages/${encodeURIComponent(id)}`, { method: 'PUT', body }),
  deletePackage: id => api(`/api/admin/packages/${encodeURIComponent(id)}`, { method: 'DELETE' }),
  staff: () => api('/api/admin/staff'),
  createStaff: body => api('/api/admin/staff', { method: 'POST', body }),
  updateStaff: (id, body) => api(`/api/admin/staff/${encodeURIComponent(id)}`, { method: 'PUT', body }),
  deleteStaff: id => api(`/api/admin/staff/${encodeURIComponent(id)}`, { method: 'DELETE' }),
  bookings: () => api('/api/admin/bookings'),
  booking: id => api(`/api/admin/bookings/${encodeURIComponent(id)}`),
  createBooking: body => api('/api/admin/bookings', { method: 'POST', body }),
  updateBooking: (id, body) => api(`/api/admin/bookings/${encodeURIComponent(id)}`, { method: 'PUT', body }),
  updateBookingStatus: (id, status) => api(`/api/admin/bookings/${encodeURIComponent(id)}/status`, {
    method: 'PUT',
    body: { status }
  }),
  deleteBooking: id => api(`/api/admin/bookings/${encodeURIComponent(id)}`, { method: 'DELETE' })
};

export const salesApi = {
  orders: () => api('/api/admin/orders'),
  order: id => api(`/api/admin/orders/${encodeURIComponent(id)}`),
  createOrder: body => api('/api/admin/orders', { method: 'POST', body }),
  updateOrder: (id, body) => api(`/api/admin/orders/${encodeURIComponent(id)}`, { method: 'PUT', body }),
  updateOrderStatus: (id, status) => api(`/api/admin/orders/${encodeURIComponent(id)}/status`, {
    method: 'PUT',
    body: { status }
  }),
  deleteOrder: id => api(`/api/admin/orders/${encodeURIComponent(id)}`, { method: 'DELETE' }),
  invoice: id => api(`/api/admin/orders/${encodeURIComponent(id)}/invoice`),
  recordPayment: (id, body) => api(`/api/admin/orders/${encodeURIComponent(id)}/payments`, {
    method: 'POST',
    body
  }),
  deletePayment: (id, paymentId) => api(
    `/api/admin/orders/${encodeURIComponent(id)}/payments/${encodeURIComponent(paymentId)}`,
    { method: 'DELETE' }
  ),
  contracts: () => api('/api/admin/contracts'),
  exportContractsUrl: (status, q) => {
    const query = new URLSearchParams();
    if (status && status !== 'all') query.set('status', status);
    if (q) query.set('q', q);
    const suffix = query.toString();
    return `/api/admin/contracts/export${suffix ? `?${suffix}` : ''}`;
  },
  contract: id => api(`/api/admin/contracts/${encodeURIComponent(id)}`),
  createContract: body => api('/api/admin/contracts', { method: 'POST', body }),
  updateContract: (id, body) => api(`/api/admin/contracts/${encodeURIComponent(id)}`, { method: 'PUT', body }),
  toggleSchedule: (id, index, paid) => api(
    `/api/admin/contracts/${encodeURIComponent(id)}/schedule/${index}`,
    { method: 'PUT', body: { paid } }
  ),
  deleteContract: id => api(`/api/admin/contracts/${encodeURIComponent(id)}`, { method: 'DELETE' }),
  reportSummary: (params = {}) => {
    const query = new URLSearchParams();
    Object.entries(params).forEach(([key, value]) => {
      if (value !== undefined && value !== null && value !== '') query.set(key, value);
    });
    const suffix = query.toString();
    return api(`/api/admin/reports/summary${suffix ? `?${suffix}` : ''}`);
  },
  overview: () => api('/api/admin/reports/overview')
};

export const boardApi = {
  context: () => api('/api/admin/board/context')
};

export const operationsApi = {
  financeOverview: month => api(`/api/admin/finance/overview${month ? `?month=${encodeURIComponent(month)}` : ''}`),
  financeEntries: (month, kind) => {
    const query = new URLSearchParams();
    if (month) query.set('month', month);
    if (kind && kind !== 'all') query.set('kind', kind);
    const suffix = query.toString();
    return api(`/api/admin/finance/entries${suffix ? `?${suffix}` : ''}`);
  },
  createFinanceEntry: body => api('/api/admin/finance/entries', { method: 'POST', body }),
  updateFinanceEntry: (id, body) => api(`/api/admin/finance/entries/${encodeURIComponent(id)}`, {
    method: 'PUT',
    body
  }),
  deleteFinanceEntry: id => api(`/api/admin/finance/entries/${encodeURIComponent(id)}`, { method: 'DELETE' }),
  uploadFinancePhoto: (id, file, caption = '') => {
    const form = new FormData();
    form.append('file', file);
    if (caption) form.append('caption', caption);
    return uploadForm(`/api/admin/finance/entries/${encodeURIComponent(id)}/photos`, form);
  },
  deleteFinancePhoto: (id, photoId) => api(
    `/api/admin/finance/entries/${encodeURIComponent(id)}/photos/${encodeURIComponent(photoId)}`,
    { method: 'DELETE' }
  ),
  exportFinanceEntriesUrl: (month, kind) => {
    const query = new URLSearchParams();
    if (month) query.set('month', month);
    if (kind && kind !== 'all') query.set('kind', kind);
    const suffix = query.toString();
    return `/api/admin/finance/entries/export${suffix ? `?${suffix}` : ''}`;
  },
  exportFinanceWorkbookUrl: month =>
    `/api/admin/finance/export${month ? `?month=${encodeURIComponent(month)}` : ''}`,
  expenses: month => api(`/api/admin/expenses${month ? `?month=${encodeURIComponent(month)}` : ''}`),
  createExpense: body => api('/api/admin/expenses', { method: 'POST', body }),
  updateExpense: (id, body) => api(`/api/admin/expenses/${encodeURIComponent(id)}`, { method: 'PUT', body }),
  deleteExpense: id => api(`/api/admin/expenses/${encodeURIComponent(id)}`, { method: 'DELETE' }),
  payrolls: month => api(`/api/admin/payrolls${month ? `?month=${encodeURIComponent(month)}` : ''}`),
  computePayrolls: month => api('/api/admin/payrolls/compute', { method: 'POST', body: { month } }),
  togglePayrollPaid: (id, paid) => api(`/api/admin/payrolls/${encodeURIComponent(id)}/paid`, {
    method: 'PUT',
    body: { paid }
  }),
  attendance: date => api(`/api/admin/attendance${date ? `?date=${encodeURIComponent(date)}` : ''}`),
  attendanceMonth: month => api(`/api/admin/attendance/month${month ? `?month=${encodeURIComponent(month)}` : ''}`),
  createAttendance: body => api('/api/admin/attendance', { method: 'POST', body }),
  updateAttendance: (id, body) => api(`/api/admin/attendance/${encodeURIComponent(id)}`, { method: 'PUT', body }),
  deleteAttendance: id => api(`/api/admin/attendance/${encodeURIComponent(id)}`, { method: 'DELETE' }),
  shifts: week => api(`/api/admin/shifts${week ? `?week=${encodeURIComponent(week)}` : ''}`),
  saveShift: body => api('/api/admin/shifts', { method: 'PUT', body }),
  deleteShift: (staffId, date) => api(
    `/api/admin/shifts?staffId=${encodeURIComponent(staffId)}&date=${encodeURIComponent(date)}`,
    { method: 'DELETE' }
  ),
  fillShifts: week => api('/api/admin/shifts/fill', { method: 'POST', body: { week } }),
  clearShifts: week => api(`/api/admin/shifts/week?week=${encodeURIComponent(week)}`, { method: 'DELETE' }),
  outfits: () => api('/api/admin/outfits'),
  createOutfit: body => api('/api/admin/outfits', { method: 'POST', body }),
  updateOutfit: (id, body) => api(`/api/admin/outfits/${encodeURIComponent(id)}`, { method: 'PUT', body }),
  deleteOutfit: id => api(`/api/admin/outfits/${encodeURIComponent(id)}`, { method: 'DELETE' }),
  concepts: () => api('/api/admin/concepts'),
  createConcept: body => api('/api/admin/concepts', { method: 'POST', body }),
  updateConcept: (id, body) => api(`/api/admin/concepts/${encodeURIComponent(id)}`, { method: 'PUT', body }),
  deleteConcept: id => api(`/api/admin/concepts/${encodeURIComponent(id)}`, { method: 'DELETE' }),
  loans: status => api(`/api/admin/loans${status ? `?status=${encodeURIComponent(status)}` : ''}`),
  createLoan: body => api('/api/admin/loans', { method: 'POST', body }),
  updateLoan: (id, body) => api(`/api/admin/loans/${encodeURIComponent(id)}`, { method: 'PUT', body }),
  returnLoan: (id, returnDate) => api(`/api/admin/loans/${encodeURIComponent(id)}/return`, {
    method: 'PUT',
    body: { returnDate }
  }),
  deleteLoan: id => api(`/api/admin/loans/${encodeURIComponent(id)}`, { method: 'DELETE' }),
  notifications: () => api('/api/admin/notifications'),
  markNotificationRead: id => api(`/api/admin/notifications/${encodeURIComponent(id)}/read`, { method: 'PUT' }),
  markAllNotificationsRead: () => api('/api/admin/notifications/read-all', { method: 'POST' }),
  deleteNotification: id => api(`/api/admin/notifications/${encodeURIComponent(id)}`, { method: 'DELETE' }),
  notificationSettings: () => api('/api/admin/notifications/settings'),
  updateNotificationSettings: body => api('/api/admin/notifications/settings', { method: 'PUT', body })
};
