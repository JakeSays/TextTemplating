// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using Std.TextTemplating.Generation.Parsing;


namespace Std.TextTemplating.Generation;

partial class TemplatingEngine
{
    public void GenerateCompileUnit(string content)
    {
        Output.Reset();

        if (_settings.Namespace != null)
        {
            Output.WriteLine($"namespace {_settings.Namespace};");
            Output.BlankLine(2);
        }

        _settings.Imports.Add("System");
        _settings.Imports.Add("System.Collections.Generic");
        _settings.Imports.Add("System.Text");

        foreach (var ns in _settings.Imports)
        {
            Output.WriteUsing(ns);
        }

        Output.BlankLine(2);

        foreach (var processor in _settings.DirectiveProcessors.Values)
        {
            var attrs = processor.GetTemplateClassCustomAttributes() ?? [];
            foreach (var attr in attrs)
            {
                Output.WriteAttribute(attr);
            }
        }

        Output.WriteBeginClass(
            _settings.Name!,
            access: _settings.InternalVisibility
                ? Access.Internal
                : Access.Public,
            modifiers: Modifiers.Partial);

        Output.WriteLine("private readonly StringBuilder _output = new(1_024);");
        Output.BlankLine();

        Output.WriteAutoProperty("IFormatProvider", "FormatProvider", "System.Globalization.CultureInfo.InvariantCulture");

        ProcessDirectives(content, _parsedTemplate.Errors);

        GenerateTransformMethod(_host.TemplateFile);

        GenerateObjectFormatterSupport();

        //generate the Host property if needed
        // if (settings is { HostSpecific: true, HostPropertyOnBase: false })
        // {
        //     GenerateHostProperty(type, settings.HostType);
        // }

        GenerateInitializationMethod();

        Output.CloseScope();
    }

    private record HelperInfo(TemplateSegment Segment, string FileName);

    private void GenerateTransformMethod(string? templateFile)
    {
        string? pragmasRelativeToDirectory = null;

        if (_settings.RelativeLinePragmas)
        {
            if (!string.IsNullOrEmpty(_settings.RelativeLinePragmasBaseDirectory))
            {
                pragmasRelativeToDirectory =
                    Path.GetFullPath(_settings.RelativeLinePragmasBaseDirectory);
            }

            if (pragmasRelativeToDirectory is null && templateFile is not null)
            {
                pragmasRelativeToDirectory = Path.GetDirectoryName(Path.GetFullPath(templateFile));
            }
        }

        var helpers = new List<HelperInfo>();

        using (Output.WriteMethod("TransformText", "string"))
        {
            Output.WriteLine("_output.Clear();");

            //            var helperMode = false;

            //build the code from the segments
            foreach (var seg in _parsedTemplate.Content)
            {
                var fileName = templateFile!;
                if (_settings.LinePragmas)
                {
                    fileName = seg.StartLocation.FileName ?? templateFile;
                    if (!string.IsNullOrEmpty(fileName))
                    {
                        if (!_settings.RelativeLinePragmas)
                        {
                            fileName = Path.GetFileName(fileName);
                        }
                        else if (pragmasRelativeToDirectory is not null)
                        {
                            fileName = Path.GetFullPath(fileName);
                            fileName = FileUtil.AbsoluteToRelativePath(pragmasRelativeToDirectory, fileName);
                        }
                    }

                    Output.LineDirectiveFile = fileName;
                    Output.LinePragma(seg.StartLocation.Line);
                }

                switch (seg.Type)
                {
                    case SegmentType.Block:
                        // if (helperMode)
                        // {
                        //     //TODO: are blocks permitted after helpers?
                        //     pt.LogError("Blocks are not permitted after helpers", seg.TagStartLocation);
                        // }
                        Output.Write(seg.Text);
                        //Output.WriteBlock($"/*block*/_output.Append({Output.QuoteString(seg.Text)});");
                        Output.BlankLine();
                        break;
                    case SegmentType.Expression:
                        Output.WriteLine($"/*expr*/_output.Append(FormatObject({seg.Text}));");
                        break;
                    case SegmentType.Content:
                    {
                        var (quotedText, isMultiLine) = Output.QuoteString(seg.Text);
                        if (isMultiLine)
                        {
                            Output.WriteLine("/*content*/_output.Append(");
                            Output.Ugh(quotedText);
                            Output.WriteLine(");");
                            break;
                        }

                        Output.WriteLine($"/*content*/_output.Append({quotedText});");
                        break;
                    }
                    case SegmentType.Helper:
                        if (!string.IsNullOrEmpty(seg.Text))
                        {
                            helpers.Add(new HelperInfo(seg, fileName!));
                        }

                        // helperMode = true;
                        break;
                    default:
                        throw new InvalidOperationException();
                }
            }
            Output.WriteLine("return _output.ToString();");
        }

        foreach (var helper in helpers)
        {
            Output.BlankLine();
            Output.LineDirectiveFile = helper.FileName;
            Output.LinePragma(helper.Segment.StartLocation.Line);
            Output.WriteBlock(Output.LiteralText(helper.Segment.Text));
        }
    }

    private void ProcessDirectives(string content, TemplateErrorCollection errors)
    {
        foreach (var processor in _settings.DirectiveProcessors.Values)
        {
            NestedWriter.Reset(Output.CurrentIndent);
            processor.StartProcessingRun(NestedWriter, content, errors);
        }

        foreach (var dt in _settings.CustomDirectives)
        {
            NestedWriter.Reset(Output.CurrentIndent);
            var processor = _settings.DirectiveProcessors[dt.ProcessorName];
            processor.ProcessDirective(NestedWriter, dt.Directive.Name, dt.Directive.Attributes);

            Output.RawWrite(NestedWriter.CodeOutput);
        }

        foreach (var processor in _settings.DirectiveProcessors.Values)
        {
            processor.FinishProcessingRun();

            var imports = processor.GetImportsForProcessingRun();
            if (imports != null)
            {
                _settings.Imports.UnionWith(imports);
            }

            // var references = processor.GetReferencesForProcessingRun();
            // if (references != null)
            // {
            //     _settings.Assemblies.UnionWith(references);
            // }
        }
    }

    private void GenerateInitializationMethod()
    {
    }

    private void GenerateObjectFormatterSupport()
    {
        Output.WriteLine("private readonly Dictionary<Type, Func<object, string>> _formatterCache = [];");
        Output.BlankLine();
        using var _ = Output.WriteMethod("FormatObject", "string", Access.Private, args: ("object", "value"));

        Output.If("value == null", () => Output.WriteLine("throw new ArgumentNullException(\"value\");"));

        Output.If("value is IConvertible vic", () => Output.WriteLine("return vic.ToString(FormatProvider);"));
        Output.WriteLine("var type = value.GetType();");
        Output.If(
            "_formatterCache.TryGetValue(type, out var formatter)", () =>
            {
                Output.WriteLine("return formatter.Invoke(FormatProvider);");
            });
        Output.WriteLine("var toString = type.GetMethod(\"ToString\", [typeof(IConvertible)]);");
        Output.If("toString != null", () =>
        {
            Output.WriteLine("formatter = value => (string) toString.Invoke(value, [FormatProvider]);");
        }, () =>
        {
            Output.WriteLine("formatter = value => value.ToString();");
        });
        Output.WriteLine("_formatterCache.Add(type, formatter);");
        Output.WriteLine("return formatter(value);");
    }
}
