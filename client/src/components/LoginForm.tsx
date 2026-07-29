import { useState } from 'react'
import type { FormEvent } from 'react'

import { login } from '../api/auth'
import type { LoginResponse } from '../types/auth'

interface LoginFormProps {
  onAuthenticated: (session: LoginResponse) => void
}

export function LoginForm({
  onAuthenticated,
}: LoginFormProps) {
  const [email, setEmail] = useState('')
  const [password, setPassword] = useState('')
  const [isSubmitting, setIsSubmitting] = useState(false)
  const [errorMessage, setErrorMessage] = useState('')

  async function handleSubmit(
    event: FormEvent<HTMLFormElement>,
  ) {
    event.preventDefault()

    setIsSubmitting(true)
    setErrorMessage('')

    try {
      const session = await login(
        email.trim(),
        password,
      )

      onAuthenticated(session)
    } catch (error) {
      setErrorMessage(
        error instanceof Error
          ? error.message
          : 'Unable to sign in.',
      )
    } finally {
      setIsSubmitting(false)
    }
  }

  return (
    <main className="login-page">
      <section className="login-card">
        <p className="eyebrow">
          Inventory Operations Platform
        </p>

        <h1>Sign in</h1>

        <p className="muted">
          Use an application account to access the
          operations dashboard.
        </p>

        {errorMessage && (
          <p className="error">{errorMessage}</p>
        )}

        <form
          className="login-form"
          onSubmit={handleSubmit}
        >
          <label>
            Email
            <input
              type="email"
              value={email}
              onChange={(event) =>
                setEmail(event.target.value)
              }
              autoComplete="username"
              required
            />
          </label>

          <label>
            Password
            <input
              type="password"
              value={password}
              onChange={(event) =>
                setPassword(event.target.value)
              }
              autoComplete="current-password"
              required
            />
          </label>

          <button
            type="submit"
            disabled={isSubmitting}
          >
            {isSubmitting
              ? 'Signing in...'
              : 'Sign in'}
          </button>
        </form>
      </section>
    </main>
  )
}