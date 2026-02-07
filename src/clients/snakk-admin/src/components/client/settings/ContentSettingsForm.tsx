'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { useToast } from '@/hooks/use-toast';
import type { ContentSettings } from '@/lib/api/settings';
import { updateContentSettings } from '@/lib/api/settings';

interface ContentSettingsFormProps {
  initialSettings: ContentSettings;
}

export function ContentSettingsForm({ initialSettings }: ContentSettingsFormProps) {
  const router = useRouter();
  const { toast } = useToast();
  const [isLoading, setIsLoading] = useState(false);
  const [settings, setSettings] = useState<ContentSettings>(initialSettings);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);

    try {
      await updateContentSettings(settings);

      toast({
        title: 'Content settings updated',
        description: 'Content settings have been updated successfully.',
      });

      router.refresh();
    } catch (error) {
      toast({
        title: 'Error',
        description: error instanceof Error ? error.message : 'Failed to update content settings',
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
          <CardTitle>Content Settings</CardTitle>
          <CardDescription>
            Configure content limits and moderation rules
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="space-y-2">
            <Label htmlFor="postMaxLength">Post Max Length (characters)</Label>
            <Input
              id="postMaxLength"
              type="number"
              min="100"
              max="100000"
              value={settings.postMaxLength}
              onChange={(e) =>
                setSettings({ ...settings, postMaxLength: parseInt(e.target.value) || 50000 })
              }
              placeholder="50000"
              required
            />
            <p className="text-sm text-gray-500">
              Maximum length for post content (100-100,000 characters)
            </p>
          </div>

          <div className="space-y-2">
            <Label htmlFor="discussionTitleMaxLength">
              Discussion Title Max Length (characters)
            </Label>
            <Input
              id="discussionTitleMaxLength"
              type="number"
              min="10"
              max="1000"
              value={settings.discussionTitleMaxLength}
              onChange={(e) =>
                setSettings({
                  ...settings,
                  discussionTitleMaxLength: parseInt(e.target.value) || 300,
                })
              }
              placeholder="300"
              required
            />
            <p className="text-sm text-gray-500">
              Maximum length for discussion titles (10-1,000 characters)
            </p>
          </div>

          <div className="flex items-center justify-between border rounded-lg p-4">
            <div>
              <Label>Allow Markdown</Label>
              <p className="text-sm text-gray-500">
                Allow users to use Markdown formatting in posts
              </p>
            </div>
            <Switch
              checked={settings.allowMarkdown}
              onCheckedChange={(checked) => setSettings({ ...settings, allowMarkdown: checked })}
            />
          </div>

          <div className="flex items-center justify-between border rounded-lg p-4">
            <div>
              <Label>Allow HTML</Label>
              <p className="text-sm text-gray-500">
                Allow users to use HTML tags in posts (not recommended)
              </p>
            </div>
            <Switch
              checked={settings.allowHtml}
              onCheckedChange={(checked) => setSettings({ ...settings, allowHtml: checked })}
            />
          </div>

          <div className="flex items-center justify-between border rounded-lg p-4">
            <div>
              <Label>Require Moderation</Label>
              <p className="text-sm text-gray-500">
                Require moderator approval for all new content
              </p>
            </div>
            <Switch
              checked={settings.requireModeration}
              onCheckedChange={(checked) =>
                setSettings({ ...settings, requireModeration: checked })
              }
            />
          </div>

          <div className="mt-4 p-4 bg-yellow-50 border border-yellow-200 rounded-lg">
            <p className="text-sm text-yellow-900">
              <strong>Warning:</strong> Enabling HTML can pose security risks. Only enable if you trust your users and have proper content sanitization in place.
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
