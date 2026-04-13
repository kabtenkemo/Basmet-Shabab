using System.Text.Json.Serialization;

namespace BasmaApi.Models;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ComplaintPriority
{
    Low = 0,
    Medium = 1,
    High = 2
}