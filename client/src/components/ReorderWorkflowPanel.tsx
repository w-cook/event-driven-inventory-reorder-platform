import type { InventoryItem } from '../types/inventoryItem'
import type { ReorderEvent } from '../types/reorderEvent'

interface Props {
  events: ReorderEvent[]
  inventoryItems: InventoryItem[]
}

function statusClass(status: string): string {
  const normalizedStatus = status.toLowerCase()

  if (normalizedStatus.includes('fail') || normalizedStatus.includes('error')) {
    return 'badge error-badge'
  }

  if (normalizedStatus.includes('process')) {
    return 'badge ok'
  }

  return 'badge neutral'
}

function formatStatus(status: string): string {
  if (status === 'Processed') {
    return 'Processed'
  }

  if (status === 'Pending') {
    return 'Pending'
  }

  return status
}

export function ReorderWorkflowPanel({
  events,
  inventoryItems,
}: Props) {
  const itemsById = new Map(
    inventoryItems.map((item) => [item.id, item]),
  )

  if (events.length === 0) {
    return (
      <section className="card">
        <h2>Workflow History</h2>
        <p>No reorder workflow events found.</p>
      </section>
    )
  }

  return (
    <section className="card">
      <h2>Workflow History</h2>

      <p className="muted">
        Requested quantity is captured when the reorder workflow begins. A processed
        event means the request was handled; it does not mean replacement stock has
        been received.
      </p>

      <div className="table-wrapper">
        <table>
          <thead>
            <tr>
              <th>Item</th>
              <th>SKU</th>
              <th>Status</th>
              <th>Quantity at Trigger</th>
              <th>Requested Quantity</th>
              <th>Triggered</th>
            </tr>
          </thead>

          <tbody>
            {events.map((event) => {
              const item = itemsById.get(event.inventoryItemId)

              return (
                <tr key={event.id}>
                  <td>
                    {item?.name ?? `Item ${event.inventoryItemId}`}
                  </td>
                  <td>{item?.sku ?? '—'}</td>
                  <td>
                    <span className={statusClass(event.status)}>
                      {formatStatus(event.status)}
                    </span>
                  </td>
                  <td>{event.quantityAtTrigger}</td>
                  <td>{event.requestedQuantity}</td>
                  <td>
                    {new Date(event.triggeredAt).toLocaleString()}
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