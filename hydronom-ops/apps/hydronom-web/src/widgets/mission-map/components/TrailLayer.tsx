interface TrailLayerProps {
  points: string;
}

export function TrailLayer({ points }: TrailLayerProps) {
  if (!points) {
    return null;
  }

  return (
    <polyline
      points={points}
      fill="none"
      stroke="#67e8f9"
      strokeWidth="3"
      strokeDasharray="6 6"
      strokeLinecap="round"
      strokeLinejoin="round"
      opacity="0.9"
    />
  );
}