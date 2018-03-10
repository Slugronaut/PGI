/**********************************************
* Power Grid Inventory
* Copyright 2015-2017 James Clark
**********************************************/
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Assertions;
using Area = PowerGridInventory.PGIModel.Area;
using Pos = PowerGridInventory.PGIModel.Pos;
using ImmutablePos = PowerGridInventory.PGIModel.ImmutablePos;

namespace PowerGridInventory
{
    /// <summary>
    /// Stores a 'simulated' space for a model's grid and equipment slots.
    /// Can be Used determine if a set of conditions would disallow
    /// an item from being properly stored without having to
    /// actually assign items to the actual model.
    /// </summary>
    public class SimulatedModelSpace
    {
        public struct SpaceState
        {
            public PGISlotItem item;
            public Area area;
            public int index;
            public bool IsEquipment { get { return index >= 0; } }
            public bool IsInvalid { get { return index < 0 && area.x < 0; } }

            public SpaceState(int i, PGISlotItem it)
            {
                item = null;
                index = i;
                area = new Area(-1, -1, -1, -1);
                item = it;
            }

            public SpaceState(Area a, PGISlotItem it)
            {
                item = null;
                index = -1;
                area = a;
                item = it;
            }

            public static SpaceState Invalid { get { return new SpaceState(-1, null); } }
        }

        bool[] Equipment;
        bool[,] Grid;
        int Width, Height;
        Stack<SpaceState> History = new Stack<SpaceState>();


        public void ResetHistory()
        {
            History.Clear();
        }
        
        /// <summary>
        /// Default not allowed.
        /// </summary>
        SimulatedModelSpace()
        {
        }

        /// <summary>
        /// Returns the state of a PGIModel's space.
        /// </summary>
        /// <param name="model"></param>
        public SimulatedModelSpace(PGIModel model)
        {
            Assert.IsNotNull(model);
            Equipment = new bool[model.EquipmentCellsCount];
            Width = model.GridCellsX;
            Height = model.GridCellsY;
            Grid = new bool[Width, Height];
        }

        /// <summary>
        /// Initializes all cells so that the are filled appropriate based on the model.
        /// Note that blocked cells are marked as filled.
        /// </summary>
        public void Init(PGIModel model)
        {
            for(int y = 0; y < Height; y++)
            {
                for(int x = 0; x < Width; x++)
                {
                    var item = model.GetCellContents(x, y);
                    Grid[x, y] = item == null ? false : true;
                    if (Grid[x, y] && (model.IsCellBlocked(x, y) || model.IsCellDisabled(x, y)))
                        Grid[x, y] = true;
                }
            }

            for(int i = 0; i < Equipment.Length; i++)
            {
                var item = model.GetEquipmentContents(i);
                Equipment[i] = item == null ? false : true;
                if (Equipment[i] && model.IsCellBlocked(i))
                    Equipment[i] = true;
            }
        }

        bool HasSpaceAt(Area area)
        {
            int startX = area.x;
            int startY = area.y;
            for(int y = startY; y < startY + area.height; y++)
            {
                for(int x = startX; x < startX + area.width; x++)
                {
                    if (Grid[x, y]) return false;
                }
            }

            return true;
        }

        bool HasSpaceAt(int index)
        {
            return !Equipment[index];
        }

        bool Push(Area area, PGISlotItem item)
        {
            if (!HasSpaceAt(area)) return false;
            History.Push(new SpaceState(area, item));
            Set(area, true);
            return true;
        }

        bool Push(int index, PGISlotItem item)
        {
            if (!HasSpaceAt(index)) return false;
            History.Push(new SpaceState(index, item));
            Equipment[index] = true;
            return true;
        }

        bool Push(SpaceState space, PGISlotItem item)
        {
            space.item = item;
            if (space.IsInvalid) return false;
            if (space.IsEquipment)
                return Push(space.index, item);
            else return Push(space.area, item);
        }

        static ImmutablePos TempPos = new ImmutablePos();
        static Area TempArea = new Area();
        /// <summary>
        /// Given a location in the grid, what is the first overlapping region of space
        /// that matches the required dimensions. It starts at the top-left corner of the
        /// given space and shifts it up and left as it tests.
        /// </summary>
        /// <param name=""></param>
        /// <returns></returns>
        public ImmutablePos? FindFirstOverlapping(int x, int y, int width, int height)
        {
            
            for (int t = 0; t < height; t++)
            {
                for (int s = 0; s < width; s++)
                {
                    TempArea.x = x - s;
                    TempArea.y = y - t;
                    TempArea.width = width;
                    TempArea.height = height;
                    if (HasSpaceAt(TempArea))
                    {
                        TempPos.X = x - s;
                        TempPos.Y = y - t;
                        return TempPos;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Sets the state for a given area in the simulation grid.
        /// Remember that <c>true</c> means 'occupied' and <c>false</c>
        /// means 'empty'.
        /// </summary>
        /// <param name="area"></param>
        /// <param name="state"></param>
        public void Set(Area area, bool state)
        {
            int startX = area.x;
            int startY = area.y;
            for (int y = startY; y < startY + area.height; y++)
            {
                for (int x = startX; x < startX + area.width; x++)
                    Grid[x, y] = state;
            }
        }

        /// <summary>
        /// Sets the state for a given index in the simulation equipment.
        /// Remember that <c>true</c> means 'occupied' and <c>false</c>
        /// means 'empty'.
        /// </summary>
        public void Set(int index, bool state)
        {
            Equipment[index] = false;
        }

        /// <summary>
        /// Sets the state for a given item in the simulation model.
        /// Remember that <c>true</c> means 'occupied' and <c>false</c>
        /// means 'empty'.
        /// </summary>
        public void Set(PGISlotItem item, bool state)
        {
            if (item == null) return;
            if (item.IsEquipped)
                Set(item.Equipped, state);
            else Set(new Area(item.xInvPos, item.yInvPos, item.CellWidth, item.CellHeight), state);
        }

        SpaceState FindFirstAvailableGridLocation(PGISlotItem item)
        {
            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    Area a = new Area(x, y, item.CellWidth, item.CellHeight);
                    if (HasSpaceAt(a))
                        return new SpaceState(a, item);
                }
            }

            return SpaceState.Invalid;
        }

        SpaceState FindFirstEquipmentLocation()
        {
            for(int i = 0; i < Equipment.Length; i++)
            {
                if (!Equipment[i])
                    return new SpaceState(i, null);
            }

            return SpaceState.Invalid;
        }
            
        /// <summary>
        /// Runs a simulation of pushing the item into a model.
        /// </summary>
        /// <param name="item">The item to test storing.</param>
        public bool PushStore(PGISlotItem item, bool allowEquipment, bool equipmentFirst, out SpaceState state)
        {
            if (!allowEquipment)
                equipmentFirst = false;


            SpaceState space = SpaceState.Invalid;

            if(equipmentFirst)
                space = FindFirstEquipmentLocation();
            if (space.IsInvalid)
                space = FindFirstAvailableGridLocation(item);
            if (space.IsInvalid && allowEquipment)
                space = FindFirstEquipmentLocation();

            if (Push(space, item))
            {
                state = History.Peek();
                return true;
            }
            else
            {
                state = SpaceState.Invalid;
                return false;
            }
                
        }

        /// <summary>
        /// Removes the last item that was simulated to be stored.
        /// </summary>
        /// <returns><c>true</c> if an item was poped, <c>false</c> if there are no more simulated items to be poped.</returns>
        public bool PopLastStore()
        {
            if (History.Count > 0)
            {
                History.Pop();
                if (History.Count > 0)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// Attempts all possible permutations of storing items until a valid option is found.
        /// This does not attempt to account for roation of items.
        /// <para>
        /// This is an O(n^n) function! Best not to use it with large lists of items.
        /// </para>
        /// </summary>
        /// <param name="items">The items to attempt to store.</param>
        /// <returns>A list of storage locations and the item to store there in the order they must be sotred, or null if no valid combination was found.</returns>
        public List<SpaceState> FindStoragePermutation(PGISlotItem[] items, bool allowEquipment, bool equipmentFirst)
        {
            if (items == null || items.Length < 1) return null;

            var final = new List<SpaceState>(5);
            Permute(ref final, items, allowEquipment, equipmentFirst);
            if (final.Count == items.Length)
                return final;
            return null;
        }

        /// <summary>
        /// Recursive helper. This is a big, ugly, nasty, ugly f'ed-in-the-a algorithm.
        /// Use with extreme caution!
        /// </summary>
        bool Permute(ref List<SpaceState> final, PGISlotItem[] items, bool aE, bool eF)
        {
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];

                if(!final.Select((x)=>x.item).Contains(item))
                {
                    SpaceState state;
                    if (PushStore(item, aE, eF, out state))
                        final.Add(state);
                }
                if (Permute(ref final, items, aE, eF))
                    return true;
            }
            return false;
        }
    }
}