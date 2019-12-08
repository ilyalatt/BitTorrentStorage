using System.Collections.Generic;
using System.Linq;
using MonoTorrent;
using MonoTorrent.Client.PiecePicking;

namespace BitTorrentStorage
{
    public sealed class CustomPiecePicker : PiecePicker
    {
        public CustomPiecePicker(PiecePicker picker) : base(picker) { }

        readonly SortedSet<int> _pieces = new SortedSet<int>();

        public void AddPiece(int pieceIndex)
        {
            lock (_pieces) _pieces.Add(pieceIndex);
        }
        
        public void RemovePiece(int pieceIndex)
        {
            lock (_pieces) _pieces.Remove(pieceIndex);
        }

        List<int> GetPieces(int startIndex, int endIndex)
        {
            lock (_pieces)
            {
                return _pieces.GetViewBetween(startIndex, endIndex).ToList();
            }
        }

        public override IList<PieceRequest> PickPiece(
            IPieceRequester peer,
            BitField available,
            IReadOnlyList<IPieceRequester> otherPeers,
            int count,
            int startIndex,
            int endIndex
        )
        {
            var requests = new List<PieceRequest>(count);
            var pieces = GetPieces(startIndex, endIndex);
            foreach (var piece in pieces)
            {
                if (requests.Count == count) return requests;
                var newRequests = base.PickPiece(peer, available, otherPeers, count, piece, piece);
                if (newRequests != null) requests.AddRange(newRequests);
            }
            return requests;
        }
    }
}