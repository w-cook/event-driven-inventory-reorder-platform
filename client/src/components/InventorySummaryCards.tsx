import { isLowStock, type InventoryItem } from '../types/inventoryItem'

interface Props {
  items: InventoryItem[]
}

export function InventorySummaryCards({ items }: Props) {
  const totalItems = items.length
  const lowStockItems = items.filter(isLowStock).length
  const totalQuantity = items.reduce(
    (sum, item) => sum + item.quantityOnHand,
    0,
  )

  return (
    <section className="summary-grid">
      <article className="card stat-card">
        <span className="stat-label">Inventory Items</span>
        <strong className="stat-value">{totalItems}</strong>
      </article>

      <article className="card stat-card">
        <span className="stat-label">Low Stock</span>
        <strong className="stat-value">{lowStockItems}</strong>
      </article>

      <article className="card stat-card">
        <span className="stat-label">Total Quantity</span>
        <strong className="stat-value">{totalQuantity}</strong>
      </article>
    </section>
  )
}