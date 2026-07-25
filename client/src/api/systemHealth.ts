import type { SystemHealth } from '../types/systemHealth'
import { apiFetch, handleJsonResponse } from './httpClient'

export async function getSystemHealth(): Promise<SystemHealth> {
  const response = await apiFetch('/api/operations/health')

  return handleJsonResponse<SystemHealth>(response)
}