//
// DirectiveProcessor.cs
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

using System.Collections.Generic;


namespace Std.TextTemplating.Generation.Directives;

public abstract class DirectiveProcessor : IDirectiveProcessor
{
    private TemplateErrorCollection _errors = null!;

    public virtual void Initialize()
    {
    }

    public virtual void StartProcessingRun(
        CodeWriter output,
        string templateContents,
        TemplateErrorCollection errors)
    {
        _errors = errors;
    }

    public abstract void FinishProcessingRun();
    public abstract string[]? GetImportsForProcessingRun();
    public abstract string[]? GetReferencesForProcessingRun();
    public abstract bool IsDirectiveSupported(string directiveName);

    public abstract void ProcessDirective(
        CodeWriter output,
        string directiveName,
        Dictionary<string, string> arguments);

    public virtual List<string>? GetTemplateClassCustomAttributes() => null;

    TemplateErrorCollection IDirectiveProcessor.Errors => _errors;

    void IDirectiveProcessor.SetProcessingRunIsHostSpecific(bool hostSpecific)
    {
    }

    bool IDirectiveProcessor.RequiresProcessingRunIsHostSpecific => false;
}
