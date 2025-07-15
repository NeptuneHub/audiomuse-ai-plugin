using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Jellyfin.Plugin.AudioMuseAi.Models
{
    /// <summary>
    /// Model for the create playlist request body.
    /// </summary>
    public class CreatePlaylistModel
    {
        /// <summary>
        /// Gets or sets the desired name for the playlist.
        /// </summary>
        [JsonPropertyName("playlist_name")]
        public string? PlaylistName { get; set; }

        /// <summary>
        /// Gets or sets the list of track item IDs to include in the playlist.
        /// </summary>
        [JsonPropertyName("track_ids")]
        public IEnumerable<string>? TrackIds { get; set; }
    }
}
