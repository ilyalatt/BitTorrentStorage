using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MonoTorrent;

namespace BitTorrentStorage
{
    public sealed class BtsFileStream : Stream
    {
        public readonly BtsTorrentManager Manager;
        public readonly BtsFile File;
        FileStream? _fileStream;

        public BtsFileStream(BtsTorrentManager manager, BtsFile file)
        {
            Manager = manager;
            File = file;
        }

        public override bool CanRead => true;
        public override bool CanSeek => true;
        public override bool CanWrite => false;
        public override long Length => File.Length;
        
        long _position;
        public override long Position
        {
            get => _position;
            set
            {
                if (0 > value || value > Length) throw new ArgumentOutOfRangeException(nameof(value));
                _position = value;
            }
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            var newPosition = origin switch
            {
                SeekOrigin.Begin => offset,
                SeekOrigin.Current => Position + offset,
                SeekOrigin.End => Length - offset,
                _ => throw new ArgumentOutOfRangeException(nameof(origin))
            };
            return Position = newPosition;
        }

        // maybe use just range
        readonly Dictionary<int, IBtsPieceRequest> _prefetchRequests = new Dictionary<int, IBtsPieceRequest>();
        int _lastPrefetchEndPiece = -1;

        const int PrefetchNextPiecesCount = 10;
        void UpdatePrefetch(int currentEndPiece)
        {
            if (_lastPrefetchEndPiece == currentEndPiece) return;
            
            var mFile = File.MonoTorrentFile;
            var startPiece = currentEndPiece + 1;
            var count = Math.Min(
                PrefetchNextPiecesCount,
                mFile.EndPieceIndex - startPiece + 1
            );
            var pieces = Enumerable.Range(startPiece, count);
            // ReSharper disable once PossibleMultipleEnumeration
            var oldPieces = _prefetchRequests.Keys.Except(pieces).ToList();
            // ReSharper disable once PossibleMultipleEnumeration
            var newPieces = pieces.Except(_prefetchRequests.Keys).ToList();

            foreach (var x in oldPieces)
            {
                _prefetchRequests[x].Dispose();
                _prefetchRequests.Remove(x);
            }
            foreach (var x in newPieces)
            {
                var req = Manager.RequestPiece(x);
                if (req != null) _prefetchRequests[x] = req;
            }

            _lastPrefetchEndPiece = currentEndPiece;
        }
        
        async Task Fetch(long offset, long count, CancellationToken ct)
        {
            if (count == 0) return;
            
            var mFile = File.MonoTorrentFile;
            var pieceLength = Manager.Torrent.MonoTorrent.PieceLength;
            var start = (long) mFile.StartPieceIndex * pieceLength + mFile.StartPieceOffset + offset;
            var end = start + count - 1;
            var startPiece = (int) (start / pieceLength);
            var endPiece = (int) (end / pieceLength);
            var pieces = Enumerable.Range(startPiece, endPiece - startPiece + 1);
            
            File.MonoTorrentFile.Priority = Priority.Normal;
            UpdatePrefetch(endPiece);
            var fetchTasks = pieces.Select(x => Manager.FetchPiece(x, ct));
            await Task.WhenAll(fetchTasks).ConfigureAwait(false);
        }

        FileStream GetFs() => _fileStream ??= System.IO.File.OpenRead(File.MonoTorrentFile.FullPath);

        public override async Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count, 
            CancellationToken cancellationToken
        )
        {
            count = Math.Max(0, Math.Min(count, (int) (Length - Position)));
            if (count == 0) return 0;
            
            await Fetch(Position, count, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            
            var fs = GetFs();
            cancellationToken.ThrowIfCancellationRequested();
            
            fs.Position = Position;
            var bytesRead = await fs.ReadAsync(buffer, offset, count, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            
            Position += bytesRead;
            return bytesRead;
        }

        public override int Read(byte[] buffer, int offset, int count) => ReadAsync(buffer, offset, count, default).Result;

        public override void SetLength(long value) => throw new InvalidOperationException();
        public override void Write(byte[] buffer, int offset, int count) => throw new InvalidOperationException();
        public override void Flush() => throw new InvalidOperationException();


        void DisposePrefetch()
        {
            foreach (var x in _prefetchRequests.Values) x.Dispose();
            _prefetchRequests.Clear();
        }
        
        public override async ValueTask DisposeAsync()
        {
            if (_fileStream != null) await _fileStream.DisposeAsync();
            DisposePrefetch();
        }

        protected override void Dispose(bool disposing)
        {
            _fileStream?.Dispose();
            DisposePrefetch();
        }
    }
}
