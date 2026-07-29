import type {
  Account,
  AccountRole,
  CreateAccountRequest,
} from '../types/account'
import {
  apiFetch,
  handleJsonResponse,
} from './httpClient'

export async function listAccounts(): Promise<Account[]> {
  const response = await apiFetch('/api/accounts')

  return handleJsonResponse<Account[]>(response)
}

export async function createAccount(
  request: CreateAccountRequest,
): Promise<Account> {
  const response = await apiFetch('/api/accounts', {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(request),
  })

  return handleJsonResponse<Account>(response)
}

export async function updateAccountRole(
  accountId: string,
  role: AccountRole,
): Promise<Account> {
  const response = await apiFetch(
    `/api/accounts/${encodeURIComponent(accountId)}/role`,
    {
      method: 'PATCH',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ role }),
    },
  )

  return handleJsonResponse<Account>(response)
}

export async function updateAccountStatus(
  accountId: string,
  isActive: boolean,
): Promise<Account> {
  const response = await apiFetch(
    `/api/accounts/${encodeURIComponent(accountId)}/status`,
    {
      method: 'PATCH',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ isActive }),
    },
  )

  return handleJsonResponse<Account>(response)
}