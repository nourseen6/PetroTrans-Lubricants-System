export type AuthUser = {
  id: string
  userName: string
  displayName: string
  locale: string
  theme: string
  roles: string[]
  permissions: string[]
}

export type AuthStatus = {
  needsSetup: boolean
  authenticated: boolean
  user: AuthUser | null
}

export type AccountSetupInput = {
  displayName: string
  userName: string
  password: string
  confirmPassword: string
}

export async function api<T>(path: string, init?: RequestInit): Promise<T> {
  const headers = new Headers(init?.headers)
  if (init?.body && !headers.has('Content-Type')) {
    headers.set('Content-Type', 'application/json')
  }

  let response: Response
  try {
    response = await fetch(path, {
      credentials: 'include',
      ...init,
      headers,
    })
  } catch {
    throw new Error('حدث خطأ أثناء تحميل البيانات. حاول مرة أخرى.')
  }

  const text = await response.text()
  let parsed: unknown = null
  if (text.trim()) {
    try {
      parsed = JSON.parse(text) as unknown
    } catch {
      if (response.ok) {
        return text as T
      }
      throw new Error('حدث خطأ أثناء تحميل البيانات. حاول مرة أخرى.')
    }
  }

  if (!response.ok) {
    const body = parsed as { error?: string } | null
    if (body && typeof body.error === 'string' && body.error.trim()) {
      throw new Error(body.error)
    }
    if (response.status === 401) {
      throw new Error('يجب تسجيل الدخول.')
    }
    if (response.status === 403) {
      throw new Error('غير مسموح بهذه العملية.')
    }
    if (response.status === 400) {
      throw new Error('من فضلك أدخل البيانات المطلوبة.')
    }
    throw new Error('حدث خطأ أثناء تحميل البيانات. حاول مرة أخرى.')
  }

  return parsed as T
}

export function fetchAuthStatus(signal?: AbortSignal): Promise<AuthStatus> {
  return api<AuthStatus>('/api/auth/status', { signal })
}

export function setupAccounts(owner: AccountSetupInput, operator: AccountSetupInput): Promise<AuthUser> {
  return api<AuthUser>('/api/auth/setup', {
    method: 'POST',
    body: JSON.stringify({ owner, operator }),
  })
}

export function login(userName: string, password: string): Promise<AuthUser> {
  return api<AuthUser>('/api/auth/login', {
    method: 'POST',
    body: JSON.stringify({ userName, password }),
  })
}

export function logout(): Promise<{ ok: boolean }> {
  return api<{ ok: boolean }>('/api/auth/logout', { method: 'POST' })
}

export function fetchHealth(signal?: AbortSignal): Promise<{ status: string; utc: string }> {
  return api('/api/health', { signal })
}
