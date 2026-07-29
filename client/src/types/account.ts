export const ACCOUNT_ROLES = [
  'Viewer',
  'Operator',
  'Administrator',
] as const

export type AccountRole =
  (typeof ACCOUNT_ROLES)[number]

export interface Account {
  id: string
  email: string
  roles: string[]
  isActive: boolean
  createdAtUtc: string
}

export interface CreateAccountRequest {
  email: string
  password: string
  role: AccountRole
}