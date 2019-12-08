using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MonoTorrent.Client;

namespace BitTorrentStorage
{
    public sealed class BtsTorrentManager : IDisposable
    {
        public readonly TorrentManager MonoTorrentManager;
        public readonly BtsTorrent Torrent;
        readonly CustomPiecePicker _customPiecePicker;
        readonly object _lock = new object();
        readonly BtsPieceRequest?[] _pieceRequests;

        public BtsTorrentManager(TorrentManager monoTorrentManager, BtsTorrent torrent, CustomPiecePicker customPiecePicker)
        {
            MonoTorrentManager = monoTorrentManager;
            Torrent = torrent;

            var piecesCount = torrent.MonoTorrent.Pieces.Count;
            _customPiecePicker = customPiecePicker;
            _pieceRequests = new BtsPieceRequest[piecesCount];
            
            MonoTorrentManager.PieceHashed += (_, args) =>
            {
                if (!args.HashPassed) return;
                var piece = args.PieceIndex;
                lock (_lock)
                {
                    _pieceRequests[piece]?.Complete();
                }
            };
        }

        void CancelPieceRequest(int piece)
        {
            lock (_lock)
            {
                _pieceRequests[piece] = null;
                _customPiecePicker.RemovePiece(piece);
            }
        }
        
        internal IBtsPieceRequest? RequestPiece(int piece)
        {
            lock (_lock)
            {
                if (MonoTorrentManager.Bitfield[piece]) return null;
                
                var pieceRequest = _pieceRequests[piece];
                if (pieceRequest != null)
                {
                    pieceRequest.AddRef();
                    return pieceRequest;
                }
                
                _customPiecePicker.AddPiece(piece);
                return _pieceRequests[piece] = new BtsPieceRequest(() => CancelPieceRequest(piece));
            }
        }
        
        internal async Task FetchPiece(int piece, CancellationToken ct)
        {
            var requestPiece = RequestPiece(piece);
            if (requestPiece == null) return;

            using (requestPiece)
            {
                await requestPiece.AwaitFetching(ct);
            }
        }

        public BtsFileStream OpenFileStream(BtsFile file) => new BtsFileStream(this, file);

        public void Delete(BtsFile file)
        {
            lock (_lock)
            {
                var mFile = file.MonoTorrentFile;
                var start = mFile.StartPieceIndex;
                var end = mFile.EndPieceIndex;
                foreach (var piece in Enumerable.Range(start, end - start + 1))
                {
                    MonoTorrentManager.Bitfield.Set(piece, false);
                }
                _customPiecePicker.Reset();
                File.Delete(mFile.Path);
            }
        }
        
        public async Task Stop()
        {
            await MonoTorrentManager.StopAsync();
        }

        public void Dispose() => MonoTorrentManager.Dispose();
    }
}
