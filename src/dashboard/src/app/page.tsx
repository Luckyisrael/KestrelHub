'use client';

import { useEffect } from 'react';
import { useRouter } from 'next/navigation';

export default function HomePage() {
  const router = useRouter();

  useEffect(() => {
    fetch('/api/setup/status')
      .then(r => r.json())
      .then(d => {
        if (!d.isSetupComplete) {
          router.replace('/setup');
        } else {
          const token = localStorage.getItem('kh_token');
          if (token) router.replace('/deployments');
          else router.replace('/login');
        }
      })
      .catch(() => router.replace('/login'));
  }, [router]);

  return (
    <div className="min-h-screen flex items-center justify-center">
      <div className="w-8 h-8 border-2 border-[var(--primary)] border-t-transparent rounded-full animate-spin" />
    </div>
  );
}
