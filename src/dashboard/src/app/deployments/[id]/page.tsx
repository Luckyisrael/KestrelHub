'use client';

import { useEffect, useState, useRef, useCallback } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { ArrowLeft, Trash2 } from 'lucide-react';
import * as signalR from '@microsoft/signalr';
import AppShell from '@/components/AppShell';
import { api } from '@/lib/api';
import { cn, statusColors, formatTime } from '@/lib/utils';

interface LogEntry { timestamp: string; message: string; isError: boolean; }
interface Setting { id: string; key: string; value: string; }

export default function DeploymentDetailPage() {
  const { id } = useParams();
  const router = useRouter();
  const logRef = useRef<HTMLDivElement>(null);
  const [deployment, setDeployment] = useState<any>(null);
  const [logs, setLogs] = useState<LogEntry[]>([]);
  const [settings, setSettings] = useState<Setting[]>([]);
  const [currentStatus, setCurrentStatus] = useState('');
  const [tab, setTab] = useState<'logs' | 'settings' | 'secrets'>('logs');
  const [loading, setLoading] = useState(true);
  const [hubConnected, setHubConnected] = useState(false);

  const loadDeployment = useCallback(async () => {
    try {
      const data = await api<any>(`/api/deployments/${id}`);
      setDeployment(data);
      setCurrentStatus(data.status);
      setLogs(data.logs || []);
      const s = await api<Setting[]>(`/api/deployments/${id}/settings`);
      setSettings(s);
    } catch { }
    setLoading(false);
  }, [id]);

  useEffect(() => { loadDeployment(); }, [loadDeployment]);

  useEffect(() => {
    const token = localStorage.getItem('kh_token');
    if (!token) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/deployments', { accessTokenFactory: () => token })
      .withAutomaticReconnect()
      .build();

    connection.on('ReceiveLog', (depId: string, message: string, isError: boolean) => {
      if (depId === id) setLogs(prev => [...prev, { timestamp: new Date().toISOString(), message, isError }]);
    });

    connection.on('StatusChanged', (depId: string, newStatus: string) => {
      if (depId === id) setCurrentStatus(newStatus);
    });

    connection.start()
      .then(() => connection.invoke('JoinDeployment', id))
      .then(() => setHubConnected(true))
      .catch(() => setHubConnected(false));

    return () => { connection.stop().catch(() => {}); };
  }, [id]);

  useEffect(() => {
    logRef.current?.scrollTo(0, logRef.current.scrollHeight);
  }, [logs]);

  const addSetting = async () => {
    const key = prompt('Setting key:');
    if (!key) return;
    const value = prompt('Setting value:') || '';
    try {
      await api(`/api/deployments/${id}/settings`, { method: 'PUT', body: JSON.stringify({ key, value }) });
      const s = await api<Setting[]>(`/api/deployments/${id}/settings`);
      setSettings(s);
    } catch (e: any) { alert(e.message); }
  };

  const deleteSetting = async (settingId: string) => {
    try {
      await api(`/api/deployments/${id}/settings/${settingId}`, { method: 'DELETE' });
      setSettings(s => s.filter(x => x.id !== settingId));
    } catch (e: any) { alert(e.message); }
  };

  if (loading) return <AppShell><div className="flex justify-center py-20"><div className="w-8 h-8 border-2 border-[var(--primary)] border-t-transparent rounded-full animate-spin" /></div></AppShell>;
  if (!deployment) return <AppShell><div className="p-6 text-red-400">Deployment not found.</div></AppShell>;

  const sc = statusColors[currentStatus] || statusColors.Pending;

  return (
    <AppShell>
      <div className="p-6">
        <button onClick={() => router.push('/deployments')} className="flex items-center gap-1.5 text-sm text-[var(--text-muted)] hover:text-[var(--text)] mb-4">
          <ArrowLeft size={16} /> Back to deployments
        </button>

        <div className="flex items-center gap-3 mb-6">
          <h1 className="text-xl font-semibold">{deployment.name}</h1>
          <span className={cn('px-2.5 py-1 rounded-full text-xs font-medium', sc.bg, sc.text)}>{currentStatus}</span>
          {deployment.assignedDomain && <span className="text-sm" style={{ color: 'var(--secondary)' }}>{deployment.assignedDomain}</span>}
          {hubConnected && <span className="w-2 h-2 rounded-full bg-emerald-400" title="Live" />}
        </div>

        {/* Tabs */}
        <div className="flex gap-1 mb-4 border-b border-[var(--border)]">
          {(['logs', 'settings', 'secrets'] as const).map(t => (
            <button key={t} onClick={() => setTab(t)}
              className={cn('px-4 py-2.5 text-sm font-medium border-b-2 -mb-px capitalize',
                tab === t ? 'border-[var(--primary)] text-[var(--text)]' : 'border-transparent text-[var(--text-muted)] hover:text-[var(--text)]')}>
              {t}
            </button>
          ))}
        </div>

        {/* Logs Tab */}
        {tab === 'logs' && (
          <div ref={logRef} className="bg-[#0a0c0f] rounded-xl border border-[var(--border)] p-4 font-mono text-[13px] h-[500px] overflow-y-auto">
            {logs.length === 0 ? (
              <div className="text-center text-[var(--text-muted)] py-12">No logs yet.</div>
            ) : (
              logs.map((log, i) => (
                <div key={i} className="mb-1 leading-relaxed">
                  <span className="text-zinc-600">{formatTime(log.timestamp)}</span>{' '}
                  <span className={log.isError ? 'text-red-400' : 'text-[var(--text-muted)]'}>{log.message}</span>
                </div>
              ))
            )}
          </div>
        )}

        {/* Settings Tab */}
        {tab === 'settings' && (
          <div>
            <div className="flex justify-between items-center mb-3">
              <h3 className="text-sm font-medium text-[var(--text-muted)]">App Settings</h3>
              <button onClick={addSetting} className="px-3 py-1.5 rounded-lg text-xs font-semibold text-black" style={{ background: 'var(--primary)' }}>Add Setting</button>
            </div>
            {settings.length === 0 ? (
              <p className="text-sm text-[var(--text-muted)]">No settings configured.</p>
            ) : (
              <div className="space-y-2">
                {settings.map(s => (
                  <div key={s.id} className="flex items-center gap-3 bg-[#0a0c0f] rounded-lg px-4 py-3 border border-[var(--border)]">
                    <span className="text-sm font-mono" style={{ color: 'var(--primary)' }}>{s.key}</span>
                    <span className="text-sm text-[var(--text)] flex-1 font-mono">{s.value}</span>
                    <button onClick={() => deleteSetting(s.id)} className="text-red-400 hover:text-red-300"><Trash2 size={14} /></button>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {/* Secrets Tab */}
        {tab === 'secrets' && (
          <div className="text-sm text-[var(--text-muted)]">
            <p>Secrets for this deployment are managed in the <a href="/secrets" className="underline" style={{ color: 'var(--primary)' }}>Secret Vault</a>.</p>
          </div>
        )}
      </div>
    </AppShell>
  );
}
