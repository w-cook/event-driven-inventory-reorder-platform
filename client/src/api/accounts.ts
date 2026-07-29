import type { Account } from '../types/account'
import { apiFetch, handleJsonResponse } from './httpClient'

export async function listAccounts(): Promise<Account[]> {
  const response = await apiFetch('/api/accounts')

  return handleJsonResponse<Account[]>(response)
}