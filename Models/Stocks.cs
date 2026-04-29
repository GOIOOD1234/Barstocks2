using System.Text.Json.Serialization;
using System.Collections.Generic;


namespace Barstocks.Models;

public class Stocks
{
    [JsonPropertyName("symbol")]
    public string Symbol { get; set; }

    [JsonPropertyName("longName")]
    public string LongName { get; set; }

    [JsonPropertyName("quoteType")]
    public string QuoteType { get; set; }

    [JsonPropertyName("currency")]
    public string Currency { get; set; }

    [JsonPropertyName("regularMarketPrice")]
    public decimal RegularMarketPrice { get; set; }

    [JsonPropertyName("regularMarketChange")]
    public decimal RegularMarketChange { get; set; }

    [JsonPropertyName("regularMarketChangePercent")]
    public double RegularMarketChangePercent { get; set; }

    [JsonPropertyName("regularMarketPreviousClose")]
    public decimal RegularMarketPreviousClose { get; set; }

    [JsonPropertyName("regularMarketOpen")]
    public decimal RegularMarketOpen { get; set; }

    [JsonPropertyName("regularMarketDayHigh")]
    public decimal RegularMarketDayHigh { get; set; }

    [JsonPropertyName("regularMarketDayLow")]
    public decimal RegularMarketDayLow { get; set; }

    [JsonPropertyName("marketState")]
    public string MarketState { get; set; }

    [JsonPropertyName("exchange")]
    public string Exchange { get; set; }

    [JsonPropertyName("marketCap")]
    public long MarketCap { get; set; }
}
