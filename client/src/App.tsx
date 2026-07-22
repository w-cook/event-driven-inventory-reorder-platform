import { useEffect, useMemo, useState } from 'react'
import './App.css'

import { listInventoryItems } from './api/inventoryItems'
import { InventorySummaryCards } from './components/InventorySummaryCards'
import { InventoryTable } from './components/InventoryTable'
import { isLowStock, type InventoryItem } from './types/inventoryItem'

function App() {
  const [items, setItems] = useState<InventoryItem[]>([])
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
    async function loadItems() {
      setIsLoading(true)
      setErrorMessage('')

      try {
        const loadedItems = await listInventoryItems()
        setItems(loadedItems)
      } catch (error) {
        setErrorMessage(
          error instanceof Error
            ? error.message
            : 'Unable to load inventory items.',
        )
      } finally {
        setIsLoading(false)
      }
    }

    loadItems()
  }, [])

  return (
    <main className="page">
      <header className="hero">
        <p className="eyebrow">Project 10 Expansion</p>
        <h1>Inventory Operations Dashboard</h1>
        <p>
          Operator-facing dashboard for inventory visibility, low-stock review,
          reorder workflow status, processing history, and system health.
        </p>
      </header>

      {errorMessage && <p className="error">{errorMessage}</p>}
      {isLoading && <p>Loading inventory...</p>}

      <InventorySummaryCards items={items} />

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
        <article className="card">
          <h2>Reorder Workflow</h2>
          <p>Reorder status and processing history will be displayed here.</p>
        </article>

        <article className="card">
          <h2>System Health</h2>
          <p>API, processor, queue, and database health will be summarized here.</p>
        </article>
      </section>
    </main>
  )
}

export default App