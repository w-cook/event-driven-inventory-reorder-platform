import type { InventoryItem } from './inventoryItem'

export interface ReorderEvent {
  id: number
  inventoryItemId: number
  inventoryItem: InventoryItem | null
  quantityAtTrigger: number
  requestedQuantity: number
  triggeredAt: string
  status: string
  supplierOrderId: string | null
  supplierOrderStatus: string | null
  supplierAcceptedAtUtc: string | null
  supplierRejectionReason: string | null
}