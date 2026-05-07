namespace FMC.Shared.DTOs;

public class FMCResult
{
    public bool Succeeded { get; set; }
    public List<string> Errors { get; set; } = new();

    public static FMCResult Success() => new() { Succeeded = true };
    public static FMCResult Failure(params string[] errors) => new() { Succeeded = false, Errors = errors.ToList() };
    public static FMCResult Failure(IEnumerable<string> errors) => new() { Succeeded = false, Errors = errors.ToList() };
}

public class FMCResult<T> : FMCResult
{
    public T? Data { get; set; }

    public static FMCResult<T> Success(T data) => new() { Succeeded = true, Data = data };
    public new static FMCResult<T> Failure(params string[] errors) => new() { Succeeded = false, Errors = errors.ToList() };
}
