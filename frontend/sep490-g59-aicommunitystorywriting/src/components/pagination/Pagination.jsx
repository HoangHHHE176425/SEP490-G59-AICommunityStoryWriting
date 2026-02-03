import { ChevronLeft, ChevronRight } from 'lucide-react';

/**
 * Pagination Component - Tái sử dụng cho các danh sách
 * 
 * @param {number} currentPage - Trang hiện tại (bắt đầu từ 1)
 * @param {number} totalPages - Tổng số trang
 * @param {number} totalItems - Tổng số items
 * @param {number} itemsPerPage - Số items mỗi trang
 * @param {function} onPageChange - Callback khi đổi trang
 * @param {boolean} showItemCount - Hiển thị "Hiển thị X / Y items"
 */
export function Pagination({
    currentPage = 1,
    totalPages = 1,
    totalItems = 0,
    itemsPerPage = 10,
    onPageChange,
    showItemCount = true,
    itemLabel = 'items' // "thể loại", "truyện", etc.
}) {
    // Tính toán items hiện tại
    const startItem = totalItems === 0 ? 0 : (currentPage - 1) * itemsPerPage + 1;
    const endItem = Math.min(currentPage * itemsPerPage, totalItems);

    // Tạo danh sách số trang để hiển thị
    const getPageNumbers = () => {
        const pages = [];
        const maxVisible = 5; // Số trang tối đa hiển thị

        if (totalPages <= maxVisible) {
            // Nếu ít trang, hiển thị tất cả
            for (let i = 1; i <= totalPages; i++) {
                pages.push(i);
            }
        } else {
            // Logic phức tạp hơn cho nhiều trang
            if (currentPage <= 3) {
                // Gần đầu
                pages.push(1, 2, 3, 4, '...', totalPages);
            } else if (currentPage >= totalPages - 2) {
                // Gần cuối
                pages.push(1, '...', totalPages - 3, totalPages - 2, totalPages - 1, totalPages);
            } else {
                // Ở giữa
                pages.push(1, '...', currentPage - 1, currentPage, currentPage + 1, '...', totalPages);
            }
        }

        return pages;
    };

    const pageNumbers = getPageNumbers();

    const handlePrevious = () => {
        if (currentPage > 1) {
            onPageChange(currentPage - 1);
        }
    };

    const handleNext = () => {
        if (currentPage < totalPages) {
            onPageChange(currentPage + 1);
        }
    };

    const handlePageClick = (page) => {
        if (page !== '...' && page !== currentPage) {
            onPageChange(page);
        }
    };

    return (
        <div style={{
            padding: '1rem',
            borderTop: '1px solid #e2e8f0',
            display: 'flex',
            alignItems: 'center',
            justifyContent: 'space-between',
            flexWrap: 'wrap',
            gap: '1rem',
            backgroundColor: '#ffffff'
        }}>
            {/* Item Count */}
            {showItemCount && (
                <p style={{ fontSize: '0.875rem', color: '#64748b', margin: 0 }}>
                    Hiển thị <span style={{ fontWeight: 600 }}>{startItem}-{endItem}</span> / {totalItems} {itemLabel}
                </p>
            )}

            {/* Page Buttons */}
            <div style={{ display: 'flex', gap: '0.5rem', alignItems: 'center' }}>
                {/* Previous Button */}
                <button
                    onClick={handlePrevious}
                    disabled={currentPage === 1}
                    style={{
                        padding: '0.5rem 0.75rem',
                        border: '1px solid #e2e8f0',
                        borderRadius: '0.5rem',
                        fontSize: '0.875rem',
                        backgroundColor: currentPage === 1 ? '#f8fafc' : '#ffffff',
                        color: currentPage === 1 ? '#94a3b8' : '#1e293b',
                        cursor: currentPage === 1 ? 'not-allowed' : 'pointer',
                        transition: 'all 0.2s',
                        display: 'flex',
                        alignItems: 'center',
                        gap: '0.25rem',
                        fontWeight: 500
                    }}
                    onMouseEnter={(e) => {
                        if (currentPage !== 1) {
                            e.currentTarget.style.backgroundColor = '#f8fafc';
                            e.currentTarget.style.borderColor = '#cbd5e1';
                        }
                    }}
                    onMouseLeave={(e) => {
                        if (currentPage !== 1) {
                            e.currentTarget.style.backgroundColor = '#ffffff';
                            e.currentTarget.style.borderColor = '#e2e8f0';
                        }
                    }}
                >
                    <ChevronLeft style={{ width: '16px', height: '16px' }} />
                    <span className="hidden sm:inline">Trước</span>
                </button>

                {/* Page Numbers */}
                {pageNumbers.map((page, index) => (
                    page === '...' ? (
                        <span
                            key={`ellipsis-${index}`}
                            style={{
                                padding: '0.5rem 0.75rem',
                                fontSize: '0.875rem',
                                color: '#64748b',
                                userSelect: 'none'
                            }}
                        >
                            ...
                        </span>
                    ) : (
                        <button
                            key={page}
                            onClick={() => handlePageClick(page)}
                            style={{
                                padding: '0.5rem 0.75rem',
                                minWidth: '40px',
                                border: page === currentPage ? 'none' : '1px solid #e2e8f0',
                                borderRadius: '0.5rem',
                                fontSize: '0.875rem',
                                backgroundColor: page === currentPage ? '#13ec5b' : '#ffffff',
                                color: page === currentPage ? '#ffffff' : '#1e293b',
                                cursor: page === currentPage ? 'default' : 'pointer',
                                transition: 'all 0.2s',
                                fontWeight: page === currentPage ? 600 : 500
                            }}
                            onMouseEnter={(e) => {
                                if (page !== currentPage) {
                                    e.currentTarget.style.backgroundColor = '#f8fafc';
                                    e.currentTarget.style.borderColor = '#cbd5e1';
                                }
                            }}
                            onMouseLeave={(e) => {
                                if (page !== currentPage) {
                                    e.currentTarget.style.backgroundColor = '#ffffff';
                                    e.currentTarget.style.borderColor = '#e2e8f0';
                                }
                            }}
                        >
                            {page}
                        </button>
                    )
                ))}

                {/* Next Button */}
                <button
                    onClick={handleNext}
                    disabled={currentPage === totalPages}
                    style={{
                        padding: '0.5rem 0.75rem',
                        border: '1px solid #e2e8f0',
                        borderRadius: '0.5rem',
                        fontSize: '0.875rem',
                        backgroundColor: currentPage === totalPages ? '#f8fafc' : '#ffffff',
                        color: currentPage === totalPages ? '#94a3b8' : '#1e293b',
                        cursor: currentPage === totalPages ? 'not-allowed' : 'pointer',
                        transition: 'all 0.2s',
                        display: 'flex',
                        alignItems: 'center',
                        gap: '0.25rem',
                        fontWeight: 500
                    }}
                    onMouseEnter={(e) => {
                        if (currentPage !== totalPages) {
                            e.currentTarget.style.backgroundColor = '#f8fafc';
                            e.currentTarget.style.borderColor = '#cbd5e1';
                        }
                    }}
                    onMouseLeave={(e) => {
                        if (currentPage !== totalPages) {
                            e.currentTarget.style.backgroundColor = '#ffffff';
                            e.currentTarget.style.borderColor = '#e2e8f0';
                        }
                    }}
                >
                    <span className="hidden sm:inline">Sau</span>
                    <ChevronRight style={{ width: '16px', height: '16px' }} />
                </button>
            </div>
        </div>
    );
}
