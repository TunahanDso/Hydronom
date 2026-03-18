interface VehicleMarkerProps {
  x: number;
  y: number;
  headingDeg: number;
}

export function VehicleMarker({ x, y, headingDeg }: VehicleMarkerProps) {
  return (
    <g transform={`translate(${x}, ${y}) rotate(${headingDeg})`}>
      <polygon
        points="0,-16 10,12 0,7 -10,12"
        fill="#38bdf8"
        stroke="#e0f2fe"
        strokeWidth="1.5"
      />
      <circle cx="0" cy="0" r="3" fill="#ffffff" />
    </g>
  );
}