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
import { AppNavigation } from './components/AppNavigation'
import { AuditRecordsPanel } from './components/AuditRecordsPanel'
import { InventoryItemForm } from './components/InventoryItemForm'
import { InventorySummaryCards } from './components/InventorySummaryCards'
import { InventoryTable } from './components/InventoryTable'
import { LoginForm } from './components/LoginForm'
import { ReorderWorkflowPanel } from './components/ReorderWorkflowPanel'
import { SystemHealthPanel } from './components/SystemHealthPanel'
import { WorkflowSummaryCards } from './components/WorkflowSummaryCards'
import { isLowStock, type InventoryItem } from './types/inventoryItem'
import type { AppView } from './types/appView'
import type { LoginResponse } from './types/auth'
import type { ReorderEvent } from './types/reorderEvent'
import type { SystemHealth } from './types/systemHealth'

function App() {
  const [session, setSession] = useState<LoginResponse | null>(null)
  const [sessionNotice, setSessionNotice] = useState('')
  const [activeView, setActiveView] = useState<AppView>('dashboard')

  const [items, setItems] = useState<InventoryItem[]>([])
  const [editingItem, setEditingItem] = useState<InventoryItem | null>(null)
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
      setActiveView('dashboard')
      setItems([])
      setEditingItem(null)
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

    setActiveView('dashboard')

    setSessionNotice('')
    setSession(authenticatedSession)
  }

  function handleLogout() {
    clearAccessToken()
    setSession(null)

    setActiveView('dashboard')

    setItems([])
    setEditingItem(null)
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

  const canManageInventory =
    session.roles.includes('Operator') ||
    isAdministrator

  async function handleInventorySaved(
    savedItem: InventoryItem,
    wasCreated: boolean,
  ): Promise<void> {
    setItems(currentItems => {
      if (wasCreated) {
        return [savedItem, ...currentItems]
      }

      return currentItems.map(item =>
        item.id === savedItem.id
          ? savedItem
          : item,
      )
    })

    if (!wasCreated) {
      setEditingItem(savedItem)
    }

    setIsLoading(true)
    setErrorMessage('')

    try {
      const [
        loadedItems,
        loadedReorderEvents,
      ] = await Promise.all([
        listInventoryItems(),
        listReorderEvents(),
      ])

      setItems(loadedItems)
      setReorderEvents(loadedReorderEvents)

      if (!wasCreated) {
        const refreshedItem =
          loadedItems.find(
            item => item.id === savedItem.id,
          ) ?? savedItem

        setEditingItem(refreshedItem)
      }
    } catch (error) {
      const refreshMessage =
        error instanceof Error
          ? error.message
          : 'Unable to reload dashboard data.'

      throw new Error(
        `The inventory item was saved, but the dashboard could not refresh. ${refreshMessage}`,
        { cause: error },
      )
    } finally {
      setIsLoading(false)
    }

    setIsHealthLoading(true)
    setHealthErrorMessage('')

    try {
      const refreshedHealth =
        await getSystemHealth()

      setSystemHealth(refreshedHealth)
    } catch (error) {
      setHealthErrorMessage(
        error instanceof Error
          ? error.message
          : 'Unable to refresh system health.',
      )
    } finally {
      setIsHealthLoading(false)
    }
  }

  function handleNavigate(view: AppView) {
    setActiveView(view)
  }

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
          Role-aware operations interface for inventory management, reorder workflow
          monitoring, system health, audit review, and account administration.
        </p>
      </header>

      <AppNavigation
        activeView={activeView}
        isAdministrator={isAdministrator}
        onNavigate={handleNavigate}
      />

      {errorMessage && <p className="error">{errorMessage}</p>}
      {isLoading && <p>Loading dashboard...</p>}

      {activeView === 'dashboard' && (
        <section
          className="app-view"
          aria-labelledby="dashboard-view-title"
        >
          <header className="view-header">
            <h2 id="dashboard-view-title">
              Operations Overview
            </h2>

            <p>
              Review inventory conditions, reorder
              activity, and application health.
            </p>
          </header>

          <InventorySummaryCards items={items} />

          <WorkflowSummaryCards
            events={reorderEvents}
          />

          <SystemHealthPanel
            health={systemHealth}
            isLoading={isHealthLoading}
            errorMessage={healthErrorMessage}
            onRefresh={handleHealthRefresh}
          />
        </section>
      )}

      {activeView === 'inventory' && (
        <div className="app-view">
          <section className="toolbar">
            <label>
              <input
                type="checkbox"
                checked={showLowStockOnly}
                onChange={event =>
                  setShowLowStockOnly(
                    event.target.checked,
                  )
                }
              />

              Show low-stock items only
            </label>
          </section>

          <InventoryTable
            items={visibleItems}
            canManageInventory={canManageInventory}
            onEdit={setEditingItem}
          />

          {canManageInventory && (
            <InventoryItemForm
              key={editingItem?.id ?? 'create'}
              itemToEdit={editingItem}
              onSaved={handleInventorySaved}
              onCancelEdit={() =>
                setEditingItem(null)
              }
            />
          )}
        </div>
      )}

      {activeView === 'workflow' && (
        <div className="app-view">
          <WorkflowSummaryCards
            events={reorderEvents}
          />

          <ReorderWorkflowPanel
            events={reorderEvents}
            inventoryItems={items}
          />
        </div>
      )}

      {activeView === 'audit' &&
        isAdministrator && (
          <div className="app-view">
            <AuditRecordsPanel />
          </div>
        )}

      {activeView === 'administration' &&
        isAdministrator && (
          <div className="app-view">
            <AccountManagementPanel
              currentUserEmail={session.email}
            />
          </div>
        )}
    </main>
  )
}

export default App