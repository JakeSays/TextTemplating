using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;


namespace Std.TextTemplating.Generation;

public class TemplateErrorCollection : IEnumerable<TemplateError>
{
    private readonly List<TemplateError> _errors = [];

    public TemplateErrorCollection()
    {
    }

    public TemplateErrorCollection(TemplateErrorCollection value)
    {
        AddRange(value);
    }

    public TemplateErrorCollection(TemplateError[] value)
    {
        AddRange(value);
    }

    public TemplateError this[int index]
    {
        get => _errors[index];
        set => _errors[index] = value;
    }

    public void Clear() => _errors.Clear();
    public int Count => _errors.Count;

    public bool HasErrors => Count > 0 && _errors.Any(e => !e.IsWarning);

    public bool HasWarnings => Count > 0 && _errors.Any(e => e.IsWarning);

    public IEnumerator<TemplateError> GetEnumerator() => _errors.GetEnumerator();

    IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable) _errors).GetEnumerator();

    public void Add(TemplateError value) => _errors.Add(value);

    public void AddRange(TemplateError[] value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        _errors.AddRange(value);
    }

    public void AddRange(TemplateErrorCollection value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        _errors.AddRange(value._errors);
    }

    public bool Contains(TemplateError value) => _errors.Contains(value);

    public void CopyTo(TemplateError[] array, int index) => _errors.CopyTo(array, index);

    public int IndexOf(TemplateError value) => _errors.IndexOf(value);

    public void Insert(int index, TemplateError value) => _errors.Insert(index, value);

    public void Remove(TemplateError value) => _errors.Remove(value);
}
