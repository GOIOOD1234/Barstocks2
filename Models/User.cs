using System.Text.Json.Serialization;
using System.Collections.Generic;


namespace Barstocks.Models;

public class User
{
    [JsonPropertyName("id")]
    public string Id { get; set; }
    
    [JsonPropertyName("name")]
    public string name { get; set; }
    
    [JsonPropertyName("SymbolStocks")]
    public List<string> SymbolStocks{ get; set; }

    [JsonPropertyName("barX")]
    public int BarX { get; set; } = 0;

    [JsonPropertyName("barY")]
    public int BarY { get; set; } = 0;

    [JsonPropertyName("barHeight")]
    public int BarHeight { get; set; } = 45;

}
