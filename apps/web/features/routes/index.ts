/**
 * Routes feature barrel export.
 *
 * Re-exports public API for the routes feature slices.
 */

// Types
export type {
  VehicleType,
  RouteStatus,
  VoteKind,
  RevisionStatus,
  Waypoint,
  RouteDto,
  CreateRouteRequest,
  CreateRouteResponse,
  BboxQuery,
  RouteListResponse,
  CreateRevisionRequest,
  CreateRevisionResponse,
  CastVoteRequest,
  CastVoteResponse,
} from './types';

// Schemas
export {
  createRouteSchema,
  createRevisionSchema,
  castVoteSchema,
  waypointSchema,
  PH_LAT_MIN,
  PH_LAT_MAX,
  PH_LNG_MIN,
  PH_LNG_MAX,
} from './schema';

// Hooks
export { useCreateRoute } from './create-route/useCreateRoute';
export { useRouteList } from './route-list/useRouteList';
export { useRouteDetail } from './route-detail/useRouteDetail';
export { useSubmitRevision } from './submit-revision/useSubmitRevision';
export { useVoteRoute } from './vote-route/useVoteRoute';

// Components
export { RouteMap } from './components/RouteMap';
export { CreateRouteForm } from './create-route/CreateRouteForm';
export { CreateRoutePage } from './create-route/CreateRoutePage';
export { RouteListPage } from './route-list/RouteListPage';
export { RouteDetailPage } from './route-detail/RouteDetailPage';
export { SubmitRevisionForm } from './submit-revision/SubmitRevisionForm';
export { SubmitRevisionPage } from './submit-revision/SubmitRevisionPage';
export { VoteRoutePanel } from './vote-route/VoteRoutePanel';
