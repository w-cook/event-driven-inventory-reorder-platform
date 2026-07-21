# Failure Scenarios

## Duplicate Reorder Message

Planned test: duplicate delivery of the same reorder message should not create duplicate reorder results.

## Processor Failure During Message Handling

Planned test: a transient processor failure should be retried or recorded in a recoverable state.

## Poison Message

Planned behavior: an unsupported or invalid message should be isolated from normal processing and surfaced for operator review.

## Database Unavailable

Planned behavior: API and processor health checks should report degraded or unhealthy state.