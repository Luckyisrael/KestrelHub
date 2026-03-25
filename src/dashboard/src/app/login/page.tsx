'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { Eye, EyeOff } from 'lucide-react';
import { login } from '@/lib/api';

export default function LoginPage() {
  const router = useRouter();
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [showPw, setShowPw] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    const token = localStorage.getItem('kh_token');
    if (token) { router.push('/deployments'); return; }
    fetch('/api/setup/status').then(r => r.json()).then(d => {
      if (!d.isSetupComplete) router.push('/setup');
    }).catch(() => {});
  }, [router]);

  const handleSubmit = async () => {
    setError('');
    setLoading(true);
    const success = await login(email, password);
    setLoading(false);
    if (success) router.push('/deployments');
    else setError('Invalid email or password.');
  };

  return (
    <div className="min-h-screen flex items-center justify-center p-4">
      <div className="w-full max-w-sm bg-[var(--surface)] border border-[var(--border)] rounded-xl p-8">
        <h1 className="text-xl font-semibold text-center">Sign in to KestrelHub</h1>
        <p className="text-sm text-[var(--text-muted)] text-center mt-1 mb-6">Enter your credentials to continue.</p>

        {error && <div className="bg-red-500/10 border border-red-500/20 text-red-400 text-sm rounded-lg px-3 py-2 mb-4">{error}</div>}

        <div className="space-y-3">
          <input value={email} onChange={e => setEmail(e.target.value)} placeholder="Email" type="email"
            className="w-full bg-[var(--bg)] border border-[var(--border)] rounded-lg px-3 py-2.5 text-sm text-[var(--text)] placeholder:text-[var(--text-muted)] focus:border-[var(--primary)] outline-none" />
          <div className="relative">
            <input value={password} onChange={e => setPassword(e.target.value)} placeholder="Password"
              type={showPw ? 'text' : 'password'} onKeyDown={e => e.key === 'Enter' && handleSubmit()}
              className="w-full bg-[var(--bg)] border border-[var(--border)] rounded-lg px-3 py-2.5 pr-10 text-sm text-[var(--text)] placeholder:text-[var(--text-muted)] focus:border-[var(--primary)] outline-none" />
            <button onClick={() => setShowPw(!showPw)} className="absolute right-3 top-1/2 -translate-y-1/2 text-[var(--text-muted)]">
              {showPw ? <EyeOff size={16} /> : <Eye size={16} />}
            </button>
          </div>
        </div>

        <button onClick={handleSubmit} disabled={loading}
          className="w-full mt-5 h-10 rounded-lg text-sm font-semibold text-black disabled:opacity-50"
          style={{ background: 'var(--primary)' }}>
          {loading ? 'Signing in...' : 'Sign In'}
        </button>
      </div>
    </div>
  );
}
