const DEMO_USER = 'operator'

export const DEMO_ROLE_LABEL = 'Operator'

export async function apiFetch(
  input: RequestInfo | URL,
  init: RequestInit = {},
): Promise<Response> {
  const headers = new Headers(init.headers)

  headers.set('X-Demo-User', DEMO_USER)

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