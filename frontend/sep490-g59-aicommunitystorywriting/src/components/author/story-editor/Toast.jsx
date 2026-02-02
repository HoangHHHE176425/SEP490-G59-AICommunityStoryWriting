import { useEffect, useState } from 'react';
import { CheckCircle, XCircle, Info, AlertCircle, X } from 'lucide-react';

export function Toast({ message, type = 'success', duration = 3000, onClose }) {
    const [isVisible, setIsVisible] = useState(true);
    const [isExiting, setIsExiting] = useState(false);

    useEffect(() => {
        const timer = setTimeout(() => {
            handleClose();
        }, duration);

        return () => clearTimeout(timer);
    }, [duration]);

    const handleClose = () => {
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

    const config = typeConfig[type];
    const Icon = config.icon;

    return (
        <div
            style={{
                position: 'fixed',
                top: '2rem',
                right: '2rem',
                zIndex: 9999,
                minWidth: '300px',
                maxWidth: '500px',
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
        const id = Date.now();
        setToasts(prev => [...prev, { id, message, type, duration }]);
    };

    const removeToast = (id) => {
        setToasts(prev => prev.filter(toast => toast.id !== id));
    };

    const ToastContainer = () => (
        <>
            {toasts.map(toast => (
                <Toast
                    key={toast.id}
                    message={toast.message}
                    type={toast.type}
                    duration={toast.duration}
                    onClose={() => removeToast(toast.id)}
                />
            ))}
        </>
    );

    return { showToast, ToastContainer };
}
