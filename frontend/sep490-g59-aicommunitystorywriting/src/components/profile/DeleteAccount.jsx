import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../../contexts/AuthContext';
import { Trash2, AlertTriangle, CheckCircle } from 'lucide-react';

export default function DeleteAccount() {
    const navigate = useNavigate();
    const { deleteMyAccount } = useAuth();
    const [confirmText, setConfirmText] = useState('');
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState('');

    const requiredText = 'XÓA TÀI KHOẢN';

    const handleDelete = async () => {
        if (confirmText !== requiredText) {
            setError('Vui lòng nhập chính xác "XÓA TÀI KHOẢN" để xác nhận');
            return;
        }

        setError('');
        setLoading(true);

        try {
            const res = await deleteMyAccount();
            if (res.success) {
                navigate('/');
            } else {
                setError(res.message || 'Đã xảy ra lỗi. Vui lòng thử lại.');
            }
        } catch (err) {
            setError('Đã xảy ra lỗi. Vui lòng thử lại.');
        } finally {
            setLoading(false);
        }
    };

    return (
        <div className="bg-white dark:bg-slate-800 rounded-xl shadow-lg p-8 border border-slate-200 dark:border-slate-700">
            <div className="max-w-2xl mx-auto">
                <div className="flex justify-center mb-6">
                    <div className="size-16 bg-red-100 dark:bg-red-900/30 rounded-full flex items-center justify-center">
                        <Trash2 className="w-8 h-8 text-red-600 dark:text-red-400" />
                    </div>
                </div>

                <h3 className="text-2xl font-bold text-slate-900 dark:text-white text-center mb-4">
                    Xóa tài khoản
                </h3>

                <div className="p-4 bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-800 rounded-lg mb-6">
                    <div className="flex items-start gap-3">
                        <AlertTriangle className="w-5 h-5 text-red-600 dark:text-red-400 flex-shrink-0 mt-0.5" />
                        <div className="text-sm text-red-600 dark:text-red-400">
                            <p className="font-semibold mb-2">Cảnh báo:</p>
                            <ul className="list-disc list-inside space-y-1">
                                <li>Tất cả dữ liệu của bạn sẽ bị xóa vĩnh viễn</li>
                                <li>Bạn sẽ mất quyền truy cập vào tất cả truyện đã viết</li>
                                <li>Coin và các giao dịch sẽ không thể khôi phục</li>
                                <li>Hành động này không thể hoàn tác</li>
                            </ul>
                        </div>
                    </div>
                </div>

                <div className="space-y-6">
                    <div>
                        <label className="block text-sm font-semibold text-slate-700 dark:text-slate-300 mb-2">
                            Để xác nhận, vui lòng nhập <span className="font-mono text-red-600 dark:text-red-400">{requiredText}</span>
                        </label>
                        <input
                            type="text"
                            value={confirmText}
                            onChange={(e) => {
                                setConfirmText(e.target.value);
                                setError('');
                            }}
                            className="block w-full px-4 py-3 bg-white dark:bg-slate-700 border border-slate-200 dark:border-slate-600 rounded-lg text-slate-900 dark:text-white focus:ring-2 focus:ring-red-500/50 focus:border-red-500 transition-all outline-none font-mono"
                            placeholder={requiredText}
                        />
                        {error && (
                            <p className="mt-2 text-sm text-red-600 dark:text-red-400">{error}</p>
                        )}
                    </div>

                    <div className="flex justify-end gap-4">
                        <button
                            onClick={handleDelete}
                            disabled={loading || confirmText !== requiredText}
                            className="px-6 py-3 bg-red-600 text-white font-bold rounded-lg hover:bg-red-700 transition-all disabled:opacity-50 disabled:cursor-not-allowed shadow-lg shadow-red-600/25"
                        >
                            {loading ? 'Đang xóa...' : 'Xóa tài khoản'}
                        </button>
                    </div>
                </div>
            </div>
        </div>
    );
}
