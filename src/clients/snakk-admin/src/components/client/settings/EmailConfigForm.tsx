'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { useToast } from '@/hooks/use-toast';
import type { EmailConfig } from '@/lib/api/settings';
import { updateEmailConfig, testEmailConfig } from '@/lib/api/settings';

interface EmailConfigFormProps {
  initialConfig: EmailConfig;
}

export function EmailConfigForm({ initialConfig }: EmailConfigFormProps) {
  const router = useRouter();
  const { toast } = useToast();
  const [isLoading, setIsLoading] = useState(false);
  const [isTesting, setIsTesting] = useState(false);
  const [config, setConfig] = useState<EmailConfig>(initialConfig);
  const [testEmail, setTestEmail] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setIsLoading(true);

    try {
      await updateEmailConfig(config);

      toast({
        title: 'Email settings updated',
        description: 'Email configuration has been updated successfully.',
      });

      router.refresh();
    } catch (error) {
      toast({
        title: 'Error',
        description: error instanceof Error ? error.message : 'Failed to update email settings',
        variant: 'destructive',
      });
    } finally {
      setIsLoading(false);
    }
  };

  const handleTest = async () => {
    if (!testEmail) {
      toast({
        title: 'Error',
        description: 'Please enter a test email address',
        variant: 'destructive',
      });
      return;
    }

    setIsTesting(true);

    try {
      const result = await testEmailConfig(testEmail);

      toast({
        title: 'Test email sent',
        description: result.message || 'Test email sent successfully. Check your inbox.',
      });
    } catch (error) {
      toast({
        title: 'Test failed',
        description: error instanceof Error ? error.message : 'Failed to send test email',
        variant: 'destructive',
      });
    } finally {
      setIsTesting(false);
    }
  };

  const handleReset = () => {
    setConfig(initialConfig);
  };

  return (
    <form onSubmit={handleSubmit}>
      <Card>
        <CardHeader>
          <CardTitle>Email Configuration</CardTitle>
          <CardDescription>
            Configure SMTP settings for sending emails
          </CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="flex items-center justify-between">
            <div className="space-y-0.5">
              <Label>Enable Email System</Label>
              <p className="text-sm text-gray-500">
                Allow the system to send emails
              </p>
            </div>
            <Switch
              checked={config.enabled}
              onCheckedChange={(checked) => setConfig({ ...config, enabled: checked })}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="smtpHost">SMTP Host</Label>
            <Input
              id="smtpHost"
              value={config.smtpHost}
              onChange={(e) => setConfig({ ...config, smtpHost: e.target.value })}
              placeholder="smtp.gmail.com"
              disabled={!config.enabled}
            />
          </div>

          <div className="grid grid-cols-2 gap-4">
            <div className="space-y-2">
              <Label htmlFor="smtpPort">SMTP Port</Label>
              <Input
                id="smtpPort"
                type="number"
                value={config.smtpPort}
                onChange={(e) => setConfig({ ...config, smtpPort: parseInt(e.target.value) || 587 })}
                placeholder="587"
                disabled={!config.enabled}
              />
            </div>

            <div className="flex items-center justify-between pt-8">
              <Label>Use SSL</Label>
              <Switch
                checked={config.useSsl}
                onCheckedChange={(checked) => setConfig({ ...config, useSsl: checked })}
                disabled={!config.enabled}
              />
            </div>
          </div>

          <div className="space-y-2">
            <Label htmlFor="smtpUsername">SMTP Username</Label>
            <Input
              id="smtpUsername"
              value={config.smtpUsername}
              onChange={(e) => setConfig({ ...config, smtpUsername: e.target.value })}
              placeholder="your-email@gmail.com"
              disabled={!config.enabled}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="smtpPassword">SMTP Password</Label>
            <Input
              id="smtpPassword"
              type="password"
              value={config.smtpPassword}
              onChange={(e) => setConfig({ ...config, smtpPassword: e.target.value })}
              placeholder="••••••••"
              disabled={!config.enabled}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="fromEmail">From Email</Label>
            <Input
              id="fromEmail"
              type="email"
              value={config.fromEmail}
              onChange={(e) => setConfig({ ...config, fromEmail: e.target.value })}
              placeholder="noreply@snakk.com"
              disabled={!config.enabled}
            />
          </div>

          <div className="space-y-2">
            <Label htmlFor="fromName">From Name</Label>
            <Input
              id="fromName"
              value={config.fromName}
              onChange={(e) => setConfig({ ...config, fromName: e.target.value })}
              placeholder="Snakk"
              disabled={!config.enabled}
            />
          </div>

          <div className="pt-4 border-t">
            <Label>Test Email Configuration</Label>
            <p className="text-sm text-gray-500 mb-3">
              Send a test email to verify your settings
            </p>
            <div className="flex gap-2">
              <Input
                type="email"
                value={testEmail}
                onChange={(e) => setTestEmail(e.target.value)}
                placeholder="test@example.com"
                disabled={!config.enabled}
              />
              <Button
                type="button"
                variant="outline"
                onClick={handleTest}
                disabled={!config.enabled || isTesting}
              >
                {isTesting ? 'Sending...' : 'Send Test'}
              </Button>
            </div>
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
