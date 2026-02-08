import React, { useState } from 'react';
import { BookOpen, Heart, Lock, Clock, Star, TrendingUp } from 'lucide-react';
import { Link } from 'react-router-dom';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';

export function LibraryPage() {
  const [activeTab, setActiveTab] = useState('reading');

  const tabs = [
    { id: 'reading', label: 'Đang đọc', icon: BookOpen, count: 12 },
    { id: 'favorite', label: 'Yêu thích', icon: Heart, count: 23 },
    { id: 'unlocked', label: 'Đã mở khóa', icon: Lock, count: 8 },
  ];

  return (
    <>
      <Header />
      <div className="min-h-[calc(100vh-200px)] bg-gray-50 py-8">
        <div className="max-w-[1200px] mx-auto px-6">
        {/* Header */}
        <div className="mb-8">
          <h1 className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[36px] mb-2">
            Tủ truyện của tôi
          </h1>
          <p className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[16px]">
            Quản lý tất cả truyện bạn đang theo dõi
          </p>
        </div>

        {/* Tabs */}
        <div className="bg-white rounded-2xl shadow-sm border border-gray-100 overflow-hidden">
          <div className="border-b border-gray-200">
            <div className="flex">
              {tabs.map((tab) => {
                const Icon = tab.icon;
                return (
                  <button
                    key={tab.id}
                    onClick={() => setActiveTab(tab.id)}
                    className={`flex items-center gap-2 px-6 py-4 font-['Plus_Jakarta_Sans',sans-serif] font-semibold text-[14px] border-b-2 transition-colors ${
                      activeTab === tab.id
                        ? 'border-[#13EC5B] text-[#13EC5B]'
                        : 'border-transparent text-[#90A1B9] hover:text-[#1A2332]'
                    }`}
                  >
                    <Icon className="w-4 h-4" />
                    {tab.label}
                    <span
                      className={`px-2 py-0.5 rounded-full text-[12px] ${
                        activeTab === tab.id
                          ? 'bg-[#13EC5B]/10 text-[#13EC5B]'
                          : 'bg-gray-100 text-[#90A1B9]'
                      }`}
                    >
                      {tab.count}
                    </span>
                  </button>
                );
              })}
            </div>
          </div>

          {/* Tab Content */}
          <div className="p-6">
            {activeTab === 'reading' && <ReadingTab />}
            {activeTab === 'favorite' && <FavoriteTab />}
            {activeTab === 'unlocked' && <UnlockedTab />}
          </div>
        </div>
        </div>
      </div>
      <Footer />
    </>
  );
}

function ReadingTab() {
  const stories = [
    {
      id: 1,
      title: 'Võ Luyện Điên Phong',
      author: 'Thiên Tàm Thổ Đậu',
      cover: 'martial-arts-training',
      currentChapter: 95,
      totalChapters: 150,
      lastRead: '2 giờ trước',
      progress: 63,
      genre: 'Võ hiệp',
    },
    {
      id: 2,
      title: 'Thần Đạo Đan Tôn',
      author: 'Cô Đơn Lữ Khách',
      cover: 'ancient-alchemy',
      currentChapter: 48,
      totalChapters: 200,
      lastRead: '1 ngày trước',
      progress: 24,
      genre: 'Tu tiên',
    },
    {
      id: 3,
      title: 'Kiếm Đạo Độc Tôn',
      author: 'Thanh Lâu Bạch Nhật Mộng',
      cover: 'sword-cultivation',
      currentChapter: 120,
      totalChapters: 180,
      lastRead: '3 ngày trước',
      progress: 67,
      genre: 'Huyền huyễn',
    },
  ];

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between mb-4">
        <p className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[14px]">
          Bạn đang đọc {stories.length} truyện
        </p>
        <select className="px-4 py-2 border-2 border-gray-200 rounded-lg text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-medium text-[14px] focus:border-[#13EC5B] focus:outline-none">
          <option>Mới đọc nhất</option>
          <option>Tên A-Z</option>
          <option>Tiến độ cao nhất</option>
        </select>
      </div>

      {stories.map((story) => (
        <div
          key={story.id}
          className="flex gap-4 p-4 border border-gray-200 rounded-xl hover:border-[#13EC5B] transition-colors group"
        >
          <div className="w-24 h-32 bg-gradient-to-br from-[#2B7FFF] to-[#13EC5B] rounded-lg flex-shrink-0 flex items-center justify-center">
            <BookOpen className="w-10 h-10 text-white" />
          </div>

          <div className="flex-1 min-w-0">
            <div className="flex items-start justify-between gap-4 mb-2">
              <div className="flex-1 min-w-0">
                <h3 className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[18px] mb-1 truncate">
                  {story.title}
                </h3>
                <p className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[14px] mb-2">
                  {story.author}
                </p>
                <span className="inline-block px-2 py-1 bg-blue-50 text-[#2B7FFF] rounded text-[12px] font-['Plus_Jakarta_Sans',sans-serif] font-semibold">
                  {story.genre}
                </span>
              </div>
              <button className="p-2 text-[#FB2C36] hover:bg-red-50 rounded-lg transition-colors">
                <Heart className="w-5 h-5 fill-current" />
              </button>
            </div>

            <div className="mt-4">
              <div className="flex items-center justify-between mb-2">
                <div className="flex items-center gap-2 text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[13px]">
                  <Clock className="w-4 h-4" />
                  <span>Đọc {story.lastRead}</span>
                </div>
                <span className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-semibold text-[13px]">
                  Chương {story.currentChapter}/{story.totalChapters}
                </span>
              </div>

              {/* Progress Bar */}
              <div className="w-full h-2 bg-gray-200 rounded-full overflow-hidden">
                <div
                  className="h-full bg-gradient-to-r from-[#13EC5B] to-[#11D350] transition-all duration-300"
                  style={{ width: `${story.progress}%` }}
                />
              </div>
              <p className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[12px] mt-1">
                Tiến độ: {story.progress}%
              </p>
            </div>

            <div className="flex gap-2 mt-4">
              <Link
                to={`/chapter?storyId=${story.id}&chapterId=${story.currentChapter}`}
                className="px-4 py-2 bg-[#13EC5B] text-white rounded-lg hover:bg-[#11D350] transition-colors font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[14px] inline-block"
              >
                Đọc tiếp
              </Link>
              <Link
                to={`/story?id=${story.id}`}
                className="px-4 py-2 border-2 border-gray-200 text-[#1A2332] rounded-lg hover:border-[#13EC5B] transition-colors font-['Plus_Jakarta_Sans',sans-serif] font-semibold text-[14px] inline-block"
              >
                Chi tiết
              </Link>
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}

function FavoriteTab() {
  const favorites = [
    {
      id: 1,
      title: 'Tu La Võ Thần',
      author: 'Thiện Lương Di Tâm',
      cover: 'asura-warrior',
      chapters: 250,
      status: 'Đang cập nhật',
      views: '15.2K',
      rating: 4.8,
      genre: 'Huyền huyễn',
      addedDate: '15/01/2026',
    },
    {
      id: 2,
      title: 'Tiên Nghịch',
      author: 'Nhĩ Căn',
      cover: 'immortal-rebel',
      chapters: 800,
      status: 'Hoàn thành',
      views: '125K',
      rating: 4.9,
      genre: 'Tu tiên',
      addedDate: '10/01/2026',
    },
    {
      id: 3,
      title: 'Đế Bá',
      author: 'Yếm Bút Tiêu Sinh',
      cover: 'emperor-domination',
      chapters: 450,
      status: 'Đang cập nhật',
      views: '89K',
      rating: 4.7,
      genre: 'Huyền huyễn',
      addedDate: '05/01/2026',
    },
  ];

  return (
    <div className="space-y-4">
      <div className="flex items-center justify-between mb-4">
        <p className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[14px]">
          {favorites.length} truyện yêu thích
        </p>
        <select className="px-4 py-2 border-2 border-gray-200 rounded-lg text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-medium text-[14px] focus:border-[#13EC5B] focus:outline-none">
          <option>Mới thêm nhất</option>
          <option>Tên A-Z</option>
          <option>Đánh giá cao nhất</option>
        </select>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {favorites.map((story) => (
          <div
            key={story.id}
            className="bg-white border border-gray-200 rounded-xl p-4 hover:border-[#13EC5B] transition-all hover:shadow-lg group"
          >
            <div className="relative mb-4">
              <div className="w-full h-48 bg-gradient-to-br from-[#FB2C36] to-[#2B7FFF] rounded-lg flex items-center justify-center">
                <BookOpen className="w-16 h-16 text-white" />
              </div>
              <button className="absolute top-2 right-2 p-2 bg-white rounded-full shadow-lg hover:scale-110 transition-transform">
                <Heart className="w-5 h-5 text-[#FB2C36] fill-current" />
              </button>
              <div className="absolute bottom-2 left-2 right-2 flex gap-2">
                <span className="px-2 py-1 bg-white/90 backdrop-blur-sm rounded text-[12px] font-['Plus_Jakarta_Sans',sans-serif] font-semibold text-[#1A2332]">
                  {story.genre}
                </span>
                <span
                  className={`px-2 py-1 rounded text-[12px] font-['Plus_Jakarta_Sans',sans-serif] font-semibold ${
                    story.status === 'Hoàn thành'
                      ? 'bg-[#13EC5B]/90 text-white'
                      : 'bg-[#2B7FFF]/90 text-white'
                  }`}
                >
                  {story.status}
                </span>
              </div>
            </div>

            <h3 className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[16px] mb-1 truncate">
              {story.title}
            </h3>
            <p className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[14px] mb-3 truncate">
              {story.author}
            </p>

            <div className="flex items-center gap-4 mb-3 text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[13px]">
              <div className="flex items-center gap-1">
                <BookOpen className="w-4 h-4" />
                <span>{story.chapters} chương</span>
              </div>
              <div className="flex items-center gap-1">
                <TrendingUp className="w-4 h-4" />
                <span>{story.views}</span>
              </div>
            </div>

            <div className="flex items-center gap-2 mb-4">
              <div className="flex items-center gap-1">
                <Star className="w-4 h-4 text-[#FFA500] fill-current" />
                <span className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[14px]">
                  {story.rating}
                </span>
              </div>
              <span className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[12px]">
                • Thêm {story.addedDate}
              </span>
            </div>

            <Link
              to={`/story?id=${story.id}`}
              className="block w-full py-2 bg-[#13EC5B] text-white rounded-lg hover:bg-[#11D350] transition-colors font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[14px] text-center"
            >
              Đọc ngay
            </Link>
          </div>
        ))}
      </div>
    </div>
  );
}

function UnlockedTab() {
  const unlocked = [
    {
      id: 1,
      storyId: 1,
      storyTitle: 'Võ Luyện Điên Phong',
      chapterTitle: 'Chương 100: Đại Chiến Khởi',
      chapterId: 100,
      unlockedDate: '30/01/2026 14:30',
      cost: 50,
      author: 'Thiên Tàm Thổ Đậu',
    },
    {
      id: 2,
      storyId: 2,
      storyTitle: 'Thần Đạo Đan Tôn',
      chapterTitle: 'Chương 50: Đan Dược Thành Công',
      chapterId: 50,
      unlockedDate: '29/01/2026 20:15',
      cost: 30,
      author: 'Cô Đơn Lữ Khách',
    },
    {
      id: 3,
      storyId: 3,
      storyTitle: 'Kiếm Đạo Độc Tôn',
      chapterTitle: 'Chương 120: Kiếm Ý Tỉnh Ngộ',
      chapterId: 120,
      unlockedDate: '28/01/2026 16:45',
      cost: 40,
      author: 'Thanh Lâu Bạch Nhật Mộng',
    },
    {
      id: 4,
      storyId: 1,
      storyTitle: 'Tu La Võ Thần',
      chapterTitle: 'Chương 200: Tu La Chi Lực',
      chapterId: 200,
      unlockedDate: '27/01/2026 10:20',
      cost: 60,
      author: 'Thiện Lương Di Tâm',
    },
  ];

  const totalSpent = unlocked.reduce((sum, item) => sum + item.cost, 0);

  return (
    <div className="space-y-4">
      <div className="p-6 bg-gradient-to-r from-[#13EC5B]/10 to-[#2B7FFF]/10 border border-[#13EC5B]/30 rounded-xl">
        <div className="flex items-center justify-between">
          <div>
            <h3 className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[18px] mb-1">
              Tổng chi tiêu mở khóa
            </h3>
            <p className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[14px]">
              {unlocked.length} chương đã mở khóa
            </p>
          </div>
          <div className="text-right">
            <p className="text-[#FB2C36] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[32px]">
              {totalSpent}
            </p>
            <p className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[14px]">
              Coins
            </p>
          </div>
        </div>
      </div>

      <div className="space-y-3">
        {unlocked.map((item) => (
          <div
            key={item.id}
            className="flex items-center gap-4 p-4 border border-gray-200 rounded-xl hover:border-[#13EC5B] transition-colors"
          >
            <div className="w-12 h-12 bg-gradient-to-br from-[#13EC5B] to-[#11D350] rounded-lg flex items-center justify-center flex-shrink-0">
              <Lock className="w-6 h-6 text-white" />
            </div>

            <div className="flex-1 min-w-0">
              <h4 className="text-[#1A2332] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[16px] mb-1 truncate">
                {item.storyTitle}
              </h4>
              <p className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[14px] mb-1 truncate">
                {item.chapterTitle}
              </p>
              <div className="flex items-center gap-2 text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[13px]">
                <span>{item.author}</span>
                <span>•</span>
                <span>{item.unlockedDate}</span>
              </div>
            </div>

            <div className="text-right flex-shrink-0">
              <p className="text-[#FB2C36] font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[18px]">
                -{item.cost}
              </p>
              <p className="text-[#90A1B9] font-['Plus_Jakarta_Sans',sans-serif] font-normal text-[12px]">
                Coins
              </p>
            </div>

            <Link
              to={`/chapter?storyId=${item.storyId}&chapterId=${item.chapterId}`}
              className="px-4 py-2 bg-[#13EC5B] text-white rounded-lg hover:bg-[#11D350] transition-colors font-['Plus_Jakarta_Sans',sans-serif] font-bold text-[14px] inline-block"
            >
              Đọc lại
            </Link>
          </div>
        ))}
      </div>
    </div>
  );
}
