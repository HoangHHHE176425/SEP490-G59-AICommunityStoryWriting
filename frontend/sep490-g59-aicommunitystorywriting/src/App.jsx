import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { AuthProvider } from './contexts/AuthContext';
import Homepage from './pages/homepage/Homepage';
import { StoryDetail } from './pages/story-detail/StoryDetail';
import { AdminPage } from './pages/admin/AdminPage';
import Login from './pages/auth/Login';
import Register from './pages/auth/Register';
import ForgotPassword from './pages/auth/ForgotPassword';
import Profile from './pages/profile/Profile';
import { ChapterReader } from './pages/chapter-detail/ChapterReader';
import { AuthorStoryManagement } from './pages/author/AuthorStoryManagement';
import { StoryBrowse } from './pages/story-list/StoryBrowse';
import AboutUs from './pages/aboutus/AboutUs';
import { LibraryPage } from './pages/library/LibraryPage';

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<AboutUs />} />
          <Route path="/home" element={<Homepage />} />
          <Route path="/story" element={<StoryDetail />} />
          <Route path="/admin" element={<AdminPage />} />
          <Route path="/login" element={<Login />} />
          <Route path="/register" element={<Register />} />
          <Route path="/forgot-password" element={<ForgotPassword />} />
          <Route path="/profile" element={<Profile />} />
          <Route path="/chapter" element={<ChapterReader />} />
          <Route path="/author" element={<AuthorStoryManagement />} />
          <Route path="/story-list" element={<StoryBrowse />} />
          <Route path="/library" element={<LibraryPage />} />
          <Route path="/about-us" element={<AboutUs />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}
