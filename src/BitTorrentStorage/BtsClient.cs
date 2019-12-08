using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MonoTorrent;
using MonoTorrent.Client;
using MonoTorrent.Client.PiecePicking;

namespace BitTorrentStorage
{
    public sealed class BtsClient : IDisposable
    {
        public readonly ClientEngine MonoTorrentEngine;
        public readonly BitTorrentStorageConfig Config;

        public BtsClient(
            ClientEngine monoTorrentEngine,
            BitTorrentStorageConfig config
        )
        {
            MonoTorrentEngine = monoTorrentEngine;
            Config = config;
        }

        public void Dispose()
        {
            MonoTorrentEngine.Dispose();
        }

        public static BtsClient Initialize(BitTorrentStorageConfig config)
        {
            var settings = new EngineSettings
            {
                PreferEncryption = true
            };
            var engine = new ClientEngine(settings);
            return new BtsClient(engine, config);
        }

        static void IgnoreTorrentFiles(ITorrentData torrent)
        {
            foreach (var file in torrent.Files) file.Priority = Priority.DoNotDownload;
        }

        static async Task<CustomPiecePicker> ChangeTorrentManagerPicker(TorrentManager torrentManager)
        {
            var picker = new CustomPiecePicker(new StandardPicker());
            await torrentManager.ChangePickerAsync(picker);
            return picker;
        }

        public async Task<BtsTorrentManager> OpenMagnetLink(MagnetLink magnetLink, CancellationToken ct = default)
        {
            var hash = magnetLink.InfoHash.ToHex();
            var torrentFilePath = Path.Combine(
                Config.CacheDir,
                "MagnetLinkTorrents",
                hash
            );
            Directory.CreateDirectory(torrentFilePath);
            var path = Path.Combine(Config.CacheDir, hash);
            var manager = new TorrentManager(magnetLink, path, new TorrentSettings(), torrentFilePath);
            var tcs = new TaskCompletionSource<int>();

            // Try... because event handler is called more than once event with the unsubscription sometimes
            void TorrentStateChanged(object _, TorrentStateChangedEventArgs args)
            {
                if (manager.Error != null)
                {
                    tcs.TrySetException(manager.Error.Exception);
                    return;
                }
                if (manager.Torrent == null) return;
                
                tcs.TrySetResult(default);
                manager.TorrentStateChanged -= TorrentStateChanged;
            }
            manager.TorrentStateChanged += TorrentStateChanged;
            
            await MonoTorrentEngine.Register(manager);
            await manager.StartAsync();
            await tcs.Task;
            IgnoreTorrentFiles(manager.Torrent);
            var piecePicker = await ChangeTorrentManagerPicker(manager);
            return new BtsTorrentManager(manager, new BtsTorrent(manager.Torrent), piecePicker);
        }
        
        public async Task<BtsTorrentManager> OpenTorrent(BtsTorrent torrent)
        {
            var mTorrent = torrent.MonoTorrent;
            IgnoreTorrentFiles(mTorrent);
            var path = Path.Combine(Config.CacheDir, mTorrent.Name);
            var manager = new TorrentManager(mTorrent, path, new TorrentSettings());
            var piecePicker = await ChangeTorrentManagerPicker(manager);
            await MonoTorrentEngine.Register(manager);
            await manager.StartAsync();
            return new BtsTorrentManager(manager, torrent, piecePicker);
        }
    }
}
