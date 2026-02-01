import { BrowserRouter, Routes, Route } from 'react-router-dom';
import Homepage from './pages/homepage/Homepage';
import { StoryDetail } from './pages/story-detail/StoryDetail';
import { AdminPage } from './pages/admin/AdminPage';

export default function App() {
  return (
    <BrowserRouter>
      <Routes>
        <Route path="/" element={<Homepage />} />
        <Route path="/story" element={<StoryDetail />} />
        <Route path="/admin" element={<AdminPage />} />
      </Routes>
    </BrowserRouter>
  );
}
