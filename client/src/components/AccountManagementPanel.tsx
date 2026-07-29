import { useCallback, useEffect, useState } from 'react'

import { listAccounts } from '../api/accounts'
import type { Account } from '../types/account'
import { CreateAccountForm } from './CreateAccountForm'

export function AccountManagementPanel() {
  const [accounts, setAccounts] =
    useState<Account[]>([])

  const [isLoading, setIsLoading] =
    useState(true)

  const [errorMessage, setErrorMessage] =
    useState('')

  const [successMessage, setSuccessMessage] =
    useState('')

  const loadAccounts = useCallback(async () => {
    setIsLoading(true)
    setErrorMessage('')

    try {
      const loadedAccounts =
        await listAccounts()

      setAccounts(loadedAccounts)
    } catch (error) {
      setErrorMessage(
        error instanceof Error
          ? error.message
          : 'Unable to load accounts.',
      )
    } finally {
      setIsLoading(false)
    }
  }, [])

  useEffect(() => {
    void loadAccounts()
  }, [loadAccounts])

  async function handleAccountCreated(
    account: Account,
  ) {
    await loadAccounts()

    setSuccessMessage(
      `Account created for ${account.email}.`,
    )
  }

  return (
    <section className="card account-management">
      <div className="section-header">
        <div>
          <h2>Account Management</h2>

          <p>
            Create and review application accounts,
            assigned roles, and access status.
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
                </tr>
              </thead>

              <tbody>
                {accounts.map((account) => (
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
                  </tr>
                ))}
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
    </section>
  )
}