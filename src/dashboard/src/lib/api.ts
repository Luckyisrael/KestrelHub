const API_BASE = typeof window !== 'undefined' ? '' : 'http://localhost:5001';

export async function api<T>(
  path: string,
  options: RequestInit = {}
): Promise<T> {
  const token = typeof window !== 'undefined' ? localStorage.getItem('kh_token') : null;

  const res = await fetch(`${API_BASE}${path}`, {
    ...options,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...options.headers,
    },
  });

  if (res.status === 401 && path !== '/api/auth/refresh' && path !== '/api/auth/login') {
    const refreshed = await refreshToken();
    if (refreshed) {
      return api<T>(path, options);
    }
    localStorage.removeItem('kh_token');
    window.location.href = '/login';
    throw new Error('Unauthorized');
  }

  if (!res.ok) {
    const error = await res.json().catch(() => ({ error: 'Request failed' }));
    throw new Error(error.error || error.title || `HTTP ${res.status}`);
  }

  if (res.status === 204) return undefined as T;
  return res.json();
}

async function refreshToken(): Promise<boolean> {
  try {
    const res = await fetch('/api/auth/refresh', {
      method: 'POST',
      credentials: 'include',
    });
    if (!res.ok) return false;
    const data = await res.json();
    if (data.accessToken) {
      localStorage.setItem('kh_token', data.accessToken);
      return true;
    }
    return false;
  } catch {
    return false;
  }
}

export async function login(email: string, password: string): Promise<boolean> {
  const res = await fetch('/api/auth/login', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  });
  if (!res.ok) return false;
  const data = await res.json();
  if (data.accessToken) {
    localStorage.setItem('kh_token', data.accessToken);
    return true;
  }
  return false;
}

export async function logout(): Promise<void> {
  try {
    await fetch('/api/auth/logout', { method: 'POST', credentials: 'include' });
  } catch {}
  localStorage.removeItem('kh_token');
}
