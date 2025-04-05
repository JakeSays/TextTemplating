using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using JetBrains.Annotations;


namespace Std.TextTemplating.Generation;

public enum Access
{
    Public,
    Private,
    Internal,
    Protected,
    ProtectedInternal,
    PrivateProtected
}

public class CodeWriter
{
    private const int MaxSummaryLineLength = 40;

    private readonly HashSet<string> _actionMethods = [];

    private readonly StringBuilder _builder = new();

    private readonly StringBuilder _codeBuilder = new();

    private bool _endsWithNewline;

    private int? _lastLineNumber;

    public bool GeneratePartialClasses { get; set; }

    public List<string> ActionMethods => _actionMethods.ToList();

    public string CodeOutput => _codeBuilder.ToString();

    private List<int> IndentLengths { get; } = [];

    public string CurrentIndent { get; private set; } = "";

    public int UsingCount { get; private set; }

    public string? LineDirectiveFile { get; set; }

    public void AddAction(string methodName)
    {
        if (!_actionMethods.Contains(methodName))
        {
            _actionMethods.Add(methodName);
        }
    }

    public void Reset(string? startingIndent = null)
    {
        _builder.Clear();
        _codeBuilder.Clear();
        _endsWithNewline = false;
        IndentLengths.Clear();
        CurrentIndent = "";
        _actionMethods.Clear();

        if (startingIndent == null)
        {
            return;
        }

        CurrentIndent = startingIndent;
        IndentLengths.Add(CurrentIndent.Length);
    }

    public void Write(char ch) => Write(ch.ToString(CultureInfo.InvariantCulture));

    public void RawWrite(string text) => _codeBuilder.Append(text);

    public void RawWriteLine(string text = "")
    {
        _codeBuilder.AppendLine(text);
        _endsWithNewline = true;
    }

    public void Ugh(string text = "")
    {
        if (!_endsWithNewline)
        {
            _codeBuilder.AppendLine();
        }
        _codeBuilder.AppendLine(text);
        _endsWithNewline = true;
    }

    public void BlankLine(int count = 1)
    {
        while (count-- > 0)
        {
            _codeBuilder.AppendLine();
        }

        _endsWithNewline = true;
    }

    public void Write(string textToAppend)
    {
        if (string.IsNullOrEmpty(textToAppend))
        {
            return;
        }

        // If we're starting off, or if the previous text ended with a newline,
        // we have to append the current indent first.
        if (CodeOutput.Length == 0 || _endsWithNewline)
        {
            _codeBuilder.Append(CurrentIndent);
            _endsWithNewline = false;
        }

        // Check if the current text ends with a newline
        if (textToAppend.EndsWith("\n", StringComparison.Ordinal) ||
            textToAppend.EndsWith("\r\n", StringComparison.Ordinal))
        {
            _endsWithNewline = true;
        }

        // This is an optimization. If the current indent is "", then we don't have to do any
        // of the more complex stuff further down.
        if (CurrentIndent.Length == 0)
        {
            _codeBuilder.Append(textToAppend);
            return;
        }

        // Everywhere there is a newline in the text, add an indent after it
        textToAppend = textToAppend
            .Replace("\r\n", $"\r\n{CurrentIndent}")
            .Replace("\n", $"\n{CurrentIndent}");

        // If the text ends with a newline, then we should strip off the indent added at the very end
        // because the appropriate indent will be added when the next time Write() is called
        if (_endsWithNewline)
        {
            _codeBuilder.Append(textToAppend, 0, textToAppend.Length - CurrentIndent.Length);
        }
        else
        {
            _codeBuilder.Append(textToAppend);
        }
    }

    public IDisposable Scope(bool openScope = false, bool addTrailingLine = false)
    {
        if (openScope)
        {
            OpenScope();
        }

        return new ScopeContext(this, addTrailingLine);
    }

    public void OpenScope(string scopeTag = "{")
    {
        WriteLine(scopeTag);
        PushIndent();
    }

    public void CloseScope(string scopeTag = "}", bool addTrailingLine = false)
    {
        PopIndent();
        WriteLine(scopeTag);

        if (addTrailingLine)
        {
            RawWriteLine();
        }
    }

    public void WriteLine(string? textToAppend = null)
    {
        if (!string.IsNullOrEmpty(textToAppend))
        {
            Write(textToAppend!);
        }

        _codeBuilder.AppendLine();
        _endsWithNewline = true;
    }

    [StringFormatMethod("format")]
    public void Write(string format, params object[] args) => Write(string.Format(format, args));

    [StringFormatMethod("format")]
    public void WriteLine(string format, params object[] args) => WriteLine(string.Format(format, args));

    public void PushIndent(string indent = "    ")
    {
        CurrentIndent += indent ?? throw new ArgumentNullException(nameof(indent));
        IndentLengths.Add(indent.Length);
    }

    public void WriteBlock(params string[] lines)
    {
        foreach (var line in lines)
        {
            if (line == "")
            {
                WriteLine();
                continue;
            }

            foreach (var ch in line)
            {
                if (ch == '\a')
                {
                    PushIndent();
                    continue;
                }

                if (ch == '\b')
                {
                    PopIndent();
                    continue;
                }

                if (ch == '\n')
                {
                    WriteLine();
                    continue;
                }

                if (ch == '{')
                {
                    if (!_endsWithNewline)
                    {
                        WriteLine();
                    }

                    OpenScope();
                    continue;
                }

                if (ch == '}')
                {
                    if (!_endsWithNewline)
                    {
                        WriteLine();
                    }

                    CloseScope();
                    continue;
                }

                Write(ch);
            }
        }
    }

    public void PopIndent()
    {
        if (IndentLengths.Count <= 0)
        {
            return;
        }

        var indentLength = IndentLengths[^1];
        IndentLengths.RemoveAt(IndentLengths.Count - 1);

        if (indentLength <= 0)
        {
            return;
        }

        CurrentIndent = CurrentIndent.Remove(CurrentIndent.Length - indentLength);
    }

    public void ClearIndent()
    {
        IndentLengths.Clear();
        CurrentIndent = "";
    }

    public virtual void WriteComment(string s) => WriteLine($"//{s}");

    public virtual void WriteSummary(string s) => WriteSummary(s, MaxSummaryLineLength);

    public virtual void WriteSummary(string s, int lineLength)
    {
        if (string.IsNullOrWhiteSpace(s))
        {
            return;
        }

        var lines = new List<string>();
        var sourceLines = s.Split('\n');

        foreach (var line in sourceLines)
        {
            if (line.Length <= lineLength)
            {
                lines.Add(line);
                continue;
            }

            var brokeLines = StringExtensions.BreakIntoLines(line, lineLength);
            lines.AddRange(brokeLines);
        }

        var text = "\n" + string.Join("\n", lines) + "\n";

        var xml = new XElement("summary", text);

        using var reader = new StringReader(xml.ToString());

        while (reader.ReadLine() is { } xline)
        {
            WriteLine("/// " + xline);
        }
    }

    public virtual void WriteUsing(string s)
    {
        WriteLine($"using {s};");
        UsingCount += 1;
    }

    public virtual void WriteBeginNamespace(string s)
    {
        WriteLine($"namespace {s}");
        WriteLine("{");
        PushIndent();
    }

    public virtual void WriteEndNamespace(bool addTrailingLine = false)
    {
        PopIndent();
        WriteLine("}");

        if (addTrailingLine)
        {
            RawWriteLine();
        }
    }

    public virtual void WriteEnum(string enumName, Access access = Access.Public, params string[] tags)
    {
        WriteBeginEnum(enumName, access);

        var lastTag = tags.Last();

        foreach (var tag in tags)
        {
            WriteLine(
                "{0}{1}",
                tag,
                tag == lastTag
                    ? ""
                    : ",");
        }

        CloseScope();
    }

    public virtual void WriteBeginEnum(string enumName, Access access = Access.Public)
    {
        WriteLine($"{GetAccessText(access)} enum {enumName}");
        OpenScope();
    }

    public virtual void WriteEndEnum(bool addTrailingLine = true) => CloseScope(addTrailingLine: addTrailingLine);

    private string GetAccessText(Access access)
    {
        switch (access)
        {
            case Access.Public:
                return "public";
            case Access.Private:
                return "private";
            case Access.Internal:
                return "internal";
            case Access.Protected:
                return "protected";
            case Access.ProtectedInternal:
                return "protected internal";
            case Access.PrivateProtected:
                return "private protected";
            default:
                throw new ArgumentOutOfRangeException(nameof(access), access, null);
        }
    }

    public virtual IDisposable WriteClass(
        string className,
        string baseClassName = null!,
        Access access = Access.Public,
        params Modifiers[] modifiers)
    {
        WriteBeginClass(className, baseClassName!, access, modifiers);
        return new ScopeContext(this, false);
    }

    public virtual void WriteBeginClass(
        string className,
        string baseClassName = null!,
        Access access = Access.Public,
        params Modifiers[] modifiers)
    {
        _builder.Clear();

        var modtext = modifiers.Format();

        if (modtext.Length > 0)
        {
            modtext = " " + modtext;
        }

        _builder.Append(GetAccessText(access));
        _builder.Append($"{modtext} class {className}");

        WriteLine(_builder.ToString());

        if (!string.IsNullOrEmpty(baseClassName))
        {
            Write($"    : {baseClassName}");
        }

        //WriteLine();
        OpenScope();
    }

    public virtual void WriteEndClass(bool addTrailingLine = false)
    {
        PopIndent();
        WriteLine("}");

        if (addTrailingLine)
        {
            RawWriteLine();
        }
    }

    public virtual IDisposable WriteMethod(
        string name,
        string returnType = "void",
        Access access = Access.Public,
        Modifiers modifiers = Modifiers.None,
        params (string Type, string Name)[] args)
    {
        WriteBeginMethod(name, returnType, access, modifiers, args);
        return Scope();
    }

    public virtual void WriteEmptyMethod(
        string name,
        string returnType = "void",
        Access access = Access.Public,
        Modifiers modifiers = Modifiers.None,
        params (string Type, string Name)[] args)
    {
        WriteBeginMethod(name, returnType, access, modifiers, args);
        WriteEndMethod();
    }

    public virtual void WriteBeginMethod(
        string name,
        string returnType = "void",
        Access access = Access.Public,
        Modifiers modifiers = Modifiers.None,
        params (string Type, string Name)[] args)
    {
        var modtext = modifiers.Format(appendSpace: true);

        Write($"{GetAccessText(access)} {modtext}{returnType} {name}(");

        var argCounter = 0;
        foreach (var (argType, argValue) in args)
        {
            if (argCounter++ > 0)
            {
                Write(", ");
            }

            Write($"{argType} {argValue}");
        }

        WriteLine(")");
        OpenScope();
    }

    public virtual void WriteBeginLocalMethod(
        string name,
        bool isStatic,
        string returnType = "void",
        params (string Type, string Name)[] args)
    {
        if (isStatic)
        {
            Write("static ");
        }

        Write($"{returnType} {name}(");

        var argCounter = 0;
        foreach (var (argType, argValue) in args)
        {
            if (argCounter++ > 0)
            {
                Write(", ");
            }

            Write($"{argType} {argValue}");
        }

        WriteLine(")");
        OpenScope();
    }

    public virtual void WriteEndMethod(bool addTrailingLine = true) => CloseScope(addTrailingLine: addTrailingLine);

    public virtual void WriteEmptyConstructor(
        string name,
        string baseName = null!,
        Access access = Access.Public,
        Modifiers modifiers = Modifiers.None,
        params (string Type, string Name)[] args)
    {
        WriteBeginConstructor(name, baseName!, access, modifiers, args);
        CloseScope();
    }

    public virtual void WriteBeginStaticConstructor(string className)
    {
        WriteLine($"{className}()");
        OpenScope();
    }

    public virtual void WriteBeginConstructor(
        string name,
        string baseName = null!,
        Access access = Access.Public,
        Modifiers modifiers = Modifiers.None,
        params (string Type, string Name)[] args)
    {
        var modtext = modifiers.Format(appendSpace: true);

        Write($"{GetAccessText(access)} {modtext}{name}(");
        var argCounter = 0;

        foreach (var arg in args)
        {
            if (argCounter++ > 0)
            {
                Write(", ");
            }

            Write($"{arg.Type} {arg.Name}");
        }

        WriteLine(")");

        if (baseName!.NotNullOrEmpty())
        {
            PushIndent();
            Write(": base(");
            argCounter = 0;

            foreach (var arg in args)
            {
                if (argCounter++ > 0)
                {
                    Write(", ");
                }

                Write($"{arg.Name}");
            }

            WriteLine(")");
            PopIndent();
        }

        OpenScope();
    }

    public virtual void WritePragmaIf(string condition)
    {
        WriteLine($"#if {condition}");
        PushIndent();
    }

    public virtual void WritePragmaElse()
    {
        PopIndent();
        WriteLine("#else");
        PushIndent();
    }

    public virtual void WritePragmaEndIf(bool addTrailingLine = true)
    {
        PopIndent();
        WriteLine("#endif");

        if (addTrailingLine)
        {
            RawWriteLine();
        }
    }

    public virtual void WriteAttribute(string a) => WriteLine("[{0}]", a);

    public virtual void WriteAttributeLine() => WriteLine();

    public virtual void WriteBeginInterface(string interfaceName, Access access = Access.Public) =>
        WriteBeginInterface(interfaceName, null, access);

    public void WriteReadonlyProperty(
        string type,
        string name,
        string value,
        Access access = Access.Public,
        params Modifiers[] modifiers)
    {
        var modtext = modifiers.Format();

        if (modtext.Length > 0)
        {
            modtext = " " + modtext;
        }

        WriteLine($"{GetAccessText(access)}{modtext} {type} {name}");
        OpenScope();
        WriteLine($"get => {value};");
        CloseScope();
    }

    public void WriteAutoProperty(
        string type,
        string name,
        string? initializer = null,
        Access access = Access.Public,
        Access setterAccess = Access.Public,
        params Modifiers[] modifiers)
    {
        var modtext = modifiers.Format();

        if (modtext.Length > 0)
        {
            modtext = " " + modtext;
        }

        var setterAccessText = setterAccess == access
            ? ""
            : GetAccessText(setterAccess) + " ";

        Write($"{GetAccessText(access)}{modtext} {type} {name} {{ get; {setterAccessText}set; }}");
        if (initializer != null)
        {
            Write($" = {initializer};");
        }

        BlankLine();
    }

    public virtual void WriteBeginInterface(
        string interfaceName,
        string? baseInterfaceName,
        Access access = Access.Public)
    {
        Write($"{GetAccessText(access)} interface {interfaceName}");

        if (!string.IsNullOrEmpty(baseInterfaceName))
        {
            Write($" : {baseInterfaceName}");
        }

        OpenScope();
    }

    public virtual void WriteEndInterface(bool addTrailingLine = true) => CloseScope(addTrailingLine: addTrailingLine);

    public void LineDefault()
    {
        _lastLineNumber = null;
        WriteLine("#line default");
    }

    public void LineHidden()
    {
        _lastLineNumber = null;
        WriteLine("#line hidden");
    }

    public void LinePragma(int lineNumber, string? fileName = null)
    {
        if (_lastLineNumber == lineNumber &&
            fileName == LineDirectiveFile)
        {
            return;
        }

        fileName ??= LineDirectiveFile;

        WriteLine($"#line {lineNumber} \"{fileName}\"");
    }

    private class ScopeContext : IDisposable
    {
        private readonly bool _addTrailingLine;
        private readonly CodeWriter _writer;

        public ScopeContext(CodeWriter writer, bool addTrailingLine)
        {
            _writer = writer;
            _addTrailingLine = addTrailingLine;
        }

        public void Dispose() => _writer.CloseScope(addTrailingLine: _addTrailingLine);
    }

    public string LiteralText(string text) => $"\"\"\"{text}\"\"\"";

    public void WriteLiteralText(string text)
    {
        text = $"\"\"\"{text}\"\"\"";
        RawWrite(text);
    }

    public void WriteBlock(string block, bool reindent = false)
    {
        if (!reindent)
        {
            RawWrite(block);
            return;
        }

        var lineReader = new StringReader(block);

        while (lineReader.ReadLine() is { } line)
        {
            if (line.IsNullOrWhiteSpace())
            {
                RawWriteLine();
                continue;
            }

            WriteLine(line);
        }
    }

    public string EncodeLine(string line)
    {
        return line.Replace(@"\", @"\\")
            .Replace("\"", "\\\"");
    }

    public string QuotedText(string text, bool encode = true)
    {
        if (encode)
        {
            text = text.Replace(@"\", @"\\")
                .Replace("\"", "\\\"");
        }

        return $"\"{text}\"";
    }

    public CodeWriter If(string condition, Action? body = null, Action? esle = null)
    {
        WriteLine($"if ({condition})");
        OpenScope();
        if (body == null)
        {
            return this;
        }

        body();

        if (esle != null)
        {
            CloseScope();
            WriteLine("else");
            OpenScope();
            esle();
        }

        CloseScope();
        return this;
    }

    public CodeWriter ElseIf(string condition, Action? body = null, Action? esle = null)
    {
        Write("else ");
        If(condition, body, esle);
        return this;
    }

    public CodeWriter Else(Action? body = null)
    {
        Write("else");
        OpenScope();
        if (body == null)
        {
            return this;
        }

        body();
        CloseScope();

        return this;
    }

    public void EndIf() => CloseScope();


    public (string Text, bool MultiLine) QuoteString(string value)
    {
        // If the string is short, use C style quoting (e.g "\r\n")
        // Also do it if it is too long to fit in one line
        // If the string contains '\0', verbatim style won't work.
        if (value.Length < 256 ||
            value.Length > 1500 ||
            value.Contains('\0'))
        {
            return QuoteSnippetStringCStyle(value);
        }

        // Otherwise, use 'verbatim' style quoting (e.g. @"foo")
        return QuoteSnippetStringVerbatimStyle(value);
    }

    private const int MaxLineLength = 120;

    private (string Text, bool MultiLine) QuoteSnippetStringCStyle(string value)
    {
        var b = new StringBuilder(value.Length + 5);

        b.Append("\"");

        var isStringMultiline = false;
        var i = 0;
        while (i < value.Length)
        {
            switch (value[i])
            {
                case '\r':
                    b.Append("\\r");
                    break;
                case '\t':
                    b.Append("\\t");
                    break;
                case '\"':
                    b.Append("\\\"");
                    break;
                case '\'':
                    b.Append("\\\'");
                    break;
                case '\\':
                    b.Append("\\\\");
                    break;
                case '\0':
                    b.Append("\\0");
                    break;
                case '\n':
                    b.Append("\\n");
                    break;
                case '\u2028':
                case '\u2029':
                case '\u0085':
                    AppendEscapedChar(b, value[i]);
                    break;

                default:
                    b.Append(value[i]);
                    break;
            }

            if (i > 0 &&
                i % MaxLineLength == 0)
            {
                // If current character is a high surrogate and the following
                // character is a low surrogate, don't break them.
                // Otherwise when we write the string to a file, we might lose
                // the characters.
                if (char.IsHighSurrogate(value[i]) &&
                    i < value.Length - 1 &&
                    char.IsLowSurrogate(value[i + 1]))
                {
                    b.Append(value[++i]);
                }

                if (i != value.Length - 1)
                {
                    b.Append("\" +");
                    b.Append(StringExtensions.NewLine);
                    b.Append(CurrentIndent);
                    b.Append('\"');
                    isStringMultiline = true;
                }
            }

            ++i;
        }

        b.Append('\"');

        if (isStringMultiline)
        {
            b.Insert(0, '(');
            b.Append(')');
        }

        return (b.ToString(), isStringMultiline);
    }

    private static void AppendEscapedChar(StringBuilder b, char value)
    {
        b.Append("\\u");
        b.Append(((int)value).ToString("X4"));
    }

    private (string Text, bool MultiLine)  QuoteSnippetStringVerbatimStyle(string value)
    {
        var b = new StringBuilder(value.Length + 5);

        var isStringMultiline = false;

        b.Append("@\"");

        for (var i = 0; i < value.Length; i++)
        {
            if (value[i] == '\r' ||
                value[i] == '\n')
            {
                isStringMultiline = true;
            }

            if (value[i] == '\"')
                b.Append("\"\"");
            else
                b.Append(value[i]);
        }

        b.Append('\"');

        return (b.ToString(), isStringMultiline);
    }
}
