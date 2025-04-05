//
// Engine.cs
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
using System.Text;
using Std.TextTemplating.Generation.Directives;
using Std.TextTemplating.Generation.Parsing;


namespace Std.TextTemplating.Generation;

public partial class TemplatingEngine
{
    private ParsedTemplate _parsedTemplate = null!;
    private TemplateSettings _settings = null!;
    private TemplateGenerator _host = null!;

    private CodeWriter Output { get; } = new();
    private CodeWriter NestedWriter { get; } = new();

    public string? PreprocessTemplate(
        ParsedTemplate parsedTemplate,
        string content,
        TemplateSettings settings,
        TemplateGenerator host)
    {
        if (string.IsNullOrEmpty(content))
        {
            throw new ArgumentException(
                $"'{nameof(content)}' cannot be null or empty.", nameof(content));
        }

        _parsedTemplate = parsedTemplate;
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _host = host ?? throw new ArgumentNullException(nameof(host));

        _parsedTemplate = parsedTemplate ?? throw new ArgumentNullException(nameof(parsedTemplate));
        return PreprocessTemplateInternal(content);
    }

    private string? PreprocessTemplateInternal(string content)
    {
        _settings.IncludePreprocessingHelpers = string.IsNullOrEmpty(_settings.Inherits);

        GenerateCompileUnit(content);

        _host.LogErrors(_parsedTemplate.Errors);
        if (_parsedTemplate.Errors.HasErrors)
        {
            return null;
        }

        return Output.CodeOutput;
    }

    public static TemplateSettings GetSettings(ParsedTemplate parsedTemplates)
    {
        var settings = new TemplateSettings();

        var relativeLinePragmas = false;// host.GetHostOption("UseRelativeLinePragmas") as bool? ?? false;
        foreach (var dt in parsedTemplates.Directives)
        {
            switch (dt.Name.ToLowerInvariant())
            {
                case "template":
                    settings.Language = dt.Extract("language") ?? "C#";
                    if (dt.Extract("langversion") is { } langVersion)
                    {
                        settings.LangVersion = langVersion;
                    }

                    settings.Debug = dt.ExtractBool("debug");
                    settings.Inherits = dt.Extract("inherits");

                    // val = dt.Extract ("culture");
                    // if (val != null) {
//                     	var culture = System.Globalization.CultureInfo.GetCultureInfo (val);
                    // 	if (culture == null)
                    // 		pt.LogWarning ("Could not find culture '" + val + "'", dt.StartLocation);
                    // 	else
                    // 		settings.Culture = culture;
                    // }

                    relativeLinePragmas = dt.Extract("relativeLinePragmas", false);
                    settings.LinePragmas = dt.Extract("linePragmas", true);
                    settings.InternalVisibility = dt.Extract("visibility").EqualsNoCase("internal");

                    break;

                case "class":
                {
                    settings.Name = dt.Extract("name");
                    if (settings.Name == null)
                    {
                        parsedTemplates.LogError("Missing name attribute in class directive", dt.StartLocation);
                    }

                    break;
                }

                case "namespace":
                {
                    settings.Namespace = dt.Extract("name");
                    if (settings.Namespace == null)
                    {
                        parsedTemplates.LogError(
                            "Missing name attribute in namespace directive", dt.StartLocation);
                    }

                    break;
                }
                //
                // case "assembly":
                // {
                //     var name = dt.Extract("name");
                //     if (name == null)
                //     {
                //         parsedTemplates.LogError(
                //             "Missing name attribute in assembly directive", dt.StartLocation);
                //         break;
                //     }
                //
                //     settings.Assemblies.Add(name);
                //
                //     break;
                // }

                case "import":
                    var ns = dt.Extract("namespace");
                    if (ns == null)
                    {
                        parsedTemplates.LogError(
                            "Missing namespace attribute in import directive", dt.StartLocation);
                        break;
                    }

                    settings.Imports.Add(ns);

                    break;

                case "output":
                    settings.Extension = dt.Extract("extension");
                    var encoding = dt.Extract("encoding");
                    if (encoding != null)
                    {
                        settings.Encoding = Encoding.GetEncoding(encoding);
                    }

                    break;

                case "include":
                    throw new InvalidOperationException("Include is handled in the parser");

                case "parameter":
                    AddDirective(settings, nameof(ParameterDirectiveProcessor), dt);
                    continue;

                default:
                    var processorName = dt.Extract("Processor");
                    if (processorName == null)
                    {
                        throw new InvalidOperationException(
                            $"Custom directive '{dt.Name}' does not specify a processor");
                    }

                    AddDirective(settings, processorName, dt);
                    continue;
            }

            ComplainExcessAttributes(dt, parsedTemplates);
        }

        settings.Name ??= "GeneratedTextTransformation";
        settings.Namespace ??= "N42";
        //$"{typeof (TextTransformation).Namespace}{new Random ().Next ():x}";

        settings.RelativeLinePragmas = relativeLinePragmas;

        return settings;
    }

    private static void AddDirective(
        TemplateSettings settings,
        string processorName,
        Directive directive)
    {
        if (!settings.DirectiveProcessors.TryGetValue(processorName, out var processor))
        {
            switch (processorName)
            {
                case "ParameterDirectiveProcessor":
                    processor = new ParameterDirectiveProcessor();
                    break;
                default:
                    throw new InvalidOperationException();
            }

            settings.DirectiveProcessors[processorName] = processor;
        }

        if (!processor.IsDirectiveSupported(directive.Name))
        {
            throw new InvalidOperationException(
                $"Directive processor '{processorName}' does not support directive '{directive.Name}'");
        }

        settings.CustomDirectives.Add(new CustomDirective(processorName, directive));
    }

    private static bool ComplainExcessAttributes(Directive dt, ParsedTemplate pt)
    {
        if (dt.Attributes.Count == 0)
        {
            return false;
        }

        var sb = new StringBuilder("Unknown attributes ");
        var first = true;
        foreach (var key in dt.Attributes.Keys)
        {
            if (!first)
            {
                sb.Append(", ");
            }
            else
            {
                first = false;
            }

            sb.Append(key);
        }

        sb.Append(" found in ");
        sb.Append(dt.Name);
        sb.Append(" directive.");
        pt.LogWarning(sb.ToString(), dt.StartLocation);
        return false;
    }
}
