/**
 * MSW request handlers for BiyaHero frontend tests.
 *
 * Provides default happy-path responses for all feature slices.
 * Individual tests can override these with server.use(...) for error paths.
 */

import { http, HttpResponse } from 'msw';

const BASE_URL = 'http://localhost:3001';

export const handlers = [
  // ─── Auth: Login ─────────────────────────────────────────────────────
  http.post(`${BASE_URL}/v1/auth/sessions`, async ({ request }) => {
    const body = (await request.json()) as { email: string; password: string };

    if (body.email === 'test@example.com' && body.password === 'Password123!') {
      return HttpResponse.json({
        accessToken: 'mock-access-token',
        refreshToken: 'mock-refresh-token',
        expiresIn: 86400,
        user: {
          id: 'user-1',
          email: 'test@example.com',
          role: 'commuter',
          displayName: 'Test User',
          languagePreference: 'en',
        },
      });
    }

    return HttpResponse.json(
      {
        error: {
          code: 'auth.invalid_credentials',
          message: 'Invalid email or password',
        },
      },
      { status: 401 },
    );
  }),

  // ─── Auth: Me ────────────────────────────────────────────────────────
  http.get(`${BASE_URL}/v1/auth/me`, () => {
    return HttpResponse.json({
      id: 'user-1',
      email: 'test@example.com',
      role: 'commuter',
      displayName: 'Test User',
      languagePreference: 'en',
    });
  }),

  // ─── Auth: Language Preference ───────────────────────────────────────
  http.patch(`${BASE_URL}/v1/auth/me/language-preference`, () => {
    return HttpResponse.json({ languagePreference: 'en' });
  }),

  // ─── Routes: Create ──────────────────────────────────────────────────
  http.post(`${BASE_URL}/v1/routes`, async ({ request }) => {
    const body = (await request.json()) as Record<string, unknown>;

    return HttpResponse.json(
      {
        id: 'route-1',
        ownerId: 'user-1',
        name: body.name,
        vehicleType: body.vehicleType,
        baseFare: body.baseFare,
        status: 'unverified',
        waypoints: body.waypoints,
        voteCounts: { stillAccurate: 0, noLongerAccurate: 0 },
        createdAt: new Date().toISOString(),
        updatedAt: new Date().toISOString(),
      },
      { status: 201 },
    );
  }),

  // ─── Fare: Calculate ─────────────────────────────────────────────────
  http.post(`${BASE_URL}/v1/fare/:calculate`, async ({ request }) => {
    const body = (await request.json()) as {
      vehicleType?: string;
      origin?: { lat: number; lng: number };
      destination?: { lat: number; lng: number };
    };

    // Simulate invalid vehicle type error
    if (body.vehicleType && !['Jeepney', 'Bus', 'UV_Express', 'Tricycle'].includes(body.vehicleType)) {
      return HttpResponse.json(
        {
          error: {
            code: 'input.validation_failed',
            message: 'Unsupported vehicle type',
          },
        },
        { status: 422 },
      );
    }

    return HttpResponse.json({
      amountPhp: 13.0,
      distanceKm: 2.5,
      matrixVersion: 'v1',
    });
  }),

  // ─── Payments: Audio Failures ────────────────────────────────────────
  http.post(`${BASE_URL}/v1/payments/audio-failures`, () => {
    return HttpResponse.json({}, { status: 201 });
  }),
];
