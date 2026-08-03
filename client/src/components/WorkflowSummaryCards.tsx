import type { ReorderEvent } from '../types/reorderEvent'

interface Props {
  events: ReorderEvent[]
}

interface WorkflowCounts {
  pending: number
  supplierAccepted: number
  supplierRejected: number
}

export function WorkflowSummaryCards({ events }: Props) {
  const counts = events.reduce<WorkflowCounts>(
    (currentCounts, event) => {
      switch (event.status.toLowerCase()) {
        case 'pending':
          currentCounts.pending += 1
          break

        case 'supplieraccepted':
          currentCounts.supplierAccepted += 1
          break

        case 'supplierrejected':
          currentCounts.supplierRejected += 1
          break
      }

      return currentCounts
    },
    {
      pending: 0,
      supplierAccepted: 0,
      supplierRejected: 0,
    },
  )

  return (
    <section className="summary-grid">
      <article className="card stat-card">
        <span className="stat-label">
          Pending Reorder Events
        </span>

        <strong className="stat-value">
          {counts.pending}
        </strong>
      </article>

      <article className="card stat-card">
        <span className="stat-label">
          Supplier Accepted
        </span>

        <strong className="stat-value">
          {counts.supplierAccepted}
        </strong>
      </article>

      <article className="card stat-card">
        <span className="stat-label">
          Supplier Rejected
        </span>

        <strong className="stat-value">
          {counts.supplierRejected}
        </strong>
      </article>
    </section>
  )
}