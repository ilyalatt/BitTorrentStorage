# BitTorrentStorage

[![NuGet version](https://badge.fury.io/nu/BitTorrentStorage.svg)](https://www.nuget.org/packages/BitTorrentStorage)
[![NuGet version](https://badge.fury.io/nu/BitTorrentStorage.Fuse.svg)](https://www.nuget.org/packages/BitTorrentStorage.Fuse)

## Why?

`BitTorrentStorage` gives you torrent files random access:

```C#
var magnetLink = MagnetLink.Parse(
    "magnet:?xt=urn:btih:84A7A22AE870281D9E9FC1A1D1B27E6EFE500242&tr=http%3A%2F%2Fbt.t-ru.org%2Fann%3Fmagnet";
);
var cacheDir = Path.Join(Path.GetTempPath(), "BitTorrentStorage");
var config = new BitTorrentStorageConfig(cacheDir);
using var btsStorage = BtsClient.Initialize(config);

Console.WriteLine("Initializing the torrent.");
using var torrentManager = await btsStorage.OpenMagnetLink(magnetLink);

var file = torrentManager.Torrent.Files.First();
await using var fs = torrentManager.OpenFileStream(file);

var ms = new MemoryStream();
await fs.CopyToAsync(ms);
```

`BitTorrentStorage.Fuse` gives you a simple way to use tools like VLC:

```C#
// You need to run `sudo umount ~/mnt/bts` if you want to run this project again.
var mountPath = Path.Join(
    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
    "/mnt/bts"
);
Console.WriteLine("Starting BitTorrentStorage FUSE.");
Console.WriteLine($"Mounting to \"{MountPath}\".");
var fs = new BtsFs(torrentManager) { MountPoint = MountPath };
Directory.CreateDirectory(MountPath);
fs.Start();
```

## Notes

* BitTorrentStorage is not optimized.
* It needs to fetch the whole piece to return one byte in the piece for example.
* There is a hardcoded prefetch of 10 next pieces in `BtsFileStream`.
* There is no cache management. You need to call `BtsTorrentManager.Delete(file)` yourself.
