//
// TemplateSettings.cs
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
using System.Globalization;
using System.Text;
using Std.TextTemplating.Generation.Directives;
using Std.TextTemplating.Generation.Parsing;


namespace Std.TextTemplating.Generation;

public class TemplateSettings
{
    public TemplateSettings()
    {
        Imports = [];
        CustomDirectives = [];
        DirectiveProcessors = new Dictionary<string, IDirectiveProcessor>();
    }

    public bool Debug { get; set; }

    public string? Inherits { get; set; }
    public string? Name { get; set; }
    public string? Namespace { get; set; }
    public HashSet<string> Imports { get; private set; }
    public string Language { get; set; } = null!;
    public string LangVersion { get; set; } = null!;
    public Encoding Encoding { get; set; } = null!;
    public string? Extension { get; set; }
    public List<CustomDirective> CustomDirectives { get; private set; }
    public Dictionary<string, IDirectiveProcessor> DirectiveProcessors { get; private set; }

    public bool IncludePreprocessingHelpers { get; set; }

//	public bool IsPreprocessed { get; set; }
    public bool RelativeLinePragmas { get; set; }
    public bool LinePragmas { get; set; }
    public bool InternalVisibility { get; set; }

    /// <summary>
    ///     Base directory for calculation of relative line pragmas.
    ///     Internal until we clean up the settings API.
    /// </summary>
    internal string? RelativeLinePragmasBaseDirectory { get; set; }

    public string GetFullName() =>
        string.IsNullOrEmpty(Namespace)
            ? Name!
            : Namespace + "." + Name;
}

public class CustomDirective
{
    public CustomDirective(string processorName, Directive directive)
    {
        ProcessorName = processorName;
        Directive = directive;
    }

    public string ProcessorName { get; set; }
    public Directive Directive { get; set; }
}
