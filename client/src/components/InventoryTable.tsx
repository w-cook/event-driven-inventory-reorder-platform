import { isLowStock, type InventoryItem } from '../types/inventoryItem'

interface Props {
  items: InventoryItem[]
  canManageInventory: boolean
  onEdit: (item: InventoryItem) => void
}

export function InventoryTable({
  items,
  canManageInventory,
  onEdit,
}: Props) {
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
              {canManageInventory && <th>Actions</th>}
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
                  {canManageInventory && (
                    <td>
                      <button
                        type="button"
                        className="table-action-button"
                        onClick={() => onEdit(item)}
                      >
                        Edit
                      </button>
                    </td>
                  )}
                </tr>
              )
            })}
          </tbody>
        </table>
      </div>
    </section>
  )
}