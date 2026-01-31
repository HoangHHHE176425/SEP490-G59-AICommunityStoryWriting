import { Header } from '../../components/homepage/Header';
import { HeroCarousel } from '../../components/homepage/HeroCarousel';
import { NewUpdates } from '../../components/homepage/NewUpdates';
import { RecommendedStories } from '../../components/homepage/RecommendedStories';
import { HotStories } from '../../components/homepage/HotStories';
import { CompletedStories } from '../../components/homepage/CompletedStories';
import { Sidebar } from '../../components/homepage/Sidebar';
import { Footer } from '../../components/homepage/Footer';

export default function Homepage() {
    return (
        <div className="min-h-screen bg-background-light dark:bg-background-dark text-slate-900 dark:text-slate-100 transition-colors duration-200">
            <Header />

            <main className="max-w-[1280px] mx-auto px-4 py-6 flex flex-col gap-10">
                <HeroCarousel />

                <div className="grid grid-cols-1 lg:grid-cols-12 gap-8">
                    {/* Main content */}
                    <div className="lg:col-span-8 flex flex-col gap-10">
                        <NewUpdates />
                        <RecommendedStories />
                        <HotStories />
                        <CompletedStories />
                    </div>

                    {/* Sidebar */}
                    <aside className="lg:col-span-4">
                        <Sidebar />
                    </aside>
                </div>
            </main>

            <Footer />
        </div>
    );
}
