'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { Eye, EyeOff, CheckCircle2, XCircle } from 'lucide-react';

export default function SetupPage() {
  const router = useRouter();
  const [name, setName] = useState('');
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [showPw, setShowPw] = useState(false);
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);

  const checks = [
    { ok: password.length >= 12, label: '12+ characters' },
    { ok: /[A-Z]/.test(password), label: 'Uppercase letter' },
    { ok: /[a-z]/.test(password), label: 'Lowercase letter' },
    { ok: /\d/.test(password), label: 'Number' },
    { ok: /[^A-Za-z0-9]/.test(password), label: 'Special character' },
  ];

  const handleSubmit = async () => {
    setError('');
    if (password !== confirm) { setError('Passwords do not match.'); return; }
    if (password.length < 12) { setError('Password must be at least 12 characters.'); return; }

    setLoading(true);
    try {
      const res = await fetch('/api/setup/complete', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ adminEmail: email, adminPassword: password, adminDisplayName: name }),
      });
      if (res.ok) router.push('/login');
      else {
        const data = await res.json();
        setError(data.error || data.errors?.join(', ') || 'Setup failed.');
      }
    } catch { setError('Could not connect to server.'); }
    setLoading(false);
  };

  return (
    <div className="min-h-screen flex items-center justify-center p-4">
      <div className="w-full max-w-sm bg-[var(--surface)] border border-[var(--border)] rounded-xl p-8">
        <h1 className="text-xl font-semibold text-center">Welcome to KestrelHub</h1>
        <p className="text-sm text-[var(--text-muted)] text-center mt-1 mb-6">Create your admin account to get started.</p>

        {error && <div className="bg-red-500/10 border border-red-500/20 text-red-400 text-sm rounded-lg px-3 py-2 mb-4">{error}</div>}

        <div className="space-y-3">
          <input value={name} onChange={e => setName(e.target.value)} placeholder="Display Name"
            className="w-full bg-[var(--bg)] border border-[var(--border)] rounded-lg px-3 py-2.5 text-sm text-[var(--text)] placeholder:text-[var(--text-muted)] focus:border-[var(--primary)] outline-none" />
          <input value={email} onChange={e => setEmail(e.target.value)} placeholder="Email" type="email"
            className="w-full bg-[var(--bg)] border border-[var(--border)] rounded-lg px-3 py-2.5 text-sm text-[var(--text)] placeholder:text-[var(--text-muted)] focus:border-[var(--primary)] outline-none" />
          <div className="relative">
            <input value={password} onChange={e => setPassword(e.target.value)} placeholder="Password"
              type={showPw ? 'text' : 'password'}
              className="w-full bg-[var(--bg)] border border-[var(--border)] rounded-lg px-3 py-2.5 pr-10 text-sm text-[var(--text)] placeholder:text-[var(--text-muted)] focus:border-[var(--primary)] outline-none" />
            <button onClick={() => setShowPw(!showPw)} className="absolute right-3 top-1/2 -translate-y-1/2 text-[var(--text-muted)]">
              {showPw ? <EyeOff size={16} /> : <Eye size={16} />}
            </button>
          </div>
          <input value={confirm} onChange={e => setConfirm(e.target.value)} placeholder="Confirm Password"
            type={showPw ? 'text' : 'password'}
            className="w-full bg-[var(--bg)] border border-[var(--border)] rounded-lg px-3 py-2.5 text-sm text-[var(--text)] placeholder:text-[var(--text-muted)] focus:border-[var(--primary)] outline-none" />
        </div>

        {password && (
          <div className="mt-3 space-y-1">
            {checks.map(c => (
              <div key={c.label} className="flex items-center gap-2 text-xs">
                {c.ok ? <CheckCircle2 size={14} className="text-emerald-400" /> : <XCircle size={14} className="text-zinc-600" />}
                <span className={c.ok ? 'text-emerald-400' : 'text-[var(--text-muted)]'}>{c.label}</span>
              </div>
            ))}
          </div>
        )}

        <button onClick={handleSubmit} disabled={loading}
          className="w-full mt-5 h-10 rounded-lg text-sm font-semibold text-black disabled:opacity-50"
          style={{ background: 'var(--primary)' }}>
          {loading ? 'Creating...' : 'Create Admin Account'}
        </button>
      </div>
    </div>
  );
}
