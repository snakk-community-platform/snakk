'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Switch } from '@/components/ui/switch';
import { Label } from '@/components/ui/label';
import { Badge } from '@/components/ui/badge';
import { useToast } from '@/hooks/use-toast';
import type { OAuthProvider } from '@/lib/api/settings';
import { updateOAuthProvider } from '@/lib/api/settings';

interface OAuthProvidersFormProps {
  initialProviders: OAuthProvider[];
}

export function OAuthProvidersForm({ initialProviders }: OAuthProvidersFormProps) {
  const router = useRouter();
  const { toast } = useToast();
  const [providers, setProviders] = useState<OAuthProvider[]>(initialProviders);
  const [updatingProvider, setUpdatingProvider] = useState<string | null>(null);

  const handleToggle = async (provider: OAuthProvider) => {
    setUpdatingProvider(provider.provider);

    try {
      await updateOAuthProvider(provider.provider, !provider.enabled);

      // Update local state
      setProviders(
        providers.map((p) =>
          p.provider === provider.provider ? { ...p, enabled: !p.enabled } : p
        )
      );

      toast({
        title: 'OAuth provider updated',
        description: `${provider.provider} OAuth has been ${!provider.enabled ? 'enabled' : 'disabled'}.`,
      });

      router.refresh();
    } catch (error) {
      toast({
        title: 'Error',
        description: error instanceof Error ? error.message : 'Failed to update OAuth provider',
        variant: 'destructive',
      });
    } finally {
      setUpdatingProvider(null);
    }
  };

  return (
    <Card>
      <CardHeader>
        <CardTitle>OAuth Providers</CardTitle>
        <CardDescription>
          Enable or disable OAuth authentication providers. Providers must be configured in appsettings.json to work.
        </CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {providers.map((provider) => (
          <div
            key={provider.provider}
            className="flex items-center justify-between border rounded-lg p-4"
          >
            <div className="flex items-center gap-4">
              <div>
                <Label className="text-base font-medium">{provider.provider}</Label>
                <p className="text-sm text-gray-500">
                  {provider.configured ? 'Configured in appsettings.json' : 'Not configured'}
                </p>
              </div>
              {provider.configured ? (
                <Badge variant="default">Configured</Badge>
              ) : (
                <Badge variant="secondary">Not Configured</Badge>
              )}
            </div>

            <Switch
              checked={provider.enabled}
              onCheckedChange={() => handleToggle(provider)}
              disabled={!provider.configured || updatingProvider === provider.provider}
            />
          </div>
        ))}

        {providers.length === 0 && (
          <div className="text-center py-8 text-gray-500">
            No OAuth providers available
          </div>
        )}

        <div className="mt-6 p-4 bg-blue-50 border border-blue-200 rounded-lg">
          <p className="text-sm text-blue-900">
            <strong>Note:</strong> To configure OAuth providers, add ClientId and ClientSecret values in your appsettings.json file under Authentication:ProviderName section.
          </p>
        </div>
      </CardContent>
    </Card>
  );
}
