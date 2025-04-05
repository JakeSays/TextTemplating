using System;


namespace Std.TextTemplating.Generation.Parsing;

public readonly struct Location : IEquatable<Location>
{
    public Location(string? fileName, int line, int column)
        : this()
    {
        FileName = fileName;
        Column = column;
        Line = line;
    }

    public int Line { get; }
    public int Column { get; }
    public string? FileName { get; }

    public static Location Empty => new(null, -1, -1);

    public Location AddLine() => new(FileName, Line + 1, 1);

    public Location AddCol() => AddCols(1);

    public Location AddCols(int number) => new(FileName, Line, Column + number);

    public override string ToString() => $"[{FileName} ({Line},{Column})]";

    public bool Equals(Location other) => other.Line == Line && other.Column == Column && other.FileName == FileName;

    public override bool Equals(object? obj) => obj is Location loc && Equals(loc);

    public override int GetHashCode() => (FileName, Line, Column).GetHashCode();

    public static bool operator ==(Location left, Location right) => left.Equals(right);

    public static bool operator !=(Location left, Location right) => !(left == right);
}
