export function PolicyBody({ content }) {
  if (!content || !String(content).trim()) {
    return (
      <div className="rounded-xl border border-slate-200 bg-slate-50 p-4 text-sm text-slate-600">
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
    <div className="space-y-4 text-[#90A1B9] leading-relaxed">
      {blocks.map((b, idx) => (
        <p key={idx} className="whitespace-pre-wrap">
          {b}
        </p>
      ))}
    </div>
  );
}

