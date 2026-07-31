import type { AppView } from '../types/appView'

interface AppNavigationProps {
  activeView: AppView
  isAdministrator: boolean
  onNavigate: (view: AppView) => void
}

interface NavigationItem {
  view: AppView
  label: string
}

const standardItems: NavigationItem[] = [
  {
    view: 'dashboard',
    label: 'Dashboard',
  },
  {
    view: 'inventory',
    label: 'Inventory',
  },
  {
    view: 'workflow',
    label: 'Workflow',
  },
]

const administratorItems: NavigationItem[] = [
  {
    view: 'audit',
    label: 'Audit',
  },
  {
    view: 'administration',
    label: 'Administration',
  },
]

export function AppNavigation({
  activeView,
  isAdministrator,
  onNavigate,
}: AppNavigationProps) {
  const items = isAdministrator
    ? [...standardItems, ...administratorItems]
    : standardItems

  return (
    <nav
      className="app-navigation"
      aria-label="Primary application navigation"
    >
      <ul>
        {items.map(item => {
          const isActive =
            activeView === item.view

          return (
            <li key={item.view}>
              <button
                type="button"
                className={
                  isActive
                    ? 'navigation-button active'
                    : 'navigation-button'
                }
                aria-current={
                  isActive
                    ? 'page'
                    : undefined
                }
                onClick={() =>
                  onNavigate(item.view)
                }
              >
                {item.label}
              </button>
            </li>
          )
        })}
      </ul>
    </nav>
  )
}