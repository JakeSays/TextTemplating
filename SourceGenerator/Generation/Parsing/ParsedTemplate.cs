//
// Template.cs
//
// Author:
//       Mikayla Hutchinson <m.j.hutchinson@gmail.com>
//
// Copyright (c) 2009 Novell, Inc. (http://www.novell.com)
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Collections.Generic;
using System.IO;


namespace Std.TextTemplating.Generation.Parsing;

public class ParsedTemplate
{
    private readonly List<ISegment> _importedHelperSegments = [];
    private readonly string _rootFileName;

    public ParsedTemplate(string rootFileName)
    {
        _rootFileName = rootFileName;
    }

    public List<ISegment> RawSegments { get; } = [];

    public IEnumerable<Directive> Directives
    {
        get
        {
            foreach (var seg in RawSegments)
            {
                if (seg is Directive dir)
                {
                    yield return dir;
                }
            }
        }
    }

    public IEnumerable<TemplateSegment> Content
    {
        get
        {
            foreach (var seg in RawSegments)
            {
                if (seg is TemplateSegment ts)
                {
                    yield return ts;
                }
            }
        }
    }

    public TemplateErrorCollection Errors { get; } = [];

    internal static ParsedTemplate FromTextInternal(string content, TemplateGenerator host)
    {
        var filePath = host.TemplateFile;
        var template = new ParsedTemplate(filePath);
        try
        {
            template.Parse(
                host, new HashSet<string>(StringComparer.OrdinalIgnoreCase),
                new Tokeniser(filePath, content), true);
        }
        catch (ParserException ex)
        {
            template.LogError(ex.Message, ex.Location);
        }

        return template;
    }

    private void Parse(
        TemplateGenerator host,
        HashSet<string> includedFiles,
        Tokeniser tokeniser,
        bool parseIncludes) =>
        Parse(host, includedFiles, tokeniser, parseIncludes, false);

    private void Parse(
        TemplateGenerator host,
        HashSet<string> includedFiles,
        Tokeniser tokeniser,
        bool parseIncludes,
        bool isImport)
    {
        var skip = false;
        var addToImportedHelpers = false;
        while ((skip || tokeniser.Advance()) && tokeniser.State != State.EOF)
        {
            skip = false;
            ISegment? seg = null;
            switch (tokeniser.State)
            {
                case State.Block:
                    if (!string.IsNullOrEmpty(tokeniser.Value))
                    {
                        seg = new TemplateSegment(SegmentType.Block, tokeniser.Value!, tokeniser.Location);
                    }

                    break;
                case State.Content:
                    if (!string.IsNullOrEmpty(tokeniser.Value))
                    {
                        seg = new TemplateSegment(SegmentType.Content, tokeniser.Value!, tokeniser.Location);
                    }

                    break;
                case State.Expression:
                    if (!string.IsNullOrEmpty(tokeniser.Value))
                    {
                        seg = new TemplateSegment(SegmentType.Expression, tokeniser.Value!, tokeniser.Location);
                    }

                    break;
                case State.Helper:
                    addToImportedHelpers = isImport;
                    if (!string.IsNullOrEmpty(tokeniser.Value))
                    {
                        seg = new TemplateSegment(SegmentType.Helper, tokeniser.Value!, tokeniser.Location);
                    }

                    break;
                case State.Directive:
                    Directive? directive = null;
                    string? attName = null;
                    while (!skip && tokeniser.Advance())
                    {
                        switch (tokeniser.State)
                        {
                            case State.DirectiveName:
                                if (directive == null)
                                {
                                    directive = new Directive(tokeniser.Value!, tokeniser.Location)
                                    {
                                        TagStartLocation = tokeniser.TagStartLocation
                                    };
                                    if (!parseIncludes ||
                                        !string.Equals(
                                            directive.Name, "include",
                                            StringComparison.OrdinalIgnoreCase))
                                    {
                                        RawSegments.Add(directive);
                                    }
                                }
                                else
                                {
                                    attName = tokeniser.Value;
                                }

                                break;
                            case State.DirectiveValue:
                                if (attName != null &&
                                    directive != null)
                                {
                                    directive.Attributes[attName] = tokeniser.Value!;
                                }
                                else
                                {
                                    LogError("Directive value without name", tokeniser.Location);
                                }

                                attName = null;
                                break;
                            case State.Directive:
                                if (directive != null)
                                {
                                    directive.EndLocation = tokeniser.TagEndLocation;
                                }

                                break;
                            default:
                                skip = true;
                                break;
                        }
                    }

                    if (parseIncludes &&
                        directive != null &&
                        directive.Name.EqualsNoCase("include"))
                    {
                        Import(host, includedFiles, directive, Path.GetDirectoryName(tokeniser.Location.FileName)!);
                    }

                    break;
                default:
                    throw new InvalidOperationException();
            }

            if (seg != null)
            {
                seg.TagStartLocation = tokeniser.TagStartLocation;
                seg.EndLocation = tokeniser.TagEndLocation;
                if (addToImportedHelpers)
                {
                    _importedHelperSegments.Add(seg);
                }
                else
                {
                    RawSegments.Add(seg);
                }
            }
        }

        if (!isImport)
        {
            AppendAnyImportedHelperSegments();
        }
    }

    private static string FixWindowsPath(string path) =>
        Path.DirectorySeparatorChar == '/'
            ? path.Replace('\\', '/')
            : path;

    private void Import(
        TemplateGenerator host,
        HashSet<string> includedFiles,
        Directive includeDirective,
        string relativeToDirectory)
    {
        if (!includeDirective.Attributes.TryGetValue("file", out var rawFilename))
        {
            LogError("Include directive has no file attribute", includeDirective.StartLocation);
            return;
        }

        var fileName = FixWindowsPath(rawFilename);

        var once = false;
        if (includeDirective.Attributes.TryGetValue("once", out var onceStr))
        {
            if (!bool.TryParse(onceStr, out once))
            {
                LogError(
                    $"Include once attribute has unknown value '{onceStr}'",
                    includeDirective.StartLocation);
            }
        }

        //try to resolve path relative to the file that included it
        // if (relativeToDirectory != null && !Path.IsPathRooted (fileName)) {
        // 	string possible = Path.Combine (relativeToDirectory, fileName);
        // 	if (File.Exists (possible)) {
        // 		fileName = Path.GetFullPath (possible);
        // 	}
        // }

        if (host.LoadIncludeText(fileName, out var content, out var resolvedName))
        {
            // unfortunately we can't use the once check to avoid actually reading the file
            // as the host resolves the filename and reads the file in a single call
            if (!includedFiles.Add(resolvedName) && once)
            {
                return;
            }

            Parse(host, includedFiles, new Tokeniser(resolvedName, content), true, true);
        }
        else
        {
            LogError($"Could not resolve include file '{rawFilename}'.", includeDirective.StartLocation);
        }
    }

    private void AppendAnyImportedHelperSegments()
    {
        RawSegments.AddRange(_importedHelperSegments);
        _importedHelperSegments.Clear();
    }

    private void LogError(string message, Location location, bool isWarning)
    {
        var err = new TemplateError
        {
            ErrorText = message
        };
        if (location.FileName != null)
        {
            err.Line = location.Line;
            err.Column = location.Column;
            err.FileName = location.FileName ?? string.Empty;
        }
        else
        {
            err.FileName = _rootFileName ?? string.Empty;
        }

        err.IsWarning = isWarning;
        Errors.Add(err);
    }

    public void LogError(string message) => LogError(message, Location.Empty, false);

    public void LogWarning(string message) => LogError(message, Location.Empty, true);

    public void LogError(string message, Location location) => LogError(message, location, false);

    public void LogWarning(string message, Location location) => LogError(message, location, true);
}
