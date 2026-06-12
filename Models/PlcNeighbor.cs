using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace VaxDrive.Models;

public sealed class PlcNeighbor
{
    [JsonPropertyName("ip")]
    public string Ip { get; set; } = string.Empty;

    [JsonPropertyName("banner")]
    public string Banner { get; set; } = string.Empty;

    [JsonPropertyName("open")]
    public List<int> Open { get; set; } = new List<int>();
}
