'use client';

/**
 * TTS hook using Web Speech API (SpeechSynthesis) for payment audio confirmations.
 *
 * - Speaks "Payment received from [payerName], [amount] pesos" in the user's language
 * - Falls back to Filipino phrasing when the English voice is unavailable
 * - Reports audio failures to the backend when audio is muted/blocked
 *
 * Requirements: 3.3, 3.4, 3.8, 10.4
 */

import { useCallback, useRef } from 'react';
import { useLanguagePreferenceStore } from '@/infrastructure/stores/language-preference-store';
import { apiClient } from '@/infrastructure/api/client';
import { API_ENDPOINTS } from '@/infrastructure/api/endpoints';
import type { AudioFailureReport, PaymentConfirmedEvent } from './types';

interface UseAudioConfirmationReturn {
  /**
   * Attempt to play a TTS audio confirmation for the given payment event.
   * Returns true if audio played successfully, false if it failed (fallback needed).
   */
  playConfirmation: (event: PaymentConfirmedEvent) => Promise<boolean>;
}

/**
 * Format centavos to a spoken amount string.
 * e.g., 2500 → "25 pesos" or "25 piso"
 */
function formatSpokenAmount(amountCentavos: number, lang: 'en' | 'fil'): string {
  const pesos = (amountCentavos / 100).toFixed(2);
  // Remove trailing zeros for cleaner speech
  const cleanAmount = parseFloat(pesos).toString();

  if (lang === 'fil') {
    return `${cleanAmount} piso`;
  }
  return `${cleanAmount} pesos`;
}

/**
 * Build the TTS phrase for the payment confirmation.
 */
function buildPhrase(
  payerName: string,
  amountCentavos: number,
  lang: 'en' | 'fil',
): string {
  const amount = formatSpokenAmount(amountCentavos, lang);

  if (lang === 'fil') {
    return `Natanggap ang bayad mula kay ${payerName}, ${amount}`;
  }
  return `Payment received from ${payerName}, ${amount}`;
}

/**
 * Check if a voice is available for the given language.
 */
function findVoice(lang: 'en' | 'fil'): SpeechSynthesisVoice | null {
  if (typeof window === 'undefined' || !window.speechSynthesis) return null;

  const voices = window.speechSynthesis.getVoices();
  const langPrefix = lang === 'fil' ? 'fil' : 'en';

  // Try exact match first, then prefix match
  const exactMatch = voices.find(
    (v) => v.lang.toLowerCase().startsWith(langPrefix),
  );

  if (exactMatch) return exactMatch;

  // For Filipino, also try Tagalog (tl) variants
  if (lang === 'fil') {
    const tagalogMatch = voices.find(
      (v) => v.lang.toLowerCase().startsWith('tl'),
    );
    if (tagalogMatch) return tagalogMatch;
  }

  return null;
}

/**
 * Report an audio failure to the backend.
 */
async function reportAudioFailure(report: AudioFailureReport): Promise<void> {
  try {
    await apiClient.post(API_ENDPOINTS.PAYMENTS.AUDIO_FAILURES, report);
  } catch {
    // Fire-and-forget — don't block the UI if reporting fails
  }
}

export function useAudioConfirmation(): UseAudioConfirmationReturn {
  const speakingRef = useRef(false);
  const locale = useLanguagePreferenceStore((state) => state.locale);

  const playConfirmation = useCallback(
    async (event: PaymentConfirmedEvent): Promise<boolean> => {
      // Check if Speech Synthesis is available
      if (typeof window === 'undefined' || !window.speechSynthesis) {
        await reportAudioFailure({
          eventId: event.eventId,
          driverId: event.driverId,
          reason: 'unknown',
          occurredAt: new Date().toISOString(),
        });
        return false;
      }

      // Determine language: use user preference, fallback to Filipino
      let effectiveLang: 'en' | 'fil' = locale;
      let voice = findVoice(locale);

      // Req 3.4: Fall back to Filipino when English voice is missing
      if (!voice && locale === 'en') {
        effectiveLang = 'fil';
        voice = findVoice('fil');
      }

      // If no voice at all, report failure
      if (!voice) {
        await reportAudioFailure({
          eventId: event.eventId,
          driverId: event.driverId,
          reason: 'no_voice_available',
          occurredAt: new Date().toISOString(),
        });
        return false;
      }

      const phrase = buildPhrase(
        event.payerName,
        event.amountCentavos,
        effectiveLang,
      );

      return new Promise<boolean>((resolve) => {
        try {
          const utterance = new SpeechSynthesisUtterance(phrase);
          utterance.voice = voice;
          utterance.lang = voice.lang;
          utterance.rate = 0.9;
          utterance.pitch = 1.0;
          utterance.volume = 1.0;

          utterance.onend = () => {
            speakingRef.current = false;
            resolve(true);
          };

          utterance.onerror = (errorEvent) => {
            speakingRef.current = false;

            // Determine failure reason
            let reason: AudioFailureReport['reason'] = 'unknown';
            if (
              errorEvent.error === 'not-allowed' ||
              errorEvent.error === 'audio-busy'
            ) {
              reason = 'autoplay_blocked';
            }

            reportAudioFailure({
              eventId: event.eventId,
              driverId: event.driverId,
              reason,
              occurredAt: new Date().toISOString(),
            });

            resolve(false);
          };

          // Cancel any ongoing speech before starting new one
          if (speakingRef.current) {
            window.speechSynthesis.cancel();
          }

          speakingRef.current = true;
          window.speechSynthesis.speak(utterance);
        } catch {
          speakingRef.current = false;
          reportAudioFailure({
            eventId: event.eventId,
            driverId: event.driverId,
            reason: 'unknown',
            occurredAt: new Date().toISOString(),
          });
          resolve(false);
        }
      });
    },
    [locale],
  );

  return { playConfirmation };
}
