/**
 * Component tests for the Auth Login feature slice.
 *
 * Happy path: successful login with valid credentials.
 * Error path: invalid credentials show server error message.
 *
 * Requirements: 5.1 (registration/login flow)
 */

import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { LoginForm } from '@/features/auth/login/LoginForm';

function renderWithProviders(ui: React.ReactElement) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });

  return render(
    <QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>,
  );
}

describe('LoginForm', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('renders email and password fields with submit button', () => {
    renderWithProviders(<LoginForm />);

    expect(screen.getByLabelText('forms.email')).toBeInTheDocument();
    expect(screen.getByLabelText('forms.password')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'auth.login' })).toBeInTheDocument();
  });

  it('happy path: logs in successfully with valid credentials', async () => {
    const user = userEvent.setup();
    const onSuccess = vi.fn();

    renderWithProviders(<LoginForm onSuccess={onSuccess} />);

    await user.type(screen.getByLabelText('forms.email'), 'test@example.com');
    await user.type(screen.getByLabelText('forms.password'), 'Password123!');
    await user.click(screen.getByRole('button', { name: 'auth.login' }));

    await waitFor(() => {
      expect(onSuccess).toHaveBeenCalled();
    });

    // Verify tokens are stored
    expect(localStorage.getItem('biyahero_access_token')).toBe('mock-access-token');
    expect(localStorage.getItem('biyahero_refresh_token')).toBe('mock-refresh-token');
  });

  it('error path: shows server error on invalid credentials', async () => {
    const user = userEvent.setup();

    renderWithProviders(<LoginForm />);

    await user.type(screen.getByLabelText('forms.email'), 'wrong@example.com');
    await user.type(screen.getByLabelText('forms.password'), 'WrongPass!');
    await user.click(screen.getByRole('button', { name: 'auth.login' }));

    await waitFor(() => {
      expect(screen.getByRole('alert')).toHaveTextContent('Invalid email or password');
    });
  });

  it('shows client-side validation error for empty email', async () => {
    const user = userEvent.setup();

    renderWithProviders(<LoginForm />);

    // Leave email empty, fill password
    await user.type(screen.getByLabelText('forms.password'), 'Password123!');
    await user.click(screen.getByRole('button', { name: 'auth.login' }));

    await waitFor(() => {
      expect(screen.getByText('forms.required')).toBeInTheDocument();
    });
  });
});
