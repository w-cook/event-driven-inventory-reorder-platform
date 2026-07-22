export interface InventoryItem {
  id: number
  name: string
  sku: string
  quantityOnHand: number
  reorderThreshold: number
  status: string
  createdAt: string
  updatedAt: string
}

export function isLowStock(item: InventoryItem): boolean {
  return item.quantityOnHand <= item.reorderThreshold
}