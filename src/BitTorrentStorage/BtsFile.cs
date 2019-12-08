using MonoTorrent;

namespace BitTorrentStorage
{
    public sealed class BtsFile
    {
        public readonly TorrentFile MonoTorrentFile;
        public string Path => MonoTorrentFile.Path;
        public long Length => MonoTorrentFile.Length;

        public BtsFile(TorrentFile torrentFile)
        {
            MonoTorrentFile = torrentFile;
        }
    }
}
