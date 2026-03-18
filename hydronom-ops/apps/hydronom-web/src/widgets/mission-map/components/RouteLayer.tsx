interface RouteLayerProps {
  points: string;
}

export function RouteLayer({ points }: RouteLayerProps) {
  if (!points) {
    return null;
  }

  return (
    <polyline
      points={points}
      fill="none"
      stroke="#4ade80"
      strokeWidth="3"
      strokeLinecap="round"
      strokeLinejoin="round"
      opacity="0.95"
    />
  );
}