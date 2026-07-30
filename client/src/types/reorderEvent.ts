import type { InventoryItem } from './inventoryItem'

export interface ReorderEvent {
  id: number
  inventoryItemId: number
  inventoryItem: InventoryItem | null
  quantityAtTrigger: number
  requestedQuantity: number
  triggeredAt: string
  status: string
}