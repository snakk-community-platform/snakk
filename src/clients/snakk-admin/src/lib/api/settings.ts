import { apiRequest } from './client';

// ==================== Types ====================

export interface SiteInfo {
  siteName: string;
  siteDescription: string;
  logoUrl: string;
  timezone: string;
  language: string;
}

export interface OAuthProvider {
  provider: string;
  enabled: boolean;
  configured: boolean;
}

export interface OAuthProvidersResponse {
  providers: OAuthProvider[];
}

export interface EmailConfig {
  enabled: boolean;
  smtpHost: string;
  smtpPort: number;
  useSsl: boolean;
  smtpUsername: string;
  smtpPassword: string;
  fromEmail: string;
  fromName: string;
}

export interface TestEmailRequest {
  recipientEmail: string;
}

export interface AvatarSettings {
  defaultAvatarSize: number;
  maxUploadSizeMb: number;
  allowUploads: boolean;
  generateOnStartup: boolean;
}

export interface ContentSettings {
  postMaxLength: number;
  discussionTitleMaxLength: number;
  allowMarkdown: boolean;
  allowHtml: boolean;
  requireModeration: boolean;
}

export interface RateLimitingSettings {
  authPermitLimit: number;
  authWindowMinutes: number;
  apiPermitLimit: number;
  apiWindowMinutes: number;
  expensivePermitLimit: number;
  expensiveWindowMinutes: number;
}

// ==================== API Functions ====================

// General Settings
export async function getGeneralSettings(): Promise<SiteInfo> {
  return apiRequest('/admin/settings/general');
}

export async function updateGeneralSettings(siteInfo: SiteInfo): Promise<SiteInfo> {
  return apiRequest('/admin/settings/general', {
    method: 'PUT',
    body: JSON.stringify(siteInfo),
  });
}

// OAuth Provider Settings
export async function getOAuthProviders(): Promise<OAuthProvidersResponse> {
  return apiRequest('/admin/settings/oauth');
}

export async function updateOAuthProvider(
  provider: string,
  enabled: boolean
): Promise<{ provider: string; enabled: boolean }> {
  return apiRequest(`/admin/settings/oauth/${provider}`, {
    method: 'PUT',
    body: JSON.stringify({ enabled }),
  });
}

// Email Settings
export async function getEmailConfig(): Promise<EmailConfig> {
  return apiRequest('/admin/settings/email');
}

export async function updateEmailConfig(config: EmailConfig): Promise<EmailConfig> {
  return apiRequest('/admin/settings/email', {
    method: 'PUT',
    body: JSON.stringify(config),
  });
}

export async function testEmailConfig(
  recipientEmail: string
): Promise<{ message: string }> {
  return apiRequest('/admin/settings/email/test', {
    method: 'POST',
    body: JSON.stringify({ recipientEmail }),
  });
}

// Avatar Settings
export async function getAvatarSettings(): Promise<AvatarSettings> {
  return apiRequest('/admin/settings/avatar');
}

export async function updateAvatarSettings(
  settings: AvatarSettings
): Promise<AvatarSettings> {
  return apiRequest('/admin/settings/avatar', {
    method: 'PUT',
    body: JSON.stringify(settings),
  });
}

// Content Settings
export async function getContentSettings(): Promise<ContentSettings> {
  return apiRequest('/admin/settings/content');
}

export async function updateContentSettings(
  settings: ContentSettings
): Promise<ContentSettings> {
  return apiRequest('/admin/settings/content', {
    method: 'PUT',
    body: JSON.stringify(settings),
  });
}

// Rate Limiting Settings
export async function getRateLimitingSettings(): Promise<RateLimitingSettings> {
  return apiRequest('/admin/settings/rate-limiting');
}

export async function updateRateLimitingSettings(
  settings: RateLimitingSettings
): Promise<RateLimitingSettings> {
  return apiRequest('/admin/settings/rate-limiting', {
    method: 'PUT',
    body: JSON.stringify(settings),
  });
}
