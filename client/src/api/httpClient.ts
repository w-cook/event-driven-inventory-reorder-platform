const configuredDemoUser = import.meta.env.VITE_DEMO_USER
  ?.trim()
  .toLowerCase()

const DEMO_USER = configuredDemoUser || 'operator'

const DEMO_ROLE_LABELS = {
  viewer: 'Viewer',
  operator: 'Operator',
  admin: 'Administrator',
} as const

export const DEMO_ROLE_LABEL =
  DEMO_ROLE_LABELS[DEMO_USER as keyof typeof DEMO_ROLE_LABELS] ??
  DEMO_USER

export async function apiFetch(
  input: RequestInfo | URL,
  init: RequestInit = {},
): Promise<Response> {
  const headers = new Headers(init.headers)

  if (!headers.has('X-Demo-User')) {
    headers.set('X-Demo-User', DEMO_USER)
  }

  return fetch(input, {
    ...init,
    headers,
  })
}

export async function handleJsonResponse<T>(
  response: Response,
): Promise<T> {
  if (!response.ok) {
    const responseBody = await response.text()

    throw new Error(
      responseBody || `Request failed with status ${response.status}`,
    )
  }

  return response.json() as Promise<T>
}