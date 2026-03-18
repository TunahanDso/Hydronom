import { useMemo } from "react";
import type { Vec2 } from "../../shared/types/common.types";
import {
  computeBounds,
  buildPolyline,
  worldToPanel
} from "../../features/vehicle-state/lib/map.utils";
import { MapLegend } from "./components/MapLegend";
import { VehicleMarker } from "./components/VehicleMarker";
import { TrailLayer } from "./components/TrailLayer";
import { RouteLayer } from "./components/RouteLayer";
import { GoalMarker } from "./components/GoalMarker";
import { ObstacleLayer } from "./components/ObstacleLayer";

const PANEL_WIDTH = 920;
const PANEL_HEIGHT = 440;
const MIN_HALF_SPAN = 6;

interface MissionMapPanelProps {
  vehiclePosition: Vec2;
  headingDeg: number;
  trail: Vec2[];
  route: Vec2[];
  goal: Vec2 | null;
  obstacles: Array<{
    id: string;
    position: Vec2;
    radius: number;
  }>;
  lidarPoints: Vec2[];

  occupancyPreview?: Vec2[];
  occupancyCells?: Vec2[];
  ekfTrail?: Vec2[];
  ekfPose?: Vec2 | null;
  odomHint?: Vec2 | null;
}

interface MapBounds {
  minX: number;
  maxX: number;
  minY: number;
  maxY: number;
}

// Araç merkezli daha stabil bounds üretir.
// Obstacle ve lidar verisi viewport'u zıplatmasın diye bounds hesabına doğrudan katılmaz.
// Ama rota, hedef ve yeni mapping katmanları da görünürlük için dikkate alınır.
function buildStableBounds(
  vehiclePosition: Vec2,
  trail: Vec2[],
  route: Vec2[],
  goal: Vec2 | null,
  ekfTrail: Vec2[],
  occupancyPreview: Vec2[]
): MapBounds {
  const anchorPoints: Vec2[] = [
    vehiclePosition,
    ...trail,
    ...route,
    ...ekfTrail,
    ...occupancyPreview
  ];

  if (goal) {
    anchorPoints.push(goal);
  }

  const raw = computeBounds(anchorPoints.length > 0 ? anchorPoints : [vehiclePosition]);

  const centerX =
    Number.isFinite(vehiclePosition.x) ? vehiclePosition.x : (raw.minX + raw.maxX) * 0.5;
  const centerY =
    Number.isFinite(vehiclePosition.y) ? vehiclePosition.y : (raw.minY + raw.maxY) * 0.5;

  const spanX = Math.max(raw.maxX - raw.minX, MIN_HALF_SPAN * 2);
  const spanY = Math.max(raw.maxY - raw.minY, MIN_HALF_SPAN * 2);

  const halfX = spanX * 0.5;
  const halfY = spanY * 0.5;

  return {
    minX: centerX - halfX,
    maxX: centerX + halfX,
    minY: centerY - halfY,
    maxY: centerY + halfY
  };
}

// Hydronom Ops için genişletilmiş 2D operasyon haritası
export function MissionMapPanel({
  vehiclePosition,
  headingDeg,
  trail,
  route,
  goal,
  obstacles,
  lidarPoints,
  occupancyPreview = [],
  occupancyCells = [],
  ekfTrail = [],
  ekfPose = null,
  odomHint = null
}: MissionMapPanelProps) {
  const bounds = useMemo(
    () =>
      buildStableBounds(
        vehiclePosition,
        trail,
        route,
        goal,
        ekfTrail,
        occupancyPreview
      ),
    [vehiclePosition, trail, route, goal, ekfTrail, occupancyPreview]
  );

  const trailPolyline = useMemo(
    () => buildPolyline(trail, bounds, PANEL_WIDTH, PANEL_HEIGHT),
    [trail, bounds]
  );

  const ekfTrailPolyline = useMemo(
    () => buildPolyline(ekfTrail, bounds, PANEL_WIDTH, PANEL_HEIGHT),
    [ekfTrail, bounds]
  );

  const routePolyline = useMemo(
    () => buildPolyline(route, bounds, PANEL_WIDTH, PANEL_HEIGHT),
    [route, bounds]
  );

  const occupancyPreviewPolyline = useMemo(
    () => buildPolyline(occupancyPreview, bounds, PANEL_WIDTH, PANEL_HEIGHT),
    [occupancyPreview, bounds]
  );

  const vehicleProjected = useMemo(
    () => worldToPanel(vehiclePosition, bounds, PANEL_WIDTH, PANEL_HEIGHT),
    [vehiclePosition, bounds]
  );

  const goalProjected = useMemo(
    () => (goal ? worldToPanel(goal, bounds, PANEL_WIDTH, PANEL_HEIGHT) : null),
    [goal, bounds]
  );

  const ekfPoseProjected = useMemo(
    () => (ekfPose ? worldToPanel(ekfPose, bounds, PANEL_WIDTH, PANEL_HEIGHT) : null),
    [ekfPose, bounds]
  );

  const odomHintProjected = useMemo(
    () => (odomHint ? worldToPanel(odomHint, bounds, PANEL_WIDTH, PANEL_HEIGHT) : null),
    [odomHint, bounds]
  );

  const projectedObstacles = useMemo(
    () =>
      obstacles.map((obstacle) => {
        const projected = worldToPanel(
          obstacle.position,
          bounds,
          PANEL_WIDTH,
          PANEL_HEIGHT
        );

        return {
          id: obstacle.id,
          x: projected.x,
          y: projected.y,
          radius: obstacle.radius
        };
      }),
    [obstacles, bounds]
  );

  const projectedLidar = useMemo(
    () =>
      lidarPoints.map((point, index) => {
        const projected = worldToPanel(point, bounds, PANEL_WIDTH, PANEL_HEIGHT);

        return {
          id: `lidar-${index}`,
          x: projected.x,
          y: projected.y
        };
      }),
    [lidarPoints, bounds]
  );

  const projectedOccupancyCells = useMemo(
    () =>
      occupancyCells.map((point, index) => {
        const projected = worldToPanel(point, bounds, PANEL_WIDTH, PANEL_HEIGHT);

        return {
          id: `occ-cell-${index}`,
          x: projected.x,
          y: projected.y
        };
      }),
    [occupancyCells, bounds]
  );

  return (
    <div className="space-y-4">
      <div className="relative overflow-hidden rounded-2xl border border-slate-800 bg-slate-950/70">
        <svg
          viewBox={`0 0 ${PANEL_WIDTH} ${PANEL_HEIGHT}`}
          className="h-[440px] w-full"
          preserveAspectRatio="none"
        >
          <defs>
            <pattern
              id="gridPattern"
              width="40"
              height="40"
              patternUnits="userSpaceOnUse"
            >
              <path
                d="M 40 0 L 0 0 0 40"
                fill="none"
                stroke="#1e293b"
                strokeWidth="1"
              />
            </pattern>
          </defs>

          <rect
            width={PANEL_WIDTH}
            height={PANEL_HEIGHT}
            fill="url(#gridPattern)"
          />

          {/* Standart görev katmanları */}
          <TrailLayer points={trailPolyline} />
          <RouteLayer points={routePolyline} />

          {/* EKF trail */}
          {ekfTrailPolyline && ekfTrail.length >= 2 && (
            <polyline
              points={ekfTrailPolyline}
              fill="none"
              stroke="#22c55e"
              strokeWidth="2"
              opacity="0.95"
              strokeLinejoin="round"
              strokeLinecap="round"
            />
          )}

          {/* Occupancy preview polyline */}
          {occupancyPreviewPolyline && occupancyPreview.length >= 2 && (
            <polyline
              points={occupancyPreviewPolyline}
              fill="none"
              stroke="#f59e0b"
              strokeWidth="1.5"
              opacity="0.9"
              strokeLinejoin="round"
              strokeLinecap="round"
            />
          )}

          {/* Occupancy cells */}
          {projectedOccupancyCells.map((point) => (
            <circle
              key={point.id}
              cx={point.x}
              cy={point.y}
              r="1.6"
              fill="#f59e0b"
              opacity="0.45"
            />
          ))}

          {/* LiDAR points */}
          {projectedLidar.map((point) => (
            <circle
              key={point.id}
              cx={point.x}
              cy={point.y}
              r="3"
              fill="#a78bfa"
              opacity="0.85"
            />
          ))}

          <ObstacleLayer obstacles={projectedObstacles} />

          {goalProjected && (
            <GoalMarker x={goalProjected.x} y={goalProjected.y} />
          )}

          {/* EKF pose */}
          {ekfPoseProjected && (
            <>
              <circle
                cx={ekfPoseProjected.x}
                cy={ekfPoseProjected.y}
                r="6"
                fill="#22c55e"
                opacity="0.95"
              />
              <circle
                cx={ekfPoseProjected.x}
                cy={ekfPoseProjected.y}
                r="11"
                fill="none"
                stroke="#22c55e"
                strokeWidth="1.5"
                opacity="0.55"
              />
            </>
          )}

          {/* Odom hint */}
          {odomHintProjected && (
            <>
              <circle
                cx={odomHintProjected.x}
                cy={odomHintProjected.y}
                r="4"
                fill="#38bdf8"
                opacity="0.9"
              />
              <circle
                cx={odomHintProjected.x}
                cy={odomHintProjected.y}
                r="8"
                fill="none"
                stroke="#38bdf8"
                strokeWidth="1"
                opacity="0.4"
              />
            </>
          )}

          <VehicleMarker
            x={vehicleProjected.x}
            y={vehicleProjected.y}
            headingDeg={headingDeg}
          />
        </svg>

        <div className="absolute left-4 top-4 max-w-sm rounded-2xl border border-slate-800 bg-slate-950/80 px-4 py-3 shadow-panel">
          <div className="text-[10px] uppercase tracking-[0.25em] text-slate-500">
            World View
          </div>
          <div className="mt-2 text-sm text-slate-300">
            Araç pozisyonu, trail, rota, hedef, lidar, runtime obstacle,
            occupancy-grid önizlemesi, occupancy hücreleri, EKF trail ve odometry
            ipuçları aynı panelde gösteriliyor.
          </div>
        </div>
      </div>

      <MapLegend />
    </div>
  );
}