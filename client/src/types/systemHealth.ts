export interface SystemHealth {
  status: string
  databaseStatus: string
  inventoryItemCount: number | null
  reorderEventCount: number | null
  checkedAt: string
}