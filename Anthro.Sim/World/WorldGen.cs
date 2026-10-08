using System;
using System.Collections.Generic;
using System.Linq;
using Anthro.Sim.Core;

namespace Anthro.Sim.World
{
    /// <summary>
    /// Builds a valley: a meandering river with marsh along it, forest and grassland on the slopes,
    /// and hills on one side holding flint, a cave and ochre. Clay sits on the riverbank.
    /// </summary>
    public static class WorldGen
    {
        /// <summary>Scales every cell's plant food. The calibration knob for carrying capacity.</summary>
        public static double PlantDensity = 1.0;

        public static WorldMap Generate(Rng rng, int size = 100)
        {
            var map = new WorldMap(size);
            int seed = (int)rng.NextUInt();
            double phase = rng.Range(0, Math.PI * 2);
            int hillSide = rng.Chance(0.5) ? 1 : -1;

            for (int y = 0; y < size; y++)
            {
                double riverX = size / 2.0 + size * 0.12 * Math.Sin(y / (size / 7.0) + phase)
                    + (Fractal(seed, 0.5, y * 0.05) - 0.5) * 6;
                for (int x = 0; x < size; x++)
                {
                    double offset = x - riverX;
                    double dist = Math.Abs(offset);
                    double elev = dist / (size / 2.0)
                        + (Fractal(seed + 1, x * 0.08, y * 0.08) - 0.5) * 0.5
                        + Math.Max(0, hillSide * offset / size) * 0.9;
                    double moisture = Fractal(seed + 2, x * 0.1, y * 0.1) + (dist < 4 ? 0.35 : 0);

                    var cell = new Cell { X = x, Y = y, Elevation = elev };
                    if (dist < 0.8) cell.Terrain = Terrain.River;
                    else if (elev > 0.8) cell.Terrain = Terrain.Hills;
                    else if (moisture > 0.8 && dist < WorldMap.FromMeters(1200)) cell.Terrain = Terrain.Marsh;
                    else if (moisture > 0.5) cell.Terrain = Terrain.Forest;
                    else cell.Terrain = Terrain.Grassland;

                    AssignPlants(cell, seed, rng);
                    map.Cells[x, y] = cell;
                }
            }

            PlaceFeatures(map, rng);
            foreach (var c in map.All())
            {
                c.PlantStock = c.PlantCapacity * Cell.SeasonFactor(c.Plant, Season.Spring);
                c.WoodCapacity = Cell.WoodCapacityOf(c.Terrain);
                c.Wood = c.WoodCapacity;
                c.FishStock = c.FishCapacity;
                c.WildCapacity = c.PlantCapacity;
                if (c.Terrain == Terrain.Forest) c.Bark = Cell.BarkCapacity;
            }
            return map;
        }

        static void AssignPlants(Cell c, int seed, Rng rng)
        {
            double n = Fractal(seed + 3, c.X * 0.15, c.Y * 0.15);
            switch (c.Terrain)
            {
                case Terrain.Marsh:
                    c.Plant = PlantFood.Roots; c.PlantCapacity = 5; c.Features |= Feature.Reeds;
                    c.FishCapacity = 8 * rng.Range(0.7, 1.3); break;
                case Terrain.River:
                    c.FishCapacity = 50 * rng.Range(0.7, 1.3); break;
                case Terrain.Forest:
                    c.Plant = n > 0.5 ? PlantFood.Nuts : PlantFood.Berries; c.PlantCapacity = 6; break;
                case Terrain.Grassland:
                    c.Plant = n > 0.6 ? PlantFood.WildGrain : n > 0.3 ? PlantFood.Roots : PlantFood.Greens;
                    c.PlantCapacity = 4; break;
                case Terrain.Hills:
                    c.Plant = PlantFood.Berries; c.PlantCapacity = 1.5; break;
                default:
                    c.Plant = PlantFood.None; c.PlantCapacity = 0; break;
            }
            // Per-cell plant food, calibrated so the range supports a real forager band (roughly 0.1
            // people per km² in a temperate climate) rather than an unrealistically packed one.
            c.PlantCapacity *= rng.Range(0.7, 1.3) * PlantDensity;
        }

        static void PlaceFeatures(WorldMap map, Rng rng)
        {
            var hills = map.All().Where(c => c.Terrain == Terrain.Hills).ToList();
            if (hills.Count < 6)
            {
                // Guarantee some high ground: promote the highest cells.
                foreach (var c in map.All().Where(c => c.Terrain != Terrain.River).OrderByDescending(c => c.Elevation).Take(12))
                {
                    c.Terrain = Terrain.Hills; c.Plant = PlantFood.Berries; c.PlantCapacity = 1.5;
                    hills.Add(c);
                }
            }

            foreach (var c in hills) if (rng.Chance(0.06)) c.Features |= Feature.Flint;
            if (!hills.Any(c => c.Has(Feature.Flint))) rng.Pick(hills).Features |= Feature.Flint;

            var high = hills.OrderByDescending(c => c.Elevation).ToList();
            high[rng.Range(0, Math.Min(5, high.Count))].Features |= Feature.Cave;
            rng.Pick(hills).Features |= Feature.Ochre;

            var bank = map.All().Where(c => c.Terrain != Terrain.River && NextToRiver(map, c)).ToList();
            for (int i = 0; i < 3 && bank.Count > 0; i++)
            {
                var c = rng.Pick(bank);
                c.Features |= Feature.Clay;
                bank.Remove(c);
            }
        }

        static bool NextToRiver(WorldMap map, Cell c)
        {
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if (map.InBounds(c.X + dx, c.Y + dy) && map[c.X + dx, c.Y + dy].Terrain == Terrain.River) return true;
            return false;
        }

        // Value noise: smooth, seeded, no dependencies.
        static double Fractal(int seed, double x, double y)
        {
            double sum = 0, amp = 1, norm = 0;
            for (int o = 0; o < 4; o++)
            {
                sum += Value(seed + o * 101, x, y) * amp;
                norm += amp;
                amp *= 0.5; x *= 2; y *= 2;
            }
            return sum / norm;
        }

        static double Value(int seed, double x, double y)
        {
            int xi = (int)Math.Floor(x), yi = (int)Math.Floor(y);
            double fx = x - xi, fy = y - yi;
            double sx = fx * fx * (3 - 2 * fx), sy = fy * fy * (3 - 2 * fy);
            double a = Hash(seed, xi, yi), b = Hash(seed, xi + 1, yi), c = Hash(seed, xi, yi + 1), d = Hash(seed, xi + 1, yi + 1);
            return Lerp(Lerp(a, b, sx), Lerp(c, d, sx), sy);
        }

        static double Lerp(double a, double b, double t) => a + (b - a) * t;

        static double Hash(int seed, int x, int y)
        {
            unchecked
            {
                uint h = (uint)seed * 374761393u + (uint)x * 668265263u + (uint)y * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return h / 4294967296.0;
            }
        }
    }
}
