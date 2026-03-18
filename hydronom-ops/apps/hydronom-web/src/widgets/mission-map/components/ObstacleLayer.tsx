interface ObstacleItem {
  id: string;
  x: number;
  y: number;
  radius: number;
}

interface ObstacleLayerProps {
  obstacles: ObstacleItem[];
}

export function ObstacleLayer({ obstacles }: ObstacleLayerProps) {
  return (
    <>
      {obstacles.map((obstacle) => (
        <g key={obstacle.id} transform={`translate(${obstacle.x}, ${obstacle.y})`}>
          <circle
            r={Math.max(obstacle.radius * 10, 6)}
            fill="#fb7185"
            opacity="0.18"
          />
          <circle
            r={Math.max(obstacle.radius * 6, 4)}
            fill="#f43f5e"
            stroke="#fecdd3"
            strokeWidth="1.5"
          />
        </g>
      ))}
    </>
  );
}