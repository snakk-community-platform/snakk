'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useToast } from '@/hooks/use-toast';
import type { RateLimitingSettings } from '@/lib/api/settings';
import { updateRateLimitingSettings } from '@/lib/api/settings';

interface RateLimitingFormProps {
  initialSettings: RateLimitingSettings;
}

export function RateLimitingForm({ initialSettings }: RateLimitingFormProps) {
  const router = useRouter();
  const { toast } = useToast();
  const [isLoading, setIsLoading] = useState(false);
  const [settings, setSettings] = useState<RateLimitingSettings>(initialSettings);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);

    try {
      await updateRateLimitingSettings(settings);

      toast({
        title: 'Rate limiting settings updated',
        description: 'Rate limiting settings have been updated successfully. Application restart may be required for changes to take effect.',
      });

      router.refresh();
    } catch (error) {
      toast({
        title: 'Error',
        description: error instanceof Error ? error.message : 'Failed to update rate limiting settings',
        variant: 'destructive',
      });
    } finally {
      setIsLoading(false);
    }
  };

  const handleReset = () => {
    setSettings(initialSettings);
  };

  return (
    <form onSubmit={handleSubmit}>
      <Card>
        <CardHeader>
          <CardTitle>Rate Limiting Settings</CardTitle>
          <CardDescription>
            Configure rate limits to protect your API from abuse
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-6">
          {/* Authentication Endpoints */}
          <div className="space-y-4 border rounded-lg p-4">
            <div>
              <h3 className="font-medium text-lg">Authentication Endpoints</h3>
              <p className="text-sm text-gray-500">
                Rate limits for login, registration, and password reset
              </p>
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="authPermitLimit">Permit Limit</Label>
                <Input
                  id="authPermitLimit"
                  type="number"
                  min="1"
                  max="1000"
                  value={settings.authPermitLimit}
                  onChange={(e) =>
                    setSettings({ ...settings, authPermitLimit: parseInt(e.target.value) || 5 })
                  }
                  placeholder="5"
                  required
                />
                <p className="text-xs text-gray-500">Requests allowed per window</p>
              </div>

              <div className="space-y-2">
                <Label htmlFor="authWindowMinutes">Window (minutes)</Label>
                <Input
                  id="authWindowMinutes"
                  type="number"
                  min="1"
                  max="1440"
                  value={settings.authWindowMinutes}
                  onChange={(e) =>
                    setSettings({ ...settings, authWindowMinutes: parseInt(e.target.value) || 15 })
                  }
                  placeholder="15"
                  required
                />
                <p className="text-xs text-gray-500">Time window in minutes</p>
              </div>
            </div>
          </div>

          {/* API Endpoints */}
          <div className="space-y-4 border rounded-lg p-4">
            <div>
              <h3 className="font-medium text-lg">API Endpoints</h3>
              <p className="text-sm text-gray-500">
                Rate limits for general API calls
              </p>
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="apiPermitLimit">Permit Limit</Label>
                <Input
                  id="apiPermitLimit"
                  type="number"
                  min="1"
                  max="10000"
                  value={settings.apiPermitLimit}
                  onChange={(e) =>
                    setSettings({ ...settings, apiPermitLimit: parseInt(e.target.value) || 100 })
                  }
                  placeholder="100"
                  required
                />
                <p className="text-xs text-gray-500">Requests allowed per window</p>
              </div>

              <div className="space-y-2">
                <Label htmlFor="apiWindowMinutes">Window (minutes)</Label>
                <Input
                  id="apiWindowMinutes"
                  type="number"
                  min="1"
                  max="1440"
                  value={settings.apiWindowMinutes}
                  onChange={(e) =>
                    setSettings({ ...settings, apiWindowMinutes: parseInt(e.target.value) || 1 })
                  }
                  placeholder="1"
                  required
                />
                <p className="text-xs text-gray-500">Time window in minutes</p>
              </div>
            </div>
          </div>

          {/* Expensive Operations */}
          <div className="space-y-4 border rounded-lg p-4">
            <div>
              <h3 className="font-medium text-lg">Expensive Operations</h3>
              <p className="text-sm text-gray-500">
                Rate limits for resource-intensive operations (search, reports, exports)
              </p>
            </div>

            <div className="grid grid-cols-2 gap-4">
              <div className="space-y-2">
                <Label htmlFor="expensivePermitLimit">Permit Limit</Label>
                <Input
                  id="expensivePermitLimit"
                  type="number"
                  min="1"
                  max="1000"
                  value={settings.expensivePermitLimit}
                  onChange={(e) =>
                    setSettings({
                      ...settings,
                      expensivePermitLimit: parseInt(e.target.value) || 10,
                    })
                  }
                  placeholder="10"
                  required
                />
                <p className="text-xs text-gray-500">Requests allowed per window</p>
              </div>

              <div className="space-y-2">
                <Label htmlFor="expensiveWindowMinutes">Window (minutes)</Label>
                <Input
                  id="expensiveWindowMinutes"
                  type="number"
                  min="1"
                  max="1440"
                  value={settings.expensiveWindowMinutes}
                  onChange={(e) =>
                    setSettings({
                      ...settings,
                      expensiveWindowMinutes: parseInt(e.target.value) || 1,
                    })
                  }
                  placeholder="1"
                  required
                />
                <p className="text-xs text-gray-500">Time window in minutes</p>
              </div>
            </div>
          </div>

          <div className="p-4 bg-blue-50 border border-blue-200 rounded-lg">
            <p className="text-sm text-blue-900">
              <strong>Note:</strong> Rate limiting changes may require an application restart to take full effect. Lower limits provide better protection but may impact legitimate users.
            </p>
          </div>
        </CardContent>
        <CardFooter className="flex justify-between">
          <Button type="button" variant="outline" onClick={handleReset} disabled={isLoading}>
            Reset
          </Button>
          <Button type="submit" disabled={isLoading}>
            {isLoading ? 'Saving...' : 'Save Changes'}
          </Button>
        </CardFooter>
      </Card>
    </form>
  );
}
