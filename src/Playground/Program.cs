using System;
using System.IO;
using System.Threading.Tasks;
using BitTorrentStorage;
using BitTorrentStorage.Fuse;
using MonoTorrent;

namespace Playground
{
    static class Program
    {
        // You need to run `sudo umount ~/mnt/bts` if you want to run this project again.
        const string MountPathInUserDir = "/mnt/bts";
        static readonly string MountPath = Path.Join(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            MountPathInUserDir
        );
        const string MagnetUrl =
            "magnet:?xt=urn:btih:84A7A22AE870281D9E9FC1A1D1B27E6EFE500242&tr=http%3A%2F%2Fbt.t-ru.org%2Fann%3Fmagnet";
        static async Task Main()
        {
            var magnetLink = MagnetLink.Parse(MagnetUrl);
            var cacheDir = Path.Join(Path.GetTempPath(), "BitTorrentStorage");
            var config = new BitTorrentStorageConfig(cacheDir);
            using var btsStorage = BtsClient.Initialize(config);

            Console.WriteLine("Initializing the torrent.");
            using var torrentManager = await btsStorage.OpenMagnetLink(magnetLink);
            
            Console.WriteLine("Starting BitTorrentStorage FUSE.");
            Console.WriteLine($"Mounting to \"{MountPath}\".");
            var fs = new BtsFs(torrentManager) { MountPoint = MountPath };
            Directory.CreateDirectory(MountPath);
            fs.Start();
        }
    }
}
