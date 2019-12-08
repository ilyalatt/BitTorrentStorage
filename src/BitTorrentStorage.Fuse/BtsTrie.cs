using System;
using System.Collections.Generic;
using System.Linq;

namespace BitTorrentStorage.Fuse
{
    public sealed class BtsTrie
    {
        public readonly IReadOnlyDictionary<string, BtsFile> Files;
        public readonly IReadOnlyDictionary<string, BtsTrie> Directories;

        public BtsTrie(IReadOnlyDictionary<string, BtsFile> files, IReadOnlyDictionary<string, BtsTrie> directories)
        {
            Files = files;
            Directories = directories;
        }

        public BtsTrie? TryFindDirectory(Memory<string> path) => path.Length == 0
            ? this
            : Directories.TryGetValue(path.Span[0], out var res) ? res.TryFindDirectory(path.Slice(1)) : null;

        public BtsTrie? TryFindDirectory(string path) =>
            TryFindDirectory(path.Split('/', StringSplitOptions.RemoveEmptyEntries));


        public BtsFile? TryFindFile(Memory<string> path) => path.Length switch
        {
            0 => null,
            1 => Files.TryGetValue(path.Span[0], out var file) ? file : null,
            _ => TryFindDirectory(path.Slice(0, path.Length - 1))?.TryFindFile(path.Slice(path.Length - 1))
        };

        public BtsFile? TryFindFile(string path) =>
            TryFindFile(path.Split('/', StringSplitOptions.RemoveEmptyEntries));

        
        public static BtsTrie Build(IEnumerable<BtsFile> files)
        {
            BtsTrie Rec(IEnumerable<(Memory<string>, BtsFile)> pathWithFileSeq)
            {
                var isFileDichotomy = pathWithFileSeq
                    .GroupBy(x => x.Item1.Length == 1)
                    .ToDictionary(g => g.Key, g => g.AsEnumerable());
                var trieFiles = isFileDichotomy
                    .GetValueOrDefault(true, Enumerable.Empty<(Memory<string>, BtsFile)>())
                    .ToDictionary(x => x.Item1.Span[0], x => x.Item2);
                var trieDirectories = isFileDichotomy
                    .GetValueOrDefault(false, Enumerable.Empty<(Memory<string>, BtsFile)>())
                    .Where(x => x.Item1.Length != 1)
                    .GroupBy(x => x.Item1.Span[0])
                    .ToDictionary(g => g.Key, g => Rec(g.Select(x => (x.Item1.Slice(1), x.Item2))));
                return new BtsTrie(trieFiles, trieDirectories);
            }

            return Rec(files.Select(x => (x.Path.Split('/').AsMemory(), x)));
        }
    }
}
