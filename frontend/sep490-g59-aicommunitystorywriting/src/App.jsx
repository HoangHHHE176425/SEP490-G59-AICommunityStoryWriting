import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { AuthProvider } from './contexts/AuthContext';
import { AdminProtectedRoute } from './components/admin/AdminProtectedRoute';
import Homepage from './pages/homepage/Homepage';
import { StoryDetail } from './pages/story-detail/StoryDetail';
import { AdminPage } from './pages/admin/AdminPage';
import { AdminLogin } from './pages/admin/AdminLogin';
import Login from './pages/auth/Login';
import Register from './pages/auth/Register';
import ForgotPassword from './pages/auth/ForgotPassword';
import VerifyOtp from './pages/auth/VerifyOtp';
import Profile from './pages/profile/Profile';
import Wallet from './pages/wallet/Wallet';
import Library from './pages/library/Library';
import { ChapterReader } from './pages/chapter-detail/ChapterReader';
import AuthorStoryManagement from './pages/author/AuthorStoryManagement';
import { AuthorDetail } from './pages/author/AuthorDetail';
import { StoryBrowse } from './pages/story-list/StoryBrowse';
import AboutUs from './pages/aboutus/AboutUs';
import PolicyPage from './pages/policy/PolicyPage';
import Donate from './pages/donate/Donate';
import GoogleCallback from './pages/auth/GoogleCallback';

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<AboutUs />} />
          <Route path="/home" element={<Homepage />} />
          <Route path="/story/:storyId" element={<StoryDetail />} />
          {/* User/guest/author: /login. Quản trị (admin, moderator, compliance): /admin/login */}
          <Route path="/admin" element={<AdminProtectedRoute><AdminPage /></AdminProtectedRoute>} />
          <Route path="/admin/login" element={<AdminLogin />} />
          <Route path="/login" element={<Login />} />
          <Route path="/register" element={<Register />} />
          <Route path="/forgot-password" element={<ForgotPassword />} />
          <Route path="/verify-otp" element={<VerifyOtp />} />
          <Route path="/auth/google/callback" element={<GoogleCallback />} />
          <Route path="/profile" element={<Profile />} />
          <Route path="/wallet" element={<Wallet />} />
          <Route path="/library" element={<Library />} />
          <Route path="/chapter" element={<ChapterReader />} />
          <Route path="/author" element={<AuthorStoryManagement />} />
          <Route path="/authors/:authorId" element={<AuthorDetail />} />
          <Route path="/donate/:authorId" element={<Donate />} />
          <Route path="/story-list" element={<StoryBrowse />} />
          <Route path="/about-us" element={<AboutUs />} />
          <Route path="/policy" element={<PolicyPage />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}
