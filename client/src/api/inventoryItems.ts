import type { InventoryItem } from '../types/inventoryItem'

async function handleResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    throw new Error(`Request failed with status ${response.status}`)
  }

  return response.json() as Promise<T>
}

export async function listInventoryItems(): Promise<InventoryItem[]> {
  const response = await fetch('/api/inventoryitems')

  return handleResponse<InventoryItem[]>(response)
}