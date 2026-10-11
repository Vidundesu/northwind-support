import { useState } from 'react';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { toErrorMessage } from '../../api/client';
import { useAuth } from '../../auth/useAuth';
import { ErrorMessage } from '../../components/ErrorMessage';
import { Field } from '../../components/Field';

interface LoginErrors {
  email?: string;
  password?: string;
}

interface RedirectState {
  from?: { pathname?: string; search?: string; hash?: string };
}

const defaultDestination = '/tickets';
const emailPattern = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/** Where to go after signing in: the page the visitor wanted, else the ticket list. */
function destinationFrom(state: unknown): string {
  const from = (state as RedirectState | null)?.from;
  const pathname = from?.pathname;

  // Only same-site paths, and never bounce back to the login page itself.
  if (!pathname || !pathname.startsWith('/') || pathname.startsWith('//') || pathname === '/login') {
    return defaultDestination;
  }

  return `${pathname}${from?.search ?? ''}${from?.hash ?? ''}`;
}

function validate(email: string, password: string): LoginErrors {
  const errors: LoginErrors = {};

  if (!email.trim()) {
    errors.email = 'Enter your email address.';
  } else if (!emailPattern.test(email.trim())) {
    errors.email = 'Enter a valid email address.';
  }

  if (!password) {
    errors.password = 'Enter your password.';
  }

  return errors;
}

export function LoginPage() {
  const { isAuthenticated, login } = useAuth();
  const navigate = useNavigate();
  const location = useLocation();
  const destination = destinationFrom(location.state);

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [errors, setErrors] = useState<LoginErrors>({});
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<string>();

  if (isAuthenticated) {
    return <Navigate to={destination} replace />;
  }

  const submit = async (event: React.FormEvent<HTMLFormElement>) => {
    event.preventDefault();

    const validationErrors = validate(email, password);
    setErrors(validationErrors);
    setSubmitError(undefined);

    if (Object.keys(validationErrors).length > 0) {
      return;
    }

    setIsSubmitting(true);

    try {
      await login({ email: email.trim(), password });
      void navigate(destination, { replace: true });
    } catch (caught) {
      setSubmitError(toErrorMessage(caught, 'Could not sign in. Please try again.'));
      setIsSubmitting(false);
    }
  };

  return (
    <main className="login">
      <form className="card login__card" onSubmit={(event) => void submit(event)} noValidate>
        <h1>Northwind Support</h1>
        <p className="page-header__subtitle">Sign in with your agent account.</p>

        {submitError && <ErrorMessage message={submitError} />}

        <Field id="login-email" label="Email" error={errors.email}>
          {(props) => (
            <input
              {...props}
              type="email"
              autoComplete="username"
              value={email}
              onChange={(event) => setEmail(event.target.value)}
            />
          )}
        </Field>

        <Field id="login-password" label="Password" error={errors.password}>
          {(props) => (
            <input
              {...props}
              type="password"
              autoComplete="current-password"
              value={password}
              onChange={(event) => setPassword(event.target.value)}
            />
          )}
        </Field>

        <button type="submit" className="button button--primary" disabled={isSubmitting}>
          {isSubmitting ? 'Signing in...' : 'Sign in'}
        </button>
      </form>
    </main>
  );
}
