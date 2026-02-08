import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import { FeaturedBanner } from '../../components/homepage/FeaturedBanner';
import { HotStories } from '../../components/homepage/HotStories';
import { NewUpdates } from '../../components/homepage/NewUpdates';
import { CompletedStories } from '../../components/homepage/CompletedStories';
import { TopAuthors } from '../../components/homepage/TopAuthors';
import { Community } from '../../components/homepage/Community';
import { HomeCTA } from '../../components/homepage/HomeCTA';
import { Rankings } from '../../components/homepage/Rankings';

export default function Homepage() {
  return (
    <div className="w-full bg-gray-50">
      <Header />

      <FeaturedBanner />

      <div className="max-w-[1400px] mx-auto px-6 py-8">
        <div className="grid grid-cols-1 lg:grid-cols-[1fr_320px] gap-8">
          <div className="space-y-8">
            <HotStories />
            <NewUpdates />
            <CompletedStories />
            <TopAuthors />
            <Community />
          </div>

          <div className="lg:sticky lg:top-8 lg:self-start">
            <Rankings />
          </div>
        </div>
      </div>

      <HomeCTA />

      <Footer />
    </div>
  );
}
