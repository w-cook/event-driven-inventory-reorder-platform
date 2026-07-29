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

interface ApiProblemDetails {
  title?: string
  detail?: string
  errors?: Record<string, string[]>
}

export async function handleJsonResponse<T>(
  response: Response,
): Promise<T> {
  const responseBody = await response.text()

  if (!response.ok) {
    throw new Error(
      getApiErrorMessage(
        responseBody,
        response.status,
      ),
    )
  }

  if (!responseBody) {
    throw new Error(
      'The server returned an empty response.',
    )
  }

  return JSON.parse(responseBody) as T
}

function getApiErrorMessage(
  responseBody: string,
  status: number,
): string {
  if (!responseBody) {
    return `Request failed with status ${status}.`
  }

  try {
    const problem =
      JSON.parse(responseBody) as ApiProblemDetails

    const validationMessages =
      Object.values(problem.errors ?? {}).flat()

    if (validationMessages.length > 0) {
      return validationMessages.join(' ')
    }

    return (
      problem.detail ??
      problem.title ??
      `Request failed with status ${status}.`
    )
  } catch {
    return responseBody
  }
}