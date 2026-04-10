import { useEffect, useRef, useState } from 'react';
import { CheckCircle, XCircle, Info, AlertCircle, X } from 'lucide-react';

const GLOBAL_TOAST_DEDUPE = new Map();

export function Toast({ message, type = 'success', duration = 3000, onClose }) {
    const [isVisible, setIsVisible] = useState(true);
    const [isExiting, setIsExiting] = useState(false);
    const hasClosedRef = useRef(false);

    useEffect(() => {
        const safeDuration = Number.isFinite(Number(duration)) ? Math.max(1000, Number(duration)) : 3000;
        const timer = setTimeout(() => {
            handleClose();
        }, safeDuration);

        return () => clearTimeout(timer);
    }, [duration]);

    const handleClose = () => {
        if (hasClosedRef.current) return;
        hasClosedRef.current = true;
        setIsExiting(true);
        setTimeout(() => {
            setIsVisible(false);
            if (onClose) onClose();
        }, 300);
    };

    if (!isVisible) return null;

    const typeConfig = {
        success: {
            icon: CheckCircle,
            bgColor: '#d1fae5',
            borderColor: '#6ee7b7',
            textColor: '#065f46',
            iconColor: '#10b981'
        },
        error: {
            icon: XCircle,
            bgColor: '#fee2e2',
            borderColor: '#fca5a5',
            textColor: '#991b1b',
            iconColor: '#ef4444'
        },
        info: {
            icon: Info,
            bgColor: '#dbeafe',
            borderColor: '#93c5fd',
            textColor: '#1e40af',
            iconColor: '#3b82f6'
        },
        warning: {
            icon: AlertCircle,
            bgColor: '#fef3c7',
            borderColor: '#fcd34d',
            textColor: '#92400e',
            iconColor: '#f59e0b'
        }
    };

    const config = typeConfig[type] ?? typeConfig.info;
    const Icon = config.icon;

    return (
        <div
            style={{
                minWidth: '280px',
                maxWidth: '460px',
                width: '100%',
                backgroundColor: config.bgColor,
                border: `1px solid ${config.borderColor}`,
                borderRadius: '8px',
                padding: '1rem',
                boxShadow: '0 4px 12px rgba(0, 0, 0, 0.15)',
                display: 'flex',
                alignItems: 'center',
                gap: '0.75rem',
                animation: isExiting ? 'slideOut 0.3s ease-out' : 'slideIn 0.3s ease-out',
                transform: isExiting ? 'translateX(100%)' : 'translateX(0)',
                opacity: isExiting ? 0 : 1,
                transition: 'transform 0.3s ease-out, opacity 0.3s ease-out'
            }}
        >
            <style>
                {`
          @keyframes slideIn {
            from {
              transform: translateX(100%);
              opacity: 0;
            }
            to {
              transform: translateX(0);
              opacity: 1;
            }
          }
          @keyframes slideOut {
            from {
              transform: translateX(0);
              opacity: 1;
            }
            to {
              transform: translateX(100%);
              opacity: 0;
            }
          }
        `}
            </style>

            <Icon style={{ width: '20px', height: '20px', color: config.iconColor, flexShrink: 0 }} />

            <div style={{ flex: 1, fontSize: '0.875rem', fontWeight: 500, color: config.textColor }}>
                {message}
            </div>

            <button
                onClick={handleClose}
                style={{
                    padding: '0.25rem',
                    backgroundColor: 'transparent',
                    border: 'none',
                    cursor: 'pointer',
                    color: config.textColor,
                    opacity: 0.7,
                    transition: 'opacity 0.2s',
                    display: 'flex',
                    alignItems: 'center',
                    justifyContent: 'center'
                }}
                onMouseEnter={(e) => {
                    e.currentTarget.style.opacity = 1;
                }}
                onMouseLeave={(e) => {
                    e.currentTarget.style.opacity = 0.7;
                }}
            >
                <X style={{ width: '16px', height: '16px' }} />
            </button>
        </div>
    );
}

export function useToast() {
    const [toasts, setToasts] = useState([]);

    const showToast = (message, type = 'success', duration = 3000) => {
        const text = String(message ?? '').trim();
        if (!text) return;
        const toastType = String(type || 'info');
        const now = Date.now();
        const dedupeKey = `${toastType}::${text}`;
        const lastShownAt = GLOBAL_TOAST_DEDUPE.get(dedupeKey) ?? 0;
        // Chặn spam cùng nội dung/type trong thời gian ngắn.
        if (now - lastShownAt < 2000) return;
        GLOBAL_TOAST_DEDUPE.set(dedupeKey, now);
        if (GLOBAL_TOAST_DEDUPE.size > 80) {
            const pruneBefore = now - 15000;
            for (const [k, t] of GLOBAL_TOAST_DEDUPE.entries()) {
                if (t < pruneBefore) GLOBAL_TOAST_DEDUPE.delete(k);
            }
        }
        const id = (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function')
            ? crypto.randomUUID()
            : `${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
        setToasts(prev => {
            // Không thêm lại nếu cùng message/type đang hiện trên màn hình.
            if (prev.some((t) => t.message === text && String(t.type || 'info') === toastType)) {
                return prev;
            }
            return [...prev, { id, message: text, type: toastType, duration }].slice(-4);
        });
    };

    const removeToast = (id) => {
        setToasts(prev => prev.filter(toast => toast.id !== id));
    };

    const clearToasts = () => setToasts([]);

    const ToastContainer = () => (
        <div
            style={{
                position: 'fixed',
                top: '1rem',
                right: '1rem',
                zIndex: 9999,
                display: 'flex',
                flexDirection: 'column',
                gap: '0.625rem',
                pointerEvents: 'none',
                maxWidth: 'calc(100vw - 2rem)',
            }}
        >
            {toasts.map(toast => (
                <div key={toast.id} style={{ pointerEvents: 'auto' }}>
                    <Toast
                        message={toast.message}
                        type={toast.type}
                        duration={toast.duration}
                        onClose={() => removeToast(toast.id)}
                    />
                </div>
            ))}
        </div>
    );

    return { showToast, ToastContainer, clearToasts };
}
