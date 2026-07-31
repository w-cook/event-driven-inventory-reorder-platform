export interface AuditRecord {
  id: number
  userName: string
  role: string
  action: string
  entityType: string
  entityId: string
  occurredAt: string
  details: string | null
}