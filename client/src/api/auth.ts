import { apiFetch, handleJsonResponse } from './httpClient'
import type { LoginResponse } from '../types/auth'

export async function login(
  email: string,
  password: string,
): Promise<LoginResponse> {
  const response = await apiFetch('/api/auth/login', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({
      email,
      password,
    }),
  })

  return handleJsonResponse<LoginResponse>(response)
}