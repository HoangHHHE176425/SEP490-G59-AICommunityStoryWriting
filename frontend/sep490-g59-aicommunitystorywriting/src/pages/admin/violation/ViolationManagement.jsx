import { useCallback, useEffect, useMemo, useState } from 'react';
import { RotateCcw, X } from 'lucide-react';
import { Pagination } from '../../../components/pagination/Pagination';
import { useAuth } from '../../../contexts/AuthContext';
import {
    adminReleaseComplianceCommentClaim,
    adminReleaseComplianceStoryClaim,
    claimComplianceCommentReports,
    claimComplianceStoryReports,
    getAdminComplianceLockRequests,
    getAdminComplianceLogs,
    getAdminComplianceOfficers,
    getAdminCompliancePerformance,
    getComplianceCommentReports,
    getComplianceStoryReports,
    requestComplianceCommentAdminAction,
    requestComplianceStoryAdminAction,
    resolveAdminComplianceLockRequest,
    resolveAllOpenComplianceCommentReports,
    resolveAllOpenComplianceStoryReports,
    resolveComplianceCommentReport,
    resolveComplianceStoryReport,
    setComplianceCommentThreadHidden,
    setComplianceStoryCommentsDisabled,
    setComplianceStoryFlag,
    setComplianceStoryHidden,
} from '../../../api/admin/adminComplianceApi';

const PAGE_SIZE = 10;

function formatDate(value) {
    if (!value) return '—';
    const d = new Date(value);
    if (Number.isNaN(d.getTime())) return '—';
    return d.toLocaleString('vi-VN');
}

function readPaged(data) {
    return {
        items: data?.items ?? data?.Items ?? [],
        totalCount: Number(data?.totalCount ?? data?.TotalCount ?? 0) || 0,
        page: Number(data?.page ?? data?.Page ?? 1) || 1,
    };
}

function Modal({ title, onClose, children }) {
    return (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(2,6,23,0.45)', zIndex: 1200, display: 'flex', alignItems: 'center', justifyContent: 'center', padding: 12 }}>
            <div style={{ background: '#fff', width: 'min(1100px,96vw)', maxHeight: '88vh', borderRadius: 12, border: '1px solid #e2e8f0', display: 'flex', flexDirection: 'column' }}>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', borderBottom: '1px solid #e2e8f0', padding: '0.75rem 1rem' }}>
                    <h3 style={{ margin: 0 }}>{title}</h3>
                    <button style={btn} onClick={onClose}><X style={{ width: 16, height: 16 }} /> Đóng</button>
                </div>
                <div style={{ padding: '1rem', overflow: 'auto' }}>{children}</div>
            </div>
        </div>
    );
}

export default function ViolationManagement() {
    const { role } = useAuth();
    const roleUpper = String(role ?? '').toUpperCase();
    const isAdmin = roleUpper === 'ADMIN';
    const tabs = useMemo(() => {
        const base = [
            { id: 'story-reports', label: 'Report truyện' },
            { id: 'comment-reports', label: 'Report bình luận' },
            { id: 'compliance-logs', label: 'Compliance log' },
            { id: 'compliance-performance', label: 'Hiệu suất compliance' },
        ];
        if (isAdmin) base.splice(2, 0, { id: 'lock-requests', label: 'Yêu cầu gỡ lock' });
        return base;
    }, [isAdmin]);

    const [activeTab, setActiveTab] = useState('story-reports');
    const [loading, setLoading] = useState(false);
    const [error, setError] = useState(null);
    const [rows, setRows] = useState([]);
    const [totalCount, setTotalCount] = useState(0);
    const [currentPage, setCurrentPage] = useState(1);
    const [filters, setFilters] = useState({ search: '', statuses: 'NEW,IN_REVIEW', flaggedOnly: false });
    const [selectedStory, setSelectedStory] = useState(null);
    const [storyTickets, setStoryTickets] = useState([]);
    const [storyTicketLoading, setStoryTicketLoading] = useState(false);
    const [selectedComment, setSelectedComment] = useState(null);
    const [actionModal, setActionModal] = useState(null);
    const [adminActionForm, setAdminActionForm] = useState({ requestKind: 'BAN_USER', message: '', proposedSuspendUntilUtc: '' });
    const [officers, setOfficers] = useState([]);
    const [lockResolveForm, setLockResolveForm] = useState({ decision: 'APPROVE_UNLOCK', newAssigneeId: '', adminNote: '' });

    const totalPages = useMemo(() => Math.max(1, Math.ceil(totalCount / PAGE_SIZE)), [totalCount]);

    const loadData = useCallback(async (page = 1) => {
        setLoading(true);
        setError(null);
        try {
            let data;
            if (activeTab === 'story-reports') {
                data = await getComplianceStoryReports({
                    page, pageSize: PAGE_SIZE, groupByStory: true, sortBy: 'priority_desc',
                    statuses: filters.statuses || undefined, search: filters.search || undefined, flaggedOnly: filters.flaggedOnly ? true : undefined,
                });
            } else if (activeTab === 'comment-reports') {
                data = await getComplianceCommentReports({ page, pageSize: PAGE_SIZE, status: filters.statuses || undefined, search: filters.search || undefined });
            } else if (activeTab === 'lock-requests') {
                data = await getAdminComplianceLockRequests({ status: 'PENDING' });
            } else if (activeTab === 'compliance-logs') {
                data = await getAdminComplianceLogs({ page, pageSize: PAGE_SIZE, search: filters.search || undefined, sortBy: 'created_at', sortOrder: 'desc' });
            } else {
                data = await getAdminCompliancePerformance({ page, pageSize: PAGE_SIZE, search: filters.search || undefined, sortBy: 'total', sortOrder: 'desc' });
            }
            const paged = readPaged(data);
            setRows(Array.isArray(paged.items) ? paged.items : []);
            setTotalCount(activeTab === 'lock-requests' ? (paged.items?.length ?? 0) : paged.totalCount);
            setCurrentPage(activeTab === 'lock-requests' ? 1 : paged.page);
        } catch (e) {
            setError(e?.response?.data?.message ?? e?.message ?? 'Không tải được dữ liệu vi phạm.');
            setRows([]);
            setTotalCount(0);
            setCurrentPage(1);
        } finally {
            setLoading(false);
        }
    }, [activeTab, filters.flaggedOnly, filters.search, filters.statuses]);

    useEffect(() => { loadData(1); }, [activeTab, loadData]);
    useEffect(() => {
        if (isAdmin) getAdminComplianceOfficers().then((x) => setOfficers(Array.isArray(x) ? x : x?.items ?? [])).catch(() => setOfficers([]));
    }, [isAdmin]);

    const actionWithReload = async (fn) => {
        try {
            await fn();
            await loadData(currentPage);
        } catch (e) {
            alert(e?.response?.data?.message ?? e?.message ?? 'Thao tác thất bại.');
        }
    };

    const openStoryTicketsModal = async (story) => {
        setSelectedStory(story);
        setStoryTicketLoading(true);
        try {
            const data = await getComplianceStoryReports({
                groupByStory: false,
                storyId: story.storyId,
                statuses: 'ALL',
                page: 1,
                pageSize: 200,
                sortBy: 'newest',
            });
            const list = data?.items ?? data?.Items ?? [];
            setStoryTickets(Array.isArray(list) ? list : []);
        } catch (e) {
            setStoryTickets([]);
            alert(e?.response?.data?.message ?? e?.message ?? 'Không tải được ticket chi tiết.');
        } finally {
            setStoryTicketLoading(false);
        }
    };

    const submitAdminAction = async () => {
        if (!actionModal) return;
        const payload = {
            requestKind: adminActionForm.requestKind,
            message: adminActionForm.message || undefined,
            proposedSuspendUntilUtc: adminActionForm.requestKind === 'SUSPEND_AUTHOR_WRITING' && adminActionForm.proposedSuspendUntilUtc
                ? new Date(adminActionForm.proposedSuspendUntilUtc).toISOString()
                : undefined,
        };
        if (actionModal.type === 'story') {
            await actionWithReload(() => requestComplianceStoryAdminAction(actionModal.targetId, payload));
        } else {
            await actionWithReload(() => requestComplianceCommentAdminAction(actionModal.targetId, payload));
        }
        setActionModal(null);
    };

    const renderStoryReports = () => (
        <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                <thead><tr style={{ background: '#f8fafc' }}>
                    <th style={th}>Truyện</th><th style={th}>Ưu tiên</th><th style={th}>Report</th><th style={th}>Claim</th><th style={th}>SLA</th><th style={th}>Thao tác</th>
                </tr></thead>
                <tbody>
                    {rows.map((r) => (
                        <tr key={r.storyId} style={{ borderTop: '1px solid #e2e8f0' }}>
                            <td style={td}><div style={{ fontWeight: 600 }}>{r.storyTitle || '—'}</div><div style={{ color: '#64748b', fontSize: 12 }}>{r.storyId}</div></td>
                            <td style={td}>{(r.priorityScore ?? 0).toFixed?.(1) ?? r.priorityScore}</td>
                            <td style={td}>{r.reportCount ?? 0}</td>
                            <td style={td}>{r.isComplianceLocked ? `Đã lock - ${r.complianceClaimedByDisplayName || '—'}` : 'Chưa lock'}</td>
                            <td style={td}>{r.complianceHandlingSlaMessageVi || '—'}</td>
                            <td style={td}>
                                <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                                    <button style={btn} onClick={() => openStoryTicketsModal(r)}>Chi tiết ticket</button>
                                    <button style={btn} onClick={() => actionWithReload(() => claimComplianceStoryReports(r.storyId))}>Claim</button>
                                    {isAdmin && <button style={btn} onClick={() => actionWithReload(() => adminReleaseComplianceStoryClaim(r.storyId))}>Admin release claim</button>}
                                    <button style={btn} onClick={() => actionWithReload(() => resolveAllOpenComplianceStoryReports(r.storyId, { status: 'RESOLVED' }))}>Resolve all</button>
                                    <button style={btn} onClick={() => actionWithReload(() => setComplianceStoryFlag(r.storyId, { flagged: !r.complianceFlagged }))}>{r.complianceFlagged ? 'Bỏ cờ' : 'Gắn cờ'}</button>
                                    <button style={btn} onClick={() => actionWithReload(() => setComplianceStoryCommentsDisabled(r.storyId, { value: !r.commentsDisabled }))}>{r.commentsDisabled ? 'Mở comment' : 'Khóa comment'}</button>
                                    <button style={btn} onClick={() => actionWithReload(() => setComplianceStoryHidden(r.storyId, { value: !r.complianceHidden }))}>{r.complianceHidden ? 'Hiện truyện' : 'Ẩn truyện'}</button>
                                    <button style={btn} onClick={() => { setAdminActionForm({ requestKind: 'BAN_USER', message: '', proposedSuspendUntilUtc: '' }); setActionModal({ type: 'story', targetId: r.storyId }); }}>Yêu cầu BAN/SUSPEND</button>
                                </div>
                            </td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );

    const renderCommentReports = () => (
        <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                <thead><tr style={{ background: '#f8fafc' }}>
                    <th style={th}>Ticket</th><th style={th}>Truyện</th><th style={th}>Ưu tiên</th><th style={th}>Trạng thái</th><th style={th}>Cảnh báo</th><th style={th}>Thao tác</th>
                </tr></thead>
                <tbody>
                    {rows.map((r) => (
                        <tr key={r.reportId} style={{ borderTop: '1px solid #e2e8f0' }}>
                            <td style={td}><div style={{ fontWeight: 600 }}>{r.reportId}</div><div style={{ color: '#64748b', fontSize: 12 }}>Comment: {r.commentId}</div></td>
                            <td style={td}>{r.storyTitle || '—'}</td>
                            <td style={td}>{(r.priorityScore ?? 0).toFixed?.(1) ?? r.priorityScore}</td>
                            <td style={td}>{r.status || '—'}</td>
                            <td style={td}>{r.adminOrModeratorReplyWarningVi || '—'}</td>
                            <td style={td}>
                                <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                                    <button style={btn} onClick={() => setSelectedComment(r)}>Chi tiết ticket</button>
                                    <button style={btn} onClick={() => actionWithReload(() => claimComplianceCommentReports(r.commentId))}>Claim</button>
                                    {isAdmin && <button style={btn} onClick={() => actionWithReload(() => adminReleaseComplianceCommentClaim(r.commentId))}>Admin release claim</button>}
                                    <button style={btn} onClick={() => actionWithReload(() => resolveAllOpenComplianceCommentReports(r.commentId, { status: 'RESOLVED', hideComment: true, includeReplies: true }))}>Resolve all</button>
                                    <button style={btn} onClick={() => actionWithReload(() => setComplianceCommentThreadHidden(r.commentId, { value: true, includeReplies: true }))}>Ẩn thread</button>
                                    <button style={btn} onClick={() => { setAdminActionForm({ requestKind: 'BAN_USER', message: '', proposedSuspendUntilUtc: '' }); setActionModal({ type: 'comment', targetId: r.commentId }); }}>Yêu cầu BAN/SUSPEND</button>
                                </div>
                            </td>
                        </tr>
                    ))}
                </tbody>
            </table>
        </div>
    );

    const renderLockRequests = () => (
        <div style={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                <thead><tr style={{ background: '#f8fafc' }}>
                    <th style={th}>Truyện</th><th style={th}>Người gửi</th><th style={th}>Message</th><th style={th}>Thời điểm</th><th style={th}>Thao tác admin</th>
                </tr></thead>
                <tbody>{rows.map((r) => (
                    <tr key={r.id} style={{ borderTop: '1px solid #e2e8f0' }}>
                        <td style={td}><div style={{ fontWeight: 600 }}>{r.storyTitle || '—'}</div><div style={{ color: '#64748b', fontSize: 12 }}>{r.storyId}</div></td>
                        <td style={td}>{r.requesterDisplayName || r.requesterEmail || '—'}</td>
                        <td style={td}>{r.message || '—'}</td>
                        <td style={td}>{formatDate(r.createdAtUtc)}</td>
                        <td style={td}>
                            <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap' }}>
                                <button style={btn} onClick={() => { setLockResolveForm({ decision: 'APPROVE_UNLOCK', newAssigneeId: '', adminNote: '' }); setActionModal({ type: 'lock', targetId: r.id }); }}>Resolve</button>
                                <button style={btn} onClick={() => actionWithReload(() => adminReleaseComplianceStoryClaim(r.storyId))}>Gỡ lock trực tiếp</button>
                            </div>
                        </td>
                    </tr>
                ))}</tbody>
            </table>
        </div>
    );

    return (
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
            <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: 12, padding: '1rem' }}>
                <h2 style={{ margin: '0 0 0.75rem 0' }}>Xử lý báo cáo vi phạm (Compliance)</h2>
                <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap', marginBottom: '0.75rem' }}>
                    {tabs.map((tab) => (
                        <button key={tab.id} onClick={() => setActiveTab(tab.id)} style={{ ...btn, background: activeTab === tab.id ? '#13ec5b' : '#fff' }}>{tab.label}</button>
                    ))}
                </div>
                <div style={{ display: 'grid', gridTemplateColumns: '2fr 1fr 1fr', gap: 8 }}>
                    <input value={filters.search} onChange={(e) => setFilters((p) => ({ ...p, search: e.target.value }))} placeholder="Tìm kiếm..." style={input} />
                    <input value={filters.statuses} onChange={(e) => setFilters((p) => ({ ...p, statuses: e.target.value }))} placeholder="Statuses (VD: NEW,IN_REVIEW)" style={input} />
                    <button onClick={() => setFilters({ search: '', statuses: 'NEW,IN_REVIEW', flaggedOnly: false })} style={{ ...btn, justifyContent: 'center' }}><RotateCcw style={{ width: 14, height: 14 }} /> Đặt lại</button>
                </div>
                {activeTab === 'story-reports' && (
                    <label style={{ display: 'inline-flex', gap: 8, marginTop: 8, fontSize: 14 }}>
                        <input type="checkbox" checked={filters.flaggedOnly} onChange={(e) => setFilters((p) => ({ ...p, flaggedOnly: e.target.checked }))} />
                        Chỉ hiển thị truyện đã gắn cờ
                    </label>
                )}
            </div>

            <div style={{ background: '#fff', border: '1px solid #e2e8f0', borderRadius: 12, overflow: 'hidden' }}>
                {loading ? <div style={{ padding: '1rem' }}>Đang tải...</div> : error ? <div style={{ padding: '1rem', color: '#b91c1c' }}>{error}</div> : (
                    activeTab === 'story-reports' ? renderStoryReports()
                        : activeTab === 'comment-reports' ? renderCommentReports()
                            : activeTab === 'lock-requests' ? renderLockRequests()
                                : activeTab === 'compliance-logs' ? (
                                    <table style={{ width: '100%', borderCollapse: 'collapse' }}><thead><tr style={{ background: '#f8fafc' }}><th style={th}>Thời điểm</th><th style={th}>Compliance</th><th style={th}>Nguồn</th><th style={th}>Action</th><th style={th}>Status</th></tr></thead><tbody>{rows.map((r) => <tr key={r.rowId} style={{ borderTop: '1px solid #e2e8f0' }}><td style={td}>{formatDate(r.createdAtUtc)}</td><td style={td}>{r.complianceUserName || '—'}</td><td style={td}>{r.source || '—'}</td><td style={td}>{r.action || '—'}</td><td style={td}>{r.status || '—'}</td></tr>)}</tbody></table>
                                ) : (
                                    <table style={{ width: '100%', borderCollapse: 'collapse' }}><thead><tr style={{ background: '#f8fafc' }}><th style={th}>Compliance</th><th style={th}>Resolved</th><th style={th}>Dismissed</th><th style={th}>Story</th><th style={th}>Comment</th><th style={th}>Tổng</th></tr></thead><tbody>{rows.map((r) => <tr key={r.complianceUserId} style={{ borderTop: '1px solid #e2e8f0' }}><td style={td}>{r.complianceUserName || r.complianceUserId}</td><td style={td}>{r.resolvedCount ?? 0}</td><td style={td}>{r.dismissedCount ?? 0}</td><td style={td}>{r.storyReportResolvedCount ?? 0}</td><td style={td}>{r.commentReportResolvedCount ?? 0}</td><td style={td}>{r.total ?? 0}</td></tr>)}</tbody></table>
                                )
                )}
                {activeTab !== 'lock-requests' && (
                    <Pagination currentPage={currentPage} totalPages={totalPages} totalItems={totalCount} itemsPerPage={PAGE_SIZE} onPageChange={(p) => loadData(p)} itemLabel="bản ghi" />
                )}
            </div>

            {selectedStory && (
                <Modal title={`Ticket chi tiết - ${selectedStory.storyTitle || selectedStory.storyId}`} onClose={() => setSelectedStory(null)}>
                    {storyTicketLoading ? <div>Đang tải ticket...</div> : (
                        <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                            <thead><tr style={{ background: '#f8fafc' }}><th style={th}>ReportId</th><th style={th}>Reason</th><th style={th}>Status</th><th style={th}>Created</th><th style={th}>Thao tác</th></tr></thead>
                            <tbody>{storyTickets.map((t) => (
                                <tr key={t.reportId} style={{ borderTop: '1px solid #e2e8f0' }}>
                                    <td style={td}>{t.reportId}</td><td style={td}>{t.reasonCode || '—'}</td><td style={td}>{t.status || '—'}</td><td style={td}>{formatDate(t.createdAtUtc)}</td>
                                    <td style={td}><div style={{ display: 'flex', gap: 6 }}><button style={btn} onClick={() => actionWithReload(() => resolveComplianceStoryReport(t.reportId, { status: 'RESOLVED' }))}>Resolve ticket</button><button style={btn} onClick={() => actionWithReload(() => resolveComplianceStoryReport(t.reportId, { status: 'DISMISSED' }))}>Dismiss</button></div></td>
                                </tr>
                            ))}</tbody>
                        </table>
                    )}
                </Modal>
            )}

            {selectedComment && (
                <Modal title={`Ticket comment - ${selectedComment.reportId}`} onClose={() => setSelectedComment(null)}>
                    <div style={{ display: 'grid', gap: 8 }}>
                        <div><b>CommentId:</b> {selectedComment.commentId}</div>
                        <div><b>Story:</b> {selectedComment.storyTitle || '—'}</div>
                        <div><b>Reason:</b> {selectedComment.reasonCode || '—'}</div>
                        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
                            <button style={btn} onClick={() => actionWithReload(() => resolveComplianceCommentReport(selectedComment.reportId, { status: 'RESOLVED', hideComment: true, includeReplies: true }))}>Resolve ticket</button>
                            <button style={btn} onClick={() => actionWithReload(() => resolveComplianceCommentReport(selectedComment.reportId, { status: 'DISMISSED' }))}>Dismiss ticket</button>
                            <button style={btn} onClick={() => actionWithReload(() => setComplianceCommentThreadHidden(selectedComment.commentId, { value: true, includeReplies: true }))}>Ẩn thread</button>
                        </div>
                    </div>
                </Modal>
            )}

            {actionModal?.type === 'story' || actionModal?.type === 'comment' ? (
                <Modal title="Gửi admin-action-request (BAN/SUSPEND)" onClose={() => setActionModal(null)}>
                    <div style={{ display: 'grid', gap: 8 }}>
                        <select value={adminActionForm.requestKind} onChange={(e) => setAdminActionForm((p) => ({ ...p, requestKind: e.target.value }))} style={input}>
                            <option value="BAN_USER">BAN_USER</option>
                            <option value="SUSPEND_AUTHOR_WRITING">SUSPEND_AUTHOR_WRITING</option>
                        </select>
                        {adminActionForm.requestKind === 'SUSPEND_AUTHOR_WRITING' && (
                            <input type="datetime-local" value={adminActionForm.proposedSuspendUntilUtc} onChange={(e) => setAdminActionForm((p) => ({ ...p, proposedSuspendUntilUtc: e.target.value }))} style={input} />
                        )}
                        <textarea value={adminActionForm.message} onChange={(e) => setAdminActionForm((p) => ({ ...p, message: e.target.value }))} placeholder="Lý do đề xuất..." style={{ ...input, minHeight: 90 }} />
                        <div><button style={btn} onClick={submitAdminAction}>Gửi yêu cầu</button></div>
                    </div>
                </Modal>
            ) : null}

            {actionModal?.type === 'lock' && (
                <Modal title="Admin xử lý yêu cầu gỡ lock" onClose={() => setActionModal(null)}>
                    <div style={{ display: 'grid', gap: 8 }}>
                        <select value={lockResolveForm.decision} onChange={(e) => setLockResolveForm((p) => ({ ...p, decision: e.target.value }))} style={input}>
                            <option value="APPROVE_UNLOCK">APPROVE_UNLOCK</option>
                            <option value="APPROVE_REASSIGN">APPROVE_REASSIGN</option>
                            <option value="REJECT">REJECT</option>
                        </select>
                        {lockResolveForm.decision === 'APPROVE_REASSIGN' && (
                            <select value={lockResolveForm.newAssigneeId} onChange={(e) => setLockResolveForm((p) => ({ ...p, newAssigneeId: e.target.value }))} style={input}>
                                <option value="">Chọn compliance mới</option>
                                {officers.map((o) => <option key={o.id} value={o.id}>{o.displayName || o.email || o.id}</option>)}
                            </select>
                        )}
                        <textarea value={lockResolveForm.adminNote} onChange={(e) => setLockResolveForm((p) => ({ ...p, adminNote: e.target.value }))} placeholder="Ghi chú admin..." style={{ ...input, minHeight: 90 }} />
                        <div>
                            <button style={btn} onClick={async () => {
                                await actionWithReload(() => resolveAdminComplianceLockRequest(actionModal.targetId, {
                                    decision: lockResolveForm.decision,
                                    newAssigneeId: lockResolveForm.decision === 'APPROVE_REASSIGN' ? (lockResolveForm.newAssigneeId || undefined) : undefined,
                                    adminNote: lockResolveForm.adminNote || undefined,
                                }));
                                setActionModal(null);
                            }}>Xác nhận</button>
                        </div>
                    </div>
                </Modal>
            )}
        </div>
    );
}

const th = { textAlign: 'left', padding: '0.75rem', borderBottom: '1px solid #e2e8f0', color: '#0f172a' };
const td = { padding: '0.75rem', color: '#334155', verticalAlign: 'top' };
const input = { border: '1px solid #cbd5e1', borderRadius: 8, padding: '0.55rem 0.7rem', fontSize: '0.875rem' };
const btn = { display: 'inline-flex', alignItems: 'center', gap: 6, border: '1px solid #cbd5e1', background: '#fff', color: '#334155', borderRadius: 8, padding: '0.4rem 0.7rem', cursor: 'pointer' };
