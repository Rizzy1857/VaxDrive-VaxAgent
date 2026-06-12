using System;

namespace VaxDrive.Models;

public sealed class Device
{
    public string Id { get; set; } = string.Empty; // Same as device_fingerprint
    public DateTime LastSeen { get; set; }
    public string? OsVersion { get; set; }
    public string? BiosString { get; set; }
}
