import { useState } from 'react';
import { ChapterNavBar } from '../../components/chapter-detail/ChapterNavBar';
import { ChapterSettings } from '../../components/chapter-detail/ChapterSettings';
import { ChapterSidebar } from '../../components/chapter-detail/ChapterSidebar';
import { ChapterContent } from '../../components/chapter-detail/ChapterContent';
import { ChapterNavigation } from '../../components/chapter-detail/ChapterNavigation';
import { ChapterComments } from '../../components/chapter-detail/ChapterComments';

export function ChapterReader({ onBack, onNavigateToStory }) {
    const [fontSize, setFontSize] = useState(18);
    const [fontFamily, setFontFamily] = useState('serif');
    const [backgroundColor, setBackgroundColor] = useState('#ffffff');
    const [textColor, setTextColor] = useState('#1e293b');
    const [lineHeight, setLineHeight] = useState(1.8);
    const [showSettings, setShowSettings] = useState(false);
    const [showChapterList, setShowChapterList] = useState(false);
    const [isBookmarked, setIsBookmarked] = useState(false);

    // Mock data
    const story = {
        title: 'Tu Tiên Chi Lộ: Hành Trình Vạn Năm',
        author: 'Thiên Tằm Thổ Đậu',
    };

    const chapter = {
        number: 450,
        title: 'Đại chiến với Ma Đế',
        content: `Phương Viễn đứng giữa hư không, ánh mắt lạnh lùng nhìn về phía Ma Đế đang từ từ hiện hình.

Hắn đã chờ đợi khoảnh khắc này suốt vạn năm tu luyện. Kiếp trước, hắn chết trong tay Ma Đế một cách oan uổng. Kiếp này, với tu vi đỉnh phong và vô số bảo bối, hắn quyết tâm thay đổi vận mệnh!

"Ma Đế, ta đã trở lại!"

Thanh âm của Phương Viễn vang vọng khắp chín tầng trời, khiến không gian rung chuyển. Vô số tu sĩ từ các tông môn xa xôi đều ngước nhìn về phía chiến trường, trong lòng đầy lo âu.

Ma Đế cười khẩy, toàn thân tràn ngập ma khí đen kịt: "Tiểu tử, ta nhớ ngươi. Kiếp trước ngươi chỉ là một con kiến nhỏ nhoi, giờ đây dám đứng trước mặt ta?"

"Ngươi sẽ phải trả giá cho những gì đã làm!"

Phương Viễn không còn nói thêm, tay nhấc lên, Trấn Thiên Kiếm bỗng nhiên xuất hiện. Thanh kiếm long lanh ánh sáng vàng rực, trên thân khắc đầy đủ thất thập nhị thiên cương.

"Nhất kiếm khai thiên!"

Một đao quang khổng lồ chém xuống, chia cắt không gian thành hai. Ma Đế tái mặt, vội vàng triển khai ma khí phòng thủ.

BOOOMMMM!!!

Tiếng nổ chấn động chín tầng trời. Các núi non trong vòng nghìn dặm đều rung chuyển, sông hồ sôi sục.

"Ngươi... tu vi của ngươi đã đến mức này sao?" Ma Đế kinh ngạc.

Phương Viễn lạnh lùng cười: "Ta còn nhiều điều muốn cho ngươi biết đấy!"

Trận chiến giữa hai đại cao thủ bắt đầu. Mỗi đòn đều có thể hủy diệt một phương trời đất. Vô số tu sĩ xem chiến đều phải lùi xa hàng vạn dặm, không dám lại gần.

Ba ngày ba đêm liên tục chiến đấu, cuối cùng Phương Viễn tìm được khe hở. Hắn kết hợp cả ba đại thần thông, triển khai chiêu thức cực mạnh nhất.

"Vạn kiếm quy tông!"

Vô số thanh kiếm xuất hiện trên không, tất cả đều bay về phía Ma Đế với tốc độ kinh hoàng.

"Không... Ta là Ma Đế bất tử! Ngươi không thể giết ta!"

Nhưng mọi chống cự đều vô nghĩa. Dưới sức mạnh tuyệt đối, Ma Đế chỉ còn biết hét lên trong tuyệt vọng.

Khi ánh sáng tan biến, Ma Đế đã biến thành tro bụi, bay khắp hư không.

Phương Viễn đứng giữa không trung, áo choàng phất phới trong gió. Hắn đã hoàn thành mục tiêu của kiếp này - tiêu diệt Ma Đế!

Nhưng đây chỉ mới là bắt đầu. Con đường tu tiên còn dài, còn nhiều kẻ địch mạnh hơn đang chờ đợi.

"Ta sẽ tiếp tục tiến lên, cho đến khi đứng trên đỉnh cao của vũ trụ này!"

Với quyết tâm bất diệt, Phương Viễn bay về phía chân trời xa xôi, bắt đầu chương mới trong hành trình tu tiên của mình...`,
        publishedAt: '2 giờ trước',
        views: 15420,
        words: 1250,
    };

    const allChapters = Array.from({ length: 450 }, (_, i) => ({
        number: i + 1,
        title: i === 449
            ? 'Đại chiến với Ma Đế'
            : i === 448
                ? 'Đột phá Nguyên Anh kỳ'
                : i === 447
                    ? 'Bí mật của Thái Cổ Thần Thạch'
                    : `Chương ${i + 1}`,
        isLocked: i < 440,
    })).reverse();

    const comments = [
        {
            id: 1,
            user: { name: 'Độc Giả 123', avatar: 'https://images.unsplash.com/photo-1535713875002-d1d0cf377fde?w=50&h=50&fit=crop' },
            content: 'Chương này hay quá! Trận chiến với Ma Đế được miêu tả rất sống động và hấp dẫn!',
            time: '3 giờ trước',
            likes: 234,
        },
        {
            id: 2,
            user: { name: 'Phong Vân', avatar: 'https://images.unsplash.com/photo-1599566150163-29194dcaad36?w=50&h=50&fit=crop' },
            content: 'Tác giả viết văn rất hay, cảm xúc nhân vật được thể hiện rõ ràng. Mong chờ chương tiếp theo!',
            time: '5 giờ trước',
            likes: 189,
        },
        {
            id: 3,
            user: { name: 'Long Thiên', avatar: 'https://images.unsplash.com/photo-1527980965255-d3b416303d12?w=50&h=50&fit=crop' },
            content: 'Phần chiến đấu quá đỉnh! Đọc xong muốn xem tiếp luôn 🔥',
            time: '6 giờ trước',
            likes: 156,
        },
    ];

    const handleBackClick = () => {
        if (onBack) {
            onBack();
        } else {
            window.history.back();
        }
    };

    const handleHomeClick = () => {
        if (onNavigateToStory) {
            onNavigateToStory();
        }
    };

    const handlePrevChapter = () => {
        console.log('Navigate to previous chapter');
    };

    const handleNextChapter = () => {
        console.log('Navigate to next chapter');
    };

    const handleShare = () => {
        if (navigator.share) {
            navigator.share({
                title: `${story.title} - Chương ${chapter.number}`,
                text: chapter.title,
                url: window.location.href,
            });
        }
    };

    const handleThemeChange = (bg, text) => {
        setBackgroundColor(bg);
        setTextColor(text);
    };

    return (
        <div style={{ minHeight: '100vh', backgroundColor: '#f8fafc' }}>
            {/* Top Navigation Bar */}
            <ChapterNavBar
                story={story}
                chapter={chapter}
                isBookmarked={isBookmarked}
                onBack={handleBackClick}
                onHome={handleHomeClick}
                onToggleChapterList={() => setShowChapterList(!showChapterList)}
                onToggleSettings={() => setShowSettings(!showSettings)}
                onToggleBookmark={() => setIsBookmarked(!isBookmarked)}
                onShare={handleShare}
            />

            {/* Settings Panel */}
            <ChapterSettings
                show={showSettings}
                fontSize={fontSize}
                fontFamily={fontFamily}
                backgroundColor={backgroundColor}
                textColor={textColor}
                lineHeight={lineHeight}
                onFontSizeChange={setFontSize}
                onFontFamilyChange={setFontFamily}
                onThemeChange={handleThemeChange}
                onLineHeightChange={setLineHeight}
            />

            {/* Chapter List Sidebar */}
            <ChapterSidebar
                show={showChapterList}
                chapters={allChapters}
                currentChapter={chapter.number}
                onClose={() => setShowChapterList(false)}
                onChapterSelect={(ch) => {
                    console.log('Selected chapter:', ch);
                    setShowChapterList(false);
                }}
            />

            {/* Chapter Content */}
            <ChapterContent
                chapter={chapter}
                fontSize={fontSize}
                fontFamily={fontFamily}
                backgroundColor={backgroundColor}
                textColor={textColor}
                lineHeight={lineHeight}
            />

            {/* Navigation Buttons */}
            <ChapterNavigation
                currentChapter={chapter.number}
                totalChapters={450}
                onPrevChapter={handlePrevChapter}
                onNextChapter={handleNextChapter}
            />

            {/* Comments Section */}
            <ChapterComments
                comments={comments}
                onReportComment={(id) => console.log('Report comment:', id)}
            />
        </div>
    );
}
