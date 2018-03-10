/**********************************************
* Power Grid Inventory
* Copyright 2015-2017 James Clark
**********************************************/
using System;


namespace PowerGridInventory
{
    /// <summary>
    /// A model-side storage obj for an item.
    /// </summary>
    [Serializable]
    public class CellModel
    {
        int _xPos;
        int _yPos;
        int _Equip;

        public bool SkipAutoEquip = false;
        public PGISlotItem Item = null;
        public bool Blocked = false;
        public PGIModel Model { get; private set; }

        public int xPos { get { return _xPos; } private set { _xPos = value; } }
        public int yPos { get { return _yPos; } private set { _yPos = value; } }
        public int EquipIndex { get { return _Equip; } private set { _Equip = value; } }

        public bool IsEquipmentCell { get { return _Equip >= 0; } }
        

        public override string ToString()
        {
            return "Cell Model (" + (IsEquipmentCell ? EquipIndex.ToString() : (string)(xPos + "," + yPos)) +")"; 
        }

        public CellModel(int index, PGIModel model)
        {
            EquipIndex = index;
            xPos = -1;
            yPos = -1;
            Model = model;
        }

        public CellModel(int x, int y, PGIModel model)
        {
            EquipIndex = -1;
            xPos = x;
            yPos = y;
            Model = model;
        }
    }
}