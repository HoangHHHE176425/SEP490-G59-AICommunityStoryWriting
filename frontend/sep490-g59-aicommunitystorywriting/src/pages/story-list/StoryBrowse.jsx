/* eslint-disable react-hooks/purity */
import { useState } from 'react';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import { Pagination } from '../../components/pagination/Pagination';
import { FilterSidebar } from '../../components/story-list/FilterSidebar';
import { BrowseTopBar } from '../../components/story-list/BrowseTopBar';
import { StoryCard } from '../../components/story-list/StoryCard';
import { StoryListItem } from '../../components/story-list/StoryListItem';
import { EmptyState } from '../../components/story-list/EmptyState';

// eslint-disable-next-line no-unused-vars
export function StoryBrowse({ onBack }) {
    const [searchQuery, setSearchQuery] = useState('');
    const [viewMode, setViewMode] = useState('grid'); // 'grid' | 'list'
    const [sortBy, setSortBy] = useState('newest'); // 'newest' | 'popular' | 'views' | 'rating'
    const [currentPage, setCurrentPage] = useState(1);
    const [showMobileFilter, setShowMobileFilter] = useState(false);

    // Filters
    const [selectedStoryType, setSelectedStoryType] = useState('all'); // 'all' | 'long' | 'short'
    const [selectedCategories, setSelectedCategories] = useState([]);
    const [selectedStatus, setSelectedStatus] = useState('all'); // 'all' | 'ongoing' | 'completed'
    const [selectedAgeRating, setSelectedAgeRating] = useState('all'); // 'all' | 'all-ages' | '16+' | '18+'

    const itemsPerPage = 24;

    // Mock data - Categories
    const longStoryCategories = [
        'Tiên hiệp', 'Kiếm hiệp', 'Huyền huyễn', 'Đô thị',
        'Khoa học viễn tưởng', 'Lịch sử', 'Quân sự', 'Đam mỹ'
    ];

    const shortStoryCategories = [
        'Ngôn tình', 'Teen', 'Trinh thám', 'Kinh dị',
        'Trọng sinh', 'Xuyên không', 'Đồng nhân', 'Fanfiction'
    ];

    // Mock data - Stories
    const allStories = [
        {
            id: 1,
            title: 'Tu Tiên Chi Lộ: Hành Trình Vạn Năm',
            author: 'Quyền Đình',
            cover: 'https://images.unsplash.com/photo-1589998059171-988d887df646?w=300&h=400&fit=crop',
            type: 'long',
            categories: ['Tiên hiệp', 'Huyền huyễn'],
            status: 'ongoing',
            ageRating: 'all-ages',
            chapters: 450,
            views: 5200000,
            follows: 8900,
            rating: 4.8,
            lastUpdate: '2 giờ trước',
            description: 'Một thiếu niên bình thường bước vào con đường tu tiên đầy gian nan...'
        },
        {
            id: 2,
            title: 'Kiếm Đạo Độc Tôn',
            author: 'Phong Hỏa',
            cover: 'https://images.unsplash.com/photo-1612036801632-8e4cf4e2e1b7?w=300&h=400&fit=crop',
            type: 'long',
            categories: ['Kiếm hiệp', 'Huyền huyễn'],
            status: 'ongoing',
            ageRating: '16+',
            chapters: 523,
            views: 4100000,
            follows: 7200,
            rating: 4.7,
            lastUpdate: '5 giờ trước',
            description: 'Một thanh kiếm, một con đường, tôn vinh kiếm đạo...'
        },
        {
            id: 3,
            title: 'Ngôn Tình Mùa Hè',
            author: 'Minh Nguyệt',
            cover: 'https://images.unsplash.com/photo-1544947950-fa07a98d237f?w=300&h=400&fit=crop',
            type: 'short',
            categories: ['Ngôn tình', 'Teen'],
            status: 'completed',
            ageRating: 'all-ages',
            chapters: 45,
            views: 980000,
            follows: 3400,
            rating: 4.5,
            lastUpdate: '1 ngày trước',
            description: 'Câu chuyện tình yêu ngọt ngào của hai bạn học...'
        },
        {
            id: 4,
            title: 'Bí Ẩn Biệt Thự Cổ',
            author: 'Hắc Ảnh',
            cover: 'https://images.unsplash.com/photo-1518709268805-4e9042af9f23?w=300&h=400&fit=crop',
            type: 'short',
            categories: ['Trinh thám', 'Kinh dị'],
            status: 'ongoing',
            ageRating: '18+',
            chapters: 28,
            views: 650000,
            follows: 2100,
            rating: 4.6,
            lastUpdate: '3 giờ trước',
            description: 'Những sự kiện kỳ lạ xảy ra trong căn biệt thự bị bỏ hoang...'
        },
        // Thêm stories để test pagination
        ...Array.from({ length: 20 }, (_, i) => ({
            id: i + 5,
            title: `Truyện số ${i + 5}`,
            author: 'Tác giả',
            cover: `https://images.unsplash.com/photo-${1500000000000 + i * 10000000}?w=300&h=400&fit=crop`,
            type: i % 2 === 0 ? 'long' : 'short',
            categories: i % 2 === 0 ? ['Tiên hiệp'] : ['Ngôn tình'],
            status: i % 3 === 0 ? 'completed' : 'ongoing',
            ageRating: 'all-ages',
            chapters: Math.floor(Math.random() * 500) + 10,
            views: Math.floor(Math.random() * 1000000) + 50000,
            follows: Math.floor(Math.random() * 5000) + 500,
            rating: (Math.random() * 1.5 + 3.5).toFixed(1),
            lastUpdate: `${Math.floor(Math.random() * 24)} giờ trước`,
            description: 'Mô tả truyện...'
        }))
    ];

    // Filter stories
    const filteredStories = allStories.filter(story => {
        // Search query
        if (searchQuery && !story.title.toLowerCase().includes(searchQuery.toLowerCase()) &&
            !story.author.toLowerCase().includes(searchQuery.toLowerCase())) {
            return false;
        }

        // Story type
        if (selectedStoryType !== 'all' && story.type !== selectedStoryType) {
            return false;
        }

        // Categories
        if (selectedCategories.length > 0 &&
            !story.categories.some(cat => selectedCategories.includes(cat))) {
            return false;
        }

        // Status
        if (selectedStatus !== 'all' && story.status !== selectedStatus) {
            return false;
        }

        // Age rating
        if (selectedAgeRating !== 'all' && story.ageRating !== selectedAgeRating) {
            return false;
        }

        return true;
    });

    // Sort stories
    const sortedStories = [...filteredStories].sort((a, b) => {
        switch (sortBy) {
            case 'popular':
                return b.follows - a.follows;
            case 'views':
                return b.views - a.views;
            case 'rating':
                return b.rating - a.rating;
            case 'newest':
            default:
                return 0; // Mock: giữ nguyên thứ tự
        }
    });

    // Pagination
    const totalPages = Math.ceil(sortedStories.length / itemsPerPage);
    const paginatedStories = sortedStories.slice(
        (currentPage - 1) * itemsPerPage,
        currentPage * itemsPerPage
    );

    const clearAllFilters = () => {
        setSelectedStoryType('all');
        setSelectedCategories([]);
        setSelectedStatus('all');
        setSelectedAgeRating('all');
        setSearchQuery('');
        setCurrentPage(1);
    };

    const activeFiltersCount =
        (selectedStoryType !== 'all' ? 1 : 0) +
        selectedCategories.length +
        (selectedStatus !== 'all' ? 1 : 0) +
        (selectedAgeRating !== 'all' ? 1 : 0);

    // Handlers that reset page
    const handleSearchChange = (value) => {
        setSearchQuery(value);
        setCurrentPage(1);
    };

    const handleStoryTypeChange = (value) => {
        setSelectedStoryType(value);
        setSelectedCategories([]);
        setCurrentPage(1);
    };

    const handleCategoriesChange = (categories) => {
        setSelectedCategories(categories);
        setCurrentPage(1);
    };

    const handleStatusChange = (value) => {
        setSelectedStatus(value);
        setCurrentPage(1);
    };

    const handleAgeRatingChange = (value) => {
        setSelectedAgeRating(value);
        setCurrentPage(1);
    };

    return (
        <div style={{ minHeight: '100vh', backgroundColor: '#f8fafc', display: 'flex', flexDirection: 'column' }}>
            <Header />

            <div style={{ maxWidth: '1280px', margin: '0 auto', padding: '2rem 1rem', width: '100%', flex: 1 }}>
                <div style={{ display: 'grid', gridTemplateColumns: window.innerWidth >= 1024 ? '280px 1fr' : '1fr', gap: '2rem' }}>
                    {/* Sidebar Filters */}
                    <aside style={{ display: window.innerWidth >= 1024 ? 'block' : (showMobileFilter ? 'block' : 'none') }}>
                        <FilterSidebar
                            selectedStoryType={selectedStoryType}
                            setSelectedStoryType={handleStoryTypeChange}
                            selectedCategories={selectedCategories}
                            setSelectedCategories={handleCategoriesChange}
                            selectedStatus={selectedStatus}
                            setSelectedStatus={handleStatusChange}
                            selectedAgeRating={selectedAgeRating}
                            setSelectedAgeRating={handleAgeRatingChange}
                            activeFiltersCount={activeFiltersCount}
                            clearAllFilters={clearAllFilters}
                            longStoryCategories={longStoryCategories}
                            shortStoryCategories={shortStoryCategories}
                        />
                    </aside>

                    {/* Main Content */}
                    <main>
                        {/* Top Bar */}
                        <BrowseTopBar
                            searchQuery={searchQuery}
                            setSearchQuery={handleSearchChange}
                            viewMode={viewMode}
                            setViewMode={setViewMode}
                            sortBy={sortBy}
                            setSortBy={setSortBy}
                            totalResults={sortedStories.length}
                            showMobileFilter={showMobileFilter}
                            setShowMobileFilter={setShowMobileFilter}
                            activeFiltersCount={activeFiltersCount}
                        />

                        {/* Stories Grid/List */}
                        {paginatedStories.length === 0 ? (
                            <EmptyState onClearFilters={clearAllFilters} />
                        ) : (
                            <>
                                {viewMode === 'grid' ? (
                                    // Grid View
                                    <div style={{
                                        display: 'grid',
                                        gridTemplateColumns: 'repeat(auto-fill, minmax(160px, 1fr))',
                                        gap: '1.5rem'
                                    }}>
                                        {paginatedStories.map(story => (
                                            <StoryCard key={story.id} story={story} />
                                        ))}
                                    </div>
                                ) : (
                                    // List View
                                    <div style={{ display: 'flex', flexDirection: 'column', gap: '1rem' }}>
                                        {paginatedStories.map(story => (
                                            <StoryListItem key={story.id} story={story} />
                                        ))}
                                    </div>
                                )}

                                {/* Pagination */}
                                <div style={{ marginTop: '2rem' }}>
                                    <Pagination
                                        currentPage={currentPage}
                                        totalPages={totalPages}
                                        totalItems={sortedStories.length}
                                        itemsPerPage={itemsPerPage}
                                        onPageChange={setCurrentPage}
                                        itemLabel="truyện"
                                    />
                                </div>
                            </>
                        )}
                    </main>
                </div>
            </div>

            <Footer />
        </div>
    );
}
