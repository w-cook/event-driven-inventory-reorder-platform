import type { InventoryItem } from '../types/inventoryItem'
import type { ReorderEvent } from '../types/reorderEvent'

interface Props {
  events: ReorderEvent[]
  inventoryItems: InventoryItem[]
  isRefreshing: boolean
  refreshErrorMessage: string
  onRefresh: () => void
}

function statusClass(status: string): string {
  const normalizedStatus = status.toLowerCase()

  if (
    normalizedStatus.includes('reject') ||
    normalizedStatus.includes('fail') ||
    normalizedStatus.includes('error')
  ) {
    return 'badge error-badge'
  }

  if (
    normalizedStatus.includes('accept') ||
    normalizedStatus.includes('process')
  ) {
    return 'badge ok'
  }

  if (normalizedStatus.includes('pending')) {
    return 'badge warning'
  }

  return 'badge neutral'
}

function formatStatus(status: string): string {
  switch (status) {
    case 'SupplierAccepted':
      return 'Supplier Accepted'
    case 'SupplierRejected':
      return 'Supplier Rejected'
    case 'Processed':
      return 'Processed'
    case 'Pending':
      return 'Pending'
    default:
      return status
  }
}

function formatDate(value: string): string {
  return new Date(value).toLocaleString()
}

function SupplierResult({ event }: { event: ReorderEvent }) {
  if (event.status === 'SupplierAccepted') {
    return (
      <div className="supplier-result">
        {event.supplierOrderId && (
          <p>
            <strong>Order:</strong>{' '}
            <code
              className="supplier-order-id"
              title={event.supplierOrderId}
            >
              {event.supplierOrderId}
            </code>
          </p>
        )}

        {event.supplierOrderStatus && (
          <p>
            <strong>Supplier status:</strong>{' '}
            {event.supplierOrderStatus}
          </p>
        )}

        {event.supplierAcceptedAtUtc && (
          <p>
            <strong>Accepted:</strong>{' '}
            {formatDate(event.supplierAcceptedAtUtc)}
          </p>
        )}
      </div>
    )
  }

  if (event.status === 'SupplierRejected') {
    return (
      <div className="supplier-result">
        <p className="supplier-rejection">
          {event.supplierRejectionReason ??
            'The supplier permanently rejected this order.'}
        </p>
      </div>
    )
  }

  if (event.status === 'Pending') {
    return (
      <span className="muted">
        Awaiting supplier response
      </span>
    )
  }

  if (event.status === 'Processed') {
    return (
      <span className="muted">
        Completed before supplier tracking
      </span>
    )
  }

  return <span className="muted">—</span>
}

export function ReorderWorkflowPanel({
  events,
  inventoryItems,
  isRefreshing,
  refreshErrorMessage,
  onRefresh,
}: Props) {
  const itemsById = new Map(
    inventoryItems.map((item) => [item.id, item]),
  )

  return (
    <section className="card reorder-workflow">
      <div className="section-header">
        <div>
          <h2>Workflow History</h2>
        </div>

        <button
          type="button"
          className="secondary-button"
          disabled={isRefreshing}
          onClick={onRefresh}
        >
          {isRefreshing
            ? 'Refreshing...'
            : 'Refresh workflow'}
        </button>
      </div>

      <p className="muted">
        Requested quantity is captured when the reorder
        workflow begins. Supplier Accepted means the
        external supplier accepted the order; it does not
        mean replacement stock has been received.
      </p>

      {refreshErrorMessage && (
        <p className="error" role="alert">
          {refreshErrorMessage}
        </p>
      )}

      {isRefreshing && (
        <p className="loading-message">
          Refreshing workflow history...
        </p>
      )}

      {!isRefreshing &&
        !refreshErrorMessage &&
        events.length === 0 && (
          <p>No reorder workflow events found.</p>
        )}

      {!isRefreshing &&
        !refreshErrorMessage &&
        events.length > 0 && (
          <div className="table-wrapper">
            <table className="workflow-table">
              <thead>
                <tr>
                  <th>Item</th>
                  <th>SKU</th>
                  <th>Status</th>
                  <th>Quantity at Trigger</th>
                  <th>Requested Quantity</th>
                  <th>Supplier Result</th>
                  <th>Triggered</th>
                </tr>
              </thead>

              <tbody>
                {events.map((event) => {
                  const item =
                    itemsById.get(
                      event.inventoryItemId,
                    )

                  return (
                    <tr key={event.id}>
                      <td>
                        {item?.name ??
                          `Item ${event.inventoryItemId}`}
                      </td>

                      <td>{item?.sku ?? '—'}</td>

                      <td className="workflow-status-cell">
                        <span
                          className={statusClass(
                            event.status,
                          )}
                        >
                          {formatStatus(event.status)}
                        </span>
                      </td>

                      <td>
                        {event.quantityAtTrigger}
                      </td>

                      <td>
                        {event.requestedQuantity}
                      </td>

                      <td>
                        <SupplierResult event={event} />
                      </td>

                      <td>
                        {formatDate(event.triggeredAt)}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}
    </section>
  )
}