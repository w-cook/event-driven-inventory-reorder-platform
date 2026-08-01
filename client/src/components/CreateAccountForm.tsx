import { useState } from 'react'
import type { FormEvent } from 'react'

import { createAccount } from '../api/accounts'
import {
  ACCOUNT_ROLES,
  type Account,
  type AccountRole,
} from '../types/account'

interface CreateAccountFormProps {
  onCreated: (account: Account) => Promise<void>
}

export function CreateAccountForm({
  onCreated,
}: CreateAccountFormProps) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [role, setRole] =
    useState<AccountRole>('Viewer')

  const [isSubmitting, setIsSubmitting] =
    useState(false)

  const [errorMessage, setErrorMessage] =
    useState('')

  async function handleSubmit(
    event: FormEvent<HTMLFormElement>,
  ) {
    event.preventDefault()

    setIsSubmitting(true)
    setErrorMessage('')

    try {
      const account = await createAccount({
        email: email.trim(),
        password,
        role,
      })

      await onCreated(account)

      setEmail('')
      setPassword('')
      setRole('Viewer')
    } catch (error) {
      setErrorMessage(
        error instanceof Error
          ? error.message
          : 'Unable to create the account.',
      )
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <form
      className="account-create-form"
      onSubmit={handleSubmit}
    >
      <h3>Create Account</h3>

      <p className="muted">
        Create a password-protected application
        account and assign its initial role.
      </p>

      {errorMessage && (
        <p className="error">{errorMessage}</p>
      )}

      <div className="account-form-grid">
        <label>
          Email
          <input
            type="email"
            value={email}
            onChange={(event) =>
              setEmail(event.target.value)
            }
            autoComplete="off"
            required
          />
        </label>

        <label>
          Initial role
          <select
            value={role}
            onChange={(event) =>
              setRole(
                event.target.value as AccountRole,
              )
            }
          >
            {ACCOUNT_ROLES.map((availableRole) => (
              <option
                key={availableRole}
                value={availableRole}
              >
                {availableRole}
              </option>
            ))}
          </select>
        </label>

        <label className="account-password-field">
          Password
          <input
            type="password"
            value={password}
            onChange={(event) =>
              setPassword(event.target.value)
            }
            autoComplete="new-password"
            required
          />

          <span className="field-help">
            At least 10 characters with uppercase,
            lowercase, number, and symbol.
          </span>
        </label>
      </div>

      <button
        type="submit"
        disabled={isSubmitting}
      >
        {isSubmitting
          ? 'Creating account...'
          : 'Create account'}
      </button>
    </form>
  )
}