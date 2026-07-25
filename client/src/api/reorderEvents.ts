import type { ReorderEvent } from '../types/reorderEvent'
import { apiFetch, handleJsonResponse } from './httpClient'

export async function listReorderEvents(): Promise<ReorderEvent[]> {
  const response = await apiFetch('/api/reorderevents')

  return handleJsonResponse<ReorderEvent[]>(response)
}