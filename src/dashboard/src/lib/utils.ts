import { clsx, type ClassValue } from 'clsx';
import { twMerge } from 'tailwind-merge';

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs));
}

export function formatDate(date: string | Date) {
  return new Date(date).toLocaleString('en-US', {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

export function formatTime(date: string | Date) {
  return new Date(date).toLocaleTimeString('en-US', {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  });
}

export const statusColors: Record<string, { bg: string; text: string }> = {
  Pending: { bg: 'bg-zinc-700/50', text: 'text-zinc-400' },
  Building: { bg: 'bg-amber-500/15', text: 'text-amber-400' },
  Running: { bg: 'bg-emerald-500/15', text: 'text-emerald-400' },
  Failed: { bg: 'bg-red-500/15', text: 'text-red-400' },
  Stopped: { bg: 'bg-zinc-700/50', text: 'text-zinc-400' },
};
