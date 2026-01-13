namespace Shared;

public class PiiEntityOutput
{
    public string Text { get; set; } = "";
    public string Category { get; set; } = "";
    public double ConfidenceScore { get; set; }
}