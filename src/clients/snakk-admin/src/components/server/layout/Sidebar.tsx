import Link from 'next/link';
import {
  LayoutDashboard,
  Users,
  FileText,
  Shield,
  Lock,
  BarChart3,
  Settings
} from 'lucide-react';

const navigation = [
  { name: 'Dashboard', href: '/', icon: LayoutDashboard },
  { name: 'Users', href: '/users', icon: Users },
  { name: 'Content', href: '/content', icon: FileText },
  { name: 'Moderation', href: '/moderation', icon: Shield },
  { name: 'Security & Audit', href: '/security', icon: Lock },
  { name: 'Analytics', href: '/analytics', icon: BarChart3 },
  { name: 'Settings', href: '/settings', icon: Settings },
];

export function Sidebar() {
  return (
    <aside className="w-64 bg-gray-900 text-white flex flex-col">
      <div className="p-6">
        <h1 className="text-2xl font-bold">Snakk Admin</h1>
      </div>

      <nav className="flex-1 space-y-1 px-3">
        {navigation.map((item) => (
          <Link
            key={item.name}
            href={item.href}
            className="flex items-center px-3 py-2 rounded-md hover:bg-gray-800 transition-colors"
          >
            <item.icon className="w-5 h-5 mr-3" />
            {item.name}
          </Link>
        ))}
      </nav>

      <div className="p-4 border-t border-gray-800 text-xs text-gray-400">
        <p>&copy; 2026 Snakk</p>
        <p>Admin Panel v1.0</p>
      </div>
    </aside>
  );
}
