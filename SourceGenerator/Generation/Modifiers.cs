using System;
using System.Text;


namespace Std.TextTemplating.Generation;

[Flags]
public enum Modifiers
{
    None = 0x00,
    Virtual = 0x01,
    Abstract = 0x02,
    Override = 0x04,
    Sealed = 0x08,
    Partial = 0x10,
    Static = 0x20
}

public static class ModifiersExtensions
{
    public static bool IsSet(this Modifiers value, Modifiers mask)
    {
        return (value & mask) == mask;
    }

    public static string Format(this Modifiers[] values)
    {
        if (values.Length == 0)
        {
            return "";
        }

        var sb = new StringBuilder();

        void Append(string text)
        {
            if (sb.Length > 0)
            {
                sb.Append(' ');
            }

            sb.Append(text);
        }

        foreach (var mask in values)
        {
            if (mask == Modifiers.None)
            {
                continue;
            }

            if (mask.IsSet(Modifiers.Abstract))
            {
                Append("abstract");
                continue;
            }
            if (mask.IsSet(Modifiers.Override))
            {
                Append("override");
                continue;
            }
            if (mask.IsSet(Modifiers.Partial))
            {
                Append("partial");
                continue;
            }
            if (mask.IsSet(Modifiers.Sealed))
            {
                Append("sealed");
                continue;
            }
            if (mask.IsSet(Modifiers.Virtual))
            {
                Append("virtual");
                continue;
            }
            if (mask.IsSet(Modifiers.Static))
            {
                Append("static");
                continue;
            }
        }

        return sb.ToString();
    }

    public static string Format(this Modifiers mask, bool prependSpace = false, bool appendSpace = false)
    {
        if (mask == Modifiers.None)
        {
            return "";
        }

        var sb = new StringBuilder();

        void Append(string text)
        {
            if (prependSpace || sb.Length > 0)
            {
                sb.Append(' ');
            }

            sb.Append(text);
        }

        if (mask.IsSet(Modifiers.Abstract))
        {
            Append("abstract");
        }
        if (mask.IsSet(Modifiers.Override))
        {
            Append("override");
        }
        if (mask.IsSet(Modifiers.Partial))
        {
            Append("partial");
        }
        if (mask.IsSet(Modifiers.Sealed))
        {
            Append("sealed");
        }
        if (mask.IsSet(Modifiers.Virtual))
        {
            Append("virtual");
        }
        if (mask.IsSet(Modifiers.Static))
        {
            Append("static");
        }

        if (appendSpace)
        {
            sb.Append(' ');
        }

        return sb.ToString();
    }

    public static string Format(this Modifiers value, params Modifiers[] masks)
    {
        var sb = new StringBuilder();

        void Append(string text)
        {
            if (sb.Length > 0)
            {
                sb.Append(' ');
            }

            sb.Append(text);
        }

        foreach (var mask in masks)
        {
            if (mask == Modifiers.None)
            {
                continue;
            }

            if (mask.IsSet(Modifiers.Abstract))
            {
                Append("abstract");
                continue;
            }
            if (mask.IsSet(Modifiers.Override))
            {
                Append("override");
                continue;
            }
            if (mask.IsSet(Modifiers.Partial))
            {
                Append("partial");
                continue;
            }
            if (mask.IsSet(Modifiers.Sealed))
            {
                Append("sealed");
                continue;
            }
            if (mask.IsSet(Modifiers.Virtual))
            {
                Append("virtual");
                continue;
            }
            if (mask.IsSet(Modifiers.Static))
            {
                Append("static");
                continue;
            }
        }

        return sb.ToString();
    }
}
