'use client';

import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import type {
  SiteInfo,
  OAuthProvider,
  EmailConfig,
  AvatarSettings,
  ContentSettings,
  RateLimitingSettings,
} from '@/lib/api/settings';
import { GeneralSettingsForm } from './GeneralSettingsForm';
import { OAuthProvidersForm } from './OAuthProvidersForm';
import { EmailConfigForm } from './EmailConfigForm';
import { AvatarSettingsForm } from './AvatarSettingsForm';
import { ContentSettingsForm } from './ContentSettingsForm';
import { RateLimitingForm } from './RateLimitingForm';

interface SettingsTabsProps {
  initialGeneralSettings: SiteInfo;
  initialOAuthProviders: OAuthProvider[];
  initialEmailConfig: EmailConfig;
  initialAvatarSettings: AvatarSettings;
  initialContentSettings: ContentSettings;
  initialRateLimitingSettings: RateLimitingSettings;
}

export function SettingsTabs({
  initialGeneralSettings,
  initialOAuthProviders,
  initialEmailConfig,
  initialAvatarSettings,
  initialContentSettings,
  initialRateLimitingSettings,
}: SettingsTabsProps) {
  return (
    <Tabs defaultValue="general" className="w-full">
      <TabsList className="grid w-full grid-cols-6">
        <TabsTrigger value="general">General</TabsTrigger>
        <TabsTrigger value="oauth">OAuth</TabsTrigger>
        <TabsTrigger value="email">Email</TabsTrigger>
        <TabsTrigger value="avatar">Avatar</TabsTrigger>
        <TabsTrigger value="content">Content</TabsTrigger>
        <TabsTrigger value="rate-limiting">Rate Limiting</TabsTrigger>
      </TabsList>

      <TabsContent value="general" className="mt-6">
        <GeneralSettingsForm initialSettings={initialGeneralSettings} />
      </TabsContent>

      <TabsContent value="oauth" className="mt-6">
        <OAuthProvidersForm initialProviders={initialOAuthProviders} />
      </TabsContent>

      <TabsContent value="email" className="mt-6">
        <EmailConfigForm initialConfig={initialEmailConfig} />
      </TabsContent>

      <TabsContent value="avatar" className="mt-6">
        <AvatarSettingsForm initialSettings={initialAvatarSettings} />
      </TabsContent>

      <TabsContent value="content" className="mt-6">
        <ContentSettingsForm initialSettings={initialContentSettings} />
      </TabsContent>

      <TabsContent value="rate-limiting" className="mt-6">
        <RateLimitingForm initialSettings={initialRateLimitingSettings} />
      </TabsContent>
    </Tabs>
  );
}
