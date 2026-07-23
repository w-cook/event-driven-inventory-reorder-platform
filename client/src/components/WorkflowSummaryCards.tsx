import type { ReorderEvent } from '../types/reorderEvent'

interface Props {
  events: ReorderEvent[]
}

export function WorkflowSummaryCards({ events }: Props) {
  const pendingEvents = events.filter(
    (event) => event.status.toLowerCase() === 'pending',
  ).length

  const processedEvents = events.filter((event) => {
    const status = event.status.toLowerCase()

    return status === 'processed' || status === 'completed'
  }).length

  const failedEvents = events.filter((event) => {
    const status = event.status.toLowerCase()

    return status.includes('fail') || status.includes('error')
  }).length

  return (
    <section className="summary-grid">
      <article className="card stat-card">
        <span className="stat-label">Pending Reorder Events</span>
        <strong className="stat-value">{pendingEvents}</strong>
      </article>

      <article className="card stat-card">
        <span className="stat-label">Processed Reorder Events</span>
        <strong className="stat-value">{processedEvents}</strong>
      </article>

      <article className="card stat-card">
        <span className="stat-label">Failed Reorder Events</span>
        <strong className="stat-value">{failedEvents}</strong>
      </article>
    </section>
  )
}