'use client';

import { useState } from 'react';
import { useRouter } from 'next/navigation';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Plus, CloudOff, StopCircle, Trash2 } from 'lucide-react';
import AppShell from '@/components/AppShell';
import { api } from '@/lib/api';
import { cn, statusColors, formatDate } from '@/lib/utils';

interface Deployment {
  id: string; name: string; gitUrl: string; branch: string;
  status: string; assignedDomain: string | null; createdAt: string;
}

export default function DeploymentsPage() {
  const router = useRouter();
  const qc = useQueryClient();
  const [showCreate, setShowCreate] = useState(false);
  const [form, setForm] = useState({ name: '', gitUrl: '', branch: 'main' });

  const { data: deployments = [], isLoading } = useQuery({
    queryKey: ['deployments'],
    queryFn: () => api<Deployment[]>('/api/deployments'),
  });

  const createMut = useMutation({
    mutationFn: () => api('/api/deployments', { method: 'POST', body: JSON.stringify(form) }),
    onSuccess: () => { setShowCreate(false); setForm({ name: '', gitUrl: '', branch: 'main' }); qc.invalidateQueries({ queryKey: ['deployments'] }); },
  });

  const stopMut = useMutation({
    mutationFn: (id: string) => api(`/api/deployments/${id}/stop`, { method: 'POST' }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['deployments'] }),
  });

  const deleteMut = useMutation({
    mutationFn: (id: string) => api(`/api/deployments/${id}`, { method: 'DELETE' }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['deployments'] }),
  });

  return (
    <AppShell>
      <div className="p-6">
        <div className="flex items-center justify-between mb-6">
          <div>
            <h1 className="text-xl font-semibold">Deployments</h1>
            <p className="text-sm text-[var(--text-muted)] mt-0.5">Manage your deployed applications</p>
          </div>
          <button onClick={() => setShowCreate(true)}
            className="flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-semibold text-black"
            style={{ background: 'var(--primary)' }}>
            <Plus size={16} /> New Deployment
          </button>
        </div>

        {isLoading ? (
          <div className="flex justify-center py-16"><div className="w-8 h-8 border-2 border-[var(--primary)] border-t-transparent rounded-full animate-spin" /></div>
        ) : deployments.length === 0 ? (
          <div className="text-center py-20 bg-[var(--surface)] rounded-xl border border-[var(--border)]">
            <CloudOff size={48} className="mx-auto text-[var(--text-muted)] mb-4" />
            <h3 className="text-lg font-medium">No deployments yet</h3>
            <p className="text-sm text-[var(--text-muted)] mt-1">Create your first deployment to get started.</p>
          </div>
        ) : (
          <div className="bg-[var(--surface)] rounded-xl border border-[var(--border)] overflow-hidden">
            <table className="w-full">
              <thead>
                <tr className="border-b border-[var(--border)]">
                  {['Name', 'Repository', 'Branch', 'Status', 'Domain', 'Created', ''].map(h => (
                    <th key={h} className="px-4 py-3 text-left text-[11px] font-semibold text-[var(--text-muted)] uppercase tracking-wider">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {deployments.map(d => {
                  const sc = statusColors[d.status] || statusColors.Pending;
                  return (
                    <tr key={d.id} className="border-b border-[var(--border)] last:border-0 hover:bg-[var(--surface-hover)]">
                      <td className="px-4 py-3">
                        <button onClick={() => router.push(`/deployments/${d.id}`)}
                          className="text-sm font-medium hover:underline" style={{ color: 'var(--primary)' }}>{d.name}</button>
                      </td>
                      <td className="px-4 py-3 text-sm text-[var(--text-muted)] max-w-[200px] truncate">{d.gitUrl}</td>
                      <td className="px-4 py-3 text-sm text-[var(--text-muted)]">{d.branch}</td>
                      <td className="px-4 py-3">
                        <span className={cn('px-2.5 py-1 rounded-full text-xs font-medium', sc.bg, sc.text)}>{d.status}</span>
                      </td>
                      <td className="px-4 py-3 text-sm" style={{ color: 'var(--secondary)' }}>{d.assignedDomain || '—'}</td>
                      <td className="px-4 py-3 text-sm text-[var(--text-muted)]">{formatDate(d.createdAt)}</td>
                      <td className="px-4 py-3">
                        <div className="flex gap-1 justify-end">
                          {d.status === 'Running' && (
                            <button onClick={() => stopMut.mutate(d.id)} className="p-1.5 rounded hover:bg-[var(--surface-hover)] text-amber-400"><StopCircle size={16} /></button>
                          )}
                          <button onClick={() => { if (confirm('Delete?')) deleteMut.mutate(d.id); }} className="p-1.5 rounded hover:bg-[var(--surface-hover)] text-red-400"><Trash2 size={16} /></button>
                        </div>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}

        {showCreate && (
          <div className="fixed inset-0 bg-black/60 z-50 flex items-center justify-center p-4">
            <div className="bg-[var(--surface)] border border-[var(--border)] rounded-xl p-6 w-full max-w-md">
              <h2 className="text-lg font-semibold mb-4">New Deployment</h2>
              <div className="space-y-3">
                <input value={form.name} onChange={e => setForm(f => ({ ...f, name: e.target.value }))} placeholder="Name (auto from URL if empty)"
                  className="w-full bg-[var(--bg)] border border-[var(--border)] rounded-lg px-3 py-2.5 text-sm text-[var(--text)] placeholder:text-[var(--text-muted)] outline-none focus:border-[var(--primary)]" />
                <input value={form.gitUrl} onChange={e => setForm(f => ({ ...f, gitUrl: e.target.value }))} placeholder="Git Repository URL"
                  className="w-full bg-[var(--bg)] border border-[var(--border)] rounded-lg px-3 py-2.5 text-sm text-[var(--text)] placeholder:text-[var(--text-muted)] outline-none focus:border-[var(--primary)]" />
                <input value={form.branch} onChange={e => setForm(f => ({ ...f, branch: e.target.value }))} placeholder="Branch"
                  className="w-full bg-[var(--bg)] border border-[var(--border)] rounded-lg px-3 py-2.5 text-sm text-[var(--text)] placeholder:text-[var(--text-muted)] outline-none focus:border-[var(--primary)]" />
              </div>
              <div className="flex gap-3 mt-5">
                <button onClick={() => setShowCreate(false)} className="flex-1 h-10 rounded-lg text-sm border border-[var(--border)] text-[var(--text-muted)]">Cancel</button>
                <button onClick={() => createMut.mutate()} disabled={createMut.isPending || !form.gitUrl}
                  className="flex-1 h-10 rounded-lg text-sm font-semibold text-black disabled:opacity-50" style={{ background: 'var(--primary)' }}>
                  {createMut.isPending ? 'Deploying...' : 'Deploy'}
                </button>
              </div>
            </div>
          </div>
        )}
      </div>
    </AppShell>
  );
}
