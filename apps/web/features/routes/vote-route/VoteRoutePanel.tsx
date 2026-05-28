'use client';

/**
 * VoteRoutePanel — Accuracy vote UI for a route.
 *
 * Allows authenticated users to vote "still accurate" or "no longer accurate".
 * Optimistic UI updates vote counts immediately.
 *
 * Requirements: 1.5
 */

import { useState } from 'react';
import { useVoteRoute } from './useVoteRoute';
import type { VoteKind } from '../types';

interface VoteRoutePanelProps {
  routeId: string;
  voteCounts: {
    stillAccurate: number;
    noLongerAccurate: number;
  };
}

export function VoteRoutePanel({ routeId, voteCounts }: VoteRoutePanelProps) {
  const voteRoute = useVoteRoute(routeId);
  const [hasVoted, setHasVoted] = useState(false);
  const [voteError, setVoteError] = useState<string | null>(null);

  const handleVote = async (kind: VoteKind) => {
    setVoteError(null);
    try {
      await voteRoute.mutateAsync({ kind });
      setHasVoted(true);
    } catch {
      setVoteError('routes.voteFailed');
    }
  };

  return (
    <div className="flex flex-col gap-3 rounded-lg border border-gray-200 bg-white p-4">
      <h3 className="text-sm font-semibold text-gray-800">Is this route still accurate?</h3>

      {/* Vote counts */}
      <div className="flex items-center gap-4 text-sm">
        <span className="flex items-center gap-1 text-green-700">
          <span aria-hidden="true">✓</span>
          <span>{voteCounts.stillAccurate} say yes</span>
        </span>
        <span className="flex items-center gap-1 text-red-700">
          <span aria-hidden="true">✗</span>
          <span>{voteCounts.noLongerAccurate} say no</span>
        </span>
      </div>

      {/* Vote buttons */}
      {!hasVoted ? (
        <div className="flex gap-2">
          <button
            type="button"
            onClick={() => handleVote('still_accurate')}
            disabled={voteRoute.isPending}
            className="flex-1 min-h-[44px] rounded-lg bg-green-600 px-3 py-2 text-sm font-medium text-white transition-colors hover:bg-green-700 focus:outline-none focus:ring-2 focus:ring-green-300 disabled:bg-gray-400"
            aria-label="Vote: route is still accurate"
          >
            Still Accurate
          </button>
          <button
            type="button"
            onClick={() => handleVote('no_longer_accurate')}
            disabled={voteRoute.isPending}
            className="flex-1 min-h-[44px] rounded-lg bg-red-600 px-3 py-2 text-sm font-medium text-white transition-colors hover:bg-red-700 focus:outline-none focus:ring-2 focus:ring-red-300 disabled:bg-gray-400"
            aria-label="Vote: route is no longer accurate"
          >
            No Longer Accurate
          </button>
        </div>
      ) : (
        <p className="text-sm text-green-700" role="status" aria-live="polite">
          Thank you for your vote!
        </p>
      )}

      {/* Error */}
      {voteError && (
        <p className="text-sm text-red-600" role="alert">
          {voteError}
        </p>
      )}
    </div>
  );
}
