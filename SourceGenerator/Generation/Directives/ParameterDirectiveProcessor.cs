//
// ParameterDirectiveProcessor.cs
//
// Author:
//       Mikayla Hutchinson <m.j.hutchinson@gmail.com>
//
// Copyright (c) 2010 Novell, Inc.
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

using System.Collections.Generic;


namespace Std.TextTemplating.Generation.Directives;

public sealed class ParameterDirectiveProcessor
    : DirectiveProcessor
{
    private static readonly Dictionary<string, string> BuiltinTypesMap = new()
    {
        { "bool", "System.Boolean" },
        { "byte", "System.Byte" },
        { "sbyte", "System.SByte" },
        { "char", "System.Char" },
        { "decimal", "System.Decimal" },
        { "double", "System.Double" },
        { "float ", "System.Single" },
        { "int", "System.Int32" },
        { "uint", "System.UInt32" },
        { "long", "System.Int64" },
        { "ulong", "System.UInt64" },
        { "object", "System.Object" },
        { "short", "System.Int16" },
        { "ushort", "System.UInt16" },
        { "string", "System.String" }
    };


    public bool RequiresProcessingRunIsHostSpecific => false;

    public override void StartProcessingRun(
        CodeWriter output,
        string templateContents,
        TemplateErrorCollection errors)
    {
    }

    public override void FinishProcessingRun()
    {
        // var statement = Statement.If(
        //     Expression.This.Property("Errors")
        //         .Property("HasErrors")
        //         .IsEqualValue(Expression.False),
        //     _postStatements.ToArray());
        //
        // _postStatements.Clear();
        // _postStatements.Add(statement);
    }

    public override string[]? GetImportsForProcessingRun() => null;

    public override string[]? GetReferencesForProcessingRun() => null;

    public override bool IsDirectiveSupported(string directiveName) => directiveName == "parameter";

    public static string MapTypeName(string? typeName)
    {
        if (string.IsNullOrEmpty(typeName))
        {
            return "System.String";
        }

        return BuiltinTypesMap.TryGetValue(typeName!, out var mappedType)
            ? mappedType
            : typeName ?? "System.String";
    }

    public override void ProcessDirective(
        CodeWriter output,
        string directiveName,
        Dictionary<string, string> arguments)
    {
        if (!arguments.TryGetValue("name", out var name) ||
            string.IsNullOrEmpty(name))
        {
            throw new DirectiveProcessorException("Parameter directive has no name argument");
        }

        arguments.TryGetValue("type", out var typeName);
        typeName = MapTypeName(typeName);

        output.WriteAutoProperty(typeName, name);
    }
}
