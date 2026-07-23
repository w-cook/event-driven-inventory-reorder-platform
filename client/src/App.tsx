import { useEffect, useMemo, useState } from 'react'
import './App.css'

import { listInventoryItems } from './api/inventoryItems'
import { InventorySummaryCards } from './components/InventorySummaryCards'
import { InventoryTable } from './components/InventoryTable'
import { isLowStock, type InventoryItem } from './types/inventoryItem'
import { listReorderEvents } from './api/reorderEvents'
import { ReorderWorkflowPanel } from './components/ReorderWorkflowPanel'
import type { ReorderEvent } from './types/reorderEvent'
import { WorkflowSummaryCards } from './components/WorkflowSummaryCards'

function App() {
  const [items, setItems] = useState<InventoryItem[]>([])
  const [reorderEvents, setReorderEvents] = useState<ReorderEvent[]>([])
  const [showLowStockOnly, setShowLowStockOnly] = useState(false)
  const [isLoading, setIsLoading] = useState(false)
  const [errorMessage, setErrorMessage] = useState('')

  const visibleItems = useMemo(() => {
    if (!showLowStockOnly) {
      return items
    }

    return items.filter(isLowStock)
  }, [items, showLowStockOnly])

  useEffect(() => {
    async function loadDashboardData() {
      setIsLoading(true)
      setErrorMessage('')

      try {
        const [loadedItems, loadedReorderEvents] = await Promise.all([
          listInventoryItems(),
          listReorderEvents(),
        ])

        setItems(loadedItems)
        setReorderEvents(loadedReorderEvents)
      } catch (error) {
        setErrorMessage(
          error instanceof Error
            ? error.message
            : 'Unable to load dashboard data.',
        )
      } finally {
        setIsLoading(false)
      }
    }

    loadDashboardData()
  }, [])

  return (
    <main className="page">
      <header className="hero">
        <h1>Inventory Operations Dashboard</h1>
        <p>
          Operator-facing dashboard for inventory visibility, low-stock review,
          reorder workflow status, processing history, and system health.
        </p>
      </header>

      {errorMessage && <p className="error">{errorMessage}</p>}
      {isLoading && <p>Loading dashboard...</p>}

      <InventorySummaryCards items={items} />

      <WorkflowSummaryCards events={reorderEvents} />

      <section className="toolbar">
        <label>
          <input
            type="checkbox"
            checked={showLowStockOnly}
            onChange={(event) => setShowLowStockOnly(event.target.checked)}
          />
          Show low-stock items only
        </label>
      </section>

      <InventoryTable items={visibleItems} />

      <section className="grid">
        <ReorderWorkflowPanel
          events={reorderEvents}
          inventoryItems={items}
        />

        <article className="card">
          <h2>System Health</h2>
          <p>API, processor, queue, and database health will be summarized here.</p>
        </article>
      </section>
    </main>
  )
}

export default App