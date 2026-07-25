import type { InventoryItem } from '../types/inventoryItem'
import { apiFetch, handleJsonResponse } from './httpClient'

export async function listInventoryItems(): Promise<InventoryItem[]> {
  const response = await apiFetch('/api/inventoryitems')

  return handleJsonResponse<InventoryItem[]>(response)
}