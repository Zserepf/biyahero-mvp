import Link from 'next/link';

const features = [
  {
    title: 'Route Plotter',
    description: 'Plot and browse jeepney, bus, UV Express, and tricycle routes across the Philippines.',
    href: '/browse',
    icon: '🗺️',
    color: 'bg-blue-50 border-blue-200 hover:bg-blue-100',
  },
  {
    title: 'Fare Calculator',
    description: 'Calculate LTFRB-compliant fares with distance, vehicle type, and discount support.',
    href: '/fare',
    icon: '💰',
    color: 'bg-green-50 border-green-200 hover:bg-green-100',
  },
  {
    title: 'Commuter Heatmap',
    description: 'Signal where you\'re waiting for a ride. Help drivers find passengers in real time.',
    href: '/waiting',
    icon: '📍',
    color: 'bg-orange-50 border-orange-200 hover:bg-orange-100',
  },
  {
    title: 'Driver Dashboard',
    description: 'See real-time demand heatmap and receive instant payment confirmations.',
    href: '/driver',
    icon: '🚐',
    color: 'bg-purple-50 border-purple-200 hover:bg-purple-100',
  },
  {
    title: 'Create Route',
    description: 'Contribute a new transit route with waypoints plotted on the map.',
    href: '/create',
    icon: '✏️',
    color: 'bg-teal-50 border-teal-200 hover:bg-teal-100',
  },
  {
    title: 'Admin Panel',
    description: 'Manage users, moderate routes, and monitor system health.',
    href: '/users',
    icon: '⚙️',
    color: 'bg-gray-50 border-gray-200 hover:bg-gray-100',
  },
];

export default function Home() {
  return (
    <main className="min-h-screen bg-gradient-to-b from-blue-50 to-white">
      {/* Header */}
      <header className="border-b bg-white/80 backdrop-blur-sm sticky top-0 z-10">
        <div className="max-w-6xl mx-auto px-4 py-4 flex items-center justify-between">
          <div className="flex items-center gap-2">
            <span className="text-2xl">🇵🇭</span>
            <h1 className="text-xl font-bold text-blue-600">BiyaHero</h1>
          </div>
          <nav className="flex gap-3">
            <Link
              href="/login"
              className="px-4 py-2 text-sm font-medium text-blue-600 hover:text-blue-800 transition-colors"
            >
              Log In
            </Link>
            <Link
              href="/login"
              className="px-4 py-2 text-sm font-medium bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors"
            >
              Sign Up
            </Link>
          </nav>
        </div>
      </header>

      {/* Hero */}
      <section className="max-w-6xl mx-auto px-4 pt-16 pb-12 text-center">
        <h2 className="text-4xl md:text-5xl font-bold text-gray-900 leading-tight">
          Navigate Philippine Transit
          <br />
          <span className="text-blue-600">Like a Hero</span>
        </h2>
        <p className="mt-4 text-lg text-gray-600 max-w-2xl mx-auto">
          Community-sourced routes, LTFRB fare calculator, real-time demand heatmaps,
          and instant payment notifications — all in one offline-ready PWA.
        </p>
        <div className="mt-8 flex gap-4 justify-center flex-wrap">
          <Link
            href="/fare"
            className="px-6 py-3 bg-blue-600 text-white font-semibold rounded-xl hover:bg-blue-700 transition-colors shadow-lg shadow-blue-200"
          >
            Calculate Fare
          </Link>
          <Link
            href="/browse"
            className="px-6 py-3 bg-white text-blue-600 font-semibold rounded-xl border-2 border-blue-200 hover:border-blue-400 transition-colors"
          >
            Browse Routes
          </Link>
        </div>
      </section>

      {/* Feature Cards */}
      <section className="max-w-6xl mx-auto px-4 pb-20">
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {features.map((feature) => (
            <Link
              key={feature.href}
              href={feature.href}
              className={`block p-6 rounded-xl border-2 transition-all duration-200 ${feature.color}`}
            >
              <div className="text-3xl mb-3">{feature.icon}</div>
              <h3 className="text-lg font-semibold text-gray-900">{feature.title}</h3>
              <p className="mt-2 text-sm text-gray-600">{feature.description}</p>
            </Link>
          ))}
        </div>
      </section>

      {/* Footer */}
      <footer className="border-t bg-white py-8">
        <div className="max-w-6xl mx-auto px-4 text-center text-sm text-gray-500">
          <p>BiyaHero MVP — Built for Filipino commuters and drivers 🇵🇭</p>
          <p className="mt-1">Offline-ready • Bilingual (EN/FIL) • WCAG 2.1 AA Accessible</p>
        </div>
      </footer>
    </main>
  );
}
