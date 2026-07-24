using System.Text.Json.Serialization;

namespace TrackerForSites.Api.Models.Dtos;

/// <summary>
/// tracker.js'in POST /api/collect'e gönderdiği payload.
///
/// tracker.js küçük harfli, kısa alan adları kullanır (boyutu küçültmek için).
/// System.Text.Json varsayılan olarak case-sensitive'dir.
/// Bu yüzden her property'e [JsonPropertyName] eklemek ZORUNLU.
/// Aksi hâlde tüm alanlar null gelir, event sessizce bozuk kaydedilir!
///
/// tracker.js alan adları:
///   s  -> site api key
///   u  -> url (pathname + search)
///   r  -> referrer
///   ti -> page title
///   l  -> language
///   w  -> screen width
///   id -> session id
///   ts -> client timestamp (Unix ms)
/// </summary>
public class CollectRequest
{
    [JsonPropertyName("s")]
    public string? S { get; set; }

    [JsonPropertyName("u")]
    public string? U { get; set; }

    [JsonPropertyName("r")]
    public string? R { get; set; }

    [JsonPropertyName("ti")]
    public string? Ti { get; set; }

    [JsonPropertyName("l")]
    public string? L { get; set; }

    [JsonPropertyName("w")]
    public short? W { get; set; }

    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>
    /// Date.now()
    /// API: DateTimeOffset.FromUnixTimeMilliseconds(Ts)
    /// </summary>
    [JsonPropertyName("ts")]
    public long? Ts { get; set; }
}
