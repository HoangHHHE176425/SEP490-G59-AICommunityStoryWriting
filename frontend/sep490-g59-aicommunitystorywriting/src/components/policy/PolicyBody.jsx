/**
 * Hiển thị nội dung policy an toàn (không dùng dangerouslySetInnerHTML).
 *
 * Cấu trúc hỗ trợ (như bản người dùng dán từ Word/CMS — không bắt buộc # / ##):
 *   Dòng đầu (đoạn mở đầu)       — đoạn văn; đoạn văn đầu tiên được nhấn mạnh như tiêu đề tài liệu
 *   1. Tiêu đề mục chính         — mục chính (cùng kiểu với ##)
 *   2.1 Tiêu đề mục phụ          — mục phụ (viền trái)
 *   # / ## / ###                 — vẫn hỗ trợ; ### coi như mục phụ
 *   - hoặc *                     — danh sách (chỉ gạch đầu dòng; không dùng "1." làm list)
 *   ---                          — đường ngăn
 */
function parsePolicySegments(raw) {
  const lines = String(raw ?? '')
    .replace(/\r\n/g, '\n')
    .split('\n');

  const segments = [];
  let para = [];
  let listItems = null;

  const flushPara = () => {
    if (!para.length) return;
    const text = para.join(' ').replace(/\s+/g, ' ').trim();
    if (text) segments.push({ type: 'p', text });
    para = [];
  };

  const flushList = () => {
    if (!listItems?.length) return;
    segments.push({ type: 'ul', items: [...listItems] });
    listItems = null;
  };

  const flushAll = () => {
    flushPara();
    flushList();
  };

  for (const line of lines) {
    const trimmed = line.trim();

    if (!trimmed) {
      flushAll();
      continue;
    }

    if (trimmed === '---' || trimmed === '***' || trimmed === '___') {
      flushAll();
      segments.push({ type: 'hr' });
      continue;
    }

    if (trimmed.startsWith('#')) {
      const hm = trimmed.match(/^(#{1,3})\s+(.+)$/);
      if (hm) {
        const depth = hm[1].length;
        const title = hm[2].trim();
        flushAll();
        if (depth >= 3) {
          segments.push({ type: 'sub', text: title });
        } else if (depth === 2) {
          segments.push({ type: 'h', level: 2, text: title });
        } else {
          segments.push({ type: 'h', level: 1, text: title });
        }
        continue;
      }
    }

    // 2.1 / 3.2.1 — mục phụ (phải trước mục "10." / "1." một cấp)
    if (/^\d+\.\d+(?:\.\d+)?\s+\S/.test(trimmed)) {
      flushAll();
      segments.push({ type: 'sub', text: trimmed });
      continue;
    }

    // 1. / 2. / … / 15. — mục chính một cấp (không dùng làm danh sách)
    if (/^\d+\.\s+\S/.test(trimmed)) {
      flushAll();
      segments.push({ type: 'h', level: 2, text: trimmed });
      continue;
    }

    const bullet = trimmed.match(/^[-*]\s+(.+)$/);
    if (bullet) {
      flushPara();
      if (!listItems) listItems = [];
      listItems.push(bullet[1].trim());
      continue;
    }

    flushList();
    para.push(trimmed);
  }

  flushAll();
  return segments;
}

const headingClass = {
  1: 'text-2xl font-bold text-slate-900 dark:text-white tracking-tight',
  2: 'text-xl font-semibold text-slate-900 dark:text-white pb-2 border-b border-slate-200 dark:border-slate-600',
};

const subClass =
  'text-[15px] sm:text-base font-semibold text-slate-800 dark:text-slate-100 pl-3 border-l-4 border-emerald-500/70 dark:border-emerald-400/80 py-0.5';

const docTitleClass =
  'text-xl sm:text-2xl font-bold text-slate-900 dark:text-white tracking-tight leading-snug';

export function PolicyBody({ content, className = '' }) {
  if (!content || !String(content).trim()) {
    return (
      <div className="rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900/40 p-4 text-sm text-slate-600 dark:text-slate-300">
        Nội dung policy đang trống.
      </div>
    );
  }

  const segments = parsePolicySegments(content);

  return (
    <div
      className={`policy-body max-w-3xl space-y-5 text-[15px] sm:text-base text-slate-700 dark:text-slate-200 leading-[1.7] ${className}`.trim()}
    >
      {segments.map((seg, idx) => {
        if (seg.type === 'h') {
          const Tag = seg.level === 1 ? 'h2' : 'h3';
          const cls = seg.level === 1 ? headingClass[1] : headingClass[2];
          return (
            <Tag key={idx} className={cls}>
              {seg.text}
            </Tag>
          );
        }
        if (seg.type === 'sub') {
          return (
            <h4 key={idx} className={subClass}>
              {seg.text}
            </h4>
          );
        }
        if (seg.type === 'p') {
          const isLeadTitle = idx === 0;
          return (
            <p
              key={idx}
              className={
                isLeadTitle ? docTitleClass : 'text-slate-600 dark:text-slate-300'
              }
            >
              {seg.text}
            </p>
          );
        }
        if (seg.type === 'ul') {
          return (
            <ul
              key={idx}
              className="list-disc space-y-2.5 pl-5 marker:text-emerald-600 dark:marker:text-emerald-400 text-slate-600 dark:text-slate-300"
            >
              {seg.items.map((item, j) => (
                <li key={j} className="pl-1">
                  {item}
                </li>
              ))}
            </ul>
          );
        }
        if (seg.type === 'hr') {
          return (
            <hr
              key={idx}
              className="border-0 border-t border-slate-200 dark:border-slate-600"
            />
          );
        }
        return null;
      })}
    </div>
  );
}
