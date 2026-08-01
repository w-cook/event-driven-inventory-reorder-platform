import { isLowStock, type InventoryItem } from '../types/inventoryItem'

interface Props {
  items: InventoryItem[]
  canManageInventory: boolean
  showLowStockOnly: boolean
  onShowLowStockOnlyChange: (
    showLowStockOnly: boolean,
  ) => void
  onEdit: (item: InventoryItem) => void
}

export function InventoryTable({
  items,
  canManageInventory,
  showLowStockOnly,
  onShowLowStockOnlyChange,
  onEdit,
}: Props) {
  const header = (
    <div className="section-header inventory-table-header">
      <h3>Inventory Items</h3>

      <label className="filter-control">
        <input
          type="checkbox"
          checked={showLowStockOnly}
          onChange={event =>
            onShowLowStockOnlyChange(
              event.target.checked,
            )
          }
        />

        Show low-stock items only
      </label>
    </div>
  )

  if (items.length === 0) {
    return (
      <section className="card">
        {header}

        <p className="muted">
          {showLowStockOnly
            ? 'No low-stock inventory items found.'
            : 'No inventory items found.'}
        </p>
      </section>
    )
  }

  return (
    <section className="card">
      {header}

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