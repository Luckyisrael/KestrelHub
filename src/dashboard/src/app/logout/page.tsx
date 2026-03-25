'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { logout } from '@/lib/api';

export default function LogoutPage() {
  const router = useRouter();
  useEffect(() => {
    logout().then(() => router.push('/login'));
  }, [router]);
  return (
    <div className="min-h-screen flex items-center justify-center text-[var(--text-muted)]">
      Logging out...
    </div>
  );
}
