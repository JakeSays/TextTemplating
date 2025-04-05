using System.Collections.Immutable;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Std.TextTemplating.Generation;


namespace Std.TextTemplating;

[Generator]
public class TextTemplatingSourceGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var provider = context.AdditionalTextsProvider
            .Where(f => Path.GetExtension(f.Path) == ".tt")
            .Collect();

        context.RegisterSourceOutput(provider, GenerateCode);
    }

    private void GenerateCode(SourceProductionContext context, ImmutableArray<AdditionalText> files)
    {
        foreach (var file in files)
        {
            ProcessTemplate(context, file);
        }
    }

    private void ProcessTemplate(SourceProductionContext context, AdditionalText sourceFile)
    {
        var templateText = sourceFile.GetText()?.ToString();
        if (templateText == null)
        {
            return;
        }

        var generator = new TemplateGenerator();
        var pt = generator.ParseTemplate(sourceFile.Path, templateText);

        var settings = TemplatingEngine.GetSettings(pt);
        if (pt.Errors.Count > 0)
        {
            generator.Errors.AddRange (pt.Errors);
        }

        var templateClass = generator.PreprocessTemplate(pt, sourceFile.Path, templateText, settings);
        if (templateClass == null ||
            generator.Errors.Count > 0)
        {
            var sb = new StringBuilder();
            sb.AppendLine("//There were errors processing the template:");
            foreach (var error in generator.Errors)
            {
                sb.AppendLine($"//  {error}");
            }

            templateClass = sb.ToString();
        }

        context.AddSource($"{Path.GetFileName(sourceFile.Path)}.g.cs", SourceText.From(templateClass, Encoding.UTF8));
    }
}
