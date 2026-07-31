import type { AuditRecord } from '../types/auditRecord'
import {
  apiFetch,
  handleJsonResponse,
} from './httpClient'

export async function listAuditRecords(): Promise<
  AuditRecord[]
> {
  const response = await apiFetch(
    '/api/audit-records',
  )

  return handleJsonResponse<AuditRecord[]>(response)
}