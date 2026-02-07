'use client'

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { Button } from '@/components/ui/button';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { useToast } from '@/hooks/use-toast';
import { banUser, unbanUser, updateUserRole, type User } from '@/lib/api/users';
import { MoreVertical, Ban, Shield, UserCheck } from 'lucide-react';

interface UserActionsProps {
  user: User;
}

export function UserActions({ user }: UserActionsProps) {
  const router = useRouter();
  const { toast } = useToast();
  const [showBanDialog, setShowBanDialog] = useState(false);
  const [banReason, setBanReason] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  const handleBan = async () => {
    if (!banReason.trim()) {
      toast({
        variant: 'destructive',
        title: 'Error',
        description: 'Please provide a reason for the ban',
      });
      return;
    }

    setIsLoading(true);
    try {
      await banUser(user.id, banReason);
      toast({
        title: 'User banned',
        description: `${user.displayName} has been banned successfully`,
      });
      setShowBanDialog(false);
      setBanReason('');
      router.refresh();
    } catch (error) {
      toast({
        variant: 'destructive',
        title: 'Error',
        description: 'Failed to ban user. Please try again.',
      });
    } finally {
      setIsLoading(false);
    }
  };

  const handleUnban = async () => {
    setIsLoading(true);
    try {
      await unbanUser(user.id);
      toast({
        title: 'User unbanned',
        description: `${user.displayName} has been unbanned successfully`,
      });
      router.refresh();
    } catch (error) {
      toast({
        variant: 'destructive',
        title: 'Error',
        description: 'Failed to unban user. Please try again.',
      });
    } finally {
      setIsLoading(false);
    }
  };

  const handleRoleChange = async (newRole: string) => {
    setIsLoading(true);
    try {
      await updateUserRole(user.id, newRole);
      toast({
        title: 'Role updated',
        description: `${user.displayName}'s role has been changed to ${newRole}`,
      });
      router.refresh();
    } catch (error) {
      toast({
        variant: 'destructive',
        title: 'Error',
        description: 'Failed to update role. Please try again.',
      });
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <>
      <DropdownMenu>
        <DropdownMenuTrigger asChild>
          <Button variant="outline" size="icon" disabled={isLoading}>
            <MoreVertical className="w-4 h-4" />
            <span className="sr-only">Actions</span>
          </Button>
        </DropdownMenuTrigger>
        <DropdownMenuContent align="end">
          <DropdownMenuItem onClick={() => handleRoleChange('User')}>
            <UserCheck className="w-4 h-4 mr-2" />
            Make User
          </DropdownMenuItem>
          <DropdownMenuItem onClick={() => handleRoleChange('Moderator')}>
            <Shield className="w-4 h-4 mr-2" />
            Make Moderator
          </DropdownMenuItem>
          <DropdownMenuSeparator />
          {user.status === 'banned' ? (
            <DropdownMenuItem onClick={handleUnban}>
              <UserCheck className="w-4 h-4 mr-2" />
              Unban User
            </DropdownMenuItem>
          ) : (
            <DropdownMenuItem onClick={() => setShowBanDialog(true)}>
              <Ban className="w-4 h-4 mr-2" />
              Ban User
            </DropdownMenuItem>
          )}
        </DropdownMenuContent>
      </DropdownMenu>

      {/* Ban Dialog */}
      <Dialog open={showBanDialog} onOpenChange={setShowBanDialog}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Ban User</DialogTitle>
            <DialogDescription>
              This action will ban {user.displayName} from the platform. Please provide a reason.
            </DialogDescription>
          </DialogHeader>
          <div className="space-y-4 py-4">
            <div className="space-y-2">
              <Label htmlFor="reason">Reason</Label>
              <Input
                id="reason"
                placeholder="e.g., Spam, harassment, violation of terms"
                value={banReason}
                onChange={(e) => setBanReason(e.target.value)}
              />
            </div>
          </div>
          <DialogFooter>
            <Button
              variant="outline"
              onClick={() => setShowBanDialog(false)}
              disabled={isLoading}
            >
              Cancel
            </Button>
            <Button
              variant="destructive"
              onClick={handleBan}
              disabled={isLoading}
            >
              {isLoading ? 'Banning...' : 'Ban User'}
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </>
  );
}
