import { EmptyState } from '../../../components/EmptyState';
import { Spinner } from '../../../components/Spinner';
import { formatDateTime } from '../../../lib/format';
import type { TicketEscalation } from '../../../types/api';
import { PriorityBadge } from './TicketBadges';

export interface EscalationHistoryProps {
  escalations: TicketEscalation[];
  isLoading?: boolean;
  error?: string;
}

export function EscalationHistory({ escalations, isLoading, error }: EscalationHistoryProps) {
  return (
    <div className="card">
      <h2>Escalation History</h2>

      {isLoading && <Spinner label="Loading escalation history" />}

      {error && (
        <p className="field__error" role="alert">
          {error}
        </p>
      )}

      {!isLoading && !error && escalations.length === 0 && (
        <EmptyState
          title="No escalations"
          description="This ticket has not been escalated."
        />
      )}

      {!isLoading && escalations.length > 0 && (
        <div className="escalation-list">
          {escalations.map((item, index) => {
            const isLast = index === escalations.length - 1;
            return (
              <article
                key={item.id}
                className="escalation-item"
                style={{
                  borderBottom: isLast ? 'none' : '1px solid var(--colour-border)',
                  paddingBottom: isLast ? 0 : '14px',
                  marginBottom: isLast ? 0 : '14px',
                }}
              >
                <header
                  style={{
                    display: 'flex',
                    justifyContent: 'space-between',
                    alignItems: 'baseline',
                    marginBottom: '8px',
                    fontSize: '0.9rem',
                  }}
                >
                  <span>
                    <strong>{item.escalatedBy}</strong> escalated this ticket
                  </span>
                  <time className="summary__note" dateTime={item.escalatedAtUtc}>
                    {formatDateTime(item.escalatedAtUtc)}
                  </time>
                </header>

                <p
                  className="description"
                  style={{
                    fontStyle: 'italic',
                    marginBottom: '10px',
                    background: 'var(--colour-page)',
                    padding: '8px 12px',
                    borderRadius: '4px',
                  }}
                >
                  &ldquo;{item.reason}&rdquo;
                </p>

                <dl
                  className="summary"
                  style={{ gridTemplateColumns: 'repeat(auto-fit, minmax(160px, 1fr))' }}
                >
                  <div>
                    <dt>Priority</dt>
                    <dd style={{ flexDirection: 'row', alignItems: 'center', gap: '6px' }}>
                      <PriorityBadge priority={item.fromPriority} />
                      <span aria-hidden="true">&rarr;</span>
                      <PriorityBadge priority={item.toPriority} />
                    </dd>
                  </div>

                  <div>
                    <dt>Assigned agent</dt>
                    <dd>
                      <span>
                        {item.fromAgent?.fullName ?? 'Unassigned'} &rarr;{' '}
                        {item.toAgent?.fullName ?? 'Unassigned'}
                      </span>
                    </dd>
                  </div>

                  <div>
                    <dt>Due date</dt>
                    <dd>
                      <span>
                        {item.fromDueAtUtc ? formatDateTime(item.fromDueAtUtc) : 'None'} &rarr;{' '}
                        {item.toDueAtUtc ? formatDateTime(item.toDueAtUtc) : 'None'}
                      </span>
                    </dd>
                  </div>
                </dl>
              </article>
            );
          })}
        </div>
      )}
    </div>
  );
}
