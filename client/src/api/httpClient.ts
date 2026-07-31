let accessToken: string | null = null

type UnauthorizedHandler = () => void

let unauthorizedHandler: UnauthorizedHandler | null = null

export class ApiError extends Error {
  readonly status: number

  constructor(status: number, message: string) {
    super(message)
    this.name = 'ApiError'
    this.status = status
  }
}

export function setAccessToken(token: string | null): void {
  accessToken = token?.trim() || null
}

export function clearAccessToken(): void {
  accessToken = null
}

export function setUnauthorizedHandler(
  handler: UnauthorizedHandler | null,
): void {
  unauthorizedHandler = handler
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

  const response = await fetch(input, {
    ...init,
    headers,
  })

  if (response.status === 401 && accessToken) {
    clearAccessToken()
    unauthorizedHandler?.()
  }

  return response
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
    throw new ApiError(
      response.status,
      getApiErrorMessage(
        responseBody,
        response.status,
      ),
    )
  }

  if (!responseBody) {
    throw new ApiError(
      response.status,
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
    if (status === 401) {
      return 'Your session is no longer valid.'
    }

    if (status === 403) {
      return 'You do not have permission to perform this action.'
    }

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