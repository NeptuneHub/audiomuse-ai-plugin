using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading;
using System.Threading.Tasks;
using Jellyfin.Plugin.AudioMuseAi.Configuration;

namespace Jellyfin.Plugin.AudioMuseAi.Services
{
    /// <summary>
    /// The outcome of one similarity call to the AudioMuse AI backend.
    /// </summary>
    public sealed class SimilarTracksResult
    {
        /// <summary>Gets the item IDs, in backend rank order.</summary>
        public List<Guid> ItemIds { get; init; } = new List<Guid>();

        /// <summary>Gets the HTTP status the backend answered with, or 0 when no response was read.</summary>
        public int StatusCode { get; init; }

        /// <summary>Gets a value indicating whether the backend answered 2xx and the body parsed.</summary>
        public bool Succeeded { get; init; }

        /// <summary>Gets the backend's own error message when it rejected the call.</summary>
        public string? Error { get; init; }

        /// <summary>Gets a value indicating whether the backend simply had no answer for this seed.</summary>
        public bool SeedNotFound => StatusCode == 404;
    }

    /// <summary>
    /// Calls the AudioMuse AI similarity endpoint selected in
    /// <see cref="PluginConfiguration.SimilarityProvider"/> and reads the item IDs out of it.
    /// </summary>
    public static class SimilarTrackSearch
    {
        private const int MaxErrorLength = 200;

        private static readonly JsonSerializerOptions RelaxedJson =
            new JsonSerializerOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

        /// <summary>
        /// Runs the configured engine against one seed song. The limit is passed through: every
        /// endpoint clamps to its own configured maximum.
        /// </summary>
        /// <param name="service">The AudioMuse service client. The caller owns its lifetime.</param>
        /// <param name="engine">The search to use.</param>
        /// <param name="seedId">The seed song.</param>
        /// <param name="limit">The number of tracks to ask for.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The item IDs and any backend error.</returns>
        public static async Task<SimilarTracksResult> SearchAsync(
            IAudioMuseService service,
            SimilarityEngine engine,
            Guid seedId,
            int limit,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(service);

            var id = seedId.ToString("N", CultureInfo.InvariantCulture);
            var wanted = Math.Max(1, limit);

            // SemGrove always returns the seed itself as a row, so ask for one extra.
            using var response = engine switch
            {
                SimilarityEngine.LyricsBySong => await service.GetSemGroveSimilarAsync(id, wanted + 1, cancellationToken).ConfigureAwait(false),
                SimilarityEngine.Hyperbolic => await service.GetHyperbolicSimilarAsync(id, wanted, cancellationToken).ConfigureAwait(false),
                _ => await service.GetSimilarTracksAsync(id, null, null, wanted, null, cancellationToken).ConfigureAwait(false)
            };

            if (response is null)
            {
                return new SimilarTracksResult { Error = "No response from the AudioMuse AI backend." };
            }

            var status = (int)response.StatusCode;
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var rows = response.IsSuccessStatusCode ? RowsOf(Parse(body)) : null;

            // A 2xx whose body is not the expected JSON is a proxy answering for the backend:
            // report it rather than pretending there were no songs.
            return rows is null
                ? new SimilarTracksResult { StatusCode = status, Error = ErrorMessage(body) }
                : new SimilarTracksResult { StatusCode = status, Succeeded = true, ItemIds = ExtractIds(rows, "item_id", wanted, skipSeed: true) };
        }

        /// <summary>
        /// Reads the IDs out of any AudioMuse AI list response, in backend order.
        /// </summary>
        /// <param name="response">The backend response.</param>
        /// <param name="property">The ID property to read from each row.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The parsed IDs, empty when the call failed or the body was not the expected shape.</returns>
        public static async Task<List<Guid>> ReadIdsAsync(HttpResponseMessage? response, string property, CancellationToken cancellationToken)
        {
            if (response?.IsSuccessStatusCode != true)
            {
                return new List<Guid>();
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            return ExtractIds(RowsOf(Parse(body)), property, int.MaxValue, skipSeed: false);
        }

        /// <summary>
        /// Rewrites a response body so its rows report <c>distance</c> on the Similar Song scale.
        /// SemGrove's <c>similarity</c> becomes <c>1 - similarity</c>; hyperbolic's raw Poincare
        /// distance moves to <c>hyperbolic_distance</c> and <c>distance</c> becomes
        /// <c>d / (1 + d)</c>. Returns null when nothing changed, so the caller forwards the original.
        /// </summary>
        /// <param name="body">The raw response body.</param>
        /// <param name="engine">The engine that produced it.</param>
        /// <returns>The rewritten JSON, or null to keep the original.</returns>
        public static string? EnrichResponseBody(string body, SimilarityEngine engine)
        {
            var root = Parse(body);
            var rows = RowsOf(root);
            if (rows is null)
            {
                return null;
            }

            var changed = false;
            foreach (var row in rows)
            {
                if (row is JsonObject item && EnrichRow(item, engine))
                {
                    changed = true;
                }
            }

            // Relaxed escaping keeps non-ASCII titles as themselves on the round trip.
            return changed ? root!.ToJsonString(RelaxedJson) : null;
        }

        private static bool EnrichRow(JsonObject row, SimilarityEngine engine)
        {
            if (engine == SimilarityEngine.LyricsBySong && TryNumber(row, "similarity", out var similarity))
            {
                row.Remove("similarity");
                row["distance"] = JsonValue.Create(Math.Clamp(1d - similarity, 0d, 1d));
                return true;
            }

            if (engine == SimilarityEngine.Hyperbolic && TryNumber(row, "distance", out var poincare))
            {
                // Floating point can hand back a tiny negative for an identical vector; fold it to
                // zero rather than skipping the row and leaving one raw value among squashed ones.
                var d = Math.Max(0d, poincare);
                row["hyperbolic_distance"] = JsonValue.Create(poincare);
                row["distance"] = JsonValue.Create(d / (1d + d));
                return true;
            }

            return false;
        }

        /// <summary>
        /// Collects row IDs in order.
        /// </summary>
        /// <param name="rows">The rows to read.</param>
        /// <param name="property">The ID property to read.</param>
        /// <param name="limit">The most IDs to return.</param>
        /// <param name="skipSeed">Whether to drop the seed row SemGrove flags with <c>is_seed</c>.</param>
        /// <returns>The parsed IDs.</returns>
        private static List<Guid> ExtractIds(JsonArray? rows, string property, int limit, bool skipSeed)
        {
            var ids = new List<Guid>();
            foreach (var row in rows ?? new JsonArray())
            {
                if (ids.Count >= limit)
                {
                    break;
                }

                if (row is JsonObject item
                    && !(skipSeed && TryBool(item, "is_seed", out var seed) && seed)
                    && TryGuid(item, property, out var id))
                {
                    ids.Add(id);
                }
            }

            return ids;
        }

        /// <summary>
        /// Pulls the backend's own error text out of a rejected body, else a truncated copy of it.
        /// </summary>
        /// <param name="body">The raw response body.</param>
        /// <returns>The message to log.</returns>
        private static string ErrorMessage(string body)
        {
            if (Parse(body) is JsonObject obj
                && obj["error"] is JsonValue value
                && value.TryGetValue<string>(out var text)
                && !string.IsNullOrWhiteSpace(text))
            {
                return text;
            }

            var trimmed = body?.Trim() ?? string.Empty;
            return trimmed.Length > MaxErrorLength ? trimmed.Substring(0, MaxErrorLength) : trimmed;
        }

        private static JsonNode? Parse(string body)
        {
            try
            {
                return JsonNode.Parse(body);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        private static JsonArray? RowsOf(JsonNode? root) => root switch
        {
            JsonArray array => array,
            JsonObject obj => obj["results"] as JsonArray,
            _ => null
        };

        // JsonValue.TryGetValue never throws, unlike JsonNode.GetValue.
        private static bool TryGuid(JsonObject row, string name, out Guid id)
        {
            id = Guid.Empty;
            return row[name] is JsonValue value
                && value.TryGetValue<string>(out var text)
                && Guid.TryParse(text, out id);
        }

        private static bool TryBool(JsonObject row, string name, out bool flag)
        {
            flag = false;
            return row[name] is JsonValue value && value.TryGetValue(out flag);
        }

        // Non-finite values are rejected here: ToJsonString cannot serialise them and would throw.
        private static bool TryNumber(JsonObject row, string name, out double number)
        {
            number = 0d;
            return row[name] is JsonValue value
                && value.TryGetValue(out number)
                && double.IsFinite(number);
        }
    }
}
