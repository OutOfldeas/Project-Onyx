using System;
using System.Collections.Generic;
using System.Linq;

namespace Onyx.Core.Ships
{
    public sealed class PlacedModule
    {
        public int InstanceId { get; }
        public ModuleDefinition Definition { get; }
        public int X { get; }
        public int Y { get; }

        /// <summary>Um 90° gedreht: Breite und Höhe vertauscht.</summary>
        public bool Rotated { get; }

        public int Width => Rotated ? Definition.Height : Definition.Width;
        public int Height => Rotated ? Definition.Width : Definition.Height;

        public PlacedModule(int instanceId, ModuleDefinition definition, int x, int y, bool rotated)
        {
            InstanceId = instanceId;
            Definition = definition;
            X = x;
            Y = y;
            Rotated = rotated;
        }

        public bool Covers(int x, int y) => x >= X && x < X + Width && y >= Y && y < Y + Height;
    }

    public enum PlacementResult
    {
        Ok,
        OutOfBounds,
        NoHull,
        Occupied,
        UniqueAlreadyPlaced,
    }

    /// <summary>
    /// Bauplan eines Schiffs auf einem Kachelraster: Rumpfkacheln (begehbarer Boden) und
    /// darauf platzierte Module. Die Schiffswerft bearbeitet diesen Bauplan; das Schiff in der
    /// Welt wird daraus gebaut. Koordinaten: (0,0) unten links.
    /// </summary>
    public sealed class ShipBlueprint
    {
        private readonly bool[,] hull;
        private readonly List<PlacedModule> modules = new List<PlacedModule>();
        private int nextInstanceId = 1;

        public int Width { get; }
        public int Height { get; }
        public string Name { get; set; }

        public IReadOnlyList<PlacedModule> Modules => modules;

        public event Action Changed;

        public ShipBlueprint(int width, int height, string name = "Neues Schiff")
        {
            if (width <= 0 || height <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Raster muss mindestens 1 × 1 groß sein.");
            }

            Width = width;
            Height = height;
            Name = name;
            hull = new bool[width, height];
        }

        public bool InBounds(int x, int y) => x >= 0 && y >= 0 && x < Width && y < Height;

        public bool HasHull(int x, int y) => InBounds(x, y) && hull[x, y];

        public int HullCount
        {
            get
            {
                int count = 0;
                foreach (bool cell in hull)
                {
                    if (cell) count++;
                }

                return count;
            }
        }

        public bool SetHull(int x, int y, bool value)
        {
            if (!InBounds(x, y) || hull[x, y] == value)
            {
                return false;
            }

            // Rumpf unter einem Modul darf nicht entfernt werden.
            if (!value && ModuleAt(x, y) != null)
            {
                return false;
            }

            hull[x, y] = value;
            Changed?.Invoke();
            return true;
        }

        public void FillHull(int x, int y, int width, int height)
        {
            for (int ix = x; ix < x + width; ix++)
            {
                for (int iy = y; iy < y + height; iy++)
                {
                    if (InBounds(ix, iy))
                    {
                        hull[ix, iy] = true;
                    }
                }
            }

            Changed?.Invoke();
        }

        public PlacedModule ModuleAt(int x, int y) => modules.FirstOrDefault(m => m.Covers(x, y));

        public PlacementResult CanPlace(ModuleDefinition definition, int x, int y, bool rotated)
        {
            int w = rotated ? definition.Height : definition.Width;
            int h = rotated ? definition.Width : definition.Height;

            if (!InBounds(x, y) || !InBounds(x + w - 1, y + h - 1))
            {
                return PlacementResult.OutOfBounds;
            }

            if (definition.Unique && modules.Any(m => m.Definition.Id == definition.Id))
            {
                return PlacementResult.UniqueAlreadyPlaced;
            }

            for (int ix = x; ix < x + w; ix++)
            {
                for (int iy = y; iy < y + h; iy++)
                {
                    if (!hull[ix, iy]) return PlacementResult.NoHull;
                    if (ModuleAt(ix, iy) != null) return PlacementResult.Occupied;
                }
            }

            return PlacementResult.Ok;
        }

        public PlacementResult Place(ModuleDefinition definition, int x, int y, bool rotated = false)
        {
            var result = CanPlace(definition, x, y, rotated);
            if (result == PlacementResult.Ok)
            {
                modules.Add(new PlacedModule(nextInstanceId++, definition, x, y, rotated));
                Changed?.Invoke();
            }

            return result;
        }

        /// <summary>Entfernt das Modul an dieser Kachel. Liefert das entfernte Modul oder null.</summary>
        public PlacedModule RemoveModuleAt(int x, int y)
        {
            var module = ModuleAt(x, y);
            if (module != null)
            {
                modules.Remove(module);
                Changed?.Invoke();
            }

            return module;
        }

        /// <summary>Sind alle Rumpfkacheln zusammenhängend (4er-Nachbarschaft)?</summary>
        public bool IsHullConnected()
        {
            int total = HullCount;
            if (total == 0)
            {
                return false;
            }

            var visited = new bool[Width, Height];
            var queue = new Queue<(int x, int y)>();
            for (int x = 0; x < Width && queue.Count == 0; x++)
            {
                for (int y = 0; y < Height; y++)
                {
                    if (hull[x, y])
                    {
                        queue.Enqueue((x, y));
                        visited[x, y] = true;
                        break;
                    }
                }
            }

            int reached = 0;
            var directions = new[] { (1, 0), (-1, 0), (0, 1), (0, -1) };
            while (queue.Count > 0)
            {
                var (cx, cy) = queue.Dequeue();
                reached++;
                foreach (var (dx, dy) in directions)
                {
                    int nx = cx + dx;
                    int ny = cy + dy;
                    if (HasHull(nx, ny) && !visited[nx, ny])
                    {
                        visited[nx, ny] = true;
                        queue.Enqueue((nx, ny));
                    }
                }
            }

            return reached == total;
        }
    }
}
