using System;
using System.Collections.Generic;


namespace Std.TextTemplating.Generation.Parsing;

public class Directive : ISegment
{
    public Directive(string name, Location start)
    {
        Name = name;
        Attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        StartLocation = start;
    }

    public string Name { get; }
    public Dictionary<string, string> Attributes { get; }
    public Location TagStartLocation { get; set; }
    public Location StartLocation { get; }
    public Location EndLocation { get; set; }

    public string? Extract(string key)
    {
        if (!Attributes.TryGetValue(key, out var value))
        {
            return null;
        }

        Attributes.Remove(key);
        return value;
    }

    public string Extract(string key, string defaultValue) => Extract(key) ?? defaultValue;

    public bool Extract(string key, bool defaultValue) => ExtractBool(key, defaultValue);

    public bool ExtractBool(string key, bool defaultValue = false)
    {
        var value = Extract(key);
        bool.TryParse(value, out var result);
        return result;
    }
}