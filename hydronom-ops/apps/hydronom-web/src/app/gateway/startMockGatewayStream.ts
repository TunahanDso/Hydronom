import { dispatchGatewayMessage } from "./dispatchGatewayMessage";
import type { GatewayMessage } from "../../shared/types/gateway.types";
import type { ConnectionState, HealthState } from "../../shared/types/common.types";

// Mock gateway akışını başlatır
export function startMockGatewayStream() {
  const vehicleId = "HYD-01";

  const intervalId = window.setInterval(() => {
    const now = new Date().toISOString();
    const t = Date.now() / 1000;

    const x = 42 + Math.cos(t * 0.18) * 6;
    const y = Math.sin(t * 0.22) * 4;
    const headingDeg = ((t * 18) % 360 + 360) % 360;
    const speed = 1.2 + Math.abs(Math.sin(t * 0.7)) * 0.9;
    const obsAhead = Math.sin(t * 0.9) > 0.35;

    const connection: ConnectionState =
      Math.sin(t * 0.35) > -0.75 ? "connected" : "degraded";

    const heartbeatHealth: HealthState = obsAhead ? "warn" : "ok";

    const trail = Array.from({ length: 18 }, (_, index) => {
      const dt = index + 1;

      return {
        x: x - dt * 0.45,
        y: y - Math.sin((t - dt * 0.35) * 0.22) * 0.35
      };
    }).reverse();

    const route = [
      { x: 40, y: -2 },
      { x: 44, y: -0.5 },
      { x: 48, y: 1.4 },
      { x: 52, y: 2.2 },
      { x: 56, y: 0.8 }
    ];

    const waypoints = [
      {
        id: "wp-1",
        label: "WP-1",
        position: { x: 40, y: -2 },
        reached: true
      },
      {
        id: "wp-2",
        label: "WP-2",
        position: { x: 44, y: -0.5 },
        reached: true
      },
      {
        id: "wp-3",
        label: "WP-3",
        position: { x: 48, y: 1.4 },
        reached: false
      },
      {
        id: "wp-4",
        label: "WP-4",
        position: { x: 52, y: 2.2 },
        reached: false
      },
      {
        id: "wp-5",
        label: "WP-5",
        position: { x: 56, y: 0.8 },
        reached: false
      }
    ];

    const lidarPoints = Array.from({ length: 14 }, (_, index) => {
      const angle = -0.8 + index * 0.13;
      const distance = 2.4 + Math.sin(t * 0.8 + index * 0.4) * 0.7;

      return {
        x: x + Math.cos(angle + (headingDeg * Math.PI) / 180) * distance,
        y: y + Math.sin(angle + (headingDeg * Math.PI) / 180) * distance,
        intensity: 0.6 + (index % 4) * 0.1
      };
    });

    const obstacles = [
      {
        id: "obs-runtime-1",
        position: { x: x + 2.6, y: y + 0.9 },
        radius: 0.7,
        source: "lidar_runtime_obstacles"
      },
      {
        id: "obs-runtime-2",
        position: { x: x + 4.1, y: y - 1.2 },
        radius: 0.9,
        source: "occupancy_grid"
      }
    ];

    const baseMessage = {
      vehicleId,
      timestampUtc: now
    };

    const messages: GatewayMessage[] = [
      {
        ...baseMessage,
        type: "vehicle.telemetry",
        payload: {
          vehicleId,
          displayName: "Hydronom-01",
          mode: "mission",
          armState: "armed",
          pose: {
            position: {
              x,
              y,
              z: 0
            },
            orientation: {
              roll: Math.sin(t * 0.7) * 2,
              pitch: Math.cos(t * 0.5) * 1.5,
              yaw: headingDeg
            }
          },
          motion: {
            speed,
            linearVelocity: {
              x: speed * 0.92,
              y: Math.sin(t * 0.5) * 0.18,
              z: 0
            },
            angularVelocity: {
              x: 0,
              y: 0,
              z: Math.cos(t * 0.35) * 0.12
            },
            linearAcceleration: {
              x: Math.sin(t * 0.7) * 0.25,
              y: Math.cos(t * 0.5) * 0.08,
              z: 0
            }
          },
          map: {
            worldPosition: { x, y },
            headingDeg,
            trail
          },
          freshness: {
            timestamp: now,
            ageMs: 0,
            isStale: false,
            source: "runtime"
          },
          health: {
            overall: heartbeatHealth,
            sensors: "ok",
            actuators: "ok",
            navigation: obsAhead ? "warn" : "ok",
            autonomy: "ok"
          },
          connections: {
            runtimeConnected: true,
            gatewayConnected: true,
            pythonConnected: true,
            twinActive: true
          },
          flags: [
            {
              key: "obsAhead",
              label: "Obstacle Ahead",
              value: obsAhead
            }
          ]
        }
      },
      {
        ...baseMessage,
        type: "mission.state",
        payload: {
          vehicleId,
          missionId: "mission-harbor-scan-001",
          missionName: "Harbor Scan",
          status: "running",
          activeStepId: "step-2",
          progressPercent: Math.min(
            100,
            Math.max(8, Math.round(((Math.sin(t * 0.18) + 1) / 2) * 100))
          ),
          goalPosition: route[route.length - 1],
          route,
          waypoints,
          steps: [
            {
              id: "step-1",
              title: "Launch",
              description: "Araç başlatılır ve görev moduna alınır.",
              status: "completed",
              order: 1
            },
            {
              id: "step-2",
              title: "Survey Corridor",
              description: "Belirlenen koridorda tarama görevi yürütülür.",
              status: "active",
              order: 2
            },
            {
              id: "step-3",
              title: "Return Home",
              description: "Görev tamamlandıktan sonra dönüş rotasına geçilir.",
              status: "pending",
              order: 3
            }
          ],
          recentEvents: [
            {
              id: `evt-${Math.floor(t)}-1`,
              timestamp: now,
              level: obsAhead ? "warn" : "info",
              message: obsAhead
                ? "Obstacle-aware adjustment active."
                : "Mission path tracking nominal."
            },
            {
              id: `evt-${Math.floor(t)}-2`,
              timestamp: now,
              level: "info",
              message: "Twin-linked telemetry stream healthy."
            }
          ],
          freshness: {
            timestamp: now,
            ageMs: 0,
            isStale: false,
            source: "runtime"
          }
        }
      },
      {
        ...baseMessage,
        type: "actuator.state",
        payload: {
          vehicleId,
          thrusters: [
            {
              id: "FL",
              label: "Front Left",
              active: true,
              normalizedCommand: 0.42 + Math.sin(t * 0.9) * 0.12,
              appliedCommand: 0.39 + Math.sin(t * 0.9) * 0.1,
              rpm: Math.round(1180 + Math.sin(t * 1.1) * 180),
              direction: "forward"
            },
            {
              id: "FR",
              label: "Front Right",
              active: true,
              normalizedCommand: 0.4 + Math.cos(t * 0.85) * 0.12,
              appliedCommand: 0.37 + Math.cos(t * 0.85) * 0.1,
              rpm: Math.round(1160 + Math.cos(t * 1.05) * 170),
              direction: "forward"
            },
            {
              id: "RL",
              label: "Rear Left",
              active: true,
              normalizedCommand: 0.31 + Math.sin(t * 0.75) * 0.1,
              appliedCommand: 0.29 + Math.sin(t * 0.75) * 0.08,
              rpm: Math.round(980 + Math.sin(t * 0.95) * 120),
              direction: "forward"
            },
            {
              id: "RR",
              label: "Rear Right",
              active: true,
              normalizedCommand: 0.33 + Math.cos(t * 0.78) * 0.1,
              appliedCommand: 0.3 + Math.cos(t * 0.78) * 0.08,
              rpm: Math.round(995 + Math.cos(t * 0.98) * 120),
              direction: "forward"
            }
          ],
          wrench: {
            forceBody: {
              x: 12 + Math.sin(t * 0.7) * 1.8,
              y: Math.cos(t * 0.9) * 0.9,
              z: 0
            },
            torqueBody: {
              x: 0,
              y: 0,
              z: Math.sin(t * 0.65) * 0.42
            }
          },
          limiter: {
            satT: false,
            satR: obsAhead,
            rlT: false,
            rlR: Math.sin(t * 0.45) > 0.75,
            dbT: false,
            dbR: false,
            assist: obsAhead,
            dt: false
          },
          freshness: {
            timestamp: now,
            ageMs: 0,
            isStale: false,
            source: "runtime"
          }
        }
      },
      {
        ...baseMessage,
        type: "sensor.state",
        payload: {
          vehicleId,
          lidarPoints,
          obstacles,
          occupancy: {
            width: 80,
            height: 80,
            resolution: 0.25,
            occupiedCellCount: 120 + Math.round(Math.abs(Math.sin(t)) * 24)
          },
          sensorHealth: {
            lidar: "ok",
            imu: "ok",
            gps: connection === "connected" ? "ok" : "warn",
            camera: obsAhead ? "warn" : "ok"
          },
          freshness: {
            timestamp: now,
            ageMs: 0,
            isStale: false,
            source: "python"
          }
        }
      },
      {
        ...baseMessage,
        type: "diagnostics.state",
        payload: {
          vehicleId,
          overallConnection: connection,
          overallHealth: heartbeatHealth,
          streamMetrics: [
            {
              key: "runtime",
              label: "Runtime Stream",
              rateHz: 20,
              ageMs: 20,
              state: connection
            },
            {
              key: "lidar",
              label: "Lidar Stream",
              rateHz: 10,
              ageMs: 45,
              state: "connected"
            },
            {
              key: "twin",
              label: "Twin Stream",
              rateHz: 15,
              ageMs: 28,
              state: connection
            }
          ],
          sourceInspector: [
            {
              key: "pose",
              label: "Vehicle Pose",
              source: "runtime",
              freshnessMs: 20,
              state: "ok"
            },
            {
              key: "externalState",
              label: "External State",
              source: "python/twin",
              freshnessMs: 28,
              state: connection === "connected" ? "ok" : "warn"
            },
            {
              key: "occupancy",
              label: "Occupancy Grid",
              source: "python plugin",
              freshnessMs: 66,
              state: obsAhead ? "warn" : "ok"
            }
          ],
          logs: [
            {
              id: `diag-${Math.floor(t)}-1`,
              timestamp: now,
              level: obsAhead ? "warn" : "info",
              source: "runtime",
              message: obsAhead
                ? "Obstacle corridor detected ahead."
                : "Mission runtime healthy."
            }
          ],
          freshness: {
            timestamp: now,
            ageMs: 0,
            isStale: false,
            source: "runtime"
          }
        }
      },
      {
        ...baseMessage,
        type: "system.heartbeat",
        payload: {
          health: heartbeatHealth,
          connection
        }
      }
    ];

    for (const message of messages) {
      dispatchGatewayMessage(message);
    }

    if (Math.sin(t * 0.55) > 0.82) {
      dispatchGatewayMessage({
        ...baseMessage,
        type: "system.log",
        payload: {
          level: "warn",
          source: "gateway",
          message: "Temporary latency rise detected on telemetry channel."
        }
      });
    }
  }, 1000);

  return () => {
    window.clearInterval(intervalId);
  };
}