namespace Std.TextTemplating.Generation.Parsing;

public class TemplateSegment : ISegment
{
    public TemplateSegment(SegmentType type, string text, Location start)
    {
        Type = type;
        StartLocation = start;
        Text = text;
    }

    public SegmentType Type { get; private set; }
    public string Text { get; private set; }
    public Location TagStartLocation { get; set; }
    public Location StartLocation { get; }
    public Location EndLocation { get; set; }
}