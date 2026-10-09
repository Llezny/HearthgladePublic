using System;
using System.Collections.Generic;

namespace Hearthglade.Core.World
{
    /// <summary>A piece of furniture the player put into a house: what it is and which cells of the room it covers.</summary>
    public readonly struct PlacedPiece
    {
        public readonly int Id;
        public readonly string Item;
        /// <summary>The lowest corner cell of the footprint (after turning).</summary>
        public readonly int X, Z;
        /// <summary>The footprint after turning: a piece turned a quarter turn swaps its sides.</summary>
        public readonly int SizeX, SizeZ;
        public readonly int QuarterTurns;
        /// <summary>A rug or a mat: lies on the floor, so other furniture may stand on it and the player walks over it.</summary>
        public readonly bool Flat;

        public PlacedPiece(int id, string item, int x, int z, int sizeX, int sizeZ, int quarterTurns, bool flat)
        {
            Id = id;
            Item = item;
            X = x;
            Z = z;
            SizeX = sizeX;
            SizeZ = sizeZ;
            QuarterTurns = quarterTurns;
            Flat = flat;
        }

        public bool Covers(int x, int z)
        {
            return x >= X && x < X + SizeX && z >= Z && z < Z + SizeZ;
        }
    }

    /// <summary>
    /// What stands where in the room of a house (docs/HOME_ISLAND_PLAN.md, phase 5). A small grid of cells (the same cell as the build grid
    /// of the island) with a few cells that are always taken (the furniture that is part of the house) and one entry cell in front of
    /// the door. Furniture may not leave the room, overlap other furniture, cover the entry cell or wall off a part of the floor the
    /// player could walk to before. Pure data and arithmetic, so it is testable without Unity.
    /// </summary>
    public sealed class InteriorLayout
    {
        private readonly int minX, minZ, maxX, maxZ;
        private readonly HashSet<(int, int)> fixedCells;
        private readonly (int x, int z) entry;
        private readonly List<PlacedPiece> pieces = new List<PlacedPiece>();
        private int nextId = 1;

        /// <summary>The room covers the cells from (minX, minZ) to (maxX, maxZ), both included.</summary>
        public InteriorLayout(int minX, int minZ, int maxX, int maxZ, IEnumerable<(int, int)> fixedCells, (int x, int z) entry)
        {
            if (maxX < minX || maxZ < minZ)
            {
                throw new ArgumentOutOfRangeException(nameof(maxX), "a room needs at least one cell");
            }
            this.minX = minX;
            this.minZ = minZ;
            this.maxX = maxX;
            this.maxZ = maxZ;
            this.fixedCells = new HashSet<(int, int)>(fixedCells ?? Array.Empty<(int, int)>());
            this.entry = entry;
        }

        public IReadOnlyList<PlacedPiece> Pieces => pieces;

        public bool InRoom(int x, int z)
        {
            return x >= minX && x <= maxX && z >= minZ && z <= maxZ;
        }

        /// <summary>Whether a piece of this footprint (before turning) fits with its lowest corner (after turning) at (x, z).</summary>
        public bool CanPlace(int x, int z, int sizeX, int sizeZ, int quarterTurns, bool flat)
        {
            Footprint(sizeX, sizeZ, quarterTurns, out int width, out int depth);
            for (int cz = z; cz < z + depth; cz++)
            {
                for (int cx = x; cx < x + width; cx++)
                {
                    if (!InRoom(cx, cz) || fixedCells.Contains((cx, cz)) || IsTaken(cx, cz, flat))
                    {
                        return false;
                    }
                }
            }
            if (flat)
            {
                return true;
            }
            // A rug does not stop anyone; a solid piece must leave the entry free and every cell that could be reached still reachable.
            if (entry.x >= x && entry.x < x + width && entry.z >= z && entry.z < z + depth)
            {
                return false;
            }
            var before = ReachableCells(null);
            var after = ReachableCells((x, z, width, depth));
            int footprintOnReachable = 0;
            foreach (var cell in before)
            {
                if (cell.Item1 >= x && cell.Item1 < x + width && cell.Item2 >= z && cell.Item2 < z + depth)
                {
                    footprintOnReachable++;
                }
            }
            return after.Count == before.Count - footprintOnReachable;
        }

        /// <summary>Puts a piece down. Returns its id, or 0 when it does not fit.</summary>
        public int Place(string item, int x, int z, int sizeX, int sizeZ, int quarterTurns, bool flat)
        {
            if (!CanPlace(x, z, sizeX, sizeZ, quarterTurns, flat))
            {
                return 0;
            }
            Footprint(sizeX, sizeZ, quarterTurns, out int width, out int depth);
            int id = nextId++;
            pieces.Add(new PlacedPiece(id, item, x, z, width, depth, ((quarterTurns % 4) + 4) % 4, flat));
            return id;
        }

        public bool Remove(int id)
        {
            int index = pieces.FindIndex(piece => piece.Id == id);
            if (index < 0)
            {
                return false;
            }
            pieces.RemoveAt(index);
            return true;
        }

        /// <summary>The piece standing on a cell. Solid furniture wins over a rug under it. Null when the cell is bare.</summary>
        public PlacedPiece? PieceAt(int x, int z)
        {
            PlacedPiece? rug = null;
            foreach (var piece in pieces)
            {
                if (!piece.Covers(x, z))
                {
                    continue;
                }
                if (!piece.Flat)
                {
                    return piece;
                }
                rug = piece;
            }
            return rug;
        }

        /// <summary>The cells that furniture covers (rugs excluded) and that the player must stay out of.</summary>
        public IEnumerable<(int, int)> SolidCells()
        {
            foreach (var cell in fixedCells)
            {
                yield return cell;
            }
            foreach (var piece in pieces)
            {
                if (piece.Flat)
                {
                    continue;
                }
                for (int z = piece.Z; z < piece.Z + piece.SizeZ; z++)
                {
                    for (int x = piece.X; x < piece.X + piece.SizeX; x++)
                    {
                        yield return (x, z);
                    }
                }
            }
        }

        private static void Footprint(int sizeX, int sizeZ, int quarterTurns, out int width, out int depth)
        {
            bool turned = ((quarterTurns % 2) + 2) % 2 == 1;
            width = turned ? sizeZ : sizeX;
            depth = turned ? sizeX : sizeZ;
        }

        // A solid piece conflicts with solid pieces; a rug only with another rug (and neither with the cells that are always taken).
        private bool IsTaken(int x, int z, bool flat)
        {
            foreach (var piece in pieces)
            {
                if (piece.Flat == flat && piece.Covers(x, z))
                {
                    return true;
                }
            }
            return false;
        }

        // The free cells the player can walk to from the entry cell, with an extra footprint counted as taken.
        private HashSet<(int, int)> ReachableCells((int x, int z, int width, int depth)? extra)
        {
            var solid = new HashSet<(int, int)>(SolidCells());
            if (extra.HasValue)
            {
                var box = extra.Value;
                for (int z = box.z; z < box.z + box.depth; z++)
                {
                    for (int x = box.x; x < box.x + box.width; x++)
                    {
                        solid.Add((x, z));
                    }
                }
            }
            var reached = new HashSet<(int, int)>();
            if (!InRoom(entry.x, entry.z) || solid.Contains(entry))
            {
                return reached;
            }
            var open = new Queue<(int, int)>();
            reached.Add(entry);
            open.Enqueue(entry);
            while (open.Count > 0)
            {
                var (cx, cz) = open.Dequeue();
                foreach (var next in new[] { (cx + 1, cz), (cx - 1, cz), (cx, cz + 1), (cx, cz - 1) })
                {
                    if (InRoom(next.Item1, next.Item2) && !solid.Contains(next) && reached.Add(next))
                    {
                        open.Enqueue(next);
                    }
                }
            }
            return reached;
        }
    }
}
