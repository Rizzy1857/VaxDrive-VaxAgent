namespace VaxDrive.VaxAgent.Checks;

using System;
using System.Collections.Generic;

public sealed class CheckResult
{
    public bool Success { get; init; }
    public string? Error { get; init; }
    public bool IsSuccess => Success;

    public static CheckResult Ok()
    {
        return new CheckResult { Success = true, Error = null };
        // Instantiates a successful CheckResult with no error message.
        // Returns CheckResult instance.
    }

    public static CheckResult Failed(string error)
    {
        return new CheckResult { Success = false, Error = error };
        // Instantiates a failed CheckResult with the provided error message.
        // Returns CheckResult instance.
    }
}
