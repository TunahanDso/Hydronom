using HydronomOps.Gateway.Contracts.Vehicle;

namespace HydronomOps.Gateway.Services.Mapping;

public sealed partial class RuntimeToGatewayMapper
{
    private static void NormalizeOccupancyTelemetry(
        Dictionary<string, double> metrics,
        Dictionary<string, string> fields,
        List<LandmarkDto> landmarks)
    {
        var gridWidth =
            GetMetricFirst(metrics,
                "occupancy.width",
                "occupancy_grid.width",
                "occupancy_grid.grid_width",
                "occupancy_grid.cells_w",
                "occupancy_grid.cellsW");

        var gridHeight =
            GetMetricFirst(metrics,
                "occupancy.height",
                "occupancy_grid.height",
                "occupancy_grid.grid_height",
                "occupancy_grid.cells_h",
                "occupancy_grid.cellsH");

        var previewCount =
            GetMetricFirst(metrics,
                "occupancy.previewCount",
                "occupancy_grid.preview_count",
                "occupancy_grid.preview_points",
                "occupancy_grid.previewPoints");

        var exportCount =
            GetMetricFirst(metrics,
                "occupancy.exportCount",
                "occupancy_grid.export_count",
                "occupancy_grid.export_points",
                "occupancy_grid.exportPoints");

        var occupiedCount =
            GetMetricFirst(metrics,
                "occupancy.occupiedCount",
                "occupancy_grid.occupied_count",
                "occupancy_grid.occupied_cells",
                "occupancy_grid.occupiedCells");

        var resolutionM =
            GetMetricFirst(metrics,
                "occupancy.resolutionM",
                "occupancy_grid.resolution",
                "occupancy_grid.resolution_m",
                "occupancy_grid.resolutionM");

        var scanCount =
            GetMetricFirst(metrics,
                "occupancy.scanCount",
                "occupancy_grid.scan_count",
                "occupancy_grid.scanCount");

        var previewLandmark = landmarks.FirstOrDefault(l =>
            string.Equals(l.Type, "occupancy_preview", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(l.Id, "ogm_preview", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(l.Id, "occ_poly", StringComparison.OrdinalIgnoreCase));

        var cellsLandmark = landmarks.FirstOrDefault(l =>
            string.Equals(l.Type, "occupancy_cells", StringComparison.OrdinalIgnoreCase) ||
            l.Id.EndsWith("_cells", StringComparison.OrdinalIgnoreCase));

        if ((!previewCount.HasValue || previewCount.Value <= 0) && previewLandmark is not null)
        {
            previewCount = previewLandmark.Points.Count;
        }

        if ((!exportCount.HasValue || exportCount.Value <= 0) && cellsLandmark is not null)
        {
            exportCount = cellsLandmark.Points.Count;
        }

        if ((!occupiedCount.HasValue || occupiedCount.Value <= 0))
        {
            if (exportCount.HasValue && exportCount.Value > 0)
            {
                occupiedCount = exportCount.Value;
            }
            else if (cellsLandmark is not null)
            {
                occupiedCount = cellsLandmark.Points.Count;
            }
        }

        if (gridWidth.HasValue)
        {
            metrics["occupancy.width"] = gridWidth.Value;
            metrics["occupancy.gridWidth"] = gridWidth.Value;
            metrics["occupancy_grid.width"] = gridWidth.Value;
        }

        if (gridHeight.HasValue)
        {
            metrics["occupancy.height"] = gridHeight.Value;
            metrics["occupancy.gridHeight"] = gridHeight.Value;
            metrics["occupancy_grid.height"] = gridHeight.Value;
        }

        if (previewCount.HasValue)
        {
            metrics["occupancy.previewCount"] = previewCount.Value;
            metrics["occupancy_grid.preview_count"] = previewCount.Value;
        }

        if (exportCount.HasValue)
        {
            metrics["occupancy.exportCount"] = exportCount.Value;
            metrics["occupancy_grid.export_count"] = exportCount.Value;
        }

        if (occupiedCount.HasValue)
        {
            metrics["occupancy.occupiedCount"] = occupiedCount.Value;
            metrics["occupancy_grid.occupied_count"] = occupiedCount.Value;
        }

        if (resolutionM.HasValue)
        {
            metrics["occupancy.resolutionM"] = resolutionM.Value;
            metrics["occupancy_grid.resolution_m"] = resolutionM.Value;
        }

        if (scanCount.HasValue)
        {
            metrics["occupancy.scanCount"] = scanCount.Value;
            metrics["occupancy_grid.scan_count"] = scanCount.Value;
        }

        if (gridWidth.HasValue && gridHeight.HasValue)
        {
            fields["occupancy.gridSize"] =
                $"{Convert.ToInt32(Math.Round(gridWidth.Value, MidpointRounding.AwayFromZero))}x{Convert.ToInt32(Math.Round(gridHeight.Value, MidpointRounding.AwayFromZero))}";
        }

        if (previewLandmark is not null)
        {
            fields["occupancy.previewLandmarkId"] = previewLandmark.Id;
        }

        if (cellsLandmark is not null)
        {
            fields["occupancy.cellsLandmarkId"] = cellsLandmark.Id;
        }
    }

    private static double? GetMetricFirst(Dictionary<string, double> metrics, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (metrics.TryGetValue(key, out var value))
            {
                return value;
            }
        }

        return null;
    }
}