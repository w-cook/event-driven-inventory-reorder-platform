import { useEffect, useState } from 'react'

import { listAuditRecords } from '../api/auditRecords'
import type { AuditRecord } from '../types/auditRecord'

function formatDetails(details: string): string {
  try {
    return JSON.stringify(
      JSON.parse(details),
      null,
      2,
    )
  } catch {
    return details
  }
}

export function AuditRecordsPanel() {
  const [records, setRecords] =
    useState<AuditRecord[]>([])

  const [isLoading, setIsLoading] =
    useState(true)

  const [errorMessage, setErrorMessage] =
    useState('')

  useEffect(() => {
    let cancelled = false

    void listAuditRecords()
      .then(loadedRecords => {
        if (!cancelled) {
          setRecords(loadedRecords)
        }
      })
      .catch(error => {
        if (cancelled) {
          return
        }

        setErrorMessage(
          error instanceof Error
            ? error.message
            : 'Unable to load audit records.',
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

  function handleRefresh() {
    setIsLoading(true)
    setErrorMessage('')

    void listAuditRecords()
      .then(setRecords)
      .catch(error => {
        setErrorMessage(
          error instanceof Error
            ? error.message
            : 'Unable to load audit records.',
        )
      })
      .finally(() => {
        setIsLoading(false)
      })
  }

  return (
    <section className="card audit-records">
      <div className="section-header">
        <div>
          <h2>Audit Records</h2>

          <p>
            Review successful inventory and
            account-administration actions.
          </p>
        </div>

        <button
          type="button"
          className="secondary-button"
          disabled={isLoading}
          onClick={handleRefresh}
        >
          {isLoading
            ? 'Refreshing...'
            : 'Refresh records'}
        </button>
      </div>

      {errorMessage && (
        <p className="error">{errorMessage}</p>
      )}

      {isLoading && (
        <p>Loading audit records...</p>
      )}

      {!isLoading &&
        !errorMessage &&
        records.length === 0 && (
          <p className="muted">
            No completed actions have been audited yet.
          </p>
        )}

      {!isLoading &&
        !errorMessage &&
        records.length > 0 && (
          <div className="table-wrapper">
            <table>
              <thead>
                <tr>
                  <th>Occurred</th>
                  <th>User</th>
                  <th>Role</th>
                  <th>Action</th>
                  <th>Entity</th>
                  <th>Details</th>
                </tr>
              </thead>

              <tbody>
                {records.map(record => (
                  <tr key={record.id}>
                    <td>
                      {new Date(
                        record.occurredAt,
                      ).toLocaleString()}
                    </td>

                    <td>{record.userName}</td>

                    <td>
                      <span className="badge neutral">
                        {record.role}
                      </span>
                    </td>

                    <td>{record.action}</td>

                    <td>
                      {record.entityType}
                      {' #'}
                      {record.entityId}
                    </td>

                    <td>
                      {record.details ? (
                        <details className="audit-details">
                          <summary>
                            View details
                          </summary>

                          <pre>
                            {formatDetails(
                              record.details,
                            )}
                          </pre>
                        </details>
                      ) : (
                        <span className="muted">
                          None
                        </span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
    </section>
  )
}