import type { SystemHealth } from '../types/systemHealth'

interface Props {
  health: SystemHealth | null
  isLoading: boolean
  errorMessage: string
  onRefresh: () => void
}

function statusClass(status: string | undefined): string {
  const normalized = status?.toLowerCase() ?? ''

  if (
    normalized.includes('unhealthy') ||
    normalized.includes('unavailable') ||
    normalized.includes('error')
  ) {
    return 'badge error-badge'
  }

  if (normalized.includes('degraded')) {
    return 'badge warning'
  }

  if (
    normalized.includes('healthy') ||
    normalized.includes('connected')
  ) {
    return 'badge ok'
  }

  return 'badge neutral'
}

export function SystemHealthPanel({
  health,
  isLoading,
  errorMessage,
  onRefresh,
}: Props) {
  return (
    <section className="card">
      <div className="section-header">
        <h2>System Health</h2>

        <button
          type="button"
          onClick={onRefresh}
          disabled={isLoading}
        >
          {isLoading ? 'Checking...' : 'Refresh'}
        </button>
      </div>

      <p className="muted health-description">
        Read-only operational status for the inventory platform.
      </p>

      {errorMessage && <p className="error">{errorMessage}</p>}

      {isLoading && !health && <p>Checking system health...</p>}

      {!isLoading && !errorMessage && !health && (
        <p>No system health data loaded.</p>
      )}

      {health && (
        <div className="health-grid">
          <div>
            <span className="stat-label">API Status</span>
            <p>
              <span className={statusClass(health.status)}>
                {health.status}
              </span>
            </p>
          </div>

          <div>
            <span className="stat-label">Database</span>
            <p>
              <span className={statusClass(health.databaseStatus)}>
                {health.databaseStatus}
              </span>
            </p>
          </div>

          <div>
            <span className="stat-label">Inventory Items</span>
            <strong className="stat-value">
              {health.inventoryItemCount ?? '—'}
            </strong>
          </div>

          <div>
            <span className="stat-label">Reorder Events</span>
            <strong className="stat-value">
              {health.reorderEventCount ?? '—'}
            </strong>
          </div>

          <div className="health-last-checked">
            <span className="stat-label">Last Checked</span>
            <p>
              {health.checkedAt
                ? new Date(health.checkedAt).toLocaleString()
                : '—'}
            </p>
          </div>
        </div>
      )}
    </section>
  )
}