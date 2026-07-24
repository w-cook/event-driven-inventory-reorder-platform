import type { SystemHealth } from '../types/systemHealth'

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    throw new Error(`Request failed with status ${response.status}`)
  }

  return response.json() as Promise<T>
}

export async function getSystemHealth(): Promise<SystemHealth> {
  const response = await fetch('/api/operations/health')

  return handleResponse<SystemHealth>(response)
}