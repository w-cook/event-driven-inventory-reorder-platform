let accessToken: string | null = null

export function setAccessToken(token: string | null): void {
  accessToken = token?.trim() || null
}

export function clearAccessToken(): void {
  accessToken = null
}

export async function apiFetch(
  input: RequestInfo | URL,
  init: RequestInit = {},
): Promise<Response> {
  const headers = new Headers(init.headers)

  if (accessToken && !headers.has('Authorization')) {
    headers.set(
      'Authorization',
      `Bearer ${accessToken}`,
    )
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
      responseBody ||
        `Request failed with status ${response.status}`,
    )
  }

  return response.json() as Promise<T>
}