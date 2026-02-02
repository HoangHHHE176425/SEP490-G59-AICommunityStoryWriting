import { BrowserRouter, Routes, Route } from 'react-router-dom';
import Homepage from './pages/homepage/Homepage';
import { StoryDetail } from './pages/story-detail/StoryDetail';
import { AdminPage } from './pages/admin/AdminPage';
import { ChapterReader } from './pages/chapter-detail/ChapterReader';
import { AuthorStoryManagement } from './pages/author/AuthorStoryManagement';

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Homepage />} />
        <Route path="/story" element={<StoryDetail />} />
        <Route path="/admin" element={<AdminPage />} />
        <Route path="/chapter" element={<ChapterReader />} />
        <Route path="/author" element={<AuthorStoryManagement />} />
      </Routes>
    </BrowserRouter>
  );
}
