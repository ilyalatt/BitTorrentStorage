using System.Collections.Generic;
using System.IO;
using System.Linq;
using MonoTorrent;

namespace BitTorrentStorage
{
    public sealed class BtsTorrent
    {
        public readonly Torrent MonoTorrent;
        public readonly IReadOnlyList<BtsFile> Files;

        public BtsTorrent(Torrent monoTorrent)
        {
            var files = monoTorrent.Files.Select(x => new BtsFile(x)).ToList();
            MonoTorrent = monoTorrent;
            Files = files;
        }

        public static BtsTorrent Load(Stream stream)
        {
            var torrent = Torrent.Load(stream);
            return new BtsTorrent(torrent);
        }
    }
}
