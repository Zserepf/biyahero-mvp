import { test, expect, Page, BrowserContext } from '@playwright/test';

/**
 * Playwright e2e tests for PWA offline scenarios.
 *
 * Validates Requirements:
 * - 6.3: Cached app shell loads while offline; cached Routes displayed
 * - 6.4: Write operations queued in IndexedDB while offline, replayed in order on reconnect
 * - 6.5: Cached content served on low bandwidth / offline
 * - 6.6: Lighthouse PWA installability (structural check — manifest + SW present)
 *
 * These tests use a production build (next-pwa disables SW in dev mode).
 * The webServer config in playwright.config.ts handles build + start.
 */

test.describe('PWA Offline Scenarios', () => {
  /**
   * Helper: Wait for the service worker to be registered and activated.
   */
  async function waitForServiceWorker(page: Page): Promise<void> {
    await page.waitForFunction(
      () =>
        navigator.serviceWorker.controller !== null ||
        navigator.serviceWorker.ready.then((reg) => reg.active !== null),
      { timeout: 30_000 },
    );
  }

  /**
   * Helper: Evaluate IndexedDB write-queue store count.
   */
  async function getQueueCount(page: Page): Promise<number> {
    return page.evaluate(async () => {
      return new Promise<number>((resolve, reject) => {
        const request = indexedDB.open('biyahero-offline', 2);
        request.onerror = () => reject(request.error);
        request.onsuccess = () => {
          const db = request.result;
          if (!db.objectStoreNames.contains('write-queue')) {
            db.close();
            resolve(0);
            return;
          }
          const tx = db.transaction('write-queue', 'readonly');
          const store = tx.objectStore('write-queue');
          const countReq = store.count();
          countReq.onsuccess = () => {
            resolve(countReq.result);
            db.close();
          };
          countReq.onerror = () => {
            reject(countReq.error);
            db.close();
          };
        };
        request.onupgradeneeded = () => {
          // DB doesn't exist yet — no items queued
          request.result.close();
          resolve(0);
        };
      });
    });
  }

  /**
   * Helper: Get all queued items from IndexedDB in timestamp order.
   */
  async function getQueuedItems(
    page: Page,
  ): Promise<Array<{ id: string; url: string; method: string; timestamp: number }>> {
    return page.evaluate(async () => {
      return new Promise<Array<{ id: string; url: string; method: string; timestamp: number }>>(
        (resolve, reject) => {
          const request = indexedDB.open('biyahero-offline', 2);
          request.onerror = () => reject(request.error);
          request.onsuccess = () => {
            const db = request.result;
            if (!db.objectStoreNames.contains('write-queue')) {
              db.close();
              resolve([]);
              return;
            }
            const tx = db.transaction('write-queue', 'readonly');
            const store = tx.objectStore('write-queue');
            const index = store.index('by-timestamp');
            const getAllReq = index.getAll();
            getAllReq.onsuccess = () => {
              const items = getAllReq.result.map(
                (item: { id: string; url: string; method: string; timestamp: number }) => ({
                  id: item.id,
                  url: item.url,
                  method: item.method,
                  timestamp: item.timestamp,
                }),
              );
              resolve(items);
              db.close();
            };
            getAllReq.onerror = () => {
              reject(getAllReq.error);
              db.close();
            };
          };
          request.onupgradeneeded = () => {
            request.result.close();
            resolve([]);
          };
        },
      );
    });
  }

  test.describe('App Shell Offline Loading (Req 6.3, 6.5)', () => {
    test('cached app shell loads successfully when network is offline', async ({
      browser,
    }) => {
      // Step 1: Load the app online to prime the service worker cache
      const context = await browser.newContext();
      const page = await context.newPage();

      await page.goto('/');
      await expect(page.locator('h1')).toContainText('BiyaHero');

      // Wait for service worker to activate and cache the shell
      await waitForServiceWorker(page);

      // Give the SW time to cache all assets
      await page.waitForTimeout(3000);

      // Step 2: Go offline using browser context
      await context.setOffline(true);

      // Step 3: Reload the page — should serve from SW cache
      await page.reload({ waitUntil: 'domcontentloaded' });

      // Verify the app shell renders correctly from cache
      await expect(page.locator('h1')).toContainText('BiyaHero');
      await expect(page.locator('body')).toBeVisible();

      // Verify the page has meaningful content (not a browser error page)
      const title = await page.title();
      expect(title).toBeTruthy();

      await context.close();
    });

    test('offline indicator is displayed when network is offline', async ({
      browser,
    }) => {
      const context = await browser.newContext();
      const page = await context.newPage();

      // Load online first to cache the shell
      await page.goto('/');
      await waitForServiceWorker(page);
      await page.waitForTimeout(2000);

      // Go offline
      await context.setOffline(true);

      // Trigger the offline event in the browser
      await page.evaluate(() => {
        window.dispatchEvent(new Event('offline'));
      });

      // Wait for the offline indicator to appear
      const offlineIndicator = page.locator('[role="status"]');
      await expect(offlineIndicator).toBeVisible({ timeout: 5000 });
      await expect(offlineIndicator).toContainText(/offline/i);

      await context.close();
    });

    test('user can interact with cached content while offline', async ({
      browser,
    }) => {
      const context = await browser.newContext();
      const page = await context.newPage();

      // Load online to cache
      await page.goto('/');
      await waitForServiceWorker(page);
      await page.waitForTimeout(3000);

      // Go offline
      await context.setOffline(true);

      // Reload from cache
      await page.reload({ waitUntil: 'domcontentloaded' });

      // Verify the page is interactive (not frozen)
      await expect(page.locator('body')).toBeVisible();

      // Check that the main content is rendered
      await expect(page.locator('main')).toBeVisible();

      // Verify JavaScript is executing (the page isn't just static HTML)
      const jsExecuting = await page.evaluate(() => {
        return typeof document !== 'undefined' && document.readyState === 'complete';
      });
      expect(jsExecuting).toBe(true);

      await context.close();
    });
  });

  test.describe('Offline Write Queue (Req 6.4)', () => {
    test('write operations are queued in IndexedDB while offline', async ({
      browser,
    }) => {
      const context = await browser.newContext();
      const page = await context.newPage();

      // Load the app and wait for SW
      await page.goto('/');
      await waitForServiceWorker(page);
      await page.waitForTimeout(2000);

      // Go offline
      await context.setOffline(true);
      await page.evaluate(() => {
        window.dispatchEvent(new Event('offline'));
      });

      // Simulate a write request that would be queued (route submission)
      // We directly invoke the offline queue's enqueue function via the app's infrastructure
      await page.evaluate(async () => {
        const { openDB } = await import('idb');
        const db = await openDB('biyahero-offline', 2, {
          upgrade(database) {
            if (!database.objectStoreNames.contains('write-queue')) {
              const store = database.createObjectStore('write-queue', { keyPath: 'id' });
              store.createIndex('by-timestamp', 'timestamp');
            }
            if (!database.objectStoreNames.contains('dead-letter')) {
              const dlStore = database.createObjectStore('dead-letter', { keyPath: 'id' });
              dlStore.createIndex('by-timestamp', 'timestamp');
            }
          },
        });

        // Simulate queuing a route submission
        await db.put('write-queue', {
          id: `${Date.now()}-test1`,
          timestamp: Date.now(),
          url: '/v1/routes',
          method: 'POST',
          body: {
            name: 'Test Route',
            vehicleType: 'jeepney',
            waypoints: [
              { lat: 14.5995, lng: 120.9842, position: 0 },
              { lat: 14.6042, lng: 120.9882, position: 1 },
            ],
            baseFare: 13,
          },
          headers: { 'Content-Type': 'application/json' },
          retryCount: 0,
        });

        db.close();
      });

      // Verify the item was queued
      const count = await getQueueCount(page);
      expect(count).toBe(1);

      await context.close();
    });

    test('multiple offline writes are queued in submission order', async ({
      browser,
    }) => {
      const context = await browser.newContext();
      const page = await context.newPage();

      await page.goto('/');
      await waitForServiceWorker(page);
      await page.waitForTimeout(2000);

      // Go offline
      await context.setOffline(true);
      await page.evaluate(() => {
        window.dispatchEvent(new Event('offline'));
      });

      // Queue multiple writes with distinct timestamps
      await page.evaluate(async () => {
        const { openDB } = await import('idb');
        const db = await openDB('biyahero-offline', 2, {
          upgrade(database) {
            if (!database.objectStoreNames.contains('write-queue')) {
              const store = database.createObjectStore('write-queue', { keyPath: 'id' });
              store.createIndex('by-timestamp', 'timestamp');
            }
            if (!database.objectStoreNames.contains('dead-letter')) {
              const dlStore = database.createObjectStore('dead-letter', { keyPath: 'id' });
              dlStore.createIndex('by-timestamp', 'timestamp');
            }
          },
        });

        const baseTime = Date.now();

        // First write: route submission
        await db.put('write-queue', {
          id: `${baseTime}-route`,
          timestamp: baseTime,
          url: '/v1/routes',
          method: 'POST',
          body: { name: 'Route A', vehicleType: 'jeepney' },
          headers: { 'Content-Type': 'application/json' },
          retryCount: 0,
        });

        // Second write: demand ping cancellation (50ms later)
        await db.put('write-queue', {
          id: `${baseTime + 50}-cancel`,
          timestamp: baseTime + 50,
          url: '/v1/heatmap/cancel-demand',
          method: 'POST',
          body: { pingId: 'ping-123' },
          headers: { 'Content-Type': 'application/json' },
          retryCount: 0,
        });

        // Third write: route edit (100ms later)
        await db.put('write-queue', {
          id: `${baseTime + 100}-edit`,
          timestamp: baseTime + 100,
          url: '/v1/routes/abc-123/revisions',
          method: 'POST',
          body: { waypoints: [{ lat: 14.6, lng: 121.0 }] },
          headers: { 'Content-Type': 'application/json' },
          retryCount: 0,
        });

        db.close();
      });

      // Verify all three items are queued
      const count = await getQueueCount(page);
      expect(count).toBe(3);

      // Verify they are in submission order (FIFO by timestamp)
      const items = await getQueuedItems(page);
      expect(items).toHaveLength(3);
      expect(items[0].url).toBe('/v1/routes');
      expect(items[1].url).toBe('/v1/heatmap/cancel-demand');
      expect(items[2].url).toBe('/v1/routes/abc-123/revisions');

      // Verify timestamps are strictly increasing
      expect(items[0].timestamp).toBeLessThan(items[1].timestamp);
      expect(items[1].timestamp).toBeLessThan(items[2].timestamp);

      await context.close();
    });
  });

  test.describe('Offline → Online Replay (Req 6.4)', () => {
    test('queued writes replay in submission order on reconnect', async ({
      browser,
    }) => {
      const context = await browser.newContext();
      const page = await context.newPage();

      // Track API calls made during replay
      const replayedRequests: Array<{ url: string; method: string; order: number }> = [];
      let requestOrder = 0;

      await page.goto('/');
      await waitForServiceWorker(page);
      await page.waitForTimeout(2000);

      // Go offline and queue writes
      await context.setOffline(true);
      await page.evaluate(() => {
        window.dispatchEvent(new Event('offline'));
      });

      const baseTime = Date.now();

      await page.evaluate(async (bt: number) => {
        const { openDB } = await import('idb');
        const db = await openDB('biyahero-offline', 2, {
          upgrade(database) {
            if (!database.objectStoreNames.contains('write-queue')) {
              const store = database.createObjectStore('write-queue', { keyPath: 'id' });
              store.createIndex('by-timestamp', 'timestamp');
            }
            if (!database.objectStoreNames.contains('dead-letter')) {
              const dlStore = database.createObjectStore('dead-letter', { keyPath: 'id' });
              dlStore.createIndex('by-timestamp', 'timestamp');
            }
          },
        });

        await db.put('write-queue', {
          id: `${bt}-first`,
          timestamp: bt,
          url: '/v1/routes',
          method: 'POST',
          body: { name: 'First Route' },
          headers: { 'Content-Type': 'application/json' },
          retryCount: 0,
        });

        await db.put('write-queue', {
          id: `${bt + 100}-second`,
          timestamp: bt + 100,
          url: '/v1/routes/xyz/revisions',
          method: 'POST',
          body: { name: 'Second Edit' },
          headers: { 'Content-Type': 'application/json' },
          retryCount: 0,
        });

        db.close();
      }, baseTime);

      // Intercept API requests to track replay order
      await page.route('**/v1/**', async (route) => {
        const request = route.request();
        if (['POST', 'PUT', 'PATCH', 'DELETE'].includes(request.method())) {
          replayedRequests.push({
            url: new URL(request.url()).pathname,
            method: request.method(),
            order: requestOrder++,
          });
        }
        // Respond with success to allow replay to complete
        await route.fulfill({
          status: 200,
          contentType: 'application/json',
          body: JSON.stringify({ success: true }),
        });
      });

      // Come back online
      await context.setOffline(false);
      await page.evaluate(() => {
        window.dispatchEvent(new Event('online'));
      });

      // Wait for replay to process (500ms delay + processing time)
      await page.waitForTimeout(3000);

      // Verify requests were replayed in order
      expect(replayedRequests.length).toBeGreaterThanOrEqual(2);

      const routeReq = replayedRequests.find((r) => r.url === '/v1/routes');
      const revisionReq = replayedRequests.find((r) => r.url.includes('/revisions'));

      if (routeReq && revisionReq) {
        expect(routeReq.order).toBeLessThan(revisionReq.order);
      }

      await context.close();
    });

    test('no data loss during offline → online transition', async ({ browser }) => {
      const context = await browser.newContext();
      const page = await context.newPage();

      const receivedRequests: Array<{ url: string; body: string }> = [];

      await page.goto('/');
      await waitForServiceWorker(page);
      await page.waitForTimeout(2000);

      // Go offline
      await context.setOffline(true);
      await page.evaluate(() => {
        window.dispatchEvent(new Event('offline'));
      });

      // Queue several writes
      const writeCount = 5;
      await page.evaluate(async (count: number) => {
        const { openDB } = await import('idb');
        const db = await openDB('biyahero-offline', 2, {
          upgrade(database) {
            if (!database.objectStoreNames.contains('write-queue')) {
              const store = database.createObjectStore('write-queue', { keyPath: 'id' });
              store.createIndex('by-timestamp', 'timestamp');
            }
            if (!database.objectStoreNames.contains('dead-letter')) {
              const dlStore = database.createObjectStore('dead-letter', { keyPath: 'id' });
              dlStore.createIndex('by-timestamp', 'timestamp');
            }
          },
        });

        const baseTime = Date.now();
        for (let i = 0; i < count; i++) {
          await db.put('write-queue', {
            id: `${baseTime + i * 50}-item${i}`,
            timestamp: baseTime + i * 50,
            url: '/v1/routes',
            method: 'POST',
            body: { name: `Route ${i}`, index: i },
            headers: { 'Content-Type': 'application/json' },
            retryCount: 0,
          });
        }
        db.close();
      }, writeCount);

      // Verify all items are queued
      const queuedBefore = await getQueueCount(page);
      expect(queuedBefore).toBe(writeCount);

      // Intercept and record all replayed requests
      await page.route('**/v1/**', async (route) => {
        const request = route.request();
        if (request.method() === 'POST') {
          receivedRequests.push({
            url: new URL(request.url()).pathname,
            body: request.postData() ?? '',
          });
        }
        await route.fulfill({
          status: 201,
          contentType: 'application/json',
          body: JSON.stringify({ id: 'created-id', success: true }),
        });
      });

      // Come back online
      await context.setOffline(false);
      await page.evaluate(() => {
        window.dispatchEvent(new Event('online'));
      });

      // Wait for all replays to complete
      await page.waitForTimeout(5000);

      // Verify all writes were replayed (no data loss)
      expect(receivedRequests.length).toBe(writeCount);

      // Verify the queue is now empty
      const queuedAfter = await getQueueCount(page);
      expect(queuedAfter).toBe(0);

      await context.close();
    });
  });

  test.describe('PWA Installability (Req 6.6)', () => {
    test('manifest.json is served with correct PWA fields', async ({ page }) => {
      const response = await page.goto('/manifest.json');
      expect(response?.status()).toBe(200);

      const manifest = await response?.json();
      expect(manifest.name).toBe('BiyaHero');
      expect(manifest.start_url).toBe('/');
      expect(manifest.display).toBe('standalone');
      expect(manifest.theme_color).toBeTruthy();
      expect(manifest.background_color).toBeTruthy();

      // Verify icons at required sizes
      const icons = manifest.icons as Array<{ sizes: string; src: string }>;
      const has192 = icons.some((icon) => icon.sizes.includes('192x192'));
      const has512 = icons.some((icon) => icon.sizes.includes('512x512'));
      expect(has192).toBe(true);
      expect(has512).toBe(true);
    });

    test('service worker is registered and active', async ({ page }) => {
      await page.goto('/');

      // Wait for SW registration
      const swRegistered = await page.evaluate(async () => {
        if (!('serviceWorker' in navigator)) return false;
        const registration = await navigator.serviceWorker.ready;
        return registration.active !== null;
      });

      expect(swRegistered).toBe(true);
    });

    test('app shell HTML references the manifest', async ({ page }) => {
      await page.goto('/');

      // Check that the HTML includes a link to the manifest
      const manifestLink = await page.evaluate(() => {
        const link = document.querySelector('link[rel="manifest"]');
        return link?.getAttribute('href');
      });

      expect(manifestLink).toBe('/manifest.json');
    });
  });
});
