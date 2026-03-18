interface GoalMarkerProps {
  x: number;
  y: number;
}

export function GoalMarker({ x, y }: GoalMarkerProps) {
  return (
    <g transform={`translate(${x}, ${y})`}>
      <circle r="10" fill="#f59e0b" opacity="0.18" />
      <circle r="6" fill="#fbbf24" stroke="#fde68a" strokeWidth="1.5" />
      <path d="M 0 -14 L 0 -6" stroke="#fde68a" strokeWidth="2" />
    </g>
  );
}