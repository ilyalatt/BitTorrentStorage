namespace BitTorrentStorage
{
    public sealed class BitTorrentStorageConfig
    {
        public readonly string CacheDir;

        public BitTorrentStorageConfig(string cacheDir)
        {
            CacheDir = cacheDir;
        }
    }
}