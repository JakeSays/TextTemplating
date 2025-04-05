using System.Collections.Generic;


namespace Std.TextTemplating.Generation.Directives;

public interface IDirectiveProcessor
{
    TemplateErrorCollection Errors { get; }
    bool RequiresProcessingRunIsHostSpecific { get; }

    void FinishProcessingRun();
    string[]? GetImportsForProcessingRun();
    string[]? GetReferencesForProcessingRun();
    List<string>? GetTemplateClassCustomAttributes(); //TODO
    void Initialize();
    bool IsDirectiveSupported(string directiveName);
    void ProcessDirective(CodeWriter output, string directiveName, Dictionary<string, string> arguments);
    void SetProcessingRunIsHostSpecific(bool hostSpecific);

    void StartProcessingRun(
        CodeWriter output,
        string templateContents,
        TemplateErrorCollection errors);
}
