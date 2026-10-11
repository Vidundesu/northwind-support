import { useCallback, useEffect, useState } from 'react';
import { ApiError, toErrorMessage } from '../../../api/client';
import { ticketsApi } from '../../../api/tickets';
import { Field } from '../../../components/Field';
import { formatDateTime } from '../../../lib/format';
import type { TicketDetail, TicketEscalation } from '../../../types/api';

interface Props { ticket: TicketDetail; onUpdated: (ticket: TicketDetail) => void; }

export function EscalationPanel({ ticket, onUpdated }: Props) {
  const [reason, setReason] = useState('');
  const [fieldError, setFieldError] = useState('');
  const [error, setError] = useState('');
  const [pending, setPending] = useState(false);
  const [history, setHistory] = useState<TicketEscalation[]>([]);
  const [historyError, setHistoryError] = useState('');
  const canEscalate = ticket.priority !== 'Critical' && ticket.status !== 'Resolved' && ticket.status !== 'Closed';

  const reloadHistory = useCallback(async () => {
    try { setHistory(await ticketsApi.getEscalations(ticket.id)); setHistoryError(''); }
    catch (caught) { setHistoryError(toErrorMessage(caught, 'Could not load escalation history.')); }
  }, [ticket.id]);

  useEffect(() => {
    let current = true;
    void ticketsApi.getEscalations(ticket.id)
      .then((entries) => { if (current) { setHistory(entries); setHistoryError(''); } })
      .catch((caught: unknown) => { if (current) setHistoryError(toErrorMessage(caught, 'Could not load escalation history.')); });
    return () => { current = false; };
  }, [ticket.id]);

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();
    const trimmed = reason.trim();
    if (trimmed.length < 5 || trimmed.length > 500) {
      setFieldError('Enter a reason between 5 and 500 characters.');
      return;
    }
    setFieldError(''); setError(''); setPending(true);
    try {
      const result = await ticketsApi.escalate(ticket.id, trimmed);
      onUpdated(result.ticket);
      setReason('');
      await reloadHistory();
    } catch (caught) {
      setError(caught instanceof ApiError && caught.status === 409
        ? `Escalation was rejected: ${caught.message}`
        : toErrorMessage(caught, 'Could not escalate this ticket.'));
    } finally { setPending(false); }
  };

  return <section className="card">
    <h2>Escalation</h2>
    {canEscalate ? <form className="form" onSubmit={submit} noValidate>
      <Field id="escalation-reason" label="Reason" error={fieldError} hint="5 to 500 characters.">
        {(props) => <textarea {...props} rows={3} maxLength={500} value={reason} onChange={(event) => setReason(event.target.value)} />}
      </Field>
      {error && <p role="alert" className="field__error">{error}</p>}
      <button className="button button--primary" type="submit" disabled={pending}>{pending ? 'Escalating…' : 'Escalate ticket'}</button>
    </form> : <p>This ticket cannot be escalated in its current state.</p>}
    <h3>History</h3>
    {historyError && <p role="alert">{historyError}</p>}
    {history.length === 0 && !historyError && <p>No escalations recorded.</p>}
    <ol className="escalation-history">
      {history.map((entry) => <li key={entry.id}>
        <strong>{entry.fromPriority} → {entry.toPriority}</strong>
        <p>Agent: {entry.fromAgent?.fullName ?? 'Unassigned'} → {entry.toAgent?.fullName ?? 'Unassigned'}</p>
        <p>Due: {formatDateTime(entry.fromDueAtUtc)} → {formatDateTime(entry.toDueAtUtc)}</p>
        <p>{entry.reason}</p>
        <small>{entry.escalatedBy} · {formatDateTime(entry.escalatedAtUtc)}</small>
      </li>)}
    </ol>
  </section>;
}
