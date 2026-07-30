import { isLowStock, type InventoryItem } from '../types/inventoryItem'

interface Props {
  items: InventoryItem[]
}

export function InventoryTable({ items }: Props) {
  if (items.length === 0) {
    return (
      <section className="card">
        <h2>Inventory</h2>
        <p>No inventory items found.</p>
      </section>
    )
  }

  return (
    <section className="card">
      <h2>Inventory</h2>

      <div className="table-wrapper">
        <table>
          <thead>
            <tr>
              <th>SKU</th>
              <th>Name</th>
              <th>Quantity</th>
              <th>Reorder Threshold</th>
              <th>Reorder Quantity</th>
              <th>Status</th>
            </tr>
          </thead>

          <tbody>
            {items.map((item) => {
              const lowStock = isLowStock(item)
              const statusLabel =
                item.status === 'ReorderPending'
                  ? 'Reorder pending'
                  : item.status

              return (
                <tr key={item.id}>
                  <td>{item.sku}</td>
                  <td>{item.name}</td>
                  <td>{item.quantityOnHand}</td>
                  <td>{item.reorderThreshold}</td>
                  <td>{item.reorderQuantity}</td>
                  <td>
                    <span className={lowStock ? 'badge warning' : 'badge ok'}>
                      {statusLabel}
                    </span>
                  </td>
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
    </section>
  )
}