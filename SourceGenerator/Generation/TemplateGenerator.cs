//
// TemplatingHost.cs
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
using System.Reflection;
using System.Text;
using Std.TextTemplating.Generation.Directives;
using Std.TextTemplating.Generation.Parsing;


namespace Std.TextTemplating.Generation;

public class TemplateGenerator// : ITextTemplatingEngineHost
{
    private static readonly char[] ParameterInitialSplitChars = ['=', '!'];
    private readonly Dictionary<string, KeyValuePair<string, string>> _directiveProcessors = new();

    private readonly Dictionary<ParameterKey, string> _parameters = new();

    //re-usable
    private TemplatingEngine? _engine;

    public TemplateGenerator()
    {
        Imports.Add("System");
    }

    //host properties for consumers to access
    public TemplateErrorCollection Errors { get; } = [];
    public List<string> Imports { get; } = [];
    public List<string> IncludePaths { get; } = [];
    public List<string> ReferencePaths { get; } = [];
    public string? OutputFile { get; protected set; }
    public bool UseRelativeLinePragmas { get; set; }

    private TemplatingEngine Engine => _engine ??= new TemplatingEngine();

    public string TemplateFile { get; protected set; } = null!;

    public virtual object? GetHostOption(string optionName) =>
        optionName switch
        {
            "UseRelativeLinePragmas" => UseRelativeLinePragmas,
            _ => null
        };

    // protected virtual string ResolvePath (string path)
    // {
    // 	if (!string.IsNullOrEmpty(path)) {
    // 		path = Environment.ExpandEnvironmentVariables (path);
    // 		if (Path.IsPathRooted (path))
    // 			return path;
    // 	}
    //
    // 	// Get the template directory, or working directory if there is no file.
    // 	// This can happen if the template text is passed in on the commandline.
    // 	var dir = string.IsNullOrEmpty (TemplateFile)
    // 		? Environment.CurrentDirectory
    // 		: Path.GetDirectoryName (Path.GetFullPath (TemplateFile));
    //
    // 	// if the user passed in null or string.empty, they just want the directory.
    // 	if (string.IsNullOrEmpty (path)) {
    // 		return dir;
    // 	}
    //
    // 	var test = Path.Combine (dir, path);
    // 	if (File.Exists (test) || Directory.Exists (test))
    // 		return test;
    //
    // 	return path;
    // }

    public void LogErrors(TemplateErrorCollection errors) => Errors.AddRange(errors);


    public void SetFileExtension(string extension)
    {
        if (OutputFile is null)
        {
            return;
        }

        extension = extension.TrimStart('.');
        OutputFile = Path.HasExtension(OutputFile)
            ? Path.ChangeExtension(OutputFile, extension)
            : OutputFile + "." + extension;
    }

    public void SetOutputEncoding(Encoding encoding, bool fromOutputDirective)
    {
    }


    private void InitializeForRun(
        string inputFileName,
        string? outputFileName = null,
        Encoding? encoding = null)
    {
        Errors.Clear();

        if (outputFileName is null)
        {
            outputFileName = Path.ChangeExtension(inputFileName, ".cs");
        }

        TemplateFile = inputFileName;
        OutputFile = outputFileName;
    }

    // public bool PreprocessTemplate(
    //     string inputFileName,
    //     string className,
    //     string classNamespace,
    //     string inputContent,
    //     out string language,
    //     out string[] references,
    //     out string outputContent)
    // {
    //     InitializeForRun(null, inputFileName);
    //
    //     outputContent = Engine.PreprocessTemplate(
    //         inputContent, this, className, classNamespace, out language,
    //         out references);
    //
    //     return !Errors.HasErrors;
    // }

    private TemplateError AddError(string error)
    {
        var err = new TemplateError
        {
            ErrorText = error
        };
        Errors.Add(err);
        return err;
    }

    //KEEP!
    public ParsedTemplate ParseTemplate(string inputFile, string inputContent)
    {
        TemplateFile = inputFile;
        return ParsedTemplate.FromTextInternal(inputContent, this);
    }

    //KEEP!
    public string? PreprocessTemplate(
        ParsedTemplate pt,
        string inputFile,
        string inputContent,
        TemplateSettings settings)
    {
        InitializeForRun(inputFile);
        return new TemplatingEngine().PreprocessTemplate(pt, inputContent, settings, this);
    }

    public void AddDirectiveProcessor(string name, string klass, string assembly) =>
        _directiveProcessors.Add(name, new KeyValuePair<string, string>(klass, assembly));

    public void AddParameter(
        string processorName,
        string directiveName,
        string parameterName,
        string value) =>
        _parameters.Add(new ParameterKey(processorName, directiveName, parameterName), value);

    /// <summary>
    ///     Parses a parameter and adds it.
    /// </summary>
    /// <returns>Whether the parameter was parsed successfully.</returns>
    /// <param name="unparsedParameter">Parameter in name=value or processor!directive!name!value format.</param>
    public bool TryAddParameter(string unparsedParameter)
    {
        if (TryParseParameter(unparsedParameter, out var processor, out var directive, out var name, out var value))
        {
            AddParameter(processor, directive, name, value);
            return true;
        }

        return false;
    }

    internal static bool TryParseParameter(
        string parameter,
        out string processor,
        out string directive,
        out string name,
        out string value)
    {
        processor = directive = name = value = "";

        var start = 0;
        var end = parameter.IndexOfAny(ParameterInitialSplitChars);
        if (end < 0)
        {
            return false;
        }

        //simple format n=v
        if (parameter[end] == '=')
        {
            name = parameter.Substring(start, end);
            value = parameter.Substring(end + 1);
            return !string.IsNullOrEmpty(name);
        }

        //official format, p!d!n!v
        processor = parameter.Substring(start, end);

        start = end + 1;
        end = parameter.IndexOf('!', start);
        if (end < 0)
        {
            //unlike official version, we allow you to omit processor/directive
            name = processor;
            value = parameter.Substring(start);
            processor = "";
            return !string.IsNullOrEmpty(name);
        }

        directive = parameter.Substring(start, end - start);

        start = end + 1;
        end = parameter.IndexOf('!', start);
        if (end < 0)
        {
            //we also allow you just omit the processor
            name = directive;
            directive = processor;
            value = parameter.Substring(start);
            processor = "";
            return !string.IsNullOrEmpty(name);
        }

        name = parameter.Substring(start, end - start);
        value = parameter.Substring(end + 1);

        return !string.IsNullOrEmpty(name);
    }

    public bool LoadIncludeText(
        string requestFileName,
        out string content,
        out string location)
    {
        content = "";
        location = "";
        return false;
        // location = ResolvePath (requestFileName);
        //
        // if (location == null || !File.Exists (location)) {
        // 	foreach (string path in IncludePaths) {
        // 		string f = Path.Combine (path, requestFileName);
        // 		if (File.Exists (f)) {
        // 			location = f;
        // 			break;
        // 		}
        // 	}
        // }
        //
        // if (location == null)
        // 	return false;
        //
        // try {
        // 	content = File.ReadAllText (location);
        // 	return true;
        // } catch (IOException ex) {
        // 	AddError ("Could not read included file '" + location + "':\n" + ex);
        // }
        // return false;
    }

    /// <summary>
    ///     Gets any additional directive processors to be included in the processing run.
    /// </summary>
    public virtual IEnumerable<IDirectiveProcessor> GetAdditionalDirectiveProcessors()
    {
        yield break;
    }

    protected virtual string ResolveAssemblyReference(string assemblyReference) => "";

    // if (Path.IsPathRooted (assemblyReference))
    // 	return assemblyReference;
    // foreach (string referencePath in ReferencePaths) {
    // 	var path = Path.Combine (referencePath, assemblyReference);
    // 	if (File.Exists (path))
    // 		return path;
    // }
    //
    // var assemblyName = new AssemblyName(assemblyReference);
    // if (assemblyName.Version != null)//Load via GAC and return full path
    // 	return Assembly.Load (assemblyName).Location;
    //
    // if (KnownAssemblies.TryGetValue (assemblyReference, out string mappedAssemblyReference)) {
    // 	return mappedAssemblyReference;
    // }
    //
    // if (!assemblyReference.EndsWith (".dll", StringComparison.OrdinalIgnoreCase) && !assemblyReference.EndsWith (".exe", StringComparison.OrdinalIgnoreCase))
    // 	return assemblyReference + ".dll";
    // return assemblyReference;

    protected virtual string? ResolveParameterValue(
        string? directiveId,
        string? processorName,
        string? parameterName)
    {
        var key = new ParameterKey(processorName, directiveId, parameterName);
        if (_parameters.TryGetValue(key, out var value))
        {
            return value;
        }

        if (processorName != null ||
            directiveId != null)
        {
            return ResolveParameterValue(null, null, parameterName);
        }

        return null;
    }

    protected Type ResolveDirectiveProcessor(string processorName)
    {
        if (!_directiveProcessors.TryGetValue(processorName, out var value))
        {
            throw new TemplatingEngineException($"No directive processor registered as '{processorName}'");
        }

        var asmPath = ResolveAssemblyReference(value.Value);
        if (asmPath == null)
        {
            throw new TemplatingEngineException(
                $"Could not resolve assembly '{value.Value}' for directive processor '{processorName}'");
        }

        var asm = Assembly.LoadFrom(asmPath);
        return asm.GetType(value.Key, true);
    }

    private struct ParameterKey : IEquatable<ParameterKey>
    {
        public ParameterKey(string? processorName, string? directiveName, string? parameterName)
        {
            _processorName = processorName ?? "";
            _directiveName = directiveName ?? "";
            _parameterName = parameterName ?? "";

            unchecked
            {
                _hashCode = _processorName.GetHashCode() ^
                    _directiveName.GetHashCode() ^
                    _parameterName.GetHashCode();
            }
        }

        private readonly string _processorName;

        private readonly string _directiveName;

        private readonly string _parameterName;

        private readonly int _hashCode;

        public override bool Equals(object? obj) => obj is ParameterKey other && Equals(other);

        public bool Equals(ParameterKey other) =>
            _processorName == other._processorName &&
            _directiveName == other._directiveName &&
            _parameterName == other._parameterName;

        public override int GetHashCode() => _hashCode;
    }
}
