'use client';

/**
 * Fare calculator page — LTFRB-compliant fare between two map points.
 * Requirements: 2.1, 2.5, 2.9
 */

import { useState, type FormEvent } from 'react';
import dynamic from 'next/dynamic';
import Link from 'next/link';
import { fareCalculateSchema, VEHICLE_TYPES, DISCOUNT_CATEGORIES } from './schema';
import { useCalculateFare } from './useCalculateFare';
import { FareResult } from './FareResult';
import { ThemeToggle } from '@/shared/components/ThemeToggle';
import type { VehicleType, DiscountCategory, Coordinate } from './types';

const FareMapPicker = dynamic(
  () => import('./FareMapPicker').then((mod) => mod.FareMapPicker),
  {
    ssr: false,
    loading: () => (
      <div className="flex h-[320px] w-full items-center justify-center rounded-xl border border-white/10 bg-white/5">
        <div className="flex flex-col items-center gap-3">
          <span className="h-6 w-6 animate-spin rounded-full border-2 border-blue-500 border-t-transparent" />
          <p className="text-xs text-white/40">Loading map…</p>
        </div>
      </div>
    ),
  },
);

const VEHICLE_TYPE_LABELS: Record<VehicleType, string> = {
  Jeepney: 'Jeepney',
  Bus: 'Bus',
  UV_Express: 'UV Express',
  Tricycle: 'Tricycle',
};

const DISCOUNT_CATEGORY_LABELS: Record<DiscountCategory, string> = {
  regular: 'Regular',
  student: 'Student (20% off)',
  senior: 'Senior Citizen (20% off)',
  pwd: 'PWD (20% off)',
};

export function CalculateFarePage() {
  const [origin, setOrigin] = useState<Coordinate | null>(null);
  const [destination, setDestination] = useState<Coordinate | null>(null);
  const [vehicleType, setVehicleType] = useState<VehicleType | ''>('');
  const [discountCategory, setDiscountCategory] = useState<DiscountCategory>('regular');
  const [formError, setFormError] = useState<string | null>(null);
  const { calculateFare, result, isLoading, error } = useCalculateFare();

  const handleReset = () => { setOrigin(null); setDestination(null); setFormError(null); };

  const handleSubmit = async (e: FormEvent) => {
    e.preventDefault();
    setFormError(null);
    if (!origin) { setFormError('Tap the map to set your origin point.'); return; }
    if (!destination) { setFormError('Tap the map again to set your destination.'); return; }
    if (!vehicleType) { setFormError('Select a vehicle type.'); return; }

    const parsed = fareCalculateSchema.safeParse({ origin, destination, vehicleType, discountCategory });
    if (!parsed.success) { setFormError(parsed.error.issues[0]?.message || 'Invalid input.'); return; }

    try { await calculateFare(parsed.data); } catch { /* captured in hook */ }
  };

  const pinStep = !origin ? 'origin' : !destination ? 'destination' : 'done';

  return (
    <div className="min-h-screen bg-gradient-to-br from-blue-50 via-white to-indigo-50 dark:from-slate-900 dark:via-blue-950 dark:to-slate-900">
      {/* Header */}
      <header className="sticky top-0 z-50 border-b border-black/10 dark:border-white/10 bg-white/80 dark:bg-slate-900/80 backdrop-blur-md">
        <div className="mx-auto flex max-w-2xl items-center gap-4 px-4 py-3">
          <Link
            href="/"
            className="flex h-9 w-9 items-center justify-center rounded-xl bg-gray-100 dark:bg-white/10 text-gray-700 dark:text-white transition hover:bg-gray-200 dark:hover:bg-white/20"
            aria-label="Back to home"
          >
            <svg className="h-4 w-4" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2.5}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M15 19l-7-7 7-7" />
            </svg>
          </Link>
          <div className="flex-1">
            <h1 className="text-base font-bold text-gray-900 dark:text-white leading-none">Fare Calculator</h1>
            <p className="mt-0.5 text-xs text-gray-500 dark:text-white/50">LTFRB-compliant fare between any two points</p>
          </div>
          <ThemeToggle />
        </div>
      </header>

      <div className="mx-auto max-w-2xl px-4 py-6 space-y-4">

        {/* Step indicator */}
        <div className="flex items-center gap-3">
          {[
            { key: 'origin',      label: 'Set Origin',      done: !!origin },
            { key: 'destination', label: 'Set Destination', done: !!destination },
            { key: 'done',        label: 'Calculate',       done: false },
          ].map((step, i) => (
            <div key={step.key} className="flex items-center gap-2">
              {i > 0 && <div className="h-px w-6 bg-gray-200 dark:bg-white/10" />}
              <div className={`flex items-center gap-1.5 rounded-full px-3 py-1 text-xs font-medium transition ${
                step.done
                  ? 'bg-emerald-500/20 text-emerald-600 dark:text-emerald-400 ring-1 ring-emerald-500/30'
                  : pinStep === step.key
                  ? 'bg-blue-500/20 text-blue-600 dark:text-blue-300 ring-1 ring-blue-500/30'
                  : 'bg-gray-100 dark:bg-white/5 text-gray-400 dark:text-white/30'
              }`}>
                {step.done && (
                  <svg className="h-3 w-3" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={3}>
                    <path strokeLinecap="round" strokeLinejoin="round" d="M5 13l4 4L19 7" />
                  </svg>
                )}
                {step.label}
              </div>
            </div>
          ))}
        </div>

        {/* Map card */}
        <div className="rounded-2xl border border-gray-200 dark:border-white/10 bg-white dark:bg-white/5 p-4 shadow-sm dark:shadow-none backdrop-blur-sm">
          {/* Instruction */}
          <div className="mb-3 flex items-center gap-2 rounded-xl border border-blue-500/20 bg-blue-500/10 px-3 py-2">
            <svg className="h-4 w-4 shrink-0 text-blue-500 dark:text-blue-400" fill="none" viewBox="0 0 24 24" stroke="currentColor" strokeWidth={2}>
              <path strokeLinecap="round" strokeLinejoin="round" d="M13 16h-1v-4h-1m1-4h.01M21 12a9 9 0 11-18 0 9 9 0 0118 0z" />
            </svg>
            <p className="text-xs text-blue-600 dark:text-blue-300">
              {!origin
                ? 'Tap the map to set your origin'
                : !destination
                ? 'Tap again to set your destination'
                : 'Both pins set — fill in the form below'}
            </p>
          </div>

          {/* Map */}
          <div className="overflow-hidden rounded-xl border border-gray-200 dark:border-white/10">
            <FareMapPicker
              origin={origin}
              destination={destination}
              onOriginChange={setOrigin}
              onDestinationChange={setDestination}
            />
          </div>

          {/* Pin summary + reset */}
          <div className="mt-3 flex items-center justify-between">
            <div className="flex flex-wrap gap-2">
              {origin ? (
                <span className="inline-flex items-center gap-1.5 rounded-full bg-emerald-500/20 px-2.5 py-1 text-xs font-medium text-emerald-600 dark:text-emerald-400 ring-1 ring-emerald-500/30">
                  <span className="h-1.5 w-1.5 rounded-full bg-emerald-500 dark:bg-emerald-400" />
                  {origin.lat.toFixed(4)}, {origin.lng.toFixed(4)}
                </span>
              ) : (
                <span className="text-xs text-gray-400 dark:text-white/30 italic">No origin set</span>
              )}
              {destination && (
                <span className="inline-flex items-center gap-1.5 rounded-full bg-red-500/20 px-2.5 py-1 text-xs font-medium text-red-500 dark:text-red-400 ring-1 ring-red-500/30">
                  <span className="h-1.5 w-1.5 rounded-full bg-red-500 dark:bg-red-400" />
                  {destination.lat.toFixed(4)}, {destination.lng.toFixed(4)}
                </span>
              )}
            </div>
            {(origin || destination) && (
              <button
                type="button"
                onClick={handleReset}
                className="rounded-lg px-3 py-1.5 text-xs font-medium text-gray-400 dark:text-white/40 transition hover:bg-gray-100 dark:hover:bg-white/10 hover:text-gray-600 dark:hover:text-white/70"
              >
                Reset Pins
              </button>
            )}
          </div>
        </div>

        {/* Form card */}
        <form onSubmit={handleSubmit} noValidate aria-label="Fare calculator form">
          <div className="rounded-2xl border border-gray-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-sm dark:shadow-none backdrop-blur-sm space-y-4">
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2">
              {/* Vehicle Type */}
              <div className="flex flex-col gap-1.5">
                <label htmlFor="vehicle-type" className="text-sm font-medium text-gray-700 dark:text-white/80">
                  Vehicle Type
                </label>
                <select
                  id="vehicle-type"
                  value={vehicleType}
                  onChange={(e) => setVehicleType(e.target.value as VehicleType)}
                  className="min-h-[44px] w-full rounded-xl border border-gray-200 dark:border-white/10 bg-white dark:bg-slate-800 px-4 py-2.5 text-sm text-gray-900 dark:text-white focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/30 transition"
                >
                  <option value="">Select vehicle type</option>
                  {VEHICLE_TYPES.map((type) => (
                    <option key={type} value={type}>{VEHICLE_TYPE_LABELS[type]}</option>
                  ))}
                </select>
              </div>

              {/* Discount Category */}
              <div className="flex flex-col gap-1.5">
                <label htmlFor="discount-category" className="text-sm font-medium text-gray-700 dark:text-white/80">
                  Discount Category
                </label>
                <select
                  id="discount-category"
                  value={discountCategory}
                  onChange={(e) => setDiscountCategory(e.target.value as DiscountCategory)}
                  className="min-h-[44px] w-full rounded-xl border border-gray-200 dark:border-white/10 bg-white dark:bg-slate-800 px-4 py-2.5 text-sm text-gray-900 dark:text-white focus:border-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/30 transition"
                >
                  {DISCOUNT_CATEGORIES.map((cat) => (
                    <option key={cat} value={cat}>{DISCOUNT_CATEGORY_LABELS[cat]}</option>
                  ))}
                </select>
              </div>
            </div>

            {/* Error */}
            {(formError || error) && (
              <div className="flex items-center gap-2 rounded-xl border border-red-500/20 bg-red-500/10 px-4 py-3" role="alert">
                <svg className="h-4 w-4 shrink-0 text-red-500 dark:text-red-400" fill="currentColor" viewBox="0 0 20 20">
                  <path fillRule="evenodd" d="M18 10a8 8 0 11-16 0 8 8 0 0116 0zm-7 4a1 1 0 11-2 0 1 1 0 012 0zm-1-9a1 1 0 00-1 1v4a1 1 0 102 0V6a1 1 0 00-1-1z" clipRule="evenodd" />
                </svg>
                <p className="text-sm text-red-500 dark:text-red-400">{formError || error}</p>
              </div>
            )}

            {/* Submit */}
            <button
              type="submit"
              disabled={isLoading}
              className="min-h-[44px] w-full rounded-xl bg-blue-600 px-4 py-3 font-semibold text-white shadow-lg shadow-blue-500/20 transition hover:bg-blue-500 focus:outline-none focus:ring-2 focus:ring-blue-500/50 disabled:cursor-not-allowed disabled:opacity-40"
            >
              {isLoading ? (
                <span className="flex items-center justify-center gap-2">
                  <span className="h-4 w-4 animate-spin rounded-full border-2 border-white border-t-transparent" />
                  Calculating…
                </span>
              ) : (
                'Calculate Fare'
              )}
            </button>
          </div>
        </form>

        {/* Result */}
        {result && (
          <div className="rounded-2xl border border-gray-200 dark:border-white/10 bg-white dark:bg-white/5 p-5 shadow-sm dark:shadow-none backdrop-blur-sm">
            <FareResult result={result} />
          </div>
        )}
      </div>
    </div>
  );
}
