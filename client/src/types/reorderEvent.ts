import type { InventoryItem } from './inventoryItem'

export interface ReorderEvent {
  id: number
  inventoryItemId: number
  inventoryItem: InventoryItem | null
  quantityAtTrigger: number
  triggeredAt: string
  status: string
}