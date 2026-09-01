using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
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
        /// <summary>
        /// Gets the rows in backend rank order. Empty when the call did not succeed.
        /// </summary>
        public JsonArray Rows { get; init; } = new JsonArray();

        /// <summary>
        /// Gets the HTTP status the backend answered with, or 0 when no response was read.
        /// </summary>
        public int StatusCode { get; init; }

        /// <summary>
        /// Gets a value indicating whether the backend answered 2xx and the body parsed.
        /// </summary>
        public bool Succeeded { get; init; }

        /// <summary>
        /// Gets the backend's own error message when it rejected the call, otherwise null.
        /// </summary>
        public string? Error { get; init; }

        /// <summary>
        /// Gets a value indicating whether the backend simply had no answer for this seed,
        /// as opposed to being misconfigured. Callers should try the next seed rather than
        /// abandoning the whole mix.
        /// </summary>
        public bool SeedNotFound => StatusCode == 404;
    }

    /// <summary>
    /// Turns a seed song into a ranked list of tracks, using whichever AudioMuse AI search the
    /// administrator selected in <see cref="PluginConfiguration.SimilarityProvider"/>.
    /// Every engine takes the same input (one seed song) and produces the same output: rows in
    /// backend rank order, each carrying an <c>item_id</c> and a <c>distance</c> expressed in the
    /// same domain as Similar Song, so callers behave identically whichever engine is active.
    /// </summary>
    public static class SimilarTrackSearch
    {
        /// <summary>
        /// How much of an unrecognised error body to quote back in logs.
        /// </summary>
        private const int MaxErrorLength = 200;

        /// <summary>
        /// Runs the configured engine against one seed song.
        /// Limits are not clamped here: every engine's endpoint clamps to its own configured
        /// maximum (HYPERBOLIC_MAX_LIMIT and the SemGrove ceiling), and clamping to a copy of
        /// those values would silently under-fetch whenever an operator raises them.
        /// </summary>
        /// <param name="service">The AudioMuse service client. The caller owns its lifetime.</param>
        /// <param name="engine">The search to use.</param>
        /// <param name="item_id">The seed song item ID, in the backend's "N" format.</param>
        /// <param name="limit">The number of tracks to ask the backend for.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The outcome, including the normalised rows and any backend error.</returns>
        public static async Task<SimilarTracksResult> SearchAsync(
            IAudioMuseService service,
            SimilarityEngine engine,
            string item_id,
            int limit,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(service);

            var wanted = Math.Max(1, limit);

            switch (engine)
            {
                case SimilarityEngine.LyricsBySong:
                {
                    // SemGrove always returns the seed itself as the first row, so ask for one
                    // extra and drop it: otherwise the caller gets one song fewer than it wanted.
                    using var response = await service
                        .GetSemGroveSimilarAsync(item_id, wanted + 1, cancellationToken)
                        .ConfigureAwait(false);
                    return await BuildAsync(response, engine, wanted, cancellationToken).ConfigureAwait(false);
                }

                case SimilarityEngine.Hyperbolic:
                {
                    using var response = await service
                        .GetHyperbolicSimilarAsync(item_id, wanted, cancellationToken)
                        .ConfigureAwait(false);
                    return await BuildAsync(response, engine, wanted, cancellationToken).ConfigureAwait(false);
                }

                default:
                {
                    // Similar Song: the original call, unchanged. Its distance already defines
                    // the domain the other engines are normalised into.
                    using var response = await service
                        .GetSimilarTracksAsync(item_id, null, null, wanted, null, cancellationToken)
                        .ConfigureAwait(false);
                    return await BuildAsync(response, engine, wanted, cancellationToken).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Gets the item IDs the configured engine ranks closest to the seed song, in backend order.
        /// </summary>
        /// <param name="service">The AudioMuse service client. The caller owns its lifetime.</param>
        /// <param name="engine">The search to use.</param>
        /// <param name="seedId">The seed song.</param>
        /// <param name="limit">The number of tracks to ask the backend for.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The item IDs in backend rank order, or an empty list when the backend has no answer.</returns>
        public static async Task<List<Guid>> GetSimilarTrackIdsAsync(
            IAudioMuseService service,
            SimilarityEngine engine,
            Guid seedId,
            int limit,
            CancellationToken cancellationToken)
        {
            var result = await SearchAsync(
                service,
                engine,
                seedId.ToString("N", CultureInfo.InvariantCulture),
                limit,
                cancellationToken).ConfigureAwait(false);

            return ItemIds(result.Rows);
        }

        /// <summary>
        /// Reads the "item_id" of every row, in order.
        /// </summary>
        /// <param name="rows">The rows to read.</param>
        /// <returns>The parsed item IDs.</returns>
        public static List<Guid> ItemIds(JsonArray rows)
        {
            var ids = new List<Guid>(rows?.Count ?? 0);
            if (rows is null)
            {
                return ids;
            }

            foreach (var row in rows)
            {
                if (row is JsonObject item && TryGetId(item, "item_id", out var id))
                {
                    ids.Add(id);
                }
            }

            return ids;
        }

        /// <summary>
        /// Reads the IDs out of any AudioMuse AI list response, in backend order. Tolerates both
        /// response shapes: a bare array, or the rows wrapped in an object under "results".
        /// Shared with the similar-artists lookup, which reads a different ID property.
        /// </summary>
        /// <param name="response">The backend response. A failure or an unparseable body yields an empty list.</param>
        /// <param name="property">The name of the ID property to read from each row.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The parsed IDs.</returns>
        public static async Task<List<Guid>> ReadIdsAsync(HttpResponseMessage? response, string property, CancellationToken cancellationToken)
        {
            var ids = new List<Guid>();
            if (response?.IsSuccessStatusCode != true)
            {
                return ids;
            }

            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var rows = ParseRows(body);
            if (rows is null)
            {
                return ids;
            }

            foreach (var row in rows)
            {
                if (row is JsonObject item && TryGetId(item, property, out var id))
                {
                    ids.Add(id);
                }
            }

            return ids;
        }

        /// <summary>
        /// Reads a response into a result, normalising the rows when the backend answered.
        /// </summary>
        /// <param name="response">The backend response.</param>
        /// <param name="engine">The engine that produced it.</param>
        /// <param name="limit">The number of rows the caller asked for.</param>
        /// <param name="cancellationToken">The cancellation token.</param>
        /// <returns>The outcome.</returns>
        private static async Task<SimilarTracksResult> BuildAsync(
            HttpResponseMessage? response,
            SimilarityEngine engine,
            int limit,
            CancellationToken cancellationToken)
        {
            if (response is null)
            {
                return new SimilarTracksResult { StatusCode = 0, Error = "No response from the AudioMuse AI backend." };
            }

            var status = (int)response.StatusCode;
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                return new SimilarTracksResult { StatusCode = status, Error = ErrorMessage(body) };
            }

            var rows = ParseRows(body);
            if (rows is null)
            {
                // A 2xx with a body that is not the expected JSON: a proxy or captive portal
                // answering for the backend. Report it rather than pretending there were no songs.
                return new SimilarTracksResult { StatusCode = status, Error = ErrorMessage(body) };
            }

            return new SimilarTracksResult
            {
                Rows = Normalize(rows, engine, limit),
                StatusCode = status,
                Succeeded = true
            };
        }

        /// <summary>
        /// Parses a response body into rows, tolerating both the bare-array and the
        /// "results"-wrapped shapes. Returns null when the body is not JSON at all.
        /// </summary>
        /// <param name="body">The raw response body.</param>
        /// <returns>The rows, or null.</returns>
        private static JsonArray? ParseRows(string body)
        {
            try
            {
                return JsonNode.Parse(body) switch
                {
                    JsonArray array => array,
                    JsonObject obj => obj["results"] as JsonArray,
                    _ => null
                };
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Pulls the backend's own error text out of a rejected body, falling back to a
        /// truncated copy of the body itself.
        /// </summary>
        /// <param name="body">The raw response body.</param>
        /// <returns>The message to log.</returns>
        private static string ErrorMessage(string body)
        {
            try
            {
                if (JsonNode.Parse(body) is JsonObject obj
                    && obj.TryGetPropertyValue("error", out var error)
                    && error is not null)
                {
                    var text = error.GetValue<string>();
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        return text;
                    }
                }
            }
            catch (JsonException)
            {
                // Not JSON: fall through and quote the body.
            }
            catch (InvalidOperationException)
            {
                // "error" was not a string: fall through and quote the body.
            }

            body = body?.Trim() ?? string.Empty;
            return body.Length > MaxErrorLength ? body.Substring(0, MaxErrorLength) : body;
        }

        /// <summary>
        /// Drops the seed row, brings "distance" into the Similar Song domain and trims to the
        /// limit. Similar Song rows already carry a comparable distance and are passed through
        /// untouched; SemGrove reports a [0,1] similarity, so distance is 1 - similarity;
        /// hyperbolic reports an unbounded Poincare distance, squashed with d / (1 + d) onto [0,1).
        /// </summary>
        /// <param name="rows">The rows the backend sent.</param>
        /// <param name="engine">The engine that produced them.</param>
        /// <param name="limit">The number of rows the caller asked for.</param>
        /// <returns>The normalised rows.</returns>
        private static JsonArray Normalize(JsonArray rows, SimilarityEngine engine, int limit)
        {
            var output = new JsonArray();

            foreach (var row in rows)
            {
                if (output.Count >= limit)
                {
                    break;
                }

                if (row is not JsonObject item)
                {
                    continue;
                }

                // SemGrove returns the seed song itself, flagged is_seed. The hyperbolic search
                // excludes the seed on the backend and Similar Song never returns it.
                if (item.TryGetPropertyValue("is_seed", out var seed) && IsTrue(seed))
                {
                    continue;
                }

                var clone = item.DeepClone().AsObject();

                switch (engine)
                {
                    case SimilarityEngine.LyricsBySong:
                        if (TryGetNumber(item, "similarity", out var similarity))
                        {
                            clone["distance"] = JsonValue.Create(1d - similarity);
                        }

                        break;

                    case SimilarityEngine.Hyperbolic:
                        if (TryGetNumber(item, "distance", out var poincare) && poincare > 0d)
                        {
                            clone["distance"] = JsonValue.Create(poincare / (1d + poincare));
                        }

                        break;

                    default:
                        break;
                }

                output.Add(clone);
            }

            return output;
        }

        /// <summary>
        /// Reads a GUID property, tolerating a missing, null or non-string value.
        /// </summary>
        /// <param name="item">The row to read from.</param>
        /// <param name="name">The property name.</param>
        /// <param name="id">The parsed ID.</param>
        /// <returns>True when the property held a parseable GUID.</returns>
        private static bool TryGetId(JsonObject item, string name, out Guid id)
        {
            id = Guid.Empty;
            if (!item.TryGetPropertyValue(name, out var node) || node is null)
            {
                return false;
            }

            try
            {
                return Guid.TryParse(node.GetValue<string>(), out id);
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        /// <summary>
        /// Reads a numeric property, tolerating a missing, null or non-numeric value.
        /// </summary>
        /// <param name="item">The row to read from.</param>
        /// <param name="name">The property name.</param>
        /// <param name="value">The parsed value.</param>
        /// <returns>True when the property held a number.</returns>
        private static bool TryGetNumber(JsonObject item, string name, out double value)
        {
            value = 0d;
            if (!item.TryGetPropertyValue(name, out var node) || node is null)
            {
                return false;
            }

            try
            {
                value = node.GetValue<double>();
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }

        /// <summary>
        /// Reads a boolean node, tolerating a non-boolean value.
        /// </summary>
        /// <param name="node">The node to read.</param>
        /// <returns>True only when the node held a true boolean.</returns>
        private static bool IsTrue(JsonNode? node)
        {
            if (node is null)
            {
                return false;
            }

            try
            {
                return node.GetValue<bool>();
            }
            catch (InvalidOperationException)
            {
                return false;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
