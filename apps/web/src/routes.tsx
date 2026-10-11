import { createBrowserRouter, Navigate } from 'react-router-dom';
import { App } from './App';
import { LoginPage } from './features/auth/LoginPage';
import { ProtectedRoute } from './features/auth/ProtectedRoute';
import { NotFoundPage } from './features/NotFoundPage';
import { CustomerDetailPage } from './features/customers/CustomerDetailPage';
import { CustomerListPage } from './features/customers/CustomerListPage';
import { CreateTicketPage } from './features/tickets/CreateTicketPage';
import { TicketDetailPage } from './features/tickets/TicketDetailPage';
import { TicketListPage } from './features/tickets/TicketListPage';

export const router = createBrowserRouter([
  {
    path: '/',
    element: <App />,
    children: [
      { index: true, element: <Navigate to="/tickets" replace /> },
      { path: 'login', element: <LoginPage /> },
      {
        path: 'tickets',
        element: (
          <ProtectedRoute>
            <TicketListPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'tickets/new',
        element: (
          <ProtectedRoute>
            <CreateTicketPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'tickets/:id',
        element: (
          <ProtectedRoute>
            <TicketDetailPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'customers',
        element: (
          <ProtectedRoute>
            <CustomerListPage />
          </ProtectedRoute>
        ),
      },
      {
        path: 'customers/:id',
        element: (
          <ProtectedRoute>
            <CustomerDetailPage />
          </ProtectedRoute>
        ),
      },
      { path: '*', element: <NotFoundPage /> },
    ],
  },
]);
