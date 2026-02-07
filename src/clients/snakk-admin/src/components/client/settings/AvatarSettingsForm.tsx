'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { useToast } from '@/hooks/use-toast';
import type { AvatarSettings } from '@/lib/api/settings';
import { updateAvatarSettings } from '@/lib/api/settings';

interface AvatarSettingsFormProps {
  initialSettings: AvatarSettings;
}

export function AvatarSettingsForm({ initialSettings }: AvatarSettingsFormProps) {
  const router = useRouter();
  const { toast } = useToast();
  const [isLoading, setIsLoading] = useState(false);
  const [settings, setSettings] = useState<AvatarSettings>(initialSettings);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);

    try {
      await updateAvatarSettings(settings);

      toast({
        title: 'Avatar settings updated',
        description: 'Avatar settings have been updated successfully.',
      });

      router.refresh();
    } catch (error) {
      toast({
        title: 'Error',
        description: error instanceof Error ? error.message : 'Failed to update avatar settings',
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
          <CardTitle>Avatar Settings</CardTitle>
          <CardDescription>
            Configure avatar generation and upload settings
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="defaultAvatarSize">Default Avatar Size (pixels)</Label>
            <Input
              id="defaultAvatarSize"
              type="number"
              min="32"
              max="512"
              value={settings.defaultAvatarSize}
              onChange={(e) =>
                setSettings({ ...settings, defaultAvatarSize: parseInt(e.target.value) || 80 })
              }
              placeholder="80"
              required
            />
            <p className="text-sm text-gray-500">
              Default size for generated avatars (32-512 pixels)
            </p>
          </div>

          <div className="space-y-2">
            <Label htmlFor="maxUploadSizeMb">Max Upload Size (MB)</Label>
            <Input
              id="maxUploadSizeMb"
              type="number"
              min="1"
              max="20"
              value={settings.maxUploadSizeMb}
              onChange={(e) =>
                setSettings({ ...settings, maxUploadSizeMb: parseInt(e.target.value) || 5 })
              }
              placeholder="5"
              required
            />
            <p className="text-sm text-gray-500">
              Maximum file size for avatar uploads (1-20 MB)
            </p>
          </div>

          <div className="flex items-center justify-between border rounded-lg p-4">
            <div>
              <Label>Allow Avatar Uploads</Label>
              <p className="text-sm text-gray-500">
                Allow users to upload custom avatars
              </p>
            </div>
            <Switch
              checked={settings.allowUploads}
              onCheckedChange={(checked) => setSettings({ ...settings, allowUploads: checked })}
            />
          </div>

          <div className="flex items-center justify-between border rounded-lg p-4">
            <div>
              <Label>Generate on Startup</Label>
              <p className="text-sm text-gray-500">
                Automatically generate missing avatars when the application starts
              </p>
            </div>
            <Switch
              checked={settings.generateOnStartup}
              onCheckedChange={(checked) =>
                setSettings({ ...settings, generateOnStartup: checked })
              }
            />
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
