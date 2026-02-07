import { useState, useEffect } from 'react';
import { BookOpen } from 'lucide-react';

const FALLBACK_IMAGE = 'data:image/svg+xml,' + encodeURIComponent(`
  <svg xmlns="http://www.w3.org/2000/svg" width="200" height="200" viewBox="0 0 200 200">
    <rect fill="#e2e8f0" width="200" height="200"/>
    <text x="50%" y="50%" dominant-baseline="middle" text-anchor="middle" fill="#94a3b8" font-size="14" font-family="sans-serif">Không có ảnh</text>
  </svg>
`);

export function ImageWithFallback({ src, alt, className, style, loading, fallback = FALLBACK_IMAGE }) {
  const [imgSrc, setImgSrc] = useState(src);
  const [hasError, setHasError] = useState(false);

  useEffect(() => {
    setImgSrc(src);
    setHasError(false);
  }, [src]);

  const handleError = () => {
    if (!hasError) {
      setHasError(true);
      setImgSrc(fallback);
    }
  };

  if (hasError) {
    return (
      <div className={`flex items-center justify-center bg-slate-200 dark:bg-slate-700 ${className || ''}`}>
        <BookOpen className="w-12 h-12 text-slate-400" />
      </div>
    );
  }

  return (
    <img
      src={imgSrc}
      alt={alt}
      className={className}
      style={style}
      loading={loading}
      onError={handleError}
    />
  );
}
