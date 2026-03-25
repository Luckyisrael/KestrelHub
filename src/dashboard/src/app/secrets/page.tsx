'use client';

import { useEffect, useState } from 'react';
import { Plus, Eye, EyeOff, History, Trash2 } from 'lucide-react';
import AppShell from '@/components/AppShell';
import { api } from '@/lib/api';
import { formatDate } from '@/lib/utils';

interface Secret { id: string; key: string; environment: string; createdAt: string; }

export default function SecretsPage() {
  const [secrets, setSecrets] = useState<Secret[]>([]);
  const [revealed, setRevealed] = useState<Record<string, string>>({});
  const [loading, setLoading] = useState(true);
  const [showCreate, setShowCreate] = useState(false);
  const [form, setForm] = useState({ key: '', value: '', environment: 'Production' });

  const load = async () => {
    setLoading(true);
    try { setSecrets(await api<Secret[]>('/api/secrets')); } catch { }
    setLoading(false);
  };

  useEffect(() => { load(); }, []);

  const reveal = async (id: string) => {
    if (revealed[id]) { setRevealed(r => { const n = { ...r }; delete n[id]; return n; }); return; }
    try {
      const data = await api<{ value: string }>(`/api/secrets/${id}/value`);
      setRevealed(r => ({ ...r, [id]: data.value }));
    } catch (e: any) { alert(e.message); }
  };

  const handleCreate = async () => {
    if (!form.key || !form.value) return;
    try {
      await api('/api/secrets', { method: 'POST', body: JSON.stringify({ ...form, deploymentId: null }) });
      setShowCreate(false); setForm({ key: '', value: '', environment: 'Production' }); await load();
    } catch (e: any) { alert(e.message); }
  };

  const handleDelete = async (id: string) => {
    try { await api(`/api/secrets/${id}`, { method: 'DELETE' }); setSecrets(s => s.filter(x => x.id !== id)); }
    catch (e: any) { alert(e.message); }
  };

  return (
    <AppShell>
      <div className="p-6">
        <div className="flex items-center justify-between mb-6">
          <div>
            <h1 className="text-xl font-semibold">Secret Vault</h1>
            <p className="text-sm text-[var(--text-muted)] mt-0.5">AES-256-GCM encrypted environment variables</p>
          </div>
          <button onClick={() => setShowCreate(true)} className="flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-semibold text-black" style={{ background: 'var(--primary)' }}>
            <Plus size={16} /> Add Secret
          </button>
        </div>

        {loading ? (
          <div className="flex justify-center py-16"><div className="w-8 h-8 border-2 border-[var(--primary)] border-t-transparent rounded-full animate-spin" /></div>
        ) : secrets.length === 0 ? (
          <div className="text-center py-20 bg-[var(--surface)] rounded-xl border border-[var(--border)]">
            <Plus size={48} className="mx-auto text-[var(--text-muted)] mb-4" />
            <h3 className="text-lg font-medium">No secrets yet</h3>
            <p className="text-sm text-[var(--text-muted)] mt-1">Add encrypted environment variables for your deployments.</p>
          </div>
        ) : (
          <div className="bg-[var(--surface)] rounded-xl border border-[var(--border)] overflow-hidden">
            <table className="w-full">
              <thead>
                <tr className="border-b border-[var(--border)]">
                  {['Key', 'Value', 'Environment', 'Created', ''].map(h => (
                    <th key={h} className="px-4 py-3 text-left text-[11px] font-semibold text-[var(--text-muted)] uppercase tracking-wider">{h}</th>
                  ))}
                </tr>
              </thead>
              <tbody>
                {secrets.map(s => (
                  <tr key={s.id} className="border-b border-[var(--border)] last:border-0">
                    <td className="px-4 py-3 text-sm font-mono" style={{ color: 'var(--primary)' }}>{s.key}</td>
                    <td className="px-4 py-3 text-sm font-mono text-[var(--text-muted)]">
                      {revealed[s.id] ? <span className="text-[var(--text)]">{revealed[s.id]}</span> : '••••••••'}
                    </td>
                    <td className="px-4 py-3">
                      <span className={s.environment === 'Production' ? 'text-red-400 text-xs' : 'text-emerald-400 text-xs'}>
                        {s.environment}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-sm text-[var(--text-muted)]">{formatDate(s.createdAt)}</td>
                    <td className="px-4 py-3">
                      <div className="flex gap-1 justify-end">
                        <button onClick={() => reveal(s.id)} className="p-1.5 rounded hover:bg-[var(--surface-hover)] text-[var(--text-muted)]">
                          {revealed[s.id] ? <EyeOff size={15} /> : <Eye size={15} />}
                        </button>
                        <button onClick={() => handleDelete(s.id)} className="p-1.5 rounded hover:bg-[var(--surface-hover)] text-red-400">
                          <Trash2 size={15} />
                        </button>
                      </div>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        {showCreate && (
          <div className="fixed inset-0 bg-black/60 z-50 flex items-center justify-center p-4">
            <div className="bg-[var(--surface)] border border-[var(--border)] rounded-xl p-6 w-full max-w-md">
              <h2 className="text-lg font-semibold mb-4">Add Secret</h2>
              <div className="space-y-3">
                <input value={form.key} onChange={e => setForm(f => ({ ...f, key: e.target.value }))} placeholder="Key (e.g. MY_API_KEY)"
                  className="w-full bg-[var(--bg)] border border-[var(--border)] rounded-lg px-3 py-2.5 text-sm text-[var(--text)] placeholder:text-[var(--text-muted)] outline-none focus:border-[var(--primary)]" />
                <input value={form.value} onChange={e => setForm(f => ({ ...f, value: e.target.value }))} placeholder="Value" type="password"
                  className="w-full bg-[var(--bg)] border border-[var(--border)] rounded-lg px-3 py-2.5 text-sm text-[var(--text)] placeholder:text-[var(--text-muted)] outline-none focus:border-[var(--primary)]" />
                <select value={form.environment} onChange={e => setForm(f => ({ ...f, environment: e.target.value }))}
                  className="w-full bg-[var(--bg)] border border-[var(--border)] rounded-lg px-3 py-2.5 text-sm text-[var(--text)] outline-none focus:border-[var(--primary)]">
                  <option value="Production">Production</option>
                  <option value="Staging">Staging</option>
                  <option value="Development">Development</option>
                </select>
              </div>
              <div className="flex gap-3 mt-5">
                <button onClick={() => setShowCreate(false)} className="flex-1 h-10 rounded-lg text-sm border border-[var(--border)] text-[var(--text-muted)]">Cancel</button>
                <button onClick={handleCreate} className="flex-1 h-10 rounded-lg text-sm font-semibold text-black" style={{ background: 'var(--primary)' }}>Save</button>
              </div>
            </div>
          </div>
        )}
      </div>
    </AppShell>
  );
}
