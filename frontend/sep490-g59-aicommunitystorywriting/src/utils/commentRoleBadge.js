/**
 * Badge hiển thị cạnh tên người bình luận (theo UserRole + UserCreatedAt từ API).
 * USER: phân theo tuổi tài khoản (ngày từ users.created_at).
 */
export function getCommentRoleBadge(userRole, userCreatedAtIso) {
    const r = (userRole ?? '').toString().trim().toUpperCase();

    if (r === 'AUTHOR') {
        return { label: 'Tác giả', className: 'bg-amber-600 text-white' };
    }
    if (r === 'ADMIN') {
        return { label: 'Quản trị viên', className: 'bg-red-600 text-white' };
    }
    if (r === 'MODERATOR') {
        return { label: 'Kiểm duyệt viên', className: 'bg-violet-600 text-white' };
    }
    if (r === 'COMPLIANCE') {
        return { label: 'Tuân thủ', className: 'bg-orange-600 text-white' };
    }

    // USER hoặc role khác không map → coi như độc giả / thành viên
    let days = null;
    if (userCreatedAtIso) {
        const t = new Date(userCreatedAtIso).getTime();
        if (!Number.isNaN(t)) {
            days = Math.floor((Date.now() - t) / 86400000);
        }
    }

    if (days != null && days >= 0 && days < 30) {
        return { label: 'Thành viên mới', className: 'bg-sky-500 text-slate-900' };
    }
    if (days != null && days >= 0 && days < 180) {
        return { label: 'Độc giả', className: 'bg-primary text-white' };
    }
    if (days != null && days >= 180) {
        return { label: 'Độc giả kỳ cựu', className: 'bg-emerald-600 text-white' };
    }

    // Không có created_at: hiển thị trung tính
    return { label: 'Độc giả', className: 'bg-primary text-white' };
}
