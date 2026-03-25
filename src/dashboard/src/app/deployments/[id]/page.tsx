'use client';

import { useEffect, useState, useRef } from 'react';
import { useParams, useRouter } from 'next/navigation';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
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
  const qc = useQueryClient();
  const logRef = useRef<HTMLDivElement>(null);
  const [liveLogs, setLiveLogs] = useState<LogEntry[]>([]);
  const [currentStatus, setCurrentStatus] = useState('');
  const [tab, setTab] = useState<'logs' | 'settings' | 'secrets'>('logs');
  const [hubConnected, setHubConnected] = useState(false);

  const { data: deployment, isLoading } = useQuery({
    queryKey: ['deployment', id],
    queryFn: () => api<any>(`/api/deployments/${id}`),
  });

  const { data: settings = [] } = useQuery({
    queryKey: ['deployment-settings', id],
    queryFn: () => api<Setting[]>(`/api/deployments/${id}/settings`),
  });

  const addSettingMut = useMutation({
    mutationFn: (data: { key: string; value: string }) =>
      api(`/api/deployments/${id}/settings`, { method: 'PUT', body: JSON.stringify(data) }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['deployment-settings', id] }),
  });

  const deleteSettingMut = useMutation({
    mutationFn: (settingId: string) =>
      api(`/api/deployments/${id}/settings/${settingId}`, { method: 'DELETE' }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ['deployment-settings', id] }),
  });

  useEffect(() => {
    if (deployment?.status) setCurrentStatus(deployment.status);
  }, [deployment]);

  useEffect(() => {
    const token = localStorage.getItem('kh_token');
    if (!token) return;

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('http://localhost:5001/hubs/deployments', { accessTokenFactory: () => token })
      .withAutomaticReconnect()
      .build();

    connection.on('ReceiveLog', (depId: string, message: string, isError: boolean) => {
      if (depId === id) setLiveLogs(prev => [...prev, { timestamp: new Date().toISOString(), message, isError }]);
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
  }, [liveLogs]);

  const allLogs = [...(deployment?.logs || []), ...liveLogs];
  const sc = statusColors[currentStatus] || statusColors.Pending;

  if (isLoading) return <AppShell><div className="flex justify-center py-20"><div className="w-8 h-8 border-2 border-[var(--primary)] border-t-transparent rounded-full animate-spin" /></div></AppShell>;
  if (!deployment) return <AppShell><div className="p-6 text-red-400">Deployment not found.</div></AppShell>;

  const handleAddSetting = () => {
    const key = prompt('Setting key:');
    if (!key) return;
    const value = prompt('Setting value:') || '';
    addSettingMut.mutate({ key, value });
  };

  return (
    <AppShell>
      <div className="p-6">
        <button onClick={() => router.push('/deployments')} className="flex items-center gap-1.5 text-sm text-[var(--text-muted)] hover:text-[var(--text)] mb-4">
          <ArrowLeft size={16} /> Back
        </button>

        <div className="flex items-center gap-3 mb-6">
          <h1 className="text-xl font-semibold">{deployment.name}</h1>
          <span className={cn('px-2.5 py-1 rounded-full text-xs font-medium', sc.bg, sc.text)}>{currentStatus}</span>
          {deployment.assignedDomain && <span className="text-sm" style={{ color: 'var(--secondary)' }}>{deployment.assignedDomain}</span>}
          {hubConnected && <span className="w-2 h-2 rounded-full bg-emerald-400" title="Live" />}
        </div>

        <div className="flex gap-1 mb-4 border-b border-[var(--border)]">
          {(['logs', 'settings', 'secrets'] as const).map(t => (
            <button key={t} onClick={() => setTab(t)}
              className={cn('px-4 py-2.5 text-sm font-medium border-b-2 -mb-px capitalize',
                tab === t ? 'border-[var(--primary)] text-[var(--text)]' : 'border-transparent text-[var(--text-muted)] hover:text-[var(--text)]')}>
              {t}
            </button>
          ))}
        </div>

        {tab === 'logs' && (
          <div ref={logRef} className="bg-[#0a0c0f] rounded-xl border border-[var(--border)] p-4 font-mono text-[13px] h-[500px] overflow-y-auto">
            {allLogs.length === 0 ? (
              <div className="text-center text-[var(--text-muted)] py-12">No logs yet.</div>
            ) : allLogs.map((log, i) => (
              <div key={i} className="mb-1 leading-relaxed">
                <span className="text-zinc-600">{formatTime(log.timestamp)}</span>{' '}
                <span className={log.isError ? 'text-red-400' : 'text-[var(--text-muted)]'}>{log.message}</span>
              </div>
            ))}
          </div>
        )}

        {tab === 'settings' && (
          <div>
            <div className="flex justify-between items-center mb-3">
              <h3 className="text-sm font-medium text-[var(--text-muted)]">App Settings</h3>
              <button onClick={handleAddSetting} className="px-3 py-1.5 rounded-lg text-xs font-semibold text-black" style={{ background: 'var(--primary)' }}>Add Setting</button>
            </div>
            {settings.length === 0 ? <p className="text-sm text-[var(--text-muted)]">No settings configured.</p> : (
              <div className="space-y-2">
                {settings.map(s => (
                  <div key={s.id} className="flex items-center gap-3 bg-[#0a0c0f] rounded-lg px-4 py-3 border border-[var(--border)]">
                    <span className="text-sm font-mono" style={{ color: 'var(--primary)' }}>{s.key}</span>
                    <span className="text-sm text-[var(--text)] flex-1 font-mono">{s.value}</span>
                    <button onClick={() => deleteSettingMut.mutate(s.id)} className="text-red-400 hover:text-red-300"><Trash2 size={14} /></button>
                  </div>
                ))}
              </div>
            )}
          </div>
        )}

        {tab === 'secrets' && (
          <div className="text-sm text-[var(--text-muted)]">
            <p>Secrets are managed in the <a href="/secrets" className="underline" style={{ color: 'var(--primary)' }}>Secret Vault</a>.</p>
          </div>
        )}
      </div>
    </AppShell>
  );
}
