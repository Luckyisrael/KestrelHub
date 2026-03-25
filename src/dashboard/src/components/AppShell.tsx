'use client';

import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';
import { useEffect, useState } from 'react';
import {
  Cloud, Key, Settings, LogOut, Menu, X,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import { logout } from '@/lib/api';

const navItems = [
  { href: '/deployments', label: 'Deployments', icon: Cloud },
  { href: '/secrets', label: 'Secrets', icon: Key },
  { href: '/settings/users', label: 'Settings', icon: Settings, adminOnly: true },
];

export default function AppShell({ children }: { children: React.ReactNode }) {
  const pathname = usePathname();
  const router = useRouter();
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const [user, setUser] = useState<{ email: string; displayName: string; roles: string[] } | null>(null);

  useEffect(() => {
    const token = localStorage.getItem('kh_token');
    if (!token) {
      router.push('/login');
      return;
    }
    try {
      const payload = JSON.parse(atob(token.split('.')[1]));
      setUser({
        email: payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress'] || '',
        displayName: payload['http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name'] || '',
        roles: Array.isArray(payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'])
          ? payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role']
          : [payload['http://schemas.microsoft.com/ws/2008/06/identity/claims/role'] || ''],
      });
    } catch {
      router.push('/login');
    }
  }, [router]);

  const handleLogout = async () => {
    await logout();
    router.push('/login');
  };

  const isAdmin = user?.roles.includes('Admin');

  return (
    <div className="flex h-screen">
      {/* Sidebar */}
      <aside className={cn(
        'fixed inset-y-0 left-0 z-50 w-60 bg-[var(--surface)] border-r border-[var(--border)] flex flex-col transform transition-transform lg:translate-x-0 lg:static',
        sidebarOpen ? 'translate-x-0' : '-translate-x-full'
      )}>
        <div className="flex items-center justify-between px-5 py-4 border-b border-[var(--border)]">
          <span className="text-lg font-bold tracking-tight" style={{ color: 'var(--primary)' }}>
            KestrelHub
          </span>
          <button onClick={() => setSidebarOpen(false)} className="lg:hidden text-[var(--text-muted)]">
            <X size={18} />
          </button>
        </div>

        <nav className="flex-1 px-3 py-3 space-y-1">
          {navItems.map((item) => {
            if (item.adminOnly && !isAdmin) return null;
            const isActive = pathname.startsWith(item.href);
            return (
              <Link
                key={item.href}
                href={item.href}
                onClick={() => setSidebarOpen(false)}
                className={cn(
                  'flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium transition-colors',
                  isActive
                    ? 'bg-[rgba(0,212,255,0.08)] text-[var(--primary)]'
                    : 'text-[var(--text-muted)] hover:bg-[var(--surface-hover)] hover:text-[var(--text)]'
                )}
              >
                <item.icon size={18} />
                {item.label}
              </Link>
            );
          })}
        </nav>

        <div className="px-3 py-3 border-t border-[var(--border)]">
          <button
            onClick={handleLogout}
            className="flex items-center gap-3 px-3 py-2.5 rounded-lg text-sm font-medium text-[var(--text-muted)] hover:bg-[var(--surface-hover)] hover:text-[var(--text)] w-full"
          >
            <LogOut size={18} />
            Logout
          </button>
        </div>
      </aside>

      {/* Overlay */}
      {sidebarOpen && (
        <div className="fixed inset-0 bg-black/50 z-40 lg:hidden" onClick={() => setSidebarOpen(false)} />
      )}

      {/* Main */}
      <main className="flex-1 flex flex-col min-w-0">
        <header className="h-13 bg-[var(--surface)] border-b border-[var(--border)] flex items-center justify-between px-5 flex-shrink-0">
          <button onClick={() => setSidebarOpen(true)} className="lg:hidden text-[var(--text-muted)]">
            <Menu size={20} />
          </button>
          <div className="flex-1" />
          <span className="text-sm text-[var(--text-muted)]">{user?.displayName || user?.email}</span>
        </header>
        <div className="flex-1 overflow-y-auto">
          {children}
        </div>
      </main>
    </div>
  );
}
