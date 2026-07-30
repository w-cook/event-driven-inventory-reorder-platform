import { useEffect, useMemo, useState } from 'react'
import './App.css'

import {
  clearAccessToken,
  setAccessToken,
  setUnauthorizedHandler,
} from './api/httpClient'
import { listInventoryItems } from './api/inventoryItems'
import { listReorderEvents } from './api/reorderEvents'
import { getSystemHealth } from './api/systemHealth'
import { AccountManagementPanel } from './components/AccountManagementPanel'
import { InventorySummaryCards } from './components/InventorySummaryCards'
import { InventoryTable } from './components/InventoryTable'
import { LoginForm } from './components/LoginForm'
import { ReorderWorkflowPanel } from './components/ReorderWorkflowPanel'
import { SystemHealthPanel } from './components/SystemHealthPanel'
import { WorkflowSummaryCards } from './components/WorkflowSummaryCards'
import { isLowStock, type InventoryItem } from './types/inventoryItem'
import type { LoginResponse } from './types/auth'
import type { ReorderEvent } from './types/reorderEvent'
import type { SystemHealth } from './types/systemHealth'

function App() {
  const [session, setSession] = useState<LoginResponse | null>(null)
  const [sessionNotice, setSessionNotice] = useState('')

  const [items, setItems] = useState<InventoryItem[]>([])
  const [reorderEvents, setReorderEvents] = useState<ReorderEvent[]>([])
  const [showLowStockOnly, setShowLowStockOnly] = useState(false)
  const [isLoading, setIsLoading] = useState(false)
  const [errorMessage, setErrorMessage] = useState('')

  const [systemHealth, setSystemHealth] = useState<SystemHealth | null>(null)
  const [isHealthLoading, setIsHealthLoading] = useState(false)
  const [healthErrorMessage, setHealthErrorMessage] = useState('')

  const visibleItems = useMemo(() => {
    if (!showLowStockOnly) {
      return items
    }

    return items.filter(isLowStock)
  }, [items, showLowStockOnly])

  useEffect(() => {
    setUnauthorizedHandler(() => {
      setSessionNotice(
        'Your session expired or is no longer authorized. Please sign in again.',
      )

      setSession(null)
      setItems([])
      setReorderEvents([])
      setSystemHealth(null)

      setIsLoading(false)
      setIsHealthLoading(false)

      setErrorMessage('')
      setHealthErrorMessage('')
    })

    return () => {
      setUnauthorizedHandler(null)
    }
  }, [])

  useEffect(() => {
    if (!session) {
      return
    }

    let cancelled = false

    void Promise.all([
      listInventoryItems(),
      listReorderEvents(),
    ])
      .then(([loadedItems, loadedReorderEvents]) => {
        if (cancelled) {
          return
        }

        setItems(loadedItems)
        setReorderEvents(loadedReorderEvents)
      })
      .catch(error => {
        if (cancelled) {
          return
        }

        setErrorMessage(
          error instanceof Error
            ? error.message
            : 'Unable to load dashboard data.',
        )
      })
      .finally(() => {
        if (!cancelled) {
          setIsLoading(false)
        }
      })

    void getSystemHealth()
      .then(health => {
        if (!cancelled) {
          setSystemHealth(health)
        }
      })
      .catch(error => {
        if (cancelled) {
          return
        }

        setHealthErrorMessage(
          error instanceof Error
            ? error.message
            : 'Unable to load system health.',
        )
      })
      .finally(() => {
        if (!cancelled) {
          setIsHealthLoading(false)
        }
      })

    return () => {
      cancelled = true
    }
  }, [session])

  function handleAuthenticated(
    authenticatedSession: LoginResponse,
  ) {
    setAccessToken(
      authenticatedSession.accessToken,
    )

    setIsLoading(true)
    setErrorMessage('')

    setIsHealthLoading(true)
    setHealthErrorMessage('')

    setSessionNotice('')
    setSession(authenticatedSession)
  }

  function handleLogout() {
    clearAccessToken()
    setSession(null)

    setItems([])
    setReorderEvents([])
    setSystemHealth(null)

    setErrorMessage('')
    setHealthErrorMessage('')
  }

  function handleHealthRefresh() {
    setIsHealthLoading(true)
    setHealthErrorMessage('')

    void getSystemHealth()
      .then(setSystemHealth)
      .catch(error => {
        setHealthErrorMessage(
          error instanceof Error
            ? error.message
            : 'Unable to load system health.',
        )
      })
      .finally(() => {
        setIsHealthLoading(false)
      })
  }

  if (!session) {
    return (
      <LoginForm
        onAuthenticated={handleAuthenticated}
        noticeMessage={sessionNotice}
      />
    )
  }

  const isAdministrator =
    session.roles.includes('Administrator')

  return (
    <main className="page">
      <header className="hero">
        <div className="session-bar">
          <div>
            <p className="eyebrow">
              Signed in as
            </p>

            <strong>{session.email}</strong>

            <p className="session-roles">
              {session.roles.join(', ')}
            </p>
          </div>

          <button
            type="button"
            className="secondary-button"
            onClick={handleLogout}
          >
            Sign out
          </button>
        </div>

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

        <SystemHealthPanel
          health={systemHealth}
          isLoading={isHealthLoading}
          errorMessage={healthErrorMessage}
          onRefresh={handleHealthRefresh}
        />
      </section>

      {isAdministrator && (
        <AccountManagementPanel
          currentUserEmail={session.email}
        />
      )}
    </main>
  )
}

export default App