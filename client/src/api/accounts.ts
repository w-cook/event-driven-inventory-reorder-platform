import type {
  Account,
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