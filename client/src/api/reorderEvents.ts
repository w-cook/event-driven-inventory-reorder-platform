import type { ReorderEvent } from '../types/reorderEvent'

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    throw new Error(`Request failed with status ${response.status}`)
  }

  return response.json() as Promise<T>
}

export async function listReorderEvents(): Promise<ReorderEvent[]> {
  const response = await fetch('/api/reorderevents')

  return handleResponse<ReorderEvent[]>(response)
}