import { LoginForm } from '@/components/auth/LoginForm';
import { redirect } from 'next/navigation';
import { getServerSession } from '@/lib/auth/server';

export default async function LoginPage() {
  const session = await getServerSession();

  // Redirect if already logged in
  if (session) {
    redirect('/');
  }

  return (
    <div className="min-h-screen flex items-center justify-center bg-gray-50">
      <div className="w-full max-w-md space-y-8 p-8">
        <div className="text-center">
          <h1 className="text-3xl font-bold tracking-tight">Snakk Admin</h1>
          <p className="mt-2 text-sm text-gray-600">
            Sign in to access the admin panel
          </p>
        </div>
        <LoginForm />
      </div>
    </div>
  );
}
