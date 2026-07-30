import {
  type InventoryItem,
  type InventoryItemMutationRequest,
} from '../types/inventoryItem'
import { apiFetch, handleJsonResponse } from './httpClient'

export async function listInventoryItems(): Promise<InventoryItem[]> {
  const response = await apiFetch('/api/inventoryitems')

  return handleJsonResponse<InventoryItem[]>(response)
}

export async function createInventoryItem(
  request: InventoryItemMutationRequest,
): Promise<InventoryItem> {
  const response = await apiFetch('/api/inventoryitems', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(request),
  })

  return handleJsonResponse<InventoryItem>(response)
}

export async function updateInventoryItem(
  id: number,
  request: InventoryItemMutationRequest,
): Promise<InventoryItem> {
  const response = await apiFetch(
    `/api/inventoryitems/${id}`,
    {
      method: 'PUT',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(request),
    },
  )

  return handleJsonResponse<InventoryItem>(response)
}