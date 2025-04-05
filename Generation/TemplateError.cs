using System.Globalization;


namespace Std.TextTemplating.Generation;

public class TemplateError
{
    public TemplateError() : this(string.Empty, 0, 0, string.Empty, string.Empty) { }

    public TemplateError(string fileName, int line, int column, string errorNumber, string errorText)
    {
        Line = line;
        Column = column;
        ErrorNumber = errorNumber;
        ErrorText = errorText;
        FileName = fileName;
    }

    public int Line { get; set; }

    public int Column { get; set; }

    public string ErrorNumber { get; set; }

    public string ErrorText { get; set; }

    public bool IsWarning { get; set; }

    public string FileName { get; set; }

    public override string ToString() => FileName.Length > 0 ?
        string.Format(CultureInfo.InvariantCulture, "{0}({1},{2}) : {3} {4}: {5}", FileName, Line, Column, WarningString, ErrorNumber, ErrorText) :
        string.Format(CultureInfo.InvariantCulture, "{0} {1}: {2}", WarningString, ErrorNumber, ErrorText);

    private string WarningString => IsWarning ? "warning" : "error";
}