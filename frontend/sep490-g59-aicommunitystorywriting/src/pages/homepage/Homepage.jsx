import React from 'react';
import { Header } from '../../components/homepage/Header';
import { Footer } from '../../components/homepage/Footer';
import { HeroAuthorStoriesBanner } from '../../components/homepage/HeroAuthorStoriesBanner';
import { TopAuthorStoriesSection } from '../../components/homepage/TopAuthorStoriesSection';
import { NewAuthorDebutsSection } from '../../components/homepage/NewAuthorDebutsSection';
import { AIAssistedStoriesWidget } from '../../components/homepage/AIAssistedStoriesWidget';
import { TrendingAuthorsSection } from '../../components/homepage/TrendingAuthorsSection';
import { CommunityHighlightsSection } from '../../components/homepage/CommunityHighlightsSection';
import { AuthorRankingsWidget } from '../../components/homepage/AuthorRankingsWidget';
import { SidebarDiscoverWidget } from '../../components/homepage/SidebarDiscoverWidget';
import { CommunityStatsWidget } from '../../components/homepage/CommunityStatsWidget';
import { CTASection } from '../../components/homepage/CTASection';
import { useCommunityStats } from '../../hooks/useCommunityStats';

export default function Homepage() {
  const communityStats = useCommunityStats();

  return (
    <div className="w-full bg-gray-50">
      <Header />
      
      {/* Hero Banner - Featured Author Stories */}
      <HeroAuthorStoriesBanner />

      {/* Main Content - 2 Columns */}
      <div className="max-w-[1400px] mx-auto px-6 py-8">
        <div className="grid grid-cols-1 lg:grid-cols-[1fr_320px] gap-8">
          {/* Left Column */}
          <div className="space-y-8">
            <TopAuthorStoriesSection />
            <NewAuthorDebutsSection />
            <AIAssistedStoriesWidget />
            <TrendingAuthorsSection />
            <CommunityHighlightsSection />
          </div>

          {/* Right Column - Sticky Sidebar */}
          <div className="lg:sticky lg:top-8 lg:self-start space-y-6">
            <AuthorRankingsWidget />
            <CommunityStatsWidget
              skipFetch
              stats={communityStats.stats}
              loading={communityStats.loading}
              error={communityStats.error}
            />
            <SidebarDiscoverWidget />
          </div>
        </div>
      </div>

      {/* CTA Section */}
      <CTASection />
      
      <Footer />
    </div>
  );
}
