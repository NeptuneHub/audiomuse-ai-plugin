namespace Jellyfin.Plugin.AudioMuseAi.Configuration
{
    /// <summary>
    /// The AudioMuse AI backend search that supplies songs to every song-seeded lookup in
    /// the plugin: the Instant Mix override, the "More Like This" similar-items rows and the
    /// re-exposed similar_tracks endpoint. Kept in the same order and with the same meaning
    /// as the "Instant Mix Functionality" setting in the Navidrome plugin.
    /// </summary>
    public enum SimilarityEngine
    {
        /// <summary>
        /// Sonic similarity, served by <c>GET /api/similar_tracks</c>.
        /// This is the historic behaviour and stays the default, so an existing
        /// configuration that predates this setting keeps working unchanged.
        /// </summary>
        SimilarSong = 0,

        /// <summary>
        /// Lyrics similarity, served by <c>POST /api/sem_grove/search</c>.
        /// SemGrove merges the lyrics and audio embeddings, so it is seeded by a
        /// song and needs the SemGrove index built on the backend.
        /// </summary>
        LyricsBySong = 1,

        /// <summary>
        /// Hyperbolic similarity, served by <c>POST /api/hyperbolic/similar</c>.
        /// Ranks the catalogue by Poincare distance from the seed song and needs
        /// the hyperbolic projection built on the backend.
        /// </summary>
        Hyperbolic = 2
    }
}
