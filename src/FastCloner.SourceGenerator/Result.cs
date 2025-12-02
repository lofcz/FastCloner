using System;
using Microsoft.CodeAnalysis;

namespace FastCloner.SourceGenerator;

/// <summary>
/// Represents a result that can be either a success (TypeModel) or an error (Diagnostic).
/// </summary>
internal sealed class Result<T>
{
    private readonly T? _value;
    private readonly Diagnostic? _error;
    private readonly bool _isSuccess;

    private Result(T value)
    {
        _value = value;
        _isSuccess = true;
    }

    private Result(Diagnostic error)
    {
        _error = error;
        _isSuccess = false;
    }

    public static Result<T> Success(T value) => new Result<T>(value);
    public static Result<T> Error(Diagnostic error) => new Result<T>(error);

    public void Handle(Action<T> onSuccess, Action<Diagnostic> onError)
    {
        if (_isSuccess && _value != null)
        {
            onSuccess(_value);
        }
        else if (!_isSuccess && _error != null)
        {
            onError(_error);
        }
    }

    public bool IsSuccess => _isSuccess;
    public T? Value => _value;
    public Diagnostic? ErrorDiagnostic => _error;
}

