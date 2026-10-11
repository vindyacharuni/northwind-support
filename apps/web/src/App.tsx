import { Link, NavLink, Outlet, useNavigate } from 'react-router-dom';
import { useAuth } from './features/auth/AuthContext';

/** Application shell: the header, the navigation, and wherever the router puts the page. */
export function App() {
  const { user, logout } = useAuth();
  const navigate = useNavigate();

  const handleLogout = () => {
    logout();
    navigate('/login', { replace: true });
  };

  return (
    <div className="app">
      <header className="app__header" style={{ justifyContent: 'space-between' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: '32px' }}>
          <Link className="app__brand" to="/tickets">
            Northwind Support
          </Link>

          {user && (
            <nav className="app__nav" aria-label="Main">
              <NavLink to="/tickets">Tickets</NavLink>
              <NavLink to="/customers">Customers</NavLink>
            </nav>
          )}
        </div>

        {user && (
          <div style={{ display: 'flex', alignItems: 'center', gap: '16px' }}>
            <span style={{ fontSize: '0.9rem', color: 'var(--colour-muted)' }}>
              Signed in as <strong>{user.fullName}</strong>
            </span>
            <button
              type="button"
              className="button button--ghost"
              onClick={handleLogout}
              style={{ fontSize: '0.85rem', padding: '4px 10px' }}
            >
              Sign out
            </button>
          </div>
        )}
      </header>

      <main className="app__main">
        <Outlet />
      </main>
    </div>
  );
}
