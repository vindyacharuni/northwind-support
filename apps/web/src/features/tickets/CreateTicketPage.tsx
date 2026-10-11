import { useState } from 'react';
import { Link, useNavigate } from 'react-router-dom';
import { ErrorMessage } from '../../components/ErrorMessage';
import { Spinner } from '../../components/Spinner';
import { useReferenceData } from '../../hooks/useReferenceData';
import type { TicketDetail } from '../../types/api';
import { CreateTicketForm } from './components/CreateTicketForm';
import { PriorityBadge } from './components/TicketBadges';
import { SlaCell } from './components/SlaCell';

export function CreateTicketPage() {
  const reference = useReferenceData();
  const navigate = useNavigate();
  const [created, setCreated] = useState<TicketDetail>();

  return (
    <section>
      <header className="page-header">
        <div>
          <p className="breadcrumb">
            <Link to="/tickets">Tickets</Link> / New
          </p>
          <h1>Raise a ticket</h1>
        </div>
      </header>

      {reference.isLoading && <Spinner label="Loading customers and categories" />}

      {reference.error && (
        <ErrorMessage message={reference.error} onRetry={() => void reference.reload()} />
      )}

      {created && (
        <div className="card notice" role="status">
          <p>
            Raised <strong>{created.reference}</strong> for {created.customer.name}.
          </p>

          <div className="triage-result" style={{ marginTop: '1rem', marginBottom: '1.25rem' }}>
            <h3 style={{ fontSize: '1rem', fontWeight: 600, marginBottom: '0.75rem' }}>
              Automated Triage Result
            </h3>
            <dl className="summary" style={{ display: 'grid', gridTemplateColumns: 'auto 1fr', gap: '0.5rem 1rem' }}>
              <dt>Applied Priority</dt>
              <dd>
                <PriorityBadge priority={created.priority} />
                {created.triage?.priorityReason && (
                  <span className="summary__note" style={{ marginLeft: '0.5rem' }}>
                    {created.triage.priorityReason}
                  </span>
                )}
              </dd>

              <dt>SLA Due Date</dt>
              <dd>
                <SlaCell dueAtUtc={created.dueAtUtc} slaStatus={created.slaStatus} />
                {created.triage?.slaReason && (
                  <span className="summary__note" style={{ display: 'block', marginTop: '0.25rem' }}>
                    {created.triage.slaReason}
                  </span>
                )}
              </dd>

              <dt>Assigned Agent</dt>
              <dd>
                {created.assignedAgent ? (
                  <span>
                    <strong>{created.assignedAgent.fullName}</strong>
                    {created.triage?.assignmentReason && (
                      <span className="summary__note" style={{ marginLeft: '0.5rem' }}>
                        ({created.triage.assignmentReason})
                      </span>
                    )}
                  </span>
                ) : (
                  <span>
                    <em>Unassigned</em>
                    {created.triage?.assignmentReason && (
                      <span className="summary__note" style={{ display: 'block', marginTop: '0.25rem', color: '#b45309' }}>
                        {created.triage.assignmentReason}
                      </span>
                    )}
                  </span>
                )}
              </dd>
            </dl>
          </div>

          <div className="button-row">
            <button
              type="button"
              className="button button--primary"
              onClick={() => void navigate(`/tickets/${created.id}`)}
            >
              Open {created.reference}
            </button>
            <button type="button" className="button button--ghost" onClick={() => setCreated(undefined)}>
              Raise another
            </button>
          </div>
        </div>
      )}

      {reference.data && !created && (
        <CreateTicketForm
          customers={reference.data.customers}
          categories={reference.data.categories}
          onCreated={setCreated}
        />
      )}
    </section>
  );
}
