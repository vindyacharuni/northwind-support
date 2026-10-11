import { Badge, type BadgeTone } from '../../../components/Badge';
import type { SlaStatus } from '../../../types/api';

export interface SlaConfig {
  text: string;
  symbol: string;
  tone: BadgeTone;
  description: string;
}

export const slaConfigs: Record<SlaStatus, SlaConfig> = {
  Met: { text: 'Met', symbol: '✓', tone: 'success', description: 'Met SLA on time' },
  Breached: { text: 'Breached', symbol: '✕', tone: 'danger', description: 'Breached SLA due date' },
  AtRisk: { text: 'At risk', symbol: '▲', tone: 'warning', description: 'At risk of breaching SLA' },
  WithinSla: { text: 'Within SLA', symbol: '●', tone: 'info', description: 'Comfortably within SLA' },
  NotApplicable: { text: 'No SLA', symbol: '—', tone: 'neutral', description: 'No SLA applies' },
};

export interface SlaIndicatorProps {
  status: SlaStatus;
  showSymbol?: boolean;
}

/**
 * Reusable SLA indicator showing SLA status with distinct tone, label and symbol.
 * State is not conveyed by color alone.
 */
export function SlaIndicator({ status, showSymbol = true }: SlaIndicatorProps) {
  const config = slaConfigs[status] ?? {
    text: status,
    symbol: '?',
    tone: 'neutral' as BadgeTone,
    description: status,
  };

  return (
    <Badge tone={config.tone} aria-label={config.description} data-testid={`sla-badge-${status}`}>
      {showSymbol && <span aria-hidden="true" style={{ marginRight: '0.35rem', fontWeight: 'bold' }}>{config.symbol}</span>}
      <span>{config.text}</span>
    </Badge>
  );
}
