/**
 * MSW server setup for Vitest tests.
 *
 * Uses the handlers defined in ./handlers.ts.
 */

import { setupServer } from 'msw/node';
import { handlers } from './handlers';

export const server = setupServer(...handlers);
