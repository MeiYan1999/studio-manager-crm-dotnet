import { api } from './api.js';
import { adminShell, mountAdminShell } from './admin.js';
import {
  icon,
  escapeHtml,
  escapeAttr,
  stripDiacritics,
  parseAlbumDate,
  formatDate,
  formatTimestamp,
  formatRelativeTime,
  initials,
  showToast,
  setButtonLoading,
  createModal,
  confirmDialog,
  errorInline
} from './ui.js';

const DAY_MS = 24 * 60 * 60 * 1000;

const THEMES = [
  { key: 'ink', label: 'Mực ấm', note: 'Nâu than · mặc định' },
  { key: 'clay', label: 'Đất nung', note: 'Ấm và nổi bật' },
  { key: 'olive', label: 'Vườn chiều', note: 'Xanh ô-liu dịu' },
  { key: 'dusk', label: 'Chiều muộn', note: 'Xám xanh trầm' },
  { key: 'sand', label: 'Giấy kraft', note: 'Nâu cát mộc mạc' }
];

const DUE_FILTERS = [
  { key: 'all', label: 'Tất cả' },
  { key: 'overdue', label: 'Quá hạn' },
  { key: 'soon', label: '7 ngày tới' },
  { key: 'none', label: 'Chưa có hạn' }
];

const LIST_COLORS = ['#b25f46', '#c3a47a', '#6d7353', '#4e7275', '#9a6b78', '#b56a64', '#7c7163'];
const LABEL_COLORS = ['#b25f46', '#b56a64', '#6d7353', '#4e7275', '#9a6b78', '#c3a47a', '#7c7163', '#3d362d'];
const CARD_DEFAULT_BACKGROUND = '#fbf8f2';
const CARD_DEFAULT_TEXT_COLOR = '#2b251e';

// The app renders one board view at a time. Markup helpers live outside the
// view factory, so they read from this shared, explicitly-reset state.
const stateRef = {
  board: null,
  albums: [],
  staff: [],
  bookings: [],
  ui: {
    query: '',
    due: 'all',
    labelId: '',
    composer: null,
    addingList: false
  }
};

function resetBoardState() {
  stateRef.board = null;
  stateRef.albums = [];
  stateRef.staff = [];
  stateRef.bookings = [];
  stateRef.ui.query = '';
  stateRef.ui.due = 'all';
  stateRef.ui.labelId = '';
  stateRef.ui.composer = null;
  stateRef.ui.addingList = false;
}

const PRIORITIES = [
  { key: 'low', label: 'Thấp', iconName: 'arrowDown' },
  { key: 'normal', label: 'Bình thường', iconName: 'minus' },
  { key: 'high', label: 'Cao', iconName: 'arrowUp' },
  { key: 'urgent', label: 'Khẩn cấp', iconName: 'alertTriangle' }
];

function priorityMeta(value) {
  return PRIORITIES.find(item => item.key === value) || PRIORITIES[1];
}

function safeColor(value, fallback = '#b25f46') {
  return /^#[0-9a-f]{6}$/i.test(String(value || '')) ? value : fallback;
}

function safeTheme(value) {
  return THEMES.some(theme => theme.key === value) ? value : 'ink';
}

function channelLuminance(value) {
  const channel = value / 255;
  return channel <= 0.04045
    ? channel / 12.92
    : Math.pow((channel + 0.055) / 1.055, 2.4);
}

function relativeLuminance(hex) {
  const value = safeColor(hex);
  const red = parseInt(value.slice(1, 3), 16);
  const green = parseInt(value.slice(3, 5), 16);
  const blue = parseInt(value.slice(5, 7), 16);
  return 0.2126 * channelLuminance(red) + 0.7152 * channelLuminance(green) + 0.0722 * channelLuminance(blue);
}

function contrastRatio(foreground, background) {
  const first = relativeLuminance(foreground);
  const second = relativeLuminance(background);
  const lighter = Math.max(first, second);
  const darker = Math.min(first, second);
  return (lighter + 0.05) / (darker + 0.05);
}

function readableLabelInk(background) {
  return contrastRatio('#ffffff', background) >= 4.5 ? '#ffffff' : '#000000';
}

function labelStyle(color) {
  const safe = safeColor(color);
  return `--label-color:${safe};--label-ink:${readableLabelInk(safe)}`;
}

function contrastStatus(background, text) {
  const ratio = contrastRatio(text, background);
  const readable = ratio >= 4.5;
  return {
    className: readable ? 'is-good' : 'is-low',
    html: `${icon(readable ? 'checkCircle' : 'alertCircle')} Tương phản ${ratio.toFixed(1)}:1 · ${readable ? 'Dễ đọc' : 'Khó đọc'}`
  };
}

function todayStart() {
  const now = new Date();
  return new Date(now.getFullYear(), now.getMonth(), now.getDate());
}

function dayDifference(date) {
  return Math.round((date.getTime() - todayStart().getTime()) / DAY_MS);
}

function shortDueDate(date) {
  const sameYear = date.getFullYear() === new Date().getFullYear();
  return sameYear
    ? `${date.getDate()} thg ${date.getMonth() + 1}`
    : `${date.getDate()} thg ${date.getMonth() + 1}, ${date.getFullYear()}`;
}

function dueMeta(card) {
  if (!card.dueDate) return null;
  const date = parseAlbumDate(card.dueDate);
  if (!date) return null;
  const diff = dayDifference(date);
  const formatted = formatDate(card.dueDate, true);
  let label = shortDueDate(date);
  let tone = 'normal';

  if (card.isDueComplete) {
    tone = 'complete';
  } else if (diff < 0) {
    tone = 'overdue';
    label = diff === -1 ? 'Hôm qua' : `${Math.abs(diff)} ngày trễ`;
  } else if (diff === 0) {
    tone = 'today';
    label = 'Hôm nay';
  } else if (diff === 1) {
    tone = 'soon';
    label = 'Ngày mai';
  } else if (diff <= 7) {
    tone = 'soon';
  }

  return {
    tone,
    label,
    title: `Hạn ${formatted}${card.isDueComplete ? ' · Đã hoàn tất' : diff < 0 ? ' · Quá hạn' : ''}`
  };
}

function checklistSummary(card) {
  const total = card.checklist?.length || 0;
  const done = (card.checklist || []).filter(item => item.done).length;
  return { total, done };
}

function boardCards(board) {
  return board.lists.flatMap(list => list.cards);
}

function boardStats(board) {
  const cards = boardCards(board);
  let checklistDone = 0;
  let checklistTotal = 0;
  let overdue = 0;
  let soon = 0;

  cards.forEach(card => {
    const summary = checklistSummary(card);
    checklistDone += summary.done;
    checklistTotal += summary.total;

    const meta = dueMeta(card);
    if (!meta || card.isDueComplete) return;
    if (meta.tone === 'overdue') overdue++;
    else if (meta.tone === 'today' || meta.tone === 'soon') soon++;
  });

  return {
    listCount: board.lists.length,
    cardCount: cards.length,
    checklistDone,
    checklistTotal,
    overdue,
    soon
  };
}

function boardHasFilters(uiState) {
  return Boolean(uiState.query.trim()) || uiState.due !== 'all' || Boolean(uiState.labelId);
}

function cardMatches(card, uiState) {
  const terms = stripDiacritics(uiState.query)
    .split(/\s+/)
    .filter(Boolean);
  if (terms.length) {
    const haystack = stripDiacritics([
      card.title,
      card.description,
      card.albumTitle || '',
      ...(card.labels || []).map(label => label.name),
      ...(card.checklist || []).map(item => item.text)
    ].join(' '));
    if (!terms.every(term => haystack.includes(term))) return false;
  }

  if (uiState.labelId && !(card.labels || []).some(label => label.id === uiState.labelId)) {
    return false;
  }

  if (uiState.due === 'none') return !card.dueDate;
  if (uiState.due === 'overdue') {
    const meta = dueMeta(card);
    return Boolean(meta && !card.isDueComplete && meta.tone === 'overdue');
  }
  if (uiState.due === 'soon') {
    const meta = dueMeta(card);
    return Boolean(meta && !card.isDueComplete && (meta.tone === 'today' || meta.tone === 'soon'));
  }

  return true;
}

function visibleCards(list, uiState) {
  return list.cards.filter(card => cardMatches(card, uiState));
}

function countDue(cards, key) {
  return cards.filter(card => {
    if (key === 'all') return true;
    if (key === 'none') return !card.dueDate;
    const meta = dueMeta(card);
    if (!meta || card.isDueComplete) return false;
    if (key === 'overdue') return meta.tone === 'overdue';
    if (key === 'soon') return meta.tone === 'today' || meta.tone === 'soon';
    return false;
  }).length;
}

function findList(board, listId) {
  return board.lists.find(list => list.id === listId) || null;
}

function findCard(board, cardId) {
  for (const list of board.lists) {
    const card = list.cards.find(item => item.id === cardId);
    if (card) return { card, list };
  }
  return null;
}

function statChip(iconName, label, tone = '') {
  return `<span class="board-meta-chip ${tone ? `board-meta-chip--${tone}` : ''}">${icon(iconName)}${escapeHtml(label)}</span>`;
}

function boardHeaderMarkup(board, stats) {
  const attention = stats.overdue + stats.soon;
  return `
    <header class="admin-head board-head">
      <div class="admin-head__copy board-head__copy">
        <p class="eyebrow"><span class="eyebrow__dot"></span>Bảng công việc</p>
        <h1>${escapeHtml(board.title)}</h1>
        <p class="admin-head__lead board-head__lead">${escapeHtml(board.description || 'Sắp xếp công việc studio theo từng bước, từ ý tưởng đến khi giao album cho khách.')}</p>
        <div class="board-head__meta">
          ${statChip('board', `${stats.listCount} danh sách`)}
          ${statChip('credit', `${stats.cardCount} thẻ`)}
          ${statChip('calendar', attention ? `${attention} việc cần chú ý` : 'Không có việc gấp', attention ? 'warn' : 'quiet')}
        </div>
      </div>
      <div class="admin-head__actions board-head__actions">
        <button class="btn btn--ghost" type="button" data-action="board-settings">${icon('palette')} Tùy chỉnh bảng</button>
        <button class="btn btn--primary" type="button" data-action="new-card">${icon('plus')} Thêm thẻ</button>
      </div>
    </header>
  `;
}

function toolbarMarkup(board, stats) {
  const cards = boardCards(board);
  const activeFilters = boardHasFilters(stateRef.ui);
  return `
    <section class="board-toolbar" aria-label="Tìm và lọc thẻ">
      <label class="searchbox board-search">
        <span class="searchbox__icon">${icon('search')}</span>
        <input type="search" data-board-search value="${escapeAttr(stateRef.ui.query)}" placeholder="Tìm thẻ, nhãn, album…" autocomplete="off" aria-label="Tìm thẻ">
      </label>
      <div class="segmented board-due-filter" role="group" aria-label="Lọc theo ngày đến hạn">
        ${DUE_FILTERS.map(filter => `
          <button class="segmented__item ${stateRef.ui.due === filter.key ? 'is-active' : ''}" type="button" data-due-filter="${filter.key}" aria-pressed="${stateRef.ui.due === filter.key}">
            ${filter.label}
            <span class="segmented__count" data-due-count="${filter.key}">${countDue(cards, filter.key)}</span>
          </button>
        `).join('')}
      </div>
      <button class="btn btn--text btn--compact board-toolbar__clear" type="button" data-action="clear-filters" ${activeFilters ? '' : 'hidden'}>
        ${icon('x')} Xoá bộ lọc
      </button>
    </section>
  `;
}

function labelFiltersMarkup(board) {
  if (!board.labels.length) return '';
  return `
    <div class="board-label-filters" aria-label="Lọc theo nhãn">
      <span class="board-label-filters__title">${icon('tag')} Nhãn</span>
      <div class="board-label-filters__scroll">
        ${board.labels.map(label => `
          <button class="board-label-filter ${stateRef.ui.labelId === label.id ? 'is-active' : ''}" type="button" data-label-filter="${escapeAttr(label.id)}" aria-pressed="${stateRef.ui.labelId === label.id}" style="--label-color:${safeColor(label.color)}">
            <span class="board-label-filter__dot"></span>
            ${escapeHtml(label.name)}
          </button>
        `).join('')}
        ${stateRef.ui.labelId ? `<button class="board-label-filter board-label-filter--clear" type="button" data-label-filter="">${icon('x')} Bỏ nhãn</button>` : ''}
      </div>
    </div>
  `;
}

function progressMarkup(stats) {
  const percent = stats.checklistTotal ? Math.round((stats.checklistDone / stats.checklistTotal) * 100) : 0;
  return `
    <div class="board-progress">
      <div class="board-progress__copy">
        <span class="eyebrow eyebrow--light">Tiến độ checklist</span>
        <strong>${stats.checklistDone}<span>/${stats.checklistTotal}</span></strong>
      </div>
      <div class="board-progress__track" role="progressbar" aria-valuemin="0" aria-valuemax="100" aria-valuenow="${percent}" aria-label="Tiến độ checklist">
        <span style="width:${percent}%"></span>
      </div>
    </div>
  `;
}

function listMarkup(board, list) {
  const cards = visibleCards(list, stateRef.ui);
  const filtered = boardHasFilters(stateRef.ui);
  const count = filtered ? `${cards.length}/${list.cards.length}` : String(list.cards.length);
  const hasComposer = stateRef.ui.composer?.listId === list.id;
  return `
    <article class="task-list" data-list-id="${escapeAttr(list.id)}" data-drop-list="${escapeAttr(list.id)}" style="--list-color:${safeColor(list.color)}">
      <header class="task-list__head">
        <button class="task-list__grip" type="button" draggable="true" data-list-drag-handle aria-label="Kéo để di chuyển danh sách ${escapeAttr(list.title)}" title="Kéo để di chuyển">${icon('gripVertical')}</button>
        <div class="task-list__title">
          <h2>${escapeHtml(list.title)}</h2>
          <span class="task-list__count">${count}</span>
        </div>
        <button class="task-list__menu" type="button" data-action="list-menu" data-list-id="${escapeAttr(list.id)}" aria-label="Tùy chọn danh sách ${escapeAttr(list.title)}" title="Tùy chọn danh sách">${icon('more')}</button>
      </header>
      <div class="task-list__cards" data-cards>
        ${cards.map(card => cardMarkup(card)).join('')}
        ${cards.length === 0 ? `
          <div class="task-list__empty">
            ${icon(filtered && list.cards.length ? 'filter' : 'plus')}
            <p>${filtered && list.cards.length ? 'Không có thẻ khớp bộ lọc.' : 'Kéo thẻ vào đây hoặc thêm thẻ mới.'}</p>
          </div>
        ` : ''}
      </div>
      <div class="task-list__foot">
        ${hasComposer ? composerMarkup(list) : `
          <button class="task-list__add" type="button" data-action="quick-add" data-list-id="${escapeAttr(list.id)}">
            ${icon('plus')} Thêm thẻ
          </button>
        `}
      </div>
    </article>
  `;
}

function cardMarkup(card) {
  const meta = dueMeta(card);
  const checklist = checklistSummary(card);
  const labels = card.labels || [];
  const shownLabels = labels.slice(0, 3);
  const hiddenLabels = labels.length - shownLabels.length;
  const priority = priorityMeta(card.priority);
  const showPriority = card.priority && card.priority !== 'normal';
  return `
    <article class="task-card" data-card-id="${escapeAttr(card.id)}" draggable="true" tabindex="0" role="button" aria-label="Mở thẻ ${escapeAttr(card.title)}">
      <span class="task-card__grip" aria-hidden="true">${icon('gripVertical')}</span>
      ${shownLabels.length ? `
        <div class="task-card__labels">
          ${shownLabels.map(label => `
            <span class="task-label" style="${labelStyle(label.color)}">${escapeHtml(label.name)}</span>
          `).join('')}
          ${hiddenLabels > 0 ? `<span class="task-label task-label--more">+${hiddenLabels}</span>` : ''}
        </div>
      ` : ''}
      <h3>${escapeHtml(card.title)}</h3>
      ${(meta || checklist.total || card.description || card.albumTitle || card.assigneeName || showPriority || card.bookingCode) ? `
        <div class="task-card__meta">
          ${meta ? `<span class="task-card__due is-${meta.tone}" title="${escapeAttr(meta.title)}">${icon(meta.tone === 'complete' ? 'checkCircle' : 'calendar')}${escapeHtml(meta.label)}</span>` : ''}
          ${checklist.total ? `<span class="task-card__meta-item ${checklist.done === checklist.total ? 'is-complete' : ''}" title="${checklist.done}/${checklist.total} việc đã xong">${icon('listChecks')}${checklist.done}/${checklist.total}</span>` : ''}
          ${card.description ? `<span class="task-card__meta-item" title="Có mô tả">${icon('alignLeft')}<span class="sr-only">Có mô tả</span></span>` : ''}
          ${showPriority ? `<span class="task-card__priority is-${escapeAttr(card.priority)}" title="Độ ưu tiên: ${escapeAttr(priority.label)}">${icon(priority.iconName)}${escapeHtml(priority.label)}</span>` : ''}
          ${card.assigneeName ? `<span class="task-card__assignee" title="Phụ trách: ${escapeAttr(card.assigneeName)}">${icon('user')}${escapeHtml(card.assigneeName)}</span>` : ''}
          ${card.bookingCode ? `<span class="task-card__booking" title="Lịch chụp liên kết: ${escapeAttr(card.bookingCode)}">${icon('calendar')}${escapeHtml(card.bookingCode)}</span>` : ''}
          ${card.albumTitle ? `<span class="task-card__album" title="Album liên kết: ${escapeAttr(card.albumTitle)}">${icon('link')}${escapeHtml(card.albumTitle)}</span>` : ''}
        </div>
      ` : ''}
    </article>
  `;
}

function composerMarkup(list) {
  return `
    <form class="task-composer" data-card-composer data-list-id="${escapeAttr(list.id)}">
      <label class="sr-only" for="card-composer-${escapeAttr(list.id)}">Tên thẻ mới trong ${escapeAttr(list.title)}</label>
      <textarea id="card-composer-${escapeAttr(list.id)}" name="title" rows="2" maxlength="160" data-composer-input placeholder="Nhập tên thẻ…" required></textarea>
      <div class="task-composer__actions">
        <button class="btn btn--primary btn--compact" type="submit">Thêm thẻ</button>
        <button class="btn btn--ghost btn--compact" type="button" data-action="cancel-composer">Huỷ</button>
      </div>
      <p class="task-composer__hint">Enter để thêm · Shift + Enter để xuống dòng</p>
    </form>
  `;
}

function addListMarkup() {
  if (stateRef.ui.addingList) {
    return `
      <div class="board-addlist board-addlist--open">
        <form class="board-addlist__form" data-list-form>
          <label class="sr-only" for="new-list-title">Tên danh sách mới</label>
          <input id="new-list-title" name="title" type="text" maxlength="60" data-new-list-input placeholder="Tên danh sách…" autocomplete="off" required>
          <div class="board-addlist__actions">
            <button class="btn btn--cream btn--compact" type="submit">Thêm danh sách</button>
            <button class="btn btn--ghost-light btn--compact" type="button" data-action="cancel-list">Huỷ</button>
          </div>
        </form>
      </div>
    `;
  }

  return `
    <div class="board-addlist">
      <button class="board-addlist__trigger" type="button" data-action="add-list">
        ${icon('plus')} Thêm danh sách
      </button>
    </div>
  `;
}

function surfaceMarkup() {
  const board = stateRef.board;
  const stats = boardStats(board);
  const visible = board.lists.reduce((sum, list) => sum + visibleCards(list, stateRef.ui).length, 0);
  const cardBackground = safeColor(board.cardBackground, CARD_DEFAULT_BACKGROUND);
  const cardTextColor = safeColor(board.cardTextColor, CARD_DEFAULT_TEXT_COLOR);
  return `
    <section class="board-surface board-theme--${safeTheme(board.theme)}" data-board-surface aria-label="Các danh sách công việc" style="--surface-card:${cardBackground};--surface-card-ink:${cardTextColor}">
      <div class="board-surface__bar">
        ${progressMarkup(stats)}
        <div class="board-surface__meta">
          <span data-visible-count>${visible}/${stats.cardCount} thẻ</span>
          <span class="board-surface__hint">${icon('gripVertical')} Kéo thả để sắp xếp</span>
        </div>
      </div>
      <div class="board-canvas" data-board-canvas>
        ${board.lists.map(list => listMarkup(board, list)).join('')}
        ${addListMarkup()}
      </div>
    </section>
  `;
}

function pageMarkup() {
  const board = stateRef.board;
  const stats = boardStats(board);
  return `
    ${boardHeaderMarkup(board, stats)}
    ${toolbarMarkup(board, stats)}
    ${labelFiltersMarkup(board)}
    ${surfaceMarkup()}
    <p class="board-tip">${icon('sparkles')} Mẹo: kéo thẻ giữa các danh sách để cập nhật tiến độ. Mở thẻ để thêm mô tả, checklist, nhãn và album liên kết.</p>
    <p class="sr-only" data-board-status aria-live="polite"></p>
  `;
}

function skeletonMarkup() {
  return `
    <div class="board-skeleton" aria-label="Đang tải bảng công việc">
      <div class="skeleton skeleton-title"></div>
      <div class="skeleton skeleton-line skeleton-line--lg"></div>
      <div class="skeleton skeleton-line skeleton-line--sm"></div>
      <div class="board-skeleton__toolbar">
        <div class="skeleton skeleton-search"></div>
        <div class="skeleton skeleton-pill"></div>
      </div>
      <div class="board-skeleton__surface">
        ${Array.from({ length: 4 }, () => `
          <div class="board-skeleton__column">
            <div class="skeleton skeleton-line skeleton-line--sm"></div>
            <div class="skeleton skeleton-card"></div>
            <div class="skeleton skeleton-card"></div>
            <div class="skeleton skeleton-card skeleton-card--short"></div>
          </div>
        `).join('')}
      </div>
    </div>
  `;
}

function errorMarkup(message) {
  return `
    <header class="admin-head">
      <div class="admin-head__copy">
        <p class="eyebrow"><span class="eyebrow__dot"></span>Bảng công việc</p>
        <h1>Chưa mở được bảng.</h1>
        <p class="admin-head__lead">Dữ liệu công việc vẫn được giữ nguyên. Hãy thử tải lại trang.</p>
      </div>
    </header>
    <section class="panel panel--pad panel--error">
      ${errorInline(message || 'Không tải được bảng công việc.', 'board_load_failed')}
      <button class="btn btn--ghost" type="button" data-action="retry">${icon('refresh')} Thử lại</button>
    </section>
  `;
}

// ------------------------------------------------------------- card dialog

function checklistMarkup(items) {
  return items.map((item, index) => `
    <div class="checklist-row ${item.done ? 'is-done' : ''}" data-check-row="${index}">
      <label class="checklist-check">
        <input type="checkbox" data-check-done ${item.done ? 'checked' : ''} aria-label="Đánh dấu hoàn tất: ${escapeAttr(item.text || 'việc mới')}">
        <span>${icon('check')}</span>
      </label>
      <input class="checklist-input" type="text" data-check-text maxlength="180" value="${escapeAttr(item.text)}" placeholder="Tên việc cần làm…" aria-label="Nội dung việc ${index + 1}">
      <button class="checklist-remove" type="button" data-remove-check aria-label="Xoá việc ${index + 1}" title="Xoá việc">${icon('x')}</button>
    </div>
  `).join('');
}

function activityMarkup(card) {
  const activities = Array.isArray(card.activities) ? card.activities : [];
  return `
    <section class="task-activity" aria-labelledby="task-activity-title">
      <header class="task-form__section-head">
        <h3 id="task-activity-title">${icon('history')} Nhật ký hoạt động</h3>
        <span class="task-form__progress">${activities.length ? `${activities.length} hoạt động` : 'Chưa có'}</span>
      </header>
      ${activities.length ? `
        <ol class="activity-list">
          ${activities.map((activity, index) => activityItem(activity, index)).join('')}
        </ol>
      ` : `
        <p class="activity-empty">${icon('history')} Chưa có hoạt động nào. Những thay đổi tiếp theo trên thẻ sẽ được ghi lại kèm người thực hiện.</p>
      `}
    </section>
  `;
}

function activityItem(activity, index) {
  const type = activity.type === 'created' || activity.type === 'moved' ? activity.type : 'updated';
  const label = type === 'created' ? 'Tạo thẻ' : type === 'moved' ? 'Di chuyển' : 'Cập nhật';
  const iconName = type === 'created' ? 'plus' : type === 'moved' ? 'arrowRight' : 'edit';
  const details = Array.isArray(activity.details) ? activity.details.filter(Boolean) : [];
  const panelId = `task-activity-details-${index}`;
  const timestamp = formatTimestamp(activity.createdAt);
  const relative = formatRelativeTime(activity.createdAt);
  const actorName = activity.actorName || 'Studio';
  const avatarClass = activity.actorRole === 'admin' ? 'avatar--ink' : 'avatar--olive';
  return `
    <li class="activity-item activity-item--${type}">
      <span class="activity-item__avatar avatar ${avatarClass}" aria-hidden="true">${escapeHtml(initials(actorName))}</span>
      <div class="activity-item__body">
        <p class="activity-item__line"><strong>${escapeHtml(actorName)}</strong> <span>${escapeHtml(activity.summary || 'đã cập nhật thẻ')}</span></p>
        <p class="activity-item__meta">
          <span class="activity-item__type">${icon(iconName)}${label}</span>
          <span aria-hidden="true">·</span>
          <time datetime="${escapeAttr(activity.createdAt || '')}" title="${escapeAttr(timestamp)}">${escapeHtml(relative || timestamp)}</time>
        </p>
        ${details.length ? `
          <button class="activity-item__toggle" type="button" data-toggle-activity-details aria-expanded="false" aria-controls="${panelId}">
            ${icon('chevronDown')}<span>Chi tiết</span>
          </button>
          <ul class="activity-item__details" id="${panelId}" hidden>
            ${details.map(detail => `<li>${escapeHtml(detail)}</li>`).join('')}
          </ul>
        ` : ''}
      </div>
    </li>
  `;
}

function taskDialogMarkup({ card, defaultListId }) {
  const editing = Boolean(card);
  const listId = card?.listId || defaultListId;
  const labels = stateRef.board.labels;
  const albumOptions = stateRef.albums.map(album => `
    <option value="${escapeAttr(album.id)}" ${card?.albumId === album.id ? 'selected' : ''}>
      ${escapeHtml(album.title)}${album.clientName ? ` · ${escapeHtml(album.clientName)}` : ''}
    </option>
  `).join('');
  const staffOptions = stateRef.staff.map(member => `
    <option value="${escapeAttr(member.id)}" ${card?.assigneeId === member.id ? 'selected' : ''}>
      ${escapeHtml(member.name)}${member.role ? ` · ${escapeHtml(member.role)}` : ''}
    </option>
  `).join('');
  const bookingOptions = stateRef.bookings.map(booking => `
    <option value="${escapeAttr(booking.id)}" ${card?.bookingId === booking.id ? 'selected' : ''}>
      ${escapeHtml(booking.code)}${booking.customerName ? ` · ${escapeHtml(booking.customerName)}` : ''}${booking.date ? ` · ${escapeHtml(booking.date)}` : ''}
    </option>
  `).join('');
  const priorityOptions = PRIORITIES.map(item => `
    <option value="${item.key}" ${(card?.priority || 'normal') === item.key ? 'selected' : ''}>${item.label}</option>
  `).join('');

  return `
    <div class="dialog-card dialog-card--task">
      <button class="dialog-close" type="button" data-close-dialog aria-label="Đóng">${icon('x')}</button>
      <div class="task-dialog__head">
        <span class="task-dialog__icon">${icon(editing ? 'edit' : 'plus')}</span>
        <div>
          <p class="eyebrow">${editing ? 'Chi tiết thẻ' : 'Thẻ mới'}</p>
          <h2 id="task-dialog-title">${editing ? 'Cập nhật công việc' : 'Thêm thẻ vào bảng'}</h2>
        </div>
      </div>
      <form class="task-form" data-task-form novalidate>
        <div class="task-form__layout">
          <div class="task-form__main">
            <div class="field">
              <label for="task-title">Tên thẻ <span class="required">*</span></label>
              <textarea id="task-title" name="title" rows="2" maxlength="160" placeholder="Ví dụ: Gửi timeline chuẩn bị cho khách" required>${escapeHtml(card?.title || '')}</textarea>
            </div>
            <div class="field">
              <label for="task-description">Mô tả</label>
              <textarea id="task-description" name="description" maxlength="5000" placeholder="Thêm ghi chú, đầu việc hoặc thông tin cần nhớ…">${escapeHtml(card?.description || '')}</textarea>
            </div>
            <section class="task-form__section" aria-labelledby="task-checklist-title">
              <div class="task-form__section-head">
                <h3 id="task-checklist-title">${icon('listChecks')} Checklist</h3>
                <span class="task-form__progress" data-check-progress>0/0</span>
              </div>
              <div class="checklist-list" data-checklist-list></div>
              <div class="checklist-add">
                <input type="text" maxlength="180" data-new-check placeholder="Thêm việc cần làm…" aria-label="Thêm việc vào checklist">
                <button class="btn btn--ghost btn--compact" type="button" data-add-check>${icon('plus')} Thêm</button>
              </div>
            </section>
          </div>
          <aside class="task-form__side">
            <div class="task-side-group">
              <label for="task-list">Danh sách</label>
              <select id="task-list" name="listId">
                ${stateRef.board.lists.map(list => `<option value="${escapeAttr(list.id)}" ${list.id === listId ? 'selected' : ''}>${escapeHtml(list.title)}</option>`).join('')}
              </select>
            </div>
            <div class="task-side-group">
              <label for="task-due">Ngày đến hạn</label>
              <input id="task-due" type="date" name="dueDate" value="${escapeAttr(card?.dueDate || '')}">
              <label class="task-check-option ${card?.dueDate ? '' : 'is-disabled'}" data-due-complete-label>
                <input type="checkbox" name="dueComplete" ${card?.isDueComplete ? 'checked' : ''} ${card?.dueDate ? '' : 'disabled'}>
                <span>${icon('check')}</span>
                Đã hoàn tất
              </label>
            </div>
            <div class="task-side-group">
              <label for="task-assignee">${icon('user')} Người phụ trách</label>
              <select id="task-assignee" name="assigneeId">
                <option value="">Chưa phân công</option>
                ${staffOptions}
              </select>
            </div>
            <div class="task-side-group">
              <label for="task-priority">${icon('alertTriangle')} Độ ưu tiên</label>
              <select id="task-priority" name="priority">${priorityOptions}</select>
            </div>
            <div class="task-side-group">
              <label for="task-booking">${icon('calendar')} Lịch chụp liên kết</label>
              <select id="task-booking" name="bookingId">
                <option value="">Không liên kết lịch</option>
                ${bookingOptions}
              </select>
            </div>
            <div class="task-side-group">
              <span class="task-side-group__label">${icon('tag')} Nhãn</span>
              ${labels.length ? `
                <div class="task-label-picker">
                  ${labels.map(label => `
                    <label class="task-label-option" style="--label-color:${safeColor(label.color)}">
                      <input type="checkbox" name="labels" value="${escapeAttr(label.id)}" ${(card?.labels || []).some(item => item.id === label.id) ? 'checked' : ''}>
                      <span class="task-label-option__dot"></span>
                      <span>${escapeHtml(label.name)}</span>
                      <span class="task-label-option__check" aria-hidden="true">${icon('check')}</span>
                    </label>
                  `).join('')}
                </div>
              ` : `<p class="task-side-hint">Chưa có nhãn. Thêm nhãn trong “Tùy chỉnh bảng”.</p>`}
            </div>
            <div class="task-side-group">
              <label for="task-album">Album liên kết</label>
              <select id="task-album" name="albumId">
                <option value="">Không liên kết album</option>
                ${albumOptions}
              </select>
              ${card?.albumTitle ? `<p class="task-side-hint">${icon('link')} ${escapeHtml(card.albumTitle)}</p>` : ''}
            </div>
          </aside>
        </div>
        ${editing ? activityMarkup(card) : ''}
        <div data-task-error></div>
        <div class="task-dialog__actions">
          <div>
            ${editing ? `<button class="btn btn--ghost task-dialog__delete" type="button" data-delete-card>${icon('trash')} Xoá thẻ</button>` : ''}
          </div>
          <div class="task-dialog__action-buttons">
            <button class="btn btn--ghost" type="button" data-close-dialog>Huỷ</button>
            <button class="btn btn--primary" type="submit" data-save-card>${icon('check')} ${editing ? 'Lưu thay đổi' : 'Thêm thẻ'}</button>
          </div>
        </div>
      </form>
    </div>
  `;
}

// -------------------------------------------------------- board settings

function labelManagerMarkup(labels) {
  return `
    <div class="label-manager__head">
      <div>
        <p class="eyebrow">Bộ nhãn</p>
        <h3>Phân loại thẻ theo màu</h3>
      </div>
      <span>${labels.length} nhãn</span>
    </div>
    <div class="label-manager__list">
      ${labels.length ? labels.map(label => `
        <div class="label-manager__row" data-label-row="${escapeAttr(label.id)}">
          <input type="color" value="${safeColor(label.color)}" data-label-color aria-label="Màu nhãn ${escapeAttr(label.name)}">
          <input type="text" value="${escapeAttr(label.name)}" maxlength="40" data-label-name aria-label="Tên nhãn ${escapeAttr(label.name)}">
          <button class="icon-btn" type="button" data-label-save title="Lưu nhãn" aria-label="Lưu nhãn ${escapeAttr(label.name)}">${icon('check')}</button>
          <button class="icon-btn label-manager__delete" type="button" data-label-delete title="Xoá nhãn" aria-label="Xoá nhãn ${escapeAttr(label.name)}">${icon('trash')}</button>
        </div>
      `).join('') : '<p class="label-manager__empty">Chưa có nhãn nào. Thêm nhãn đầu tiên bên dưới.</p>'}
    </div>
    <div class="label-manager__add">
      <input type="color" value="#b25f46" data-new-label-color aria-label="Màu nhãn mới">
      <input type="text" maxlength="40" data-new-label-name placeholder="Tên nhãn mới…" aria-label="Tên nhãn mới">
      <button class="btn btn--ghost btn--compact" type="button" data-add-label>${icon('plus')} Thêm nhãn</button>
    </div>
  `;
}

function boardSettingsMarkup() {
  const board = stateRef.board;
  const cardBackground = safeColor(board.cardBackground, CARD_DEFAULT_BACKGROUND);
  const cardTextColor = safeColor(board.cardTextColor, CARD_DEFAULT_TEXT_COLOR);
  const previewLabelColor = safeColor(board.labels[0]?.color, '#b25f46');
  const cardStatus = contrastStatus(cardBackground, cardTextColor);
  return `
    <div class="dialog-card dialog-card--board-settings">
      <button class="dialog-close" type="button" data-close-dialog aria-label="Đóng">${icon('x')}</button>
      <div class="task-dialog__head">
        <span class="task-dialog__icon">${icon('palette')}</span>
        <div>
          <p class="eyebrow">Tùy chỉnh</p>
          <h2 id="board-settings-title">Làm bảng hợp với studio</h2>
        </div>
      </div>
      <form data-board-settings-form novalidate>
        <div class="field">
          <label for="board-title">Tên bảng <span class="required">*</span></label>
          <input id="board-title" name="title" type="text" maxlength="80" value="${escapeAttr(board.title)}" required>
        </div>
        <div class="field">
          <label for="board-description">Mô tả ngắn</label>
          <textarea id="board-description" name="description" maxlength="600" rows="3">${escapeHtml(board.description)}</textarea>
        </div>
        <fieldset class="theme-picker">
          <legend>Nền bảng</legend>
          <div class="theme-picker__grid">
            ${THEMES.map(theme => `
              <label class="theme-choice board-theme--${theme.key}">
                <input type="radio" name="theme" value="${theme.key}" ${safeTheme(board.theme) === theme.key ? 'checked' : ''}>
                <span class="theme-choice__preview"><i></i><i></i><i></i></span>
                <span class="theme-choice__copy"><strong>${theme.label}</strong><small>${theme.note}</small></span>
                <span class="theme-choice__check" aria-hidden="true">${icon('check')}</span>
              </label>
            `).join('')}
          </div>
        </fieldset>
        <fieldset class="theme-picker card-style-picker">
          <legend>Màu thẻ</legend>
          <div class="card-style">
            <div class="card-style__preview" data-card-preview style="--surface-card:${cardBackground};--surface-card-ink:${cardTextColor}">
              <span class="task-label" style="${labelStyle(previewLabelColor)}">Xem trước</span>
              <strong>Thẻ công việc của studio</strong>
              <small>Màu nền và màu chữ áp dụng cho mọi thẻ trên bảng.</small>
            </div>
            <div class="card-style__fields">
              <label class="card-color-field" for="board-card-background">
                <span>Nền thẻ</span>
                <span class="card-color-field__row">
                  <input id="board-card-background" type="color" name="cardBackground" value="${cardBackground}">
                  <code data-card-bg-value>${cardBackground}</code>
                </span>
              </label>
              <label class="card-color-field" for="board-card-text">
                <span>Màu chữ</span>
                <span class="card-color-field__row">
                  <input id="board-card-text" type="color" name="cardTextColor" value="${cardTextColor}">
                  <code data-card-text-value>${cardTextColor}</code>
                </span>
              </label>
              <button class="btn btn--ghost btn--compact card-style__reset" type="button" data-reset-card-colors>${icon('refreshCw')} Đặt lại mặc định</button>
              <p class="card-contrast ${cardStatus.className}" data-card-contrast role="status" aria-live="polite">${cardStatus.html}</p>
              <p class="task-side-hint">${icon('palette')} Màu áp dụng cho mọi thẻ trên bảng.</p>
            </div>
          </div>
        </fieldset>
        <div data-board-settings-error></div>
        <div class="task-dialog__actions">
          <span class="task-side-hint">${icon('sparkles')} Thay đổi được lưu riêng cho bảng này.</span>
          <div class="task-dialog__action-buttons">
            <button class="btn btn--ghost" type="button" data-close-dialog>Đóng</button>
            <button class="btn btn--primary" type="submit" data-save-board>${icon('check')} Lưu bảng</button>
          </div>
        </div>
      </form>
      <section class="label-manager" data-label-manager>
        ${labelManagerMarkup(board.labels)}
      </section>
    </div>
  `;
}

function listEditorMarkup(list) {
  const color = safeColor(list?.color || LIST_COLORS[stateRef.board.lists.length % LIST_COLORS.length]);
  return `
    <div class="dialog-card dialog-card--list-editor">
      <button class="dialog-close" type="button" data-close-dialog aria-label="Đóng">${icon('x')}</button>
      <div class="task-dialog__head">
        <span class="task-dialog__icon">${icon('board')}</span>
        <div>
          <p class="eyebrow">${list ? 'Danh sách' : 'Danh sách mới'}</p>
          <h2>${list ? 'Đổi tên & màu' : 'Tạo danh sách'}</h2>
        </div>
      </div>
      <form data-list-editor-form novalidate>
        <div class="field">
          <label for="list-editor-title">Tên danh sách <span class="required">*</span></label>
          <input id="list-editor-title" name="title" type="text" maxlength="60" value="${escapeAttr(list?.title || '')}" placeholder="Ví dụ: Hậu kỳ" required>
        </div>
        <div class="field">
          <label for="list-editor-color">Màu nhận diện</label>
          <div class="color-input-row">
            <input id="list-editor-color" name="color" type="color" value="${color}">
            <div class="color-swatches">
              ${LIST_COLORS.map(preset => `
                <button class="color-swatch ${preset === color ? 'is-active' : ''}" type="button" data-color-preset="${preset}" style="--swatch:${preset}" aria-label="Chọn màu ${preset}"></button>
              `).join('')}
            </div>
          </div>
        </div>
        <div data-list-editor-error></div>
        <div class="task-dialog__actions">
          <span></span>
          <div class="task-dialog__action-buttons">
            <button class="btn btn--ghost" type="button" data-close-dialog>Huỷ</button>
            <button class="btn btn--primary" type="submit" data-save-list>${icon('check')} ${list ? 'Lưu danh sách' : 'Tạo danh sách'}</button>
          </div>
        </div>
      </form>
    </div>
  `;
}

// ------------------------------------------------------------------- view

export function boardView({ session }) {
  let disposed = false;
  let teardown = () => {};
  let page = null;
  let busy = false;
  let dragState = null;
  let lastDragEndAt = 0;
  const modals = new Set();

  resetBoardState();

  function openModal(html, options = {}) {
    const previousOnClose = options.onClose;
    const modal = createModal(html, {
      ...options,
      onClose: () => {
        modals.delete(modal);
        previousOnClose?.();
      }
    });
    modals.add(modal);
    return modal;
  }

  function focusComposer() {
    window.requestAnimationFrame(() => {
      const input = page?.querySelector('[data-composer-input]');
      if (input) {
        input.focus();
        input.setSelectionRange(input.value.length, input.value.length);
      }
    });
  }

  function focusNewList() {
    window.requestAnimationFrame(() => {
      page?.querySelector('[data-new-list-input]')?.focus();
    });
  }

  function paint(options = {}) {
    if (!page || !stateRef.board) return;
    const scrollLeft = page.querySelector('[data-board-canvas]')?.scrollLeft || 0;
    page.innerHTML = pageMarkup();
    const canvas = page.querySelector('[data-board-canvas]');
    if (canvas && options.preserveScroll !== false) {
      canvas.scrollLeft = scrollLeft;
    }
    if (options.focusComposer) focusComposer();
    if (options.focusNewList) focusNewList();
  }

  function renderSurface(options = {}) {
    const oldSurface = page?.querySelector('[data-board-surface]');
    if (!oldSurface) {
      paint(options);
      return;
    }
    const scrollLeft = oldSurface.querySelector('[data-board-canvas]')?.scrollLeft || 0;
    oldSurface.outerHTML = surfaceMarkup();
    const canvas = page.querySelector('[data-board-canvas]');
    if (canvas) canvas.scrollLeft = scrollLeft;
    syncFilterControls();
    if (options.focusComposer) focusComposer();
    if (options.focusNewList) focusNewList();
  }

  function normalizeUiState() {
    if (stateRef.ui.labelId && !stateRef.board.labels.some(label => label.id === stateRef.ui.labelId)) {
      stateRef.ui.labelId = '';
    }
    if (stateRef.ui.composer && !stateRef.board.lists.some(list => list.id === stateRef.ui.composer.listId)) {
      stateRef.ui.composer = null;
    }
  }

  function adoptBoard(updated) {
    if (!updated || disposed) return;
    stateRef.board = updated;
    normalizeUiState();
    paint({ preserveScroll: true });
  }

  function syncFilterControls() {
    if (!page) return;
    page.querySelectorAll('[data-due-filter]').forEach(button => {
      const active = button.dataset.dueFilter === stateRef.ui.due;
      button.classList.toggle('is-active', active);
      button.setAttribute('aria-pressed', String(active));
    });
    page.querySelectorAll('[data-label-filter]').forEach(button => {
      const active = stateRef.ui.labelId && button.dataset.labelFilter === stateRef.ui.labelId;
      button.classList.toggle('is-active', Boolean(active));
      button.setAttribute('aria-pressed', String(Boolean(active)));
    });
    const clear = page.querySelector('[data-action="clear-filters"]');
    if (clear) clear.hidden = !boardHasFilters(stateRef.ui);
  }

  async function commit(request, { successMessage = '', paintOptions = {}, mutateBeforePaint = null } = {}) {
    if (busy) return null;
    busy = true;
    page?.classList.add('is-board-busy');
    try {
      const updated = await request();
      if (disposed) return null;
      stateRef.board = updated;
      normalizeUiState();
      mutateBeforePaint?.();
      paint({ preserveScroll: true, ...paintOptions });
      if (successMessage) showToast(successMessage, 'success');
      return updated;
    } catch (error) {
      if (error.name !== 'AbortError') {
        showToast(error.message || 'Không thể cập nhật bảng. Vui lòng thử lại.', 'error');
      }
      return null;
    } finally {
      busy = false;
      page?.classList.remove('is-board-busy');
    }
  }

  async function load() {
    if (!page) return;
    page.innerHTML = skeletonMarkup();
    try {
      const [board, albums, context] = await Promise.all([
        api('/api/admin/board'),
        api('/api/admin/albums').catch(() => []),
        api('/api/admin/board/context').catch(() => null)
      ]);
      if (disposed) return;
      stateRef.board = board;
      stateRef.albums = Array.isArray(albums) ? albums : [];
      stateRef.staff = Array.isArray(context?.staff) ? context.staff : [];
      stateRef.bookings = Array.isArray(context?.bookings) ? context.bookings : [];
      normalizeUiState();
      paint();
    } catch (error) {
      if (disposed || error.name === 'AbortError') return;
      page.innerHTML = errorMarkup(error.message);
    }
  }

  function openCardDialog({ card = null, defaultListId = null } = {}) {
    if (!stateRef.board.lists.length) {
      showToast('Hãy tạo một danh sách trước khi thêm thẻ.', 'info');
      stateRef.ui.addingList = true;
      renderSurface({ focusNewList: true });
      return;
    }

    const listId = card?.listId || defaultListId || stateRef.board.lists[0].id;
    const items = (card?.checklist || []).map(item => ({ id: item.id, text: item.text, done: item.done }));
    const modal = openModal(taskDialogMarkup({ card, defaultListId: listId }), {
      className: 'dialog--task'
    });
    const dialog = modal.dialog;
    const form = dialog.querySelector('[data-task-form]');
    const checklistRoot = dialog.querySelector('[data-checklist-list]');
    const progress = dialog.querySelector('[data-check-progress]');
    const dueInput = form.elements.namedItem('dueDate');
    const dueComplete = form.elements.namedItem('dueComplete');
    const dueCompleteLabel = dialog.querySelector('[data-due-complete-label]');
    const errorRoot = dialog.querySelector('[data-task-error]');
    const submit = dialog.querySelector('[data-save-card]');

    dialog.addEventListener('click', event => {
      const toggle = event.target.closest('[data-toggle-activity-details]');
      if (!toggle) return;
      const details = toggle.closest('.activity-item__body')?.querySelector('.activity-item__details');
      if (!details) return;
      const expanded = toggle.getAttribute('aria-expanded') === 'true';
      toggle.setAttribute('aria-expanded', String(!expanded));
      toggle.classList.toggle('is-open', !expanded);
      details.hidden = expanded;
      const label = toggle.querySelector('span');
      if (label) label.textContent = expanded ? 'Chi tiết' : 'Ẩn chi tiết';
    });

    const renderChecklist = (focusIndex = null) => {
      checklistRoot.innerHTML = checklistMarkup(items);
      const done = items.filter(item => item.done).length;
      if (progress) progress.textContent = `${done}/${items.length}`;
      if (focusIndex !== null) {
        window.requestAnimationFrame(() => {
          checklistRoot.querySelector(`[data-check-row="${focusIndex}"] [data-check-text]`)?.focus();
        });
      }
    };

    const syncDueComplete = () => {
      const enabled = Boolean(dueInput.value);
      dueComplete.disabled = !enabled;
      if (!enabled) dueComplete.checked = false;
      dueCompleteLabel?.classList.toggle('is-disabled', !enabled);
    };

    renderChecklist();
    syncDueComplete();
    dueInput.addEventListener('change', syncDueComplete);

    checklistRoot.addEventListener('change', event => {
      const checkbox = event.target.closest('[data-check-done]');
      if (!checkbox) return;
      const row = checkbox.closest('[data-check-row]');
      const index = Number(row?.dataset.checkRow);
      if (!Number.isInteger(index) || !items[index]) return;
      items[index].done = checkbox.checked;
      row.classList.toggle('is-done', checkbox.checked);
      const done = items.filter(item => item.done).length;
      if (progress) progress.textContent = `${done}/${items.length}`;
    });

    checklistRoot.addEventListener('input', event => {
      const input = event.target.closest('[data-check-text]');
      if (!input) return;
      const index = Number(input.closest('[data-check-row]')?.dataset.checkRow);
      if (Number.isInteger(index) && items[index]) items[index].text = input.value;
    });

    checklistRoot.addEventListener('click', event => {
      const remove = event.target.closest('[data-remove-check]');
      if (!remove) return;
      const index = Number(remove.closest('[data-check-row]')?.dataset.checkRow);
      if (!Number.isInteger(index)) return;
      items.splice(index, 1);
      renderChecklist();
    });

    const newCheck = dialog.querySelector('[data-new-check]');
    const addCheck = () => {
      const text = newCheck.value.trim();
      if (!text) {
        newCheck.focus();
        return;
      }
      items.push({ id: '', text, done: false });
      newCheck.value = '';
      renderChecklist(items.length - 1);
    };
    dialog.querySelector('[data-add-check]').addEventListener('click', addCheck);
    newCheck.addEventListener('keydown', event => {
      if (event.key === 'Enter') {
        event.preventDefault();
        addCheck();
      }
    });

    form.addEventListener('submit', async event => {
      event.preventDefault();
      errorRoot.innerHTML = '';
      const title = form.elements.namedItem('title').value.trim();
      if (!title) {
        errorRoot.innerHTML = errorInline('Vui lòng nhập tên thẻ.');
        form.elements.namedItem('title').focus();
        return;
      }

      const payload = {
        title,
        description: form.elements.namedItem('description').value.trim(),
        dueDate: dueInput.value || '',
        isDueComplete: dueInput.value ? dueComplete.checked : false,
        labelIds: [...form.querySelectorAll('input[name="labels"]:checked')].map(input => input.value),
        checklist: items
          .map(item => ({ id: item.id || null, text: item.text.trim(), done: item.done }))
          .filter(item => item.text),
        albumId: form.elements.namedItem('albumId').value || '',
        assigneeId: form.elements.namedItem('assigneeId')?.value || '',
        priority: form.elements.namedItem('priority')?.value || 'normal',
        bookingId: form.elements.namedItem('bookingId')?.value || ''
      };

      setButtonLoading(submit, true, 'Đang lưu…');
      try {
        const updated = card
          ? await api(`/api/admin/board/cards/${encodeURIComponent(card.id)}`, {
              method: 'PUT',
              body: { ...payload, listId: form.elements.namedItem('listId').value }
            })
          : await api(`/api/admin/board/lists/${encodeURIComponent(form.elements.namedItem('listId').value)}/cards`, {
              method: 'POST',
              body: payload
            });
        if (disposed) return;
        stateRef.board = updated;
        normalizeUiState();
        modal.close();
        paint({ preserveScroll: true });
        showToast(card ? 'Đã cập nhật thẻ.' : 'Đã thêm thẻ mới.', 'success');
      } catch (error) {
        if (error.name === 'AbortError') return;
        setButtonLoading(submit, false);
        errorRoot.innerHTML = errorInline(error.message || 'Không lưu được thẻ.', error.code);
      }
    });

    const deleteButton = dialog.querySelector('[data-delete-card]');
    deleteButton?.addEventListener('click', async () => {
      const confirmed = await confirmDialog({
        title: `Xoá “${card.title}”?`,
        message: 'Mô tả, checklist và nhãn của thẻ sẽ bị xoá khỏi bảng. Thao tác này không thể hoàn tác.',
        confirmLabel: 'Xoá thẻ',
        cancelLabel: 'Giữ lại',
        danger: true
      });
      if (!confirmed) return;
      setButtonLoading(deleteButton, true, 'Đang xoá…');
      try {
        const updated = await api(`/api/admin/board/cards/${encodeURIComponent(card.id)}`, { method: 'DELETE' });
        if (disposed) return;
        stateRef.board = updated;
        normalizeUiState();
        modal.close();
        paint({ preserveScroll: true });
        showToast('Đã xoá thẻ.', 'success');
      } catch (error) {
        if (error.name === 'AbortError') return;
        setButtonLoading(deleteButton, false);
        errorRoot.innerHTML = errorInline(error.message || 'Không xoá được thẻ.', error.code);
      }
    });
  }

  function openBoardSettings() {
    const modal = openModal(boardSettingsMarkup(), { className: 'dialog--board-settings' });
    const dialog = modal.dialog;
    const form = dialog.querySelector('[data-board-settings-form]');
    const errorRoot = dialog.querySelector('[data-board-settings-error]');
    const manager = dialog.querySelector('[data-label-manager]');
    const submit = dialog.querySelector('[data-save-board]');
    const cardBackgroundInput = form.elements.namedItem('cardBackground');
    const cardTextInput = form.elements.namedItem('cardTextColor');
    const cardPreview = dialog.querySelector('[data-card-preview]');
    const cardBackgroundValue = dialog.querySelector('[data-card-bg-value]');
    const cardTextValue = dialog.querySelector('[data-card-text-value]');
    const cardContrast = dialog.querySelector('[data-card-contrast]');
    let labelBusy = false;

    const syncCardPreview = () => {
      cardPreview.style.setProperty('--surface-card', cardBackgroundInput.value);
      cardPreview.style.setProperty('--surface-card-ink', cardTextInput.value);
      cardPreview.style.background = cardBackgroundInput.value;
      cardPreview.style.color = cardTextInput.value;
      cardBackgroundValue.textContent = cardBackgroundInput.value;
      cardTextValue.textContent = cardTextInput.value;
      const status = contrastStatus(cardBackgroundInput.value, cardTextInput.value);
      cardContrast.className = `card-contrast ${status.className}`;
      cardContrast.innerHTML = status.html;
    };

    cardBackgroundInput.addEventListener('input', syncCardPreview);
    cardTextInput.addEventListener('input', syncCardPreview);
    dialog.querySelector('[data-reset-card-colors]')?.addEventListener('click', () => {
      cardBackgroundInput.value = CARD_DEFAULT_BACKGROUND;
      cardTextInput.value = CARD_DEFAULT_TEXT_COLOR;
      syncCardPreview();
    });

    const refreshManager = () => {
      manager.innerHTML = labelManagerMarkup(stateRef.board.labels);
    };

    form.addEventListener('submit', async event => {
      event.preventDefault();
      errorRoot.innerHTML = '';
      const title = form.elements.namedItem('title').value.trim();
      if (!title) {
        errorRoot.innerHTML = errorInline('Tên bảng không được để trống.');
        form.elements.namedItem('title').focus();
        return;
      }

      const theme = form.querySelector('input[name="theme"]:checked')?.value || 'ink';
      setButtonLoading(submit, true, 'Đang lưu…');
      try {
        const updated = await api('/api/admin/board', {
          method: 'PUT',
          body: {
            title,
            description: form.elements.namedItem('description').value.trim(),
            theme,
            cardBackground: form.elements.namedItem('cardBackground').value,
            cardTextColor: form.elements.namedItem('cardTextColor').value
          }
        });
        if (disposed) return;
        stateRef.board = updated;
        modal.close();
        paint({ preserveScroll: true });
        showToast('Đã lưu tùy chỉnh bảng.', 'success');
      } catch (error) {
        if (error.name === 'AbortError') return;
        setButtonLoading(submit, false);
        errorRoot.innerHTML = errorInline(error.message || 'Không lưu được bảng.', error.code);
      }
    });

    dialog.addEventListener('click', async event => {
      if (labelBusy) return;
      const row = event.target.closest('[data-label-row]');
      if (row && event.target.closest('[data-label-save]')) {
        const id = row.dataset.labelRow;
        const name = row.querySelector('[data-label-name]').value.trim();
        const color = row.querySelector('[data-label-color]').value;
        if (!name) {
          showToast('Tên nhãn không được để trống.', 'info');
          row.querySelector('[data-label-name]').focus();
          return;
        }
        labelBusy = true;
        const button = event.target.closest('[data-label-save]');
        setButtonLoading(button, true, '');
        try {
          const updated = await api(`/api/admin/board/labels/${encodeURIComponent(id)}`, {
            method: 'PUT',
            body: { name, color }
          });
          if (disposed) return;
          stateRef.board = updated;
          paint({ preserveScroll: true });
          refreshManager();
          showToast('Đã cập nhật nhãn.', 'success');
        } catch (error) {
          if (error.name !== 'AbortError') showToast(error.message || 'Không cập nhật được nhãn.', 'error');
        } finally {
          labelBusy = false;
          setButtonLoading(button, false);
        }
        return;
      }

      if (row && event.target.closest('[data-label-delete]')) {
        const id = row.dataset.labelRow;
        const label = stateRef.board.labels.find(item => item.id === id);
        if (!label) return;
        const confirmed = await confirmDialog({
          title: `Xoá nhãn “${label.name}”?`,
          message: 'Nhãn sẽ được gỡ khỏi mọi thẻ đang sử dụng. Các thẻ vẫn được giữ nguyên.',
          confirmLabel: 'Xoá nhãn',
          cancelLabel: 'Giữ lại',
          danger: true
        });
        if (!confirmed) return;
        labelBusy = true;
        try {
          const updated = await api(`/api/admin/board/labels/${encodeURIComponent(id)}`, { method: 'DELETE' });
          if (disposed) return;
          stateRef.board = updated;
          normalizeUiState();
          paint({ preserveScroll: true });
          refreshManager();
          showToast('Đã xoá nhãn.', 'success');
        } catch (error) {
          if (error.name !== 'AbortError') showToast(error.message || 'Không xoá được nhãn.', 'error');
        } finally {
          labelBusy = false;
        }
        return;
      }

      if (event.target.closest('[data-add-label]')) {
        const nameInput = dialog.querySelector('[data-new-label-name]');
        const colorInput = dialog.querySelector('[data-new-label-color]');
        const name = nameInput.value.trim();
        if (!name) {
          showToast('Nhập tên nhãn trước khi thêm.', 'info');
          nameInput.focus();
          return;
        }
        labelBusy = true;
        const button = event.target.closest('[data-add-label]');
        setButtonLoading(button, true, 'Đang thêm…');
        try {
          const updated = await api('/api/admin/board/labels', {
            method: 'POST',
            body: { name, color: colorInput.value }
          });
          if (disposed) return;
          stateRef.board = updated;
          paint({ preserveScroll: true });
          refreshManager();
          showToast('Đã thêm nhãn mới.', 'success');
        } catch (error) {
          if (error.name !== 'AbortError') showToast(error.message || 'Không thêm được nhãn.', 'error');
        } finally {
          labelBusy = false;
          setButtonLoading(button, false);
        }
      }
    });

    const newLabelName = dialog.querySelector('[data-new-label-name]');
    newLabelName.addEventListener('keydown', event => {
      if (event.key === 'Enter') {
        event.preventDefault();
        dialog.querySelector('[data-add-label]')?.click();
      }
    });

    manager.addEventListener('keydown', event => {
      if (event.key === 'Enter' && event.target.matches('[data-label-name]')) {
        event.preventDefault();
        event.target.closest('[data-label-row]')?.querySelector('[data-label-save]')?.click();
      }
    });
  }

  function openListEditor(list = null) {
    const modal = openModal(listEditorMarkup(list), { className: 'dialog--list-editor' });
    const dialog = modal.dialog;
    const form = dialog.querySelector('[data-list-editor-form]');
    const errorRoot = dialog.querySelector('[data-list-editor-error]');
    const colorInput = form.elements.namedItem('color');
    const submit = dialog.querySelector('[data-save-list]');

    dialog.querySelectorAll('[data-color-preset]').forEach(button => {
      button.addEventListener('click', () => {
        colorInput.value = button.dataset.colorPreset;
        dialog.querySelectorAll('[data-color-preset]').forEach(item => {
          item.classList.toggle('is-active', item === button);
        });
      });
    });
    colorInput.addEventListener('input', () => {
      dialog.querySelectorAll('[data-color-preset]').forEach(item => {
        item.classList.toggle('is-active', item.dataset.colorPreset === colorInput.value);
      });
    });

    form.addEventListener('submit', async event => {
      event.preventDefault();
      errorRoot.innerHTML = '';
      const title = form.elements.namedItem('title').value.trim();
      if (!title) {
        errorRoot.innerHTML = errorInline('Tên danh sách không được để trống.');
        form.elements.namedItem('title').focus();
        return;
      }

      setButtonLoading(submit, true, 'Đang lưu…');
      try {
        const updated = list
          ? await api(`/api/admin/board/lists/${encodeURIComponent(list.id)}`, {
              method: 'PUT',
              body: { title, color: colorInput.value }
            })
          : await api('/api/admin/board/lists', {
              method: 'POST',
              body: { title, color: colorInput.value }
            });
        if (disposed) return;
        stateRef.board = updated;
        modal.close();
        paint({ preserveScroll: true });
        showToast(list ? 'Đã cập nhật danh sách.' : 'Đã tạo danh sách mới.', 'success');
      } catch (error) {
        if (error.name === 'AbortError') return;
        setButtonLoading(submit, false);
        errorRoot.innerHTML = errorInline(error.message || 'Không lưu được danh sách.', error.code);
      }
    });
  }

  function openListMenu(listId) {
    const list = findList(stateRef.board, listId);
    if (!list) return;
    const index = stateRef.board.lists.findIndex(item => item.id === listId);
    const lastIndex = stateRef.board.lists.length - 1;
    const modal = openModal(`
      <div class="dialog-card dialog-card--list-menu">
        <button class="dialog-close" type="button" data-close-dialog aria-label="Đóng">${icon('x')}</button>
        <p class="eyebrow">Danh sách</p>
        <h2>${escapeHtml(list.title)}</h2>
        <p class="dialog-copy">${list.cards.length} thẻ trong danh sách này.</p>
        <div class="list-menu">
          <button type="button" data-menu-action="edit">${icon('edit')} Đổi tên & màu</button>
          <button type="button" data-menu-action="left" ${index === 0 ? 'disabled' : ''}>${icon('arrowLeft')} Di chuyển sang trái</button>
          <button type="button" data-menu-action="right" ${index === lastIndex ? 'disabled' : ''}>${icon('arrowRight')} Di chuyển sang phải</button>
          <button class="is-danger" type="button" data-menu-action="delete">${icon('trash')} Xoá danh sách</button>
        </div>
      </div>
    `, { className: 'dialog--list-menu' });

    const dialog = modal.dialog;
    dialog.addEventListener('click', async event => {
      const button = event.target.closest('[data-menu-action]');
      if (!button) return;
      const action = button.dataset.menuAction;

      if (action === 'edit') {
        modal.close();
        window.setTimeout(() => openListEditor(list), 0);
        return;
      }

      if (action === 'left' || action === 'right') {
        const target = action === 'left' ? index - 1 : index + 1;
        if (target < 0 || target > lastIndex) return;
        setButtonLoading(button, true, 'Đang chuyển…');
        try {
          const updated = await api(`/api/admin/board/lists/${encodeURIComponent(list.id)}/move`, {
            method: 'POST',
            body: { position: target }
          });
          if (disposed) return;
          stateRef.board = updated;
          modal.close();
          paint({ preserveScroll: true });
          showToast('Đã di chuyển danh sách.', 'success');
        } catch (error) {
          if (error.name !== 'AbortError') {
            setButtonLoading(button, false);
            showToast(error.message || 'Không di chuyển được danh sách.', 'error');
          }
        }
        return;
      }

      if (action === 'delete') {
        const confirmed = await confirmDialog({
          title: `Xoá danh sách “${list.title}”?`,
          message: list.cards.length
            ? `${list.cards.length} thẻ bên trong cũng sẽ bị xoá. Hãy di chuyển thẻ sang danh sách khác trước nếu muốn giữ lại.`
            : 'Danh sách trống sẽ bị xoá khỏi bảng. Thao tác này không thể hoàn tác.',
          confirmLabel: 'Xoá danh sách',
          cancelLabel: 'Giữ lại',
          danger: true
        });
        if (!confirmed) return;
        setButtonLoading(button, true, 'Đang xoá…');
        try {
          const updated = await api(`/api/admin/board/lists/${encodeURIComponent(list.id)}`, { method: 'DELETE' });
          if (disposed) return;
          stateRef.board = updated;
          normalizeUiState();
          modal.close();
          paint({ preserveScroll: true });
          showToast('Đã xoá danh sách.', 'success');
        } catch (error) {
          if (error.name !== 'AbortError') {
            setButtonLoading(button, false);
            showToast(error.message || 'Không xoá được danh sách.', 'error');
          }
        }
      }
    });
  }

  function clearDropIndicators() {
    page?.querySelectorAll('.is-drop-before, .is-drop-after, .is-drop-target, .is-drop-end').forEach(element => {
      element.classList.remove('is-drop-before', 'is-drop-after', 'is-drop-target', 'is-drop-end');
    });
  }

  function clearDragState() {
    clearDropIndicators();
    page?.querySelectorAll('.is-dragging, .is-dragging-list').forEach(element => {
      element.classList.remove('is-dragging', 'is-dragging-list');
    });
    document.body.classList.remove('is-board-dragging');
    dragState = null;
  }

  function autoScrollCanvas(event) {
    const canvas = page?.querySelector('[data-board-canvas]');
    if (!canvas) return;
    const rect = canvas.getBoundingClientRect();
    if (event.clientX > rect.right - 70) {
      canvas.scrollLeft += 18;
    } else if (event.clientX < rect.left + 70) {
      canvas.scrollLeft -= 18;
    }
  }

  function onDragStart(event) {
    if (!page || busy) return;
    const cardElement = event.target.closest?.('[data-card-id]');
    if (cardElement && page.contains(cardElement)) {
      const listElement = cardElement.closest('[data-list-id]');
      dragState = {
        type: 'card',
        cardId: cardElement.dataset.cardId,
        sourceListId: listElement?.dataset.listId || '',
        targetListId: null,
        index: null
      };
      cardElement.classList.add('is-dragging');
      document.body.classList.add('is-board-dragging');
      if (event.dataTransfer) {
        event.dataTransfer.effectAllowed = 'move';
        event.dataTransfer.setData('text/plain', dragState.cardId);
      }
      return;
    }

    const handle = event.target.closest?.('[data-list-drag-handle]');
    if (handle) {
      const listElement = handle.closest('[data-list-id]');
      if (!listElement) return;
      dragState = {
        type: 'list',
        listId: listElement.dataset.listId,
        targetIndex: null
      };
      listElement.classList.add('is-dragging-list');
      document.body.classList.add('is-board-dragging');
      if (event.dataTransfer) {
        event.dataTransfer.effectAllowed = 'move';
        event.dataTransfer.setData('text/plain', dragState.listId);
      }
    }
  }

  function onDragOver(event) {
    if (!dragState || !page) return;
    autoScrollCanvas(event);

    if (dragState.type === 'card') {
      const listElement = event.target.closest?.('[data-drop-list]');
      if (!listElement || !page.contains(listElement)) return;
      event.preventDefault();
      if (event.dataTransfer) event.dataTransfer.dropEffect = 'move';
      const cards = [...listElement.querySelectorAll('[data-card-id]')]
        .filter(element => element.dataset.cardId !== dragState.cardId);
      let index = cards.findIndex(element => {
        const rect = element.getBoundingClientRect();
        return event.clientY < rect.top + rect.height / 2;
      });
      if (index === -1) index = cards.length;

      clearDropIndicators();
      listElement.classList.add('is-drop-target');
      if (cards[index]) cards[index].classList.add('is-drop-before');
      else listElement.classList.add('is-drop-end');
      dragState.targetListId = listElement.dataset.dropList;
      dragState.index = index;
      return;
    }

    const listElement = event.target.closest?.('[data-list-id]');
    if (!listElement || !page.contains(listElement)) return;
    if (listElement.dataset.listId === dragState.listId) {
      clearDropIndicators();
      dragState.targetIndex = null;
      return;
    }
    event.preventDefault();
    if (event.dataTransfer) event.dataTransfer.dropEffect = 'move';
    const lists = [...page.querySelectorAll('.task-list')]
      .filter(element => element.dataset.listId !== dragState.listId);
    const rect = listElement.getBoundingClientRect();
    const after = event.clientX > rect.left + rect.width / 2;
    let index = lists.indexOf(listElement);
    if (index === -1) return;
    if (after) index++;
    clearDropIndicators();
    listElement.classList.add(after ? 'is-drop-after' : 'is-drop-before');
    dragState.targetIndex = index;
  }

  async function onDrop(event) {
    if (!dragState) return;
    if (dragState.type === 'card') {
      const listElement = event.target.closest?.('[data-drop-list]');
      if (!listElement) {
        clearDragState();
        return;
      }
      event.preventDefault();
      const cardId = dragState.cardId;
      const listId = dragState.targetListId || listElement.dataset.dropList;
      const index = dragState.index ?? 0;
      clearDragState();
      await commit(() => api(`/api/admin/board/cards/${encodeURIComponent(cardId)}/move`, {
        method: 'POST',
        body: { listId, position: index }
      }));
      return;
    }

    if (dragState.type === 'list') {
      const listElement = event.target.closest?.('[data-list-id]');
      if (!listElement ||
          !page.contains(listElement) ||
          listElement.dataset.listId === dragState.listId ||
          dragState.targetIndex === null) {
        clearDragState();
        return;
      }
      event.preventDefault();
      const listId = dragState.listId;
      const index = dragState.targetIndex;
      clearDragState();
      await commit(() => api(`/api/admin/board/lists/${encodeURIComponent(listId)}/move`, {
        method: 'POST',
        body: { position: index }
      }));
    }
  }

  function onDragEnd() {
    lastDragEndAt = Date.now();
    clearDragState();
  }

  const html = adminShell({
    active: 'board',
    session,
    content: `<div class="admin-page__inner admin-page__inner--board" data-board-page>${skeletonMarkup()}</div>`
  });

  return {
    title: 'Bảng công việc',
    bodyClass: 'page-admin page-board',
    html,
    mount(root) {
      teardown = mountAdminShell(root, session);
      page = root.querySelector('[data-board-page]');
      if (!page) return;

      page.addEventListener('click', async event => {
        const dueButton = event.target.closest('[data-due-filter]');
        if (dueButton) {
          stateRef.ui.due = dueButton.dataset.dueFilter;
          renderSurface();
          syncFilterControls();
          return;
        }

        const labelButton = event.target.closest('[data-label-filter]');
        if (labelButton) {
          const next = labelButton.dataset.labelFilter;
          stateRef.ui.labelId = next === stateRef.ui.labelId ? '' : next;
          renderSurface();
          syncFilterControls();
          return;
        }

        const actionElement = event.target.closest('[data-action]');
        if (actionElement) {
          const action = actionElement.dataset.action;
          if (action === 'retry') {
            load();
            return;
          }
          if (action === 'board-settings') {
            openBoardSettings();
            return;
          }
          if (action === 'new-card') {
            openCardDialog();
            return;
          }
          if (action === 'clear-filters') {
            stateRef.ui.query = '';
            stateRef.ui.due = 'all';
            stateRef.ui.labelId = '';
            const search = page.querySelector('[data-board-search]');
            if (search) search.value = '';
            renderSurface();
            syncFilterControls();
            page.querySelector('[data-board-search]')?.focus();
            return;
          }
          if (action === 'quick-add') {
            stateRef.ui.composer = { listId: actionElement.dataset.listId };
            stateRef.ui.addingList = false;
            renderSurface({ focusComposer: true });
            return;
          }
          if (action === 'cancel-composer') {
            stateRef.ui.composer = null;
            renderSurface();
            return;
          }
          if (action === 'add-list') {
            stateRef.ui.addingList = true;
            stateRef.ui.composer = null;
            renderSurface({ focusNewList: true });
            return;
          }
          if (action === 'cancel-list') {
            stateRef.ui.addingList = false;
            renderSurface();
            return;
          }
          if (action === 'list-menu') {
            openListMenu(actionElement.dataset.listId);
            return;
          }
        }

        const cardElement = event.target.closest('[data-card-id]');
        if (cardElement && !event.target.closest('button, a, input, textarea, select')) {
          if (Date.now() - lastDragEndAt < 220) return;
          const found = findCard(stateRef.board, cardElement.dataset.cardId);
          if (found) openCardDialog({ card: found.card });
        }
      });

      page.addEventListener('input', event => {
        if (event.target.matches('[data-board-search]')) {
          stateRef.ui.query = event.target.value;
          renderSurface();
          syncFilterControls();
        }
      });

      page.addEventListener('keydown', event => {
        const cardElement = event.target.closest('[data-card-id]');
        if (cardElement && event.target === cardElement && (event.key === 'Enter' || event.key === ' ')) {
          event.preventDefault();
          const found = findCard(stateRef.board, cardElement.dataset.cardId);
          if (found) openCardDialog({ card: found.card });
          return;
        }

        if (event.target.matches('[data-composer-input]')) {
          if (event.key === 'Enter' && !event.shiftKey) {
            event.preventDefault();
            event.target.closest('form')?.requestSubmit();
          } else if (event.key === 'Escape') {
            stateRef.ui.composer = null;
            renderSurface();
          }
          return;
        }

        if (event.target.matches('[data-new-list-input]') && event.key === 'Escape') {
          event.preventDefault();
          stateRef.ui.addingList = false;
          renderSurface();
        }
      });

      page.addEventListener('submit', async event => {
        const cardForm = event.target.closest('[data-card-composer]');
        if (cardForm) {
          event.preventDefault();
          const title = cardForm.elements.namedItem('title').value.trim();
          if (!title) {
            cardForm.elements.namedItem('title').focus();
            return;
          }
          const listId = cardForm.dataset.listId;
          const submit = cardForm.querySelector('[type="submit"]');
          setButtonLoading(submit, true, 'Đang thêm…');
          const updated = await commit(
            () => api(`/api/admin/board/lists/${encodeURIComponent(listId)}/cards`, {
              method: 'POST',
              body: { title }
            }),
            {
              successMessage: 'Đã thêm thẻ mới.',
              paintOptions: { focusComposer: true }
            }
          );
          if (!updated) setButtonLoading(submit, false);
          return;
        }

        const listForm = event.target.closest('[data-list-form]');
        if (listForm) {
          event.preventDefault();
          const title = listForm.elements.namedItem('title').value.trim();
          if (!title) {
            listForm.elements.namedItem('title').focus();
            return;
          }
          const submit = listForm.querySelector('[type="submit"]');
          setButtonLoading(submit, true, 'Đang tạo…');
          const updated = await commit(
            () => api('/api/admin/board/lists', {
              method: 'POST',
              body: { title, color: LIST_COLORS[stateRef.board.lists.length % LIST_COLORS.length] }
            }),
            {
              successMessage: 'Đã tạo danh sách mới.',
              mutateBeforePaint: () => {
                stateRef.ui.addingList = false;
              }
            }
          );
          if (!updated) setButtonLoading(submit, false);
        }
      });

      page.addEventListener('dragstart', onDragStart);
      page.addEventListener('dragover', onDragOver);
      page.addEventListener('drop', onDrop);
      page.addEventListener('dragend', onDragEnd);
      page.addEventListener('dragleave', event => {
        if (dragState && event.target === page.querySelector('[data-board-canvas]') && !event.relatedTarget) {
          clearDropIndicators();
        }
      });

      load();
    },
    cleanup() {
      disposed = true;
      clearDragState();
      teardown();
      for (const modal of [...modals]) {
        try {
          modal.close();
        } catch {
          // A dialog may already have closed itself.
        }
      }
      modals.clear();
    }
  };
}
