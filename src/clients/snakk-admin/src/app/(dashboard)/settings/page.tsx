import { Suspense } from 'react';
import {
  getGeneralSettings,
  getOAuthProviders,
  getEmailConfig,
  getAvatarSettings,
  getContentSettings,
  getRateLimitingSettings,
} from '@/lib/api/settings';
import { SettingsTabs } from '@/components/client/settings/SettingsTabs';
import { Skeleton } from '@/components/ui/skeleton';

export default async function SettingsPage() {
  // Fetch all settings data in parallel
  const [
    generalSettings,
    oauthProviders,
    emailConfig,
    avatarSettings,
    contentSettings,
    rateLimitingSettings,
  ] = await Promise.all([
    getGeneralSettings(),
    getOAuthProviders(),
    getEmailConfig(),
    getAvatarSettings(),
    getContentSettings(),
    getRateLimitingSettings(),
  ]);

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-3xl font-bold text-gray-900">Settings</h1>
        <p className="text-gray-500 mt-1">Configure platform settings and preferences</p>
      </div>

      <Suspense fallback={<SettingsSkeleton />}>
        <SettingsTabs
          initialGeneralSettings={generalSettings}
          initialOAuthProviders={oauthProviders.providers}
          initialEmailConfig={emailConfig}
          initialAvatarSettings={avatarSettings}
          initialContentSettings={contentSettings}
          initialRateLimitingSettings={rateLimitingSettings}
        />
      </Suspense>
    </div>
  );
}

function SettingsSkeleton() {
  return (
    <div className="space-y-6">
      <div className="flex gap-4 border-b">
        {[1, 2, 3, 4, 5, 6].map((i) => (
          <Skeleton key={i} className="h-10 w-24 rounded-t" />
        ))}
      </div>
      <div className="space-y-4">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-64 w-full" />
      </div>
    </div>
  );
}
