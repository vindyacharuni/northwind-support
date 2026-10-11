export type BadgeTone = 'neutral' | 'info' | 'success' | 'warning' | 'danger';

interface BadgeProps extends React.HTMLAttributes<HTMLSpanElement> {
  tone?: BadgeTone;
  children: React.ReactNode;
}

/** A small coloured label. The text always carries the meaning, not just the colour. */
export function Badge({ tone = 'neutral', children, className = '', ...rest }: BadgeProps) {
  return (
    <span className={`badge badge--${tone} ${className}`.trim()} {...rest}>
      {children}
    </span>
  );
}

