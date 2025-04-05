namespace Std.TextTemplating.Generation.Parsing;

public interface ISegment
{
    Location StartLocation { get; }
    Location EndLocation { get; set; }
    Location TagStartLocation { get; set; }
}