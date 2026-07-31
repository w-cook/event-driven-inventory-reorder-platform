export interface InventoryItem {
  id: number
  name: string
  sku: string
  quantityOnHand: number
  reorderThreshold: number
  reorderQuantity: number
  status: string
  createdAt: string
  updatedAt: string
}

export interface InventoryItemMutationRequest {
  name: string
  sku: string
  quantityOnHand: number
  reorderThreshold: number
  reorderQuantity: number
}

export function isLowStock(item: InventoryItem): boolean {
  return item.quantityOnHand <= item.reorderThreshold
}