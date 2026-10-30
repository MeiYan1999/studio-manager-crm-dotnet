// Copy in bounded chunks so large RAW files do not have to be loaded into memory.
// Each stage is tagged for useful diagnostics without exposing local paths.
export async function copyPhotoFile(sourceHandle, outputDirectory, targetName, chunkSize = 4 * 1024 * 1024) {
  let stage = 'đọc ảnh gốc';
  let writable;
  try {
    const file = await sourceHandle.getFile();
    stage = 'tạo tệp đích';
    const target = await outputDirectory.getFileHandle(targetName, { create: true });
    stage = 'mở quyền ghi';
    writable = await target.createWritable();
    for (let offset = 0; offset < file.size; offset += chunkSize) {
      stage = 'đọc dữ liệu ảnh';
      const chunk = await file.slice(offset, Math.min(offset + chunkSize, file.size)).arrayBuffer();
      stage = 'ghi dữ liệu ảnh';
      await writable.write(chunk);
    }
    stage = 'lưu tệp đích';
    await writable.close();
  } catch (cause) {
    if (writable) {
      try { await writable.abort(); } catch { /* Keep the original error. */ }
    }
    const error = new Error(stage);
    error.stage = stage;
    error.originalName = cause?.name || 'Error';
    throw error;
  }
}

export function describePhotoCopyError(error) {
  const cause = error?.originalName || error?.name || 'Error';
  const reasons = {
    NotAllowedError: 'Trình duyệt hoặc hệ điều hành từ chối quyền truy cập.',
    SecurityError: 'Trình duyệt chặn thao tác với tệp.',
    NotReadableError: 'Không đọc được ảnh gốc; hãy kiểm tra tệp có sẵn trên máy và không bị ứng dụng khác khóa.',
    NotFoundError: 'Tệp hoặc thư mục không còn tồn tại.',
    QuotaExceededError: 'Ổ đĩa không đủ dung lượng hoặc đã vượt giới hạn lưu trữ.',
    NoModificationAllowedError: 'Không thể ghi vào thư mục đích hoặc tệp đang bị khóa.',
    InvalidModificationError: 'Không thể tạo hoặc thay thế tệp đích.',
    AbortError: 'Thao tác ghi bị hủy.',
    TypeError: 'Trình duyệt không hỗ trợ thao tác sao chép này.'
  };
  return `${error?.stage || 'sao chép'} (${cause}): ${reasons[cause] || 'Không thể hoàn tất thao tác; hãy thử thư mục đích khác.'}`;
}
