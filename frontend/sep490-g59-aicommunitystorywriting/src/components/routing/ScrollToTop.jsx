import { useEffect } from 'react';
import { useLocation } from 'react-router-dom';

/**
 * React Router (SPA) mặc định giữ nguyên vị trí scroll khi đổi route.
 * Component này sẽ scroll lên đầu mỗi khi pathname thay đổi.
 */
export function ScrollToTop() {
  const { pathname } = useLocation();

  useEffect(() => {
    // Use instant scroll to avoid "jump" animation artifacts.
    window.scrollTo({ top: 0, left: 0, behavior: 'auto' });
  }, [pathname]);

  return null;
}

