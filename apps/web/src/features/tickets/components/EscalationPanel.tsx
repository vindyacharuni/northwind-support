import { useState } from 'react';
import { toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { Field } from '../../../components/Field';
import type { EscalateTicketResponse, TicketDetail } from '../../../types/api';

export interface EscalationPanelProps {
  ticket: TicketDetail;
  onEscalated: (response: EscalateTicketResponse) => void;
}

export function EscalationPanel({ ticket, onEscalated }: EscalationPanelProps) {
  const [reason, setReason] = useState('');
  const [escalatedBy, setEscalatedBy] = useState('Alex Turner');
  const [reasonError, setReasonError] = useState<string>();
  const [actorError, setActorError] = useState<string>();
  const [serverError, setServerError] = useState<string>();
  const [isSubmitting, setIsSubmitting] = useState(false);

  const isCritical = ticket.priority === 'Critical';
  const isResolvedOrClosed = ticket.status === 'Resolved' || ticket.status === 'Closed';
  const cannotEscalate = isCritical || isResolvedOrClosed;

  const cannotEscalateMessage = isCritical
    ? 'Critical tickets cannot be escalated.'
    : isResolvedOrClosed
      ? 'Resolved or closed tickets cannot be escalated.'
      : undefined;

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    setServerError(undefined);

    const trimmedReason = reason.trim();
    const trimmedActor = escalatedBy.trim();

    let hasValidationError = false;

    if (!trimmedReason || trimmedReason.length < 5) {
      setReasonError('Reason must be at least 5 characters.');
      hasValidationError = true;
    } else if (trimmedReason.length > 500) {
      setReasonError('Reason cannot exceed 500 characters.');
      hasValidationError = true;
    } else {
      setReasonError(undefined);
    }

    if (!trimmedActor) {
      setActorError('Escalated by is required.');
      hasValidationError = true;
    } else if (trimmedActor.length > 100) {
      setActorError('Escalated by cannot exceed 100 characters.');
      hasValidationError = true;
    } else {
      setActorError(undefined);
    }

    // Invalid input never reaches the API
    if (hasValidationError) {
      return;
    }

    setIsSubmitting(true);
    try {
      const response = await ticketsApi.escalateTicket(ticket.id, trimmedReason, trimmedActor);
      setReason('');
      setReasonError(undefined);
      setActorError(undefined);
      setServerError(undefined);
      onEscalated(response);
    } catch (caught) {
      setServerError(toErrorMessage(caught, 'Could not escalate ticket.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <div className="card">
      <h2>Escalate Ticket</h2>

      {cannotEscalate && cannotEscalateMessage && (
        <p className="field__hint" style={{ marginBottom: '12px' }}>
          {cannotEscalateMessage}
        </p>
      )}

      <form onSubmit={(e) => void handleSubmit(e)}>
        <Field
          id="escalation-reason"
          label="Escalation reason"
          error={reasonError}
          hint="Must be between 5 and 500 characters."
        >
          {(fieldProps) => (
            <textarea
              {...fieldProps}
              rows={3}
              value={reason}
              disabled={cannotEscalate || isSubmitting}
              placeholder="Explain why this ticket is being escalated..."
              onChange={(e) => {
                setReason(e.target.value);
                if (reasonError && e.target.value.trim().length >= 5) {
                  setReasonError(undefined);
                }
              }}
            />
          )}
        </Field>

        <Field
          id="escalated-by"
          label="Escalated by"
          error={actorError}
          hint="Agent performing the escalation."
        >
          {(fieldProps) => (
            <input
              {...fieldProps}
              type="text"
              value={escalatedBy}
              disabled={cannotEscalate || isSubmitting}
              onChange={(e) => {
                setEscalatedBy(e.target.value);
                if (actorError && e.target.value.trim()) {
                  setActorError(undefined);
                }
              }}
            />
          )}
        </Field>

        {serverError && (
          <p className="field__error" role="alert" style={{ marginBottom: '12px' }}>
            {serverError}
          </p>
        )}

        <button
          type="submit"
          className="button button--primary"
          disabled={cannotEscalate || isSubmitting}
        >
          {isSubmitting ? 'Escalating...' : 'Escalate ticket'}
        </button>
      </form>
    </div>
  );
}
