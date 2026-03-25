'use client';

import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Plus, Trash2, ShieldCheck, ShieldX } from 'lucide-react';
import AppShell from '@/components/AppShell';
import { api } from '@/lib/api';
import { formatDate } from '@/lib/utils';

interface User { id: string; email: string; displayName: string; isActive: boolean; createdAt: string; lastLoginAt: string | null; roles: string[]; }

export default function UsersPage() {
  const qc = useQueryClient();
  const [showInvite, setShowInvite] = useState(false);
  const [form, setForm] = useState({ email: '', role: 'Viewer' });
  const [inviteToken, setInviteToken] = useState<string | null>(null);

  const { data: users = [], isLoading } = useQuery({
    queryKey: ['users'],
    queryFn: () => api<User[]>('/api/users'),
  });

  const inviteMut = useMutation({
    mutationFn: () => api<{ inviteToken: string }>('/api/users/invite', { method: 'POST', body: JSON.stringify(form) }),
    onSuccess: (data) => { setInviteToken(data.inviteToken); setShowInvite(false); setForm({ email: '', role: 'Viewer' }); },
  });

  const updateMut = useMutation({
    mutationFn: (data: { id: string; body: any }) => api(`/api/users/${data.id}`, { method: 'PUT', body: JSON.stringify(data.body) }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['users'] }),
  });

  const deleteMut = useMutation({
    mutationFn: (id: string) => api(`/api/users/${id}`, { method: 'DELETE' }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['users'] }),
  });

  return (
    <AppShell>
      <div className="p-6">
        <div className="flex items-center justify-between mb-6">
          <div>
            <h1 className="text-xl font-semibold">User Management</h1>
            <p className="text-sm text-[var(--text-muted)] mt-0.5">Manage users, roles, and invitations</p>
          </div>
          <button onClick={() => setShowInvite(true)} className="flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-semibold text-black" style={{ background: 'var(--primary)' }}>
            <Plus size={16} /> Invite User
          </button>
        </div>

        {inviteToken && (
          <div className="bg-emerald-500/10 border border-emerald-500/20 rounded-xl p-4 mb-6">
            <p className="text-sm text-emerald-400 font-medium mb-1">Invite token:</p>
            <code className="text-xs text-emerald-300 break-all bg-black/30 px-3 py-2 rounded-lg block">{inviteToken}</code>
            <button onClick={() => setInviteToken(null)} className="text-xs text-emerald-400 underline mt-2">Dismiss</button>
          </div>
        )}

        {isLoading ? (
          <div className="flex justify-center py-16"><div className="w-8 h-8 border-2 border-[var(--primary)] border-t-transparent rounded-full animate-spin" /></div>
        ) : (
          <div className="bg-[var(--surface)] rounded-xl border border-[var(--border)] overflow-hidden">
            <table className="w-full">
              <thead>
                <tr className="border-b border-[var(--border)]">
                  {['User', 'Role', 'Status', 'Last Login', ''].map(h => (
                    <th key={h} className="px-4 py-3 text-left text-[11px] font-semibold text-[var(--text-muted)] uppercase tracking-wider">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {users.map(u => (
                  <tr key={u.id} className="border-b border-[var(--border)] last:border-0">
                    <td className="px-4 py-3">
                      <div className="text-sm font-medium">{u.displayName}</div>
                      <div className="text-xs text-[var(--text-muted)]">{u.email}</div>
                    </td>
                    <td className="px-4 py-3">
                      <select value={u.roles[0] || 'Viewer'} onChange={e => updateMut.mutate({ id: u.id, body: { role: e.target.value } })}
                        className="bg-[var(--bg)] border border-[var(--border)] rounded-lg px-2 py-1.5 text-xs text-[var(--text)] outline-none">
                        <option>Admin</option><option>Developer</option><option>Viewer</option>
                      </select>
                    </td>
                    <td className="px-4 py-3">
                      <span className={u.isActive ? 'text-emerald-400 text-xs' : 'text-red-400 text-xs'}>{u.isActive ? 'Active' : 'Inactive'}</span>
                    </td>
                    <td className="px-4 py-3 text-sm text-[var(--text-muted)]">{u.lastLoginAt ? formatDate(u.lastLoginAt) : 'Never'}</td>
                    <td className="px-4 py-3">
                      <div className="flex gap-1 justify-end">
                        <button onClick={() => updateMut.mutate({ id: u.id, body: { isActive: !u.isActive } })}
                          className={u.isActive ? 'p-1.5 rounded hover:bg-[var(--surface-hover)] text-amber-400' : 'p-1.5 rounded hover:bg-[var(--surface-hover)] text-emerald-400'}>
                          {u.isActive ? <ShieldX size={16} /> : <ShieldCheck size={16} />}
                        </button>
                        <button onClick={() => { if (confirm('Delete?')) deleteMut.mutate(u.id); }} className="p-1.5 rounded hover:bg-[var(--surface-hover)] text-red-400"><Trash2 size={16} /></button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {showInvite && (
          <div className="fixed inset-0 bg-black/60 z-50 flex items-center justify-center p-4">
            <div className="bg-[var(--surface)] border border-[var(--border)] rounded-xl p-6 w-full max-w-md">
              <h2 className="text-lg font-semibold mb-4">Invite User</h2>
              <div className="space-y-3">
                <input value={form.email} onChange={e => setForm(f => ({ ...f, email: e.target.value }))} placeholder="Email" className="w-full bg-[var(--bg)] border border-[var(--border)] rounded-lg px-3 py-2.5 text-sm text-[var(--text)] placeholder:text-[var(--text-muted)] outline-none focus:border-[var(--primary)]" />
                <select value={form.role} onChange={e => setForm(f => ({ ...f, role: e.target.value }))} className="w-full bg-[var(--bg)] border border-[var(--border)] rounded-lg px-3 py-2.5 text-sm text-[var(--text)] outline-none">
                  <option>Viewer</option><option>Developer</option><option>Admin</option>
                </select>
              </div>
              <div className="flex gap-3 mt-5">
                <button onClick={() => setShowInvite(false)} className="flex-1 h-10 rounded-lg text-sm border border-[var(--border)] text-[var(--text-muted)]">Cancel</button>
                <button onClick={() => inviteMut.mutate()} className="flex-1 h-10 rounded-lg text-sm font-semibold text-black" style={{ background: 'var(--primary)' }}>Send Invite</button>
              </div>
            </div>
          </div>
        )}
      </div>
    </AppShell>
  );
}
