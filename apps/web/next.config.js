const createNextIntlPlugin = require("next-intl/plugin");
const withNextIntl = createNextIntlPlugin("./i18n/request.ts");

const withPWA = require("@ducanh2912/next-pwa").default({
  dest: "public",
  disable: process.env.NODE_ENV === "development",
  register: false,
  skipWaiting: false,
  runtimeCaching: [
    // Cache-first: App shell — HTML pages (navigation requests)
    {
      urlPattern: /^https?:\/\/.*\/(?!api\/).*$/,
      handler: "CacheFirst",
      options: {
        cacheName: "app-shell-pages",
        expiration: {
          maxEntries: 50,
          maxAgeSeconds: 7 * 24 * 60 * 60, // 7 days
        },
        cacheableResponse: {
          statuses: [0, 200],
        },
        matchOptions: {
          ignoreVary: true,
        },
      },
      method: "GET",
    },
    // Cache-first: Static JS assets
    {
      urlPattern: /\/_next\/static\/.+\.js$/i,
      handler: "CacheFirst",
      options: {
        cacheName: "app-shell-js",
        expiration: {
          maxEntries: 64,
          maxAgeSeconds: 30 * 24 * 60 * 60, // 30 days
        },
        cacheableResponse: {
          statuses: [0, 200],
        },
      },
      method: "GET",
    },
    // Cache-first: Static CSS assets
    {
      urlPattern: /\/_next\/static\/.+\.css$/i,
      handler: "CacheFirst",
      options: {
        cacheName: "app-shell-css",
        expiration: {
          maxEntries: 32,
          maxAgeSeconds: 30 * 24 * 60 * 60, // 30 days
        },
        cacheableResponse: {
          statuses: [0, 200],
        },
      },
      method: "GET",
    },
    // Cache-first: Fonts (woff, woff2, ttf, otf, eot)
    {
      urlPattern: /\.(?:woff|woff2|ttf|otf|eot)$/i,
      handler: "CacheFirst",
      options: {
        cacheName: "app-shell-fonts",
        expiration: {
          maxEntries: 16,
          maxAgeSeconds: 365 * 24 * 60 * 60, // 1 year
        },
        cacheableResponse: {
          statuses: [0, 200],
        },
      },
      method: "GET",
    },
    // Cache-first: Google Fonts webfonts
    {
      urlPattern: /^https:\/\/fonts\.gstatic\.com\/.*/i,
      handler: "CacheFirst",
      options: {
        cacheName: "google-fonts-webfonts",
        expiration: {
          maxEntries: 16,
          maxAgeSeconds: 365 * 24 * 60 * 60, // 1 year
        },
        cacheableResponse: {
          statuses: [0, 200],
        },
      },
      method: "GET",
    },
    // StaleWhileRevalidate: Google Fonts stylesheets
    {
      urlPattern: /^https:\/\/fonts\.googleapis\.com\/.*/i,
      handler: "StaleWhileRevalidate",
      options: {
        cacheName: "google-fonts-stylesheets",
        expiration: {
          maxEntries: 8,
          maxAgeSeconds: 7 * 24 * 60 * 60, // 7 days
        },
      },
      method: "GET",
    },
    // Cache-first: Manifest and icons
    {
      urlPattern: /\/(?:manifest\.json|icons\/.+\.png)$/i,
      handler: "CacheFirst",
      options: {
        cacheName: "app-shell-manifest-icons",
        expiration: {
          maxEntries: 10,
          maxAgeSeconds: 30 * 24 * 60 * 60, // 30 days
        },
        cacheableResponse: {
          statuses: [0, 200],
        },
      },
      method: "GET",
    },
    // Cache-first: Map tiles (common tile providers — OSM, Mapbox, etc.)
    {
      urlPattern:
        /^https?:\/\/(?:(?:[a-c]\.)?tile\.openstreetmap\.org|api\.mapbox\.com\/styles|(?:[a-d]\.)?basemaps\.cartocdn\.com)\/.*/i,
      handler: "CacheFirst",
      options: {
        cacheName: "map-tiles",
        expiration: {
          maxEntries: 500,
          maxAgeSeconds: 30 * 24 * 60 * 60, // 30 days
        },
        cacheableResponse: {
          statuses: [0, 200],
        },
      },
      method: "GET",
    },
    // StaleWhileRevalidate: Route data from the API (read path)
    {
      urlPattern: /\/v1\/routes(?:\?.*)?$/i,
      handler: "StaleWhileRevalidate",
      options: {
        cacheName: "route-data",
        expiration: {
          maxEntries: 100,
          maxAgeSeconds: 24 * 60 * 60, // 24 hours
        },
        cacheableResponse: {
          statuses: [0, 200],
        },
      },
      method: "GET",
    },
    // StaleWhileRevalidate: Individual route detail
    {
      urlPattern: /\/v1\/routes\/[a-f0-9-]+$/i,
      handler: "StaleWhileRevalidate",
      options: {
        cacheName: "route-data",
        expiration: {
          maxEntries: 100,
          maxAgeSeconds: 24 * 60 * 60, // 24 hours
        },
        cacheableResponse: {
          statuses: [0, 200],
        },
      },
      method: "GET",
    },
    // NetworkFirst: Heatmap tiles (real-time, but cached for low-bandwidth fallback)
    {
      urlPattern: /\/v1\/heatmap\/tiles/i,
      handler: "NetworkFirst",
      options: {
        cacheName: "heatmap-tiles",
        networkTimeoutSeconds: 5,
        expiration: {
          maxEntries: 50,
          maxAgeSeconds: 60, // 1 minute (ephemeral data)
        },
        cacheableResponse: {
          statuses: [0, 200],
        },
      },
      method: "GET",
    },
    // NetworkFirst: Other API calls (non-cached by default)
    {
      urlPattern: /\/v1\//i,
      handler: "NetworkFirst",
      options: {
        cacheName: "api-responses",
        networkTimeoutSeconds: 10,
        expiration: {
          maxEntries: 32,
          maxAgeSeconds: 60 * 60, // 1 hour
        },
        cacheableResponse: {
          statuses: [0, 200],
        },
      },
      method: "GET",
    },
    // Cache-first: Static images
    {
      urlPattern: /\.(?:jpg|jpeg|gif|png|svg|ico|webp)$/i,
      handler: "CacheFirst",
      options: {
        cacheName: "static-image-assets",
        expiration: {
          maxEntries: 64,
          maxAgeSeconds: 30 * 24 * 60 * 60, // 30 days
        },
        cacheableResponse: {
          statuses: [0, 200],
        },
      },
      method: "GET",
    },
  ],
});

/** @type {import('next').NextConfig} */
const nextConfig = {
  // Security headers
  async headers() {
    return [
      {
        source: "/(.*)",
        headers: [
          {
            key: "X-Content-Type-Options",
            value: "nosniff",
          },
          {
            key: "X-Frame-Options",
            value: "DENY",
          },
          {
            key: "X-XSS-Protection",
            value: "1; mode=block",
          },
          {
            key: "Referrer-Policy",
            value: "strict-origin-when-cross-origin",
          },
        ],
      },
    ];
  },

  // Asset prefix for CDN deployment (CloudFront)
  assetPrefix: process.env.NEXT_PUBLIC_ASSET_PREFIX || undefined,

  // React strict mode for development
  reactStrictMode: true,
};

module.exports = withNextIntl(withPWA(nextConfig));
