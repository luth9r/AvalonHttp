namespace AvalonHttp.Models;

public class TimelineStageModel
{
    public string Name { get; set; } = string.Empty;
    public double Duration { get; set; }
    public string DurationText { get; set; } = string.Empty;
    public string Color { get; set; } = "#3B82F6";
    public double WidthPercent { get; set; }
}