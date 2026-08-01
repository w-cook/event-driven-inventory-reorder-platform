import {
  useEffect,
  useState,
} from 'react'

import {
  listAccounts,
  updateAccountRole,
  updateAccountStatus,
} from '../api/accounts'
import {
  ACCOUNT_ROLES,
  type Account,
  type AccountRole,
} from '../types/account'
import { CreateAccountForm } from './CreateAccountForm'

interface AccountManagementPanelProps {
  currentUserEmail: string
}

function getPrimaryRole(
  account: Account,
): AccountRole {
  const role = account.roles.find(
    candidate =>
      ACCOUNT_ROLES.includes(
        candidate as AccountRole,
      ),
  )

  return (role as AccountRole | undefined) ??
    'Viewer'
}

function createRoleDrafts(
  accounts: Account[],
): Record<string, AccountRole> {
  return Object.fromEntries(
    accounts.map(account => [
      account.id,
      getPrimaryRole(account),
    ]),
  ) as Record<string, AccountRole>
}

export function AccountManagementPanel({
  currentUserEmail,
}: AccountManagementPanelProps) {
  const [roleDrafts, setRoleDrafts] =
  useState<Record<string, AccountRole>>({})

  const [pendingAccountId, setPendingAccountId] =
    useState<string | null>(null)

  const [
    mutationErrorMessage,
    setMutationErrorMessage,
  ] = useState('')

  const [accounts, setAccounts] =
    useState<Account[]>([])

  const [isLoading, setIsLoading] =
    useState(true)

  const [errorMessage, setErrorMessage] =
    useState('')

  const [successMessage, setSuccessMessage] =
    useState('')

  useEffect(() => {
    let cancelled = false

    void listAccounts()
      .then(loadedAccounts => {
        if (cancelled) {
          return
        }

        setAccounts(loadedAccounts)
        setRoleDrafts(
          createRoleDrafts(loadedAccounts),
        )
      })
      .catch(error => {
        if (cancelled) {
          return
        }

        setErrorMessage(
          error instanceof Error
            ? error.message
            : 'Unable to load accounts.',
        )
      })
      .finally(() => {
        if (!cancelled) {
          setIsLoading(false)
        }
      })

    return () => {
      cancelled = true
    }
  }, [])

  async function loadAccounts() {
    try {
      const loadedAccounts =
        await listAccounts()

      setAccounts(loadedAccounts)
      setRoleDrafts(
        createRoleDrafts(loadedAccounts),
      )
    } catch (error) {
      setErrorMessage(
        error instanceof Error
          ? error.message
          : 'Unable to load accounts.',
      )
    } finally {
      setIsLoading(false)
    }
  }

  async function handleAccountCreated(
    account: Account,
  ) {
    setMutationErrorMessage('')
    setIsLoading(true)
    setErrorMessage('')

    await loadAccounts()

    setSuccessMessage(
      `Account created for ${account.email}.`,
    )
  }

  async function handleRoleUpdate(
    account: Account,
  ) {
    const currentRole =
      getPrimaryRole(account)

    const requestedRole =
      roleDrafts[account.id] ??
      currentRole

    if (requestedRole === currentRole) {
      return
    }

    setPendingAccountId(account.id)
    setMutationErrorMessage('')
    setSuccessMessage('')

    try {
      const updatedAccount =
        await updateAccountRole(
          account.id,
          requestedRole,
        )

      setAccounts(currentAccounts =>
        currentAccounts.map(currentAccount =>
          currentAccount.id === updatedAccount.id
            ? updatedAccount
            : currentAccount,
        ),
      )

      setRoleDrafts(currentDrafts => ({
        ...currentDrafts,
        [updatedAccount.id]:
          getPrimaryRole(updatedAccount),
      }))

      setSuccessMessage(
        `Role updated for ${updatedAccount.email}.`,
      )
    } catch (error) {
      setMutationErrorMessage(
        error instanceof Error
          ? error.message
          : 'Unable to update the account role.',
      )
    } finally {
      setPendingAccountId(null)
    }
  }

  async function handleStatusUpdate(
    account: Account,
  ) {
    setPendingAccountId(account.id)
    setMutationErrorMessage('')
    setSuccessMessage('')

    try {
      const updatedAccount =
        await updateAccountStatus(
          account.id,
          !account.isActive,
        )

      setAccounts(currentAccounts =>
        currentAccounts.map(currentAccount =>
          currentAccount.id === updatedAccount.id
            ? updatedAccount
            : currentAccount,
        ),
      )

      setSuccessMessage(
        `${updatedAccount.email} is now ${
          updatedAccount.isActive
            ? 'active'
            : 'inactive'
        }.`,
      )
    } catch (error) {
      setMutationErrorMessage(
        error instanceof Error
          ? error.message
          : 'Unable to update account access.',
      )
    } finally {
      setPendingAccountId(null)
    }
  }

  return (
    <section className="card account-management">
      <div className="section-header">
        <div>
          <h3>Accounts and Access</h3>

          <p>
            Review existing accounts, assigned roles,
            and access status.
          </p>
        </div>
      </div>

      {errorMessage && (
        <p className="error">{errorMessage}</p>
      )}

      {isLoading && <p>Loading accounts...</p>}

      {!isLoading &&
        !errorMessage &&
        accounts.length === 0 && (
          <p className="muted">
            No application accounts were found.
          </p>
        )}

      {!isLoading &&
        !errorMessage &&
        accounts.length > 0 && (
          <div className="table-wrapper">
            <table>
              <thead>
                <tr>
                  <th>Email</th>
                  <th>Role</th>
                  <th>Status</th>
                  <th>Created</th>
                  <th>Actions</th>
                </tr>
              </thead>

              <tbody>
                {accounts.map(account => {
                  const currentRole =
                    getPrimaryRole(account)

                  const selectedRole =
                    roleDrafts[account.id] ??
                    currentRole

                  const isPending =
                    pendingAccountId === account.id

                  const isCurrentAccount =
                    account.email.toLowerCase() ===
                    currentUserEmail.toLowerCase()

                  return (
                    <tr key={account.id}>
                      <td>{account.email}</td>

                      <td>
                        {account.roles.length > 0
                          ? account.roles.join(', ')
                          : 'No role'}
                      </td>

                      <td>
                        <span
                          className={
                            account.isActive
                              ? 'badge ok'
                              : 'badge error-badge'
                          }
                        >
                          {account.isActive
                            ? 'Active'
                            : 'Inactive'}
                        </span>
                      </td>

                      <td>
                        {new Date(
                          account.createdAtUtc,
                        ).toLocaleDateString()}
                      </td>

                      <td>
                        {isCurrentAccount ? (
                          <span className="current-account-label">
                            Current session
                          </span>
                        ) : (
                          <div className="account-actions">
                            <div className="account-role-control">
                              <select
                                aria-label={
                                  `Role for ${account.email}`
                                }
                                value={selectedRole}
                                disabled={isPending}
                                onChange={event =>
                                  setRoleDrafts(
                                    currentDrafts => ({
                                      ...currentDrafts,
                                      [account.id]:
                                        event.target
                                          .value as AccountRole,
                                    }),
                                  )
                                }
                              >
                                {ACCOUNT_ROLES.map(role => (
                                  <option
                                    key={role}
                                    value={role}
                                  >
                                    {role}
                                  </option>
                                ))}
                              </select>

                              <button
                                type="button"
                                className="secondary-button"
                                disabled={
                                  isPending ||
                                  selectedRole === currentRole
                                }
                                onClick={() =>
                                  void handleRoleUpdate(account)
                                }
                              >
                                {isPending
                                  ? 'Saving...'
                                  : 'Save role'}
                              </button>
                            </div>

                            <button
                              type="button"
                              className={
                                account.isActive
                                  ? 'secondary-button'
                                  : undefined
                              }
                              disabled={isPending}
                              onClick={() =>
                                void handleStatusUpdate(account)
                              }
                            >
                              {isPending
                                ? 'Updating...'
                                : account.isActive
                                  ? 'Deactivate'
                                  : 'Reactivate'}
                            </button>
                          </div>
                        )}
                      </td>
                    </tr>
                  )
                })}
              </tbody>
            </table>
          </div>
        )}

      <CreateAccountForm
        onCreated={handleAccountCreated}
      />

      {successMessage && (
        <p className="success-message">
          {successMessage}
        </p>
      )}

      {mutationErrorMessage && (
        <p className="error">
          {mutationErrorMessage}
        </p>
      )}
    </section>
  )
}