import { api } from './api.js';
import {
  icon,
  escapeHtml,
  escapeAttr,
  copyText,
  showToast,
  setButtonLoading
} from './ui.js';
import { adminShell, mountAdminShell, pageHead } from './admin.js';

const RAW_EXTENSIONS = new Set([
  '3fr', 'ari', 'arw', 'bay', 'cr2', 'cr3', 'crw', 'dcr', 'dng', 'erf',
  'fff', 'gpr', 'iiq', 'k25', 'kdc', 'mef', 'mos', 'mrw', 'nef', 'nrw',
  'orf', 'pef', 'raf', 'raw', 'rw2', 'rwl', 'sr2', 'srf', 'srw', 'x3f'
]);
const JPG_EXTENSIONS = new Set(['jpg', 'jpeg', 'png']);
const KNOWN_EXTENSIONS = new Set([
  ...RAW_EXTENSIONS,
  ...JPG_EXTENSIONS,
  'avif', 'bmp', 'gif', 'heic', 'heif', 'tif', 'tiff', 'webp'
]);

function number(value) {
  return Number(value || 0).toLocaleString('vi-VN');
}

export function extensionOf(fileName) {
  const match = /\.([^./\\]+)$/.exec(String(fileName ?? '').trim());
  return match ? match[1].toLowerCase() : '';
}

function removeKnownExtension(value) {
  const text = String(value ?? '').trim();
  const extension = extensionOf(text);
  if (!extension || !KNOWN_EXTENSIONS.has(extension)) return text;
  return text.slice(0, -(extension.length + 1));
}

function normalizeAlpha(value) {
  return String(value ?? '')
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[đĐ]/g, 'd')
    .toLowerCase()
    .replace(/[^a-z0-9]/g, '');
}

function numericKeys(value) {
  const base = removeKnownExtension(value);
  const groups = base.match(/\d+/g) || [];
  const keys = new Set();
  for (const group of groups) {
    keys.add(group);
    const trimmed = group.replace(/^0+(?=\d)/, '');
    if (trimmed) keys.add(trimmed);
  }
  const joined = groups.join('');
  if (joined) {
    keys.add(joined);
    keys.add(joined.replace(/^0+(?=\d)/, ''));
  }
  return keys;
}

function alphaFileCandidates(fileName) {
  const base = removeKnownExtension(fileName);
  const normalized = base
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/[đĐ]/g, 'd')
    .toLowerCase();
  const groups = normalized.split(/[^a-z0-9]+/).filter(Boolean);
  const candidates = new Set();
  for (let start = 0; start < groups.length; start++) {
    let joined = '';
    for (let end = start; end < groups.length; end++) {
      joined += groups[end];
      candidates.add(joined);
    }
  }
  return candidates;
}

export function parsePhotoCodes(input) {
  const tokens = String(input ?? '')
    .replace(/\r/g, '\n')
    .split(/[\n,;|\t ]+/)
    .map(token => token.trim())
    .filter(Boolean);
  const seen = new Set();
  const codes = [];

  for (let token of tokens) {
    token = token
      .replace(/^[•*·]+/, '')
      .replace(/^["'“”‘’]+|["'“”‘’]+$/g, '')
      .trim();
    if (!token) continue;

    const display = removeKnownExtension(token);
    const key = normalizeAlpha(display);
    if (!key || seen.has(key)) continue;
    seen.add(key);
    codes.push(display);
  }

  return codes;
}

export function countValidCodes(codes, mode = 'alphanumeric') {
  if (mode === 'numeric') {
    return codes.filter(code => numericKeys(code).size > 0).length;
  }
  return codes.filter(code => normalizeAlpha(code)).length;
}

export function createCodeMatcher(codes, mode = 'alphanumeric') {
  const list = Array.isArray(codes) ? codes : parsePhotoCodes(codes);

  if (mode === 'numeric') {
    const keys = new Set();
    for (const code of list) {
      for (const key of numericKeys(code)) keys.add(key);
    }
    return {
      mode,
      keys,
      matches(fileName) {
        if (!keys.size) return false;
        for (const key of numericKeys(fileName)) {
          if (keys.has(key)) return true;
        }
        return false;
      }
    };
  }

  const keys = new Set(list.map(normalizeAlpha).filter(Boolean));
  return {
    mode,
    keys,
    matches(fileName) {
      if (!keys.size) return false;
      const candidates = alphaFileCandidates(fileName);
      for (const key of keys) {
        if (candidates.has(key)) return true;
      }
      return false;
    }
  };
}

export function fileMatchesFormat(fileName, format = 'all') {
  const extension = extensionOf(fileName);
  if (format === 'raw') return RAW_EXTENSIONS.has(extension);
  if (format === 'jpg') return JPG_EXTENSIONS.has(extension);
  return RAW_EXTENSIONS.has(extension) || JPG_EXTENSIONS.has(extension);
}

export function sanitizeFolderName(value) {
  const clean = String(value ?? '')
    .replace(/[<>:"/\\|?*\u0000-\u001f]/g, '-')
    .replace(/\s+/g, ' ')
    .replace(/^[\s.-]+|[\s.-]+$/g, '')
    .slice(0, 80)
    .replace(/[\s.-]+$/g, '');
  if (!clean) return '';
  const reserved = /^(con|prn|aux|nul|com[1-9]|lpt[1-9])(\..*)?$/i.exec(clean);
  if (reserved) return `${reserved[1]}-anh${reserved[2] || ''}`;
  return clean;
}

function uniqueFileName(fileName, usedNames) {
  if (!usedNames.has(fileName)) {
    usedNames.add(fileName);
    return fileName;
  }

  const match = /\.([^./\\]+)$/.exec(fileName);
  const extension = match ? match[1] : '';
  const base = match ? fileName.slice(0, match.index) : fileName;
  let counter = 2;
  let candidate = `${base}-${counter}${extension ? `.${extension}` : ''}`;
  while (usedNames.has(candidate)) {
    counter++;
    candidate = `${base}-${counter}${extension ? `.${extension}` : ''}`;
  }
  usedNames.add(candidate);
  return candidate;
}

function buildCopyText(codes, format, sourceFileNames) {
  if (format === 'comma') return codes.join(', ');
  if (format === 'space') return codes.join(' ');
  if (format === 'file') {
    return codes.map(code => {
      const fileName = sourceFileNames.get(normalizeAlpha(code));
      if (fileName) return fileName;
      return `${code}.jpg`;
    }).join('\n');
  }
  return codes.join('\n');
}

function copyChip(format, label, iconName) {
  const active = format === 'newline';
  return `
    <button class="filter-copy__chip ${active ? 'is-active' : ''}" type="button" data-copy-format="${format}" aria-pressed="${active}">
      ${icon(iconName)}<span>${label}</span>
    </button>
  `;
}

export function photoFilterView({ session }) {
  const supported = typeof window.showDirectoryPicker === 'function';
  const html = adminShell({
    active: 'tools',
    session,
    content: `
      <div class="admin-page__inner admin-page__inner--tool" data-photo-filter-page>
        ${pageHead({
          eyebrow: 'Công cụ studio',
          title: 'Lọc ảnh theo mã khách chọn.',
          lead: 'Chọn album để lấy mã hình khách đã thả tim, rồi lọc đúng những ảnh đó từ thư mục RAW/JPG trên máy tính. Ảnh chỉ được xử lý trên máy của bạn.',
          back: { href: '/admin', label: 'Về tổng quan' },
          actions: `
            <a class="btn btn--ghost" href="/admin/albums">${icon('albums')} Album của bạn</a>
            <span class="pill ${supported ? 'pill--live' : 'pill--warn'}">${supported ? '<span class="pill__dot"></span>Chạy trên máy' : `${icon('alertCircle')} Cần Chrome hoặc Edge`}</span>
          `
        })}

        <div class="filter-tool" data-filter-tool>
          <section class="panel filter-panel filter-panel--codes">
            <header class="filter-panel__head">
              <span class="filter-step">1</span>
              <div class="filter-panel__heading">
                <p class="eyebrow">Mã khách đã chọn</p>
                <h2>Mã ảnh khách chọn để lọc</h2>
              </div>
              <span class="filter-count" data-code-count aria-live="polite">0 mã hợp lệ</span>
            </header>
            <div class="filter-panel__body">
              <div class="field filter-field">
                <label for="filter-album">Chọn nguồn khách hàng / album / hợp đồng</label>
                <div class="filter-select">
                  <span class="filter-select__icon">${icon('albums')}</span>
                  <select id="filter-album" data-album-select disabled>
                    <option>Đang tải album…</option>
                  </select>
                  <span class="filter-select__chevron">${icon('chevronDown')}</span>
                </div>
                <p class="field__hint" data-album-status>Đang tải danh sách album…</p>
              </div>

              <div class="filter-mode">
                <div class="filter-mode__copy">
                  <strong>Chế độ so khớp</strong>
                  <span data-match-hint>Tự động nhận diện chữ và số, có hay không đuôi file.</span>
                </div>
                <div class="segmented segmented--sm filter-mode__control" role="group" aria-label="Chế độ so khớp mã">
                  <button class="segmented__item is-active" type="button" data-match-mode="alphanumeric" aria-pressed="true">Chữ và số</button>
                  <button class="segmented__item" type="button" data-match-mode="numeric" aria-pressed="false">Chỉ lấy số</button>
                </div>
              </div>

              <div class="filter-copy">
                <div class="filter-label-row">
                  <span>Sao chép nhanh</span>
                  <small>Bấm một định dạng để sao chép ngay</small>
                </div>
                <div class="filter-copy__chips">
                  ${copyChip('newline', 'Xuống dòng', 'credit')}
                  ${copyChip('comma', 'Dấu phẩy', 'file')}
                  ${copyChip('space', 'Dấu cách', 'more')}
                  ${copyChip('file', 'Có đuôi file', 'image')}
                </div>
              </div>

              <div class="field filter-field">
                <div class="filter-label-row">
                  <label for="filter-codes">Danh sách mã cần lọc</label>
                  <button class="filter-copy__main" type="button" data-copy-codes disabled>${icon('copy')} Sao chép</button>
                </div>
                <textarea id="filter-codes" class="filter-codes" data-code-input rows="8" spellcheck="false" placeholder="Dán mã ảnh vào đây hoặc chọn album ở trên…"></textarea>
                <p class="field__hint" data-code-hint>Hỗ trợ xuống dòng, dấu phẩy, dấu cách và tên tệp có đuôi .jpg, .cr3…</p>
              </div>

              <div class="filter-album-note" data-album-note hidden></div>
            </div>
          </section>

          <section class="panel filter-panel filter-panel--folder">
            <header class="filter-panel__head">
              <span class="filter-step filter-step--olive">2</span>
              <div class="filter-panel__heading">
                <p class="eyebrow">Thư mục máy tính</p>
                <h2>Cấu hình thư mục trên máy tính</h2>
              </div>
              <span class="filter-local-badge">${icon('hardDrive')} Xử lý trên máy</span>
            </header>
            <div class="filter-panel__body">
              <div data-unsupported hidden></div>

              <div class="filter-folder-block">
                <div class="filter-label-row">
                  <div class="filter-label-copy">
                    <strong>Thư mục ảnh gốc</strong>
                    <span>Thư mục chứa RAW, JPG… có thể nằm trong thư mục con.</span>
                  </div>
                  <span class="filter-folder-state" data-source-state>Chưa chọn</span>
                </div>
                <div class="folder-picker" data-source-picker>
                  <span class="folder-picker__icon">${icon('folder')}</span>
                  <div class="folder-picker__copy">
                    <strong data-source-name>Chưa chọn thư mục nào</strong>
                    <span data-source-hint>Chọn thư mục ảnh gốc trên PC</span>
                  </div>
                  <button class="btn btn--primary btn--compact" type="button" data-pick-source>${icon('folder')} Chọn thư mục</button>
                </div>
              </div>

              <div class="filter-folder-block">
                <div class="filter-label-row">
                  <div class="filter-label-copy">
                    <strong>Thư mục lưu ảnh đã lọc</strong>
                    <span>Ảnh gốc được giữ nguyên, chỉ sao chép những ảnh khớp.</span>
                  </div>
                </div>
                <div class="folder-picker folder-picker--output" data-output-picker>
                  <label class="filter-check-row">
                    <input type="checkbox" data-create-subfolder checked>
                    <span class="filter-check-row__box">${icon('check')}</span>
                    <span class="filter-check-row__copy">
                      <strong>Tạo thư mục khách chọn trong thư mục gốc</strong>
                      <small>Mặc định được khuyến nghị</small>
                    </span>
                  </label>
                  <div class="filter-subfolder" data-subfolder-row>
                    <label for="filter-folder-name">Tên thư mục con</label>
                    <input id="filter-folder-name" data-folder-name type="text" maxlength="80" value="Ảnh khách chọn" autocomplete="off">
                  </div>
                  <div class="filter-external-output" data-external-output hidden>
                    <div class="folder-picker__copy">
                      <strong data-output-name>Chưa chọn thư mục lưu</strong>
                      <span>Chọn một thư mục có sẵn trên máy tính.</span>
                    </div>
                    <button class="btn btn--ghost btn--compact" type="button" data-pick-output>${icon('folder')} Chọn thư mục lưu</button>
                  </div>
                </div>
              </div>

              <label class="filter-check-row filter-check-row--standalone">
                <input type="checkbox" data-recursive checked>
                <span class="filter-check-row__box">${icon('check')}</span>
                <span class="filter-check-row__copy">
                  <strong>Quét cả thư mục con</strong>
                  <small>Bỏ chọn nếu chỉ muốn lọc ảnh ở cấp đầu tiên.</small>
                </span>
              </label>

              <div class="filter-folder-block">
                <div class="filter-label-row">
                  <div class="filter-label-copy">
                    <strong>Định dạng file cần lọc</strong>
                    <span>Chỉ các định dạng được chọn mới được sao chép.</span>
                  </div>
                </div>
                <div class="segmented filter-format" role="group" aria-label="Định dạng file cần lọc">
                  <button class="segmented__item is-active" type="button" data-file-format="all" aria-pressed="true">Tất cả <span>RAW &amp; JPG</span></button>
                  <button class="segmented__item" type="button" data-file-format="raw" aria-pressed="false">Chỉ RAW</button>
                  <button class="segmented__item" type="button" data-file-format="jpg" aria-pressed="false">Chỉ JPG/PNG</button>
                </div>
              </div>

              <div class="filter-run">
                <button class="btn btn--primary btn--lg btn--full" type="button" data-start-filter disabled>${icon('filter')} Bắt đầu lọc ảnh từ máy tính</button>
                <p class="filter-run__hint" data-run-hint aria-live="polite">Chọn mã ảnh và thư mục nguồn để bắt đầu.</p>
              </div>

              <div class="filter-progress" data-progress hidden>
                <div class="filter-progress__head">
                  <span>${icon('loader', 'spin')} Đang quét thư mục…</span>
                  <strong data-progress-count>0 tệp</strong>
                </div>
                <div class="filter-progress__bar"><span></span></div>
                <p data-progress-detail>Đang tìm ảnh khớp với mã khách đã chọn.</p>
              </div>

              <div data-filter-result></div>

              <p class="filter-privacy">${icon('lock')} Ảnh được đọc và sao chép trực tiếp trên máy tính. StudioCore không tải ảnh lên máy chủ.</p>
            </div>
          </section>
        </div>

        <div class="filter-footnote">
          <span>${icon('alertCircle')} Trình duyệt chỉ hiển thị tên thư mục, không hiển thị toàn bộ đường dẫn vì lý do bảo mật.</span>
          <span>${icon('checkCircle')} Hỗ trợ RAW: CR2, CR3, NEF, ARW, DNG, RAF, ORF và nhiều định dạng khác.</span>
        </div>
      </div>
    `
  });

  let teardown = () => {};
  let disposed = false;
  let albums = [];
  let selectedAlbum = null;
  let sourceHandle = null;
  let outputHandle = null;
  let matchMode = 'alphanumeric';
  let fileFormat = 'all';
  let copyFormat = 'newline';
  let sourceFileNames = new Map();
  let running = false;
  let lastCopiedNames = [];

  return {
    title: 'Lọc ảnh theo mã',
    bodyClass: 'page-admin',
    html,
    mount(root) {
      teardown = mountAdminShell(root, session);
      const page = root.querySelector('[data-photo-filter-page]');
      const tool = root.querySelector('[data-filter-tool]');
      const albumSelect = page.querySelector('[data-album-select]');
      const albumStatus = page.querySelector('[data-album-status]');
      const albumNote = page.querySelector('[data-album-note]');
      const codeInput = page.querySelector('[data-code-input]');
      const codeCount = page.querySelector('[data-code-count]');
      const codeHint = page.querySelector('[data-code-hint]');
      const matchHint = page.querySelector('[data-match-hint]');
      const copyMain = page.querySelector('[data-copy-codes]');
      const copyChips = [...page.querySelectorAll('[data-copy-format]')];
      const sourcePicker = page.querySelector('[data-source-picker]');
      const sourceName = page.querySelector('[data-source-name]');
      const sourceHint = page.querySelector('[data-source-hint]');
      const sourceState = page.querySelector('[data-source-state]');
      const pickSourceButton = page.querySelector('[data-pick-source]');
      const outputPicker = page.querySelector('[data-output-picker]');
      const outputName = page.querySelector('[data-output-name]');
      const pickOutputButton = page.querySelector('[data-pick-output]');
      const createSubfolder = page.querySelector('[data-create-subfolder]');
      const subfolderRow = page.querySelector('[data-subfolder-row]');
      const folderNameInput = page.querySelector('[data-folder-name]');
      const externalOutput = page.querySelector('[data-external-output]');
      const recursiveInput = page.querySelector('[data-recursive]');
      const formatButtons = [...page.querySelectorAll('[data-file-format]')];
      const startButton = page.querySelector('[data-start-filter]');
      const runHint = page.querySelector('[data-run-hint]');
      const progress = page.querySelector('[data-progress]');
      const progressCount = page.querySelector('[data-progress-count]');
      const progressDetail = page.querySelector('[data-progress-detail]');
      const resultRoot = page.querySelector('[data-filter-result]');

      const parseCodes = () => parsePhotoCodes(codeInput.value);
      const setCopyFormat = format => {
        copyFormat = format;
        copyChips.forEach(chip => {
          const active = chip.dataset.copyFormat === format;
          chip.classList.toggle('is-active', active);
          chip.setAttribute('aria-pressed', String(active));
        });
      };

      const updateAlbumNote = () => {
        if (!selectedAlbum) {
          albumNote.hidden = true;
          albumNote.innerHTML = '';
          albumNote.classList.remove('is-warning');
          return;
        }

        const selectedCount = selectedAlbum.photos.filter(photo => photo.isSelected).length;
        albumNote.hidden = false;
        albumNote.classList.toggle('is-warning', selectedCount === 0);
        albumNote.innerHTML = selectedCount
          ? `
            <span>${icon('heart')} Đã điền ${number(selectedCount)} mã từ album “${escapeHtml(selectedAlbum.title)}”.</span>
            <a href="/admin/albums/${encodeURIComponent(selectedAlbum.id)}">Mở album ${icon('arrowRight')}</a>
          `
          : `
            <span>${icon('alertCircle')} Album “${escapeHtml(selectedAlbum.title)}” chưa có ảnh nào khách chọn. Bạn có thể dán mã thủ công.</span>
            <a href="/admin/albums/${encodeURIComponent(selectedAlbum.id)}">Mở album ${icon('arrowRight')}</a>
          `;
      };

      const updateStartState = () => {
        if (running) return;
        const codes = parseCodes();
        const valid = countValidCodes(codes, matchMode);
        const createInside = createSubfolder.checked;
        const folderName = sanitizeFolderName(folderNameInput.value);
        const missing = [];

        if (!valid) missing.push('mã ảnh');
        if (!sourceHandle) missing.push('thư mục ảnh gốc');
        if (!createInside && !outputHandle) missing.push('thư mục lưu');
        if (createInside && !folderName) missing.push('tên thư mục con');

        const ready = supported && missing.length === 0;
        startButton.disabled = !ready;

        if (!supported) {
          runHint.textContent = 'Cần Chrome hoặc Edge để chọn thư mục trên máy tính.';
          return;
        }

        if (missing.length) {
          runHint.textContent = `Còn thiếu: ${missing.join(', ')}.`;
          return;
        }

        const destination = createInside
          ? `${sourceHandle.name} / ${folderName}`
          : outputHandle.name;
        runHint.textContent = `Sẵn sàng lọc ${valid} mã vào “${destination}”.`;
      };

      const updateCodeUi = () => {
        const codes = parseCodes();
        const valid = countValidCodes(codes, matchMode);
        codeCount.textContent = `${number(valid)} mã hợp lệ`;
        codeCount.classList.toggle('is-empty', valid === 0);
        copyMain.disabled = codes.length === 0;

        if (matchMode === 'numeric') {
          matchHint.textContent = 'Chỉ so khớp phần chữ số trong mã và tên tệp.';
          codeHint.textContent = codes.length && !valid
            ? 'Các mã hiện tại không có chữ số. Hãy thêm mã hoặc chuyển sang chế độ “Chữ và số”.'
            : 'Chế độ chỉ lấy số phù hợp khi tên tệp được đánh số đơn giản (001, 002…).';
        } else {
          matchHint.textContent = 'Tự động nhận diện chữ và số, có hay không đuôi file.';
          codeHint.textContent = 'Hỗ trợ xuống dòng, dấu phẩy, dấu cách và tên tệp có đuôi .jpg, .cr3…';
        }

        updateStartState();
      };

      const updateFolderUi = () => {
        sourceName.textContent = sourceHandle?.name || 'Chưa chọn thư mục nào';
        sourceHint.textContent = sourceHandle
          ? 'Đã cấp quyền đọc và ghi trong thư mục này.'
          : 'Chọn thư mục ảnh gốc trên PC';
        sourcePicker.classList.toggle('is-set', Boolean(sourceHandle));
        sourceState.textContent = sourceHandle ? 'Đã chọn' : 'Chưa chọn';
        sourceState.classList.toggle('is-on', Boolean(sourceHandle));

        const createInside = createSubfolder.checked;
        subfolderRow.hidden = !createInside;
        externalOutput.hidden = createInside;
        outputName.textContent = outputHandle?.name || 'Chưa chọn thư mục lưu';
        outputPicker.classList.toggle('is-set', createInside ? Boolean(sourceHandle) : Boolean(outputHandle));

        updateStartState();
      };

      const setMatchMode = mode => {
        matchMode = mode;
        page.querySelectorAll('[data-match-mode]').forEach(button => {
          const active = button.dataset.matchMode === mode;
          button.classList.toggle('is-active', active);
          button.setAttribute('aria-pressed', String(active));
        });
        updateCodeUi();
      };

      const setFileFormat = format => {
        fileFormat = format;
        formatButtons.forEach(button => {
          const active = button.dataset.fileFormat === format;
          button.classList.toggle('is-active', active);
          button.setAttribute('aria-pressed', String(active));
        });
      };

      const copyCodes = async format => {
        const codes = parseCodes();
        if (!codes.length) {
          showToast('Chưa có mã ảnh nào để sao chép.', 'info');
          return;
        }

        const ok = await copyText(buildCopyText(codes, format || copyFormat, sourceFileNames));
        if (!ok) {
          showToast('Không thể sao chép mã ảnh. Hãy thử lại.', 'error');
          return;
        }
        showToast(`Đã sao chép ${number(codes.length)} mã ảnh.`, 'success');
      };

      const loadAlbum = async id => {
        if (!id) {
          selectedAlbum = null;
          sourceFileNames = new Map();
          albumStatus.textContent = 'Bạn đang dùng danh sách mã nhập tay.';
          updateAlbumNote();
          updateStartState();
          return;
        }

        const previousId = selectedAlbum?.id || '';
        albumSelect.disabled = true;
        albumStatus.innerHTML = `${icon('loader', 'spin')} Đang lấy mã khách đã chọn…`;

        try {
          const album = await api(`/api/admin/albums/${encodeURIComponent(id)}`);
          if (disposed) return;
          selectedAlbum = album;
          const selected = album.photos
            .filter(photo => photo.isSelected)
            .sort((a, b) => a.code.localeCompare(b.code, 'vi', { numeric: true }));

          sourceFileNames = new Map(selected.map(photo => [
            normalizeAlpha(photo.code),
            photo.fileName || `${photo.code}.jpg`
          ]));
          codeInput.value = selected.map(photo => photo.code).join('\n');
          albumStatus.textContent = `${album.title} · ${number(selected.length)} ảnh khách đã chọn.`;
          updateAlbumNote();
          updateCodeUi();

          if (selected.length) {
            showToast(`Đã điền ${number(selected.length)} mã từ “${album.title}”.`, 'success');
          }
        } catch (error) {
          if (disposed || error.name === 'AbortError') return;
          albumSelect.value = previousId;
          albumStatus.textContent = 'Không tải được mã của album này. Hãy thử lại.';
          showToast(error.message || 'Không tải được album.', 'error');
        } finally {
          if (!disposed) {
            albumSelect.disabled = false;
            updateStartState();
          }
        }
      };

      const renderAlbumOptions = () => {
        if (!albums.length) {
          albumSelect.innerHTML = '<option value="">Chưa có album nào</option>';
          albumStatus.textContent = 'Chưa có album nào. Bạn vẫn có thể dán mã thủ công.';
          return;
        }

        albumSelect.innerHTML = `
          <option value="">Chọn album để tự điền mã…</option>
          ${albums.map(album => `
            <option value="${escapeAttr(album.id)}">
              ${escapeHtml(album.title)}${album.clientName ? ` · ${escapeHtml(album.clientName)}` : ''} · ${album.selectedCount ? `${number(album.selectedCount)} ảnh đã chọn` : 'chưa có lựa chọn'}
            </option>
          `).join('')}
        `;
        albumStatus.textContent = 'Chọn album để tự điền những mã khách đã chọn.';
      };

      const loadAlbums = async () => {
        try {
          const result = await api('/api/admin/albums');
          if (disposed) return;
          albums = Array.isArray(result) ? result : [];
          renderAlbumOptions();
          albumSelect.disabled = false;

          const requested = new URLSearchParams(location.search).get('album');
          if (requested && albums.some(album => album.id === requested)) {
            albumSelect.value = requested;
            await loadAlbum(requested);
          }
          updateStartState();
        } catch (error) {
          if (disposed || error.name === 'AbortError') return;
          albumSelect.innerHTML = '<option value="">Không tải được album</option>';
          albumStatus.innerHTML = `${icon('alertCircle')} Không tải được danh sách album.`;
          showToast(error.message || 'Không tải được danh sách album.', 'error');
          updateStartState();
        }
      };

      const pickSource = async () => {
        if (running || !supported) return;
        try {
          const handle = await window.showDirectoryPicker({
            id: 'may-photo-source',
            mode: 'readwrite',
            startIn: 'pictures'
          });
          if (disposed) return;
          sourceHandle = handle;
          updateFolderUi();
          showToast(`Đã chọn thư mục “${handle.name}”.`, 'success');
        } catch (error) {
          if (error?.name === 'AbortError') return;
          const message = error?.name === 'SecurityError'
            ? 'Trình duyệt chặn chọn thư mục trong khung nhúng. Hãy mở StudioCore trong một tab riêng.'
            : 'Chưa chọn được thư mục. Hãy kiểm tra quyền truy cập và thử lại.';
          showToast(message, 'error');
        }
      };

      const pickOutput = async () => {
        if (running || !supported) return;
        try {
          const handle = await window.showDirectoryPicker({
            id: 'may-photo-output',
            mode: 'readwrite',
            startIn: 'pictures'
          });
          if (disposed) return;
          if (sourceHandle) {
            try {
              if (await sourceHandle.isSameEntry(handle)) {
                showToast('Hãy chọn thư mục lưu khác thư mục ảnh gốc.', 'error');
                return;
              }
            } catch {
              // Some implementations do not expose isSameEntry for all handles.
            }
          }
          outputHandle = handle;
          updateFolderUi();
          showToast(`Đã chọn thư mục lưu “${handle.name}”.`, 'success');
        } catch (error) {
          if (error?.name === 'AbortError') return;
          const message = error?.name === 'SecurityError'
            ? 'Trình duyệt chặn chọn thư mục trong khung nhúng. Hãy mở StudioCore trong một tab riêng.'
            : 'Chưa chọn được thư mục lưu. Hãy kiểm tra quyền truy cập và thử lại.';
          showToast(message, 'error');
        }
      };

      const ensurePermission = async (handle, mode = 'readwrite') => {
        if (!handle) return false;
        const options = { mode };
        if (await handle.queryPermission(options) === 'granted') return true;
        return await handle.requestPermission(options) === 'granted';
      };

      const setProgress = (scanned, matched, copied) => {
        progressCount.textContent = `${number(scanned)} tệp`;
        progressDetail.textContent = `Đã khớp ${number(matched)} ảnh · đã sao chép ${number(copied)} ảnh.`;
      };

      const renderResult = ({ scanned, matched, copied, errors, outputLabel, stopped = false }) => {
        if (!copied && !matched) {
          resultRoot.innerHTML = `
            <div class="filter-result filter-result--empty">
              <span class="filter-result__icon">${icon('search')}</span>
              <div>
                <p class="eyebrow">Kết quả lọc</p>
                <h3>Chưa tìm thấy ảnh phù hợp</h3>
                <p>Đã quét ${number(scanned)} tệp nhưng không có tên tệp nào khớp. Thử chế độ <strong>Chỉ lấy số</strong>, kiểm tra lại mã hoặc chọn định dạng file khác.</p>
              </div>
            </div>
          `;
          return;
        }

        const names = lastCopiedNames.slice(0, 8);
        resultRoot.innerHTML = `
          <div class="filter-result ${errors ? 'filter-result--warning' : ''}">
            <header class="filter-result__head">
              <span class="filter-result__icon">${icon(errors ? 'alertCircle' : 'checkCircle')}</span>
              <div>
                <p class="eyebrow">${stopped ? 'Đã dừng' : 'Hoàn tất'}</p>
                <h3>${copied ? `Đã lọc và sao chép ${number(copied)} ảnh.` : `Tìm thấy ${number(matched)} ảnh khớp nhưng chưa sao chép được.`}</h3>
                <p>${outputLabel ? `Thư mục nhận: <strong>${escapeHtml(outputLabel)}</strong>.` : ''}</p>
              </div>
            </header>
            <div class="filter-result__stats">
              <div><strong>${number(scanned)}</strong><span>tệp đã quét</span></div>
              <div><strong>${number(matched)}</strong><span>ảnh khớp mã</span></div>
              <div><strong>${number(copied)}</strong><span>ảnh đã sao chép</span></div>
              <div><strong>${number(errors)}</strong><span>lỗi</span></div>
            </div>
            ${names.length ? `
              <ul class="filter-result__files">
                ${names.map(name => `<li>${icon('check')}<span>${escapeHtml(name)}</span></li>`).join('')}
                ${copied > names.length ? `<li class="filter-result__more">+ ${number(copied - names.length)} ảnh khác</li>` : ''}
              </ul>
            ` : ''}
            ${errors ? `<p class="filter-result__error">${icon('alertCircle')} Một số tệp không sao chép được. Hãy kiểm tra quyền ghi hoặc dung lượng ổ đĩa rồi thử lại.</p>` : ''}
            ${copied ? `
              <div class="filter-result__actions">
                <button class="btn btn--ghost btn--compact" type="button" data-copy-filtered-names>${icon('copy')} Sao chép tên ảnh đã lọc</button>
              </div>
            ` : ''}
          </div>
        `;
      };

      const scanDirectory = async function* (directory, prefix, options) {
        for await (const entry of directory.values()) {
          if (disposed) return;
          const path = prefix ? `${prefix}/${entry.name}` : entry.name;

          if (entry.kind === 'directory') {
            if (options.skipRootName && !prefix && entry.name === options.skipRootName) continue;
            if (options.skipOutputHandle) {
              try {
                if (await options.skipOutputHandle.isSameEntry(entry)) continue;
              } catch {
                // Fall through and scan the directory.
              }
            }
            if (options.recursive) {
              yield* scanDirectory(entry, path, { ...options, skipRootName: '' });
            }
            continue;
          }

          if (entry.kind === 'file') {
            yield { handle: entry, path };
          }
        }
      };

      const runFilter = async () => {
        if (running) return;
        const codes = parseCodes();
        const valid = countValidCodes(codes, matchMode);
        if (!valid) {
          showToast('Hãy chọn album hoặc dán ít nhất một mã ảnh.', 'error');
          codeInput.focus();
          return;
        }
        if (!sourceHandle) {
          showToast('Hãy chọn thư mục ảnh gốc trên máy tính.', 'error');
          return;
        }
        if (!createSubfolder.checked && !outputHandle) {
          showToast('Hãy chọn thư mục lưu ảnh đã lọc.', 'error');
          return;
        }

        const folderName = sanitizeFolderName(folderNameInput.value);
        if (createSubfolder.checked && !folderName) {
          showToast('Hãy đặt tên cho thư mục con.', 'error');
          folderNameInput.focus();
          return;
        }
        folderNameInput.value = folderName;

        if (!createSubfolder.checked && sourceHandle && outputHandle) {
          try {
            if (await sourceHandle.isSameEntry(outputHandle)) {
              showToast('Thư mục lưu phải khác thư mục ảnh gốc.', 'error');
              return;
            }
          } catch {
            // isSameEntry may not be available on every implementation.
          }
        }

        running = true;
        tool.setAttribute('aria-busy', 'true');
        resultRoot.innerHTML = '';
        progress.hidden = false;
        setProgress(0, 0, 0);
        setButtonLoading(startButton, true, 'Đang quét và lọc ảnh…');
        runHint.textContent = 'Đang đọc thư mục. Không đóng tab trong khi đang lọc.';

        let scanned = 0;
        let matched = 0;
        let copied = 0;
        let errors = 0;
        let outputLabel = '';
        let stopped = false;
        const usedNames = new Set();
        lastCopiedNames = [];
        const matcher = createCodeMatcher(codes, matchMode);

        try {
          const sourcePermission = await ensurePermission(sourceHandle, 'readwrite');
          if (!sourcePermission) {
            throw new Error('Bạn chưa cấp quyền đọc và ghi cho thư mục ảnh gốc.');
          }

          let outputDirectory;
          if (createSubfolder.checked) {
            try {
              outputDirectory = await sourceHandle.getDirectoryHandle(folderName, { create: true });
            } catch {
              throw new Error('Không tạo được thư mục con trong thư mục gốc. Hãy kiểm tra quyền ghi hoặc chọn thư mục lưu khác.');
            }
            outputLabel = `${sourceHandle.name} / ${folderName}`;
          } else {
            const outputPermission = await ensurePermission(outputHandle, 'readwrite');
            if (!outputPermission) {
              throw new Error('Bạn chưa cấp quyền ghi cho thư mục lưu.');
            }
            outputDirectory = outputHandle;
            outputLabel = outputHandle.name;
          }

          // Keep files from previous runs. New copies get a "-2", "-3"… suffix
          // instead of overwriting what is already in the destination folder.
          try {
            for await (const entry of outputDirectory.values()) {
              usedNames.add(entry.name);
            }
          } catch {
            // Listing is a safety net; in-run duplicate protection still applies.
          }

          const scanOptions = {
            recursive: recursiveInput.checked,
            skipRootName: createSubfolder.checked ? folderName : '',
            skipOutputHandle: outputDirectory
          };

          for await (const entry of scanDirectory(sourceHandle, '', scanOptions)) {
            if (disposed) {
              stopped = true;
              break;
            }

            scanned++;
            if (scanned % 10 === 0) {
              setProgress(scanned, matched, copied);
              await new Promise(resolve => window.setTimeout(resolve, 0));
            }

            if (!fileMatchesFormat(entry.handle.name, fileFormat)) continue;
            if (!matcher.matches(entry.handle.name)) continue;
            matched++;

            try {
              const file = await entry.handle.getFile();
              const targetName = uniqueFileName(entry.handle.name, usedNames);
              const targetHandle = await outputDirectory.getFileHandle(targetName, { create: true });
              const writable = await targetHandle.createWritable();
              await writable.write(file);
              await writable.close();
              copied++;
              lastCopiedNames.push(targetName);
            } catch {
              errors++;
            }
          }

          setProgress(scanned, matched, copied);
          renderResult({ scanned, matched, copied, errors, outputLabel, stopped });

          if (copied) {
            showToast(`Đã lọc xong ${number(copied)} ảnh vào “${outputLabel}”.`, 'success');
          } else if (matched) {
            showToast('Tìm thấy ảnh khớp nhưng chưa sao chép được. Hãy kiểm tra quyền ghi.', 'error');
          } else {
            showToast('Không tìm thấy ảnh nào khớp với danh sách mã.', 'info');
          }
        } catch (error) {
          if (!disposed) {
            resultRoot.innerHTML = `
              <div class="inline-alert inline-alert--error" role="alert">
                ${icon('alertCircle')}
                <p>${escapeHtml(error?.message || 'Chưa lọc được ảnh. Hãy thử lại.')}</p>
              </div>
            `;
            showToast(error?.message || 'Chưa lọc được ảnh.', 'error');
          }
        } finally {
          if (!disposed) {
            running = false;
            tool.removeAttribute('aria-busy');
            setButtonLoading(startButton, false);
            progress.hidden = true;
            updateStartState();
          }
        }
      };

      albumSelect.addEventListener('change', () => loadAlbum(albumSelect.value));
      codeInput.addEventListener('input', updateCodeUi);
      codeInput.addEventListener('paste', () => window.setTimeout(updateCodeUi, 0));

      page.querySelectorAll('[data-match-mode]').forEach(button => {
        button.addEventListener('click', () => {
          if (running) return;
          setMatchMode(button.dataset.matchMode);
        });
      });

      copyChips.forEach(chip => {
        chip.addEventListener('click', () => {
          if (running) return;
          setCopyFormat(chip.dataset.copyFormat);
          copyCodes(chip.dataset.copyFormat);
        });
      });

      copyMain.addEventListener('click', () => {
        if (!running) copyCodes(copyFormat);
      });

      createSubfolder.addEventListener('change', updateFolderUi);
      folderNameInput.addEventListener('input', updateStartState);
      folderNameInput.addEventListener('blur', () => {
        const clean = sanitizeFolderName(folderNameInput.value);
        if (clean && clean !== folderNameInput.value) folderNameInput.value = clean;
        updateStartState();
      });
      recursiveInput.addEventListener('change', updateStartState);

      formatButtons.forEach(button => {
        button.addEventListener('click', () => {
          if (!running) setFileFormat(button.dataset.fileFormat);
        });
      });

      pickSourceButton.addEventListener('click', pickSource);
      pickOutputButton.addEventListener('click', pickOutput);
      startButton.addEventListener('click', runFilter);

      resultRoot.addEventListener('click', event => {
        const copyButton = event.target.closest('[data-copy-filtered-names]');
        if (!copyButton) return;
        if (!lastCopiedNames.length) return;
        copyText(lastCopiedNames.join('\n')).then(ok => {
          if (!ok) {
            showToast('Không thể sao chép tên ảnh. Hãy thử lại.', 'error');
            return;
          }
          showToast(`Đã sao chép ${number(lastCopiedNames.length)} tên ảnh.`, 'success');
        });
      });

      if (!supported) {
        const notice = page.querySelector('[data-unsupported]');
        notice.hidden = false;
        notice.innerHTML = `
          <div class="inline-alert inline-alert--error" role="alert">
            ${icon('alertCircle')}
            <p>Trình duyệt này chưa hỗ trợ chọn thư mục trực tiếp. Hãy mở StudioCore bằng <strong>Chrome</strong> hoặc <strong>Edge</strong> trên máy tính để lọc ảnh. Bạn vẫn có thể chọn album và sao chép mã ở cột bên trái.</p>
          </div>
        `;
        pickSourceButton.disabled = true;
        pickOutputButton.disabled = true;
      }

      updateFolderUi();
      updateCodeUi();
      loadAlbums();
    },
    cleanup() {
      disposed = true;
      teardown();
    }
  };
}
