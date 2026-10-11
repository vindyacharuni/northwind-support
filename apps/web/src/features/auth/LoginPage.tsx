import { useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { toErrorMessage } from '../../api/client';
import { Field } from '../../components/Field';
import { useAuth } from './AuthContext';

export function LoginPage() {
  const { user, login } = useAuth();
  const location = useLocation();
  const navigate = useNavigate();

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [emailError, setEmailError] = useState<string>();
  const [passwordError, setPasswordError] = useState<string>();
  const [serverError, setServerError] = useState<string>();
  const [isSubmitting, setIsSubmitting] = useState(false);

  const from =
    (location.state as { from?: { pathname?: string } })?.from?.pathname ?? '/tickets';

  if (user) {
    return <Navigate to={from} replace />;
  }

  const handleSubmit = async (event: React.FormEvent) => {
    event.preventDefault();
    setServerError(undefined);

    const trimmedEmail = email.trim();
    let hasValidationError = false;

    if (!trimmedEmail) {
      setEmailError('Email address is required.');
      hasValidationError = true;
    } else if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(trimmedEmail)) {
      setEmailError('Please enter a valid email address.');
      hasValidationError = true;
    } else {
      setEmailError(undefined);
    }

    if (!password) {
      setPasswordError('Password is required.');
      hasValidationError = true;
    } else {
      setPasswordError(undefined);
    }

    if (hasValidationError) {
      return;
    }

    setIsSubmitting(true);
    try {
      await login({ email: trimmedEmail, password });
      navigate(from, { replace: true });
    } catch (caught) {
      setServerError(toErrorMessage(caught, 'Invalid email or password.'));
    } finally {
      setIsSubmitting(false);
    }
  };

  return (
    <section style={{ maxWidth: '440px', margin: '48px auto' }}>
      <header className="page-header" style={{ marginBottom: '24px' }}>
        <div>
          <h1>Sign in</h1>
          <p className="page-header__subtitle">
            Sign in with your agent credentials to access Northwind Support Desk.
          </p>
        </div>
      </header>

      <div className="card">
        <form onSubmit={(e) => void handleSubmit(e)} noValidate>
          <Field id="login-email" label="Email address" error={emailError}>
            {(fieldProps) => (
              <input
                {...fieldProps}
                type="email"
                autoComplete="username"
                disabled={isSubmitting}
                value={email}
                placeholder="agent@northwind-support.example"
                onChange={(e) => {
                  setEmail(e.target.value);
                  if (emailError) {
                    setEmailError(undefined);
                  }
                }}
              />
            )}
          </Field>

          <Field id="login-password" label="Password" error={passwordError}>
            {(fieldProps) => (
              <input
                {...fieldProps}
                type="password"
                autoComplete="current-password"
                disabled={isSubmitting}
                value={password}
                onChange={(e) => {
                  setPassword(e.target.value);
                  if (passwordError) {
                    setPasswordError(undefined);
                  }
                }}
              />
            )}
          </Field>

          {serverError && (
            <p className="field__error" role="alert" style={{ marginBottom: '16px' }}>
              {serverError}
            </p>
          )}

          <button
            type="submit"
            className="button button--primary"
            style={{ width: '100%', marginTop: '8px' }}
            disabled={isSubmitting}
          >
            {isSubmitting ? 'Signing in...' : 'Sign in'}
          </button>
        </form>
      </div>
    </section>
  );
}
