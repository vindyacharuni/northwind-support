import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import type { SlaStatus } from '../../../types/api';
import { SlaIndicator, slaConfigs } from './SlaIndicator';

const allStatuses: SlaStatus[] = ['WithinSla', 'AtRisk', 'Breached', 'Met', 'NotApplicable'];

describe('SlaIndicator', () => {
  it('renders every state distinguishably', () => {
    const renderedTexts = new Set<string>();
    const renderedSymbols = new Set<string>();

    for (const status of allStatuses) {
      const { unmount } = render(<SlaIndicator status={status} />);
      const config = slaConfigs[status];

      const badge = screen.getByTestId(`sla-badge-${status}`);
      expect(badge).toBeInTheDocument();
      expect(badge).toHaveTextContent(config.text);
      expect(badge).toHaveTextContent(config.symbol);
      expect(badge).toHaveAttribute('aria-label', config.description);

      // Verify each state has distinct textual and symbolic representation
      expect(renderedTexts.has(config.text)).toBe(false);
      expect(renderedSymbols.has(config.symbol)).toBe(false);
      renderedTexts.add(config.text);
      renderedSymbols.add(config.symbol);

      unmount();
    }

    expect(renderedTexts.size).toBe(5);
    expect(renderedSymbols.size).toBe(5);
  });
});
