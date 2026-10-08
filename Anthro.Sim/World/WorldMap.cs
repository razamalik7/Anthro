using System;
using System.Collections.Generic;
using System.Linq;

namespace Anthro.Sim.World
{
    public sealed class WorldMap
    {
        /// <summary>
        /// Real-world size of one cell. The map is a band's real home range (~20 km across) at real
        /// densities of plants and animals; cells are just how finely the ecology is tracked.
        /// </summary>
        public const double CellMeters = 200;

        /// <summary>A real distance expressed in cells. Every range in the sim is written in meters through this.</summary>
        public static double FromMeters(double meters) => meters / CellMeters;

        public readonly int Size;
        public readonly Cell[,] Cells;
        public readonly Climate Climate = new Climate();

        public WorldMap(int size)
        {
            Size = size;
            Cells = new Cell[size, size];
        }

        public Cell this[int x, int y] => Cells[x, y];

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Size && y < Size;

        List<Cell>? _flint;

        /// <summary>Every cell where flint can be dug (it never moves, so it's found once).</summary>
        public IReadOnlyList<Cell> FlintCells => _flint ??= All().Where(c => c.Has(Feature.Flint)).ToList();

        public IEnumerable<Cell> All()
        {
            for (int x = 0; x < Size; x++)
                for (int y = 0; y < Size; y++)
                    yield return Cells[x, y];
        }

        public IEnumerable<Cell> Around(double cx, double cy, double radius)
        {
            int r = (int)Math.Ceiling(radius);
            int x0 = (int)cx, y0 = (int)cy;
            for (int x = x0 - r; x <= x0 + r; x++)
                for (int y = y0 - r; y <= y0 + r; y++)
                    if (InBounds(x, y) && Distance(cx, cy, x, y) <= radius)
                        yield return Cells[x, y];
        }

        public static double Distance(double ax, double ay, double bx, double by)
        {
            double dx = ax - bx, dy = ay - by;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>Walking hours for a distance in cells, at about 3 km/h over rough ground.</summary>
        public static int TravelHours(double cells) => Math.Max(0, (int)Math.Round(cells * CellMeters / 3000.0));
    }
}
