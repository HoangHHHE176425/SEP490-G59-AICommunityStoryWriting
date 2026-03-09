export function PolicyBody({ content }) {
  if (!content || !String(content).trim()) {
    return (
      <div className="rounded-xl border border-slate-200 dark:border-slate-700 bg-slate-50 dark:bg-slate-900/40 p-4 text-sm text-slate-600 dark:text-slate-300">
        Policy đang trống.
      </div>
    );
  }

  // Render as plain text (avoid HTML injection).
  const blocks = String(content)
    .replace(/\r\n/g, '\n')
    .split(/\n{2,}/g)
    .map((s) => s.trim())
    .filter(Boolean);

  return (
    <div className="space-y-4 text-sm leading-relaxed text-slate-700 dark:text-slate-200">
      {blocks.map((b, idx) => (
        <p key={idx} className="whitespace-pre-wrap">
          {b}
        </p>
      ))}
    </div>
  );
}

