﻿using SharpDX;
using RectangleF = SharpDX.RectangleF;

namespace ExileCore.PoEMemory.Elements.InventoryElements
{
    public class BlightInventoryItem : NormalInventoryItem
    {
        // Inventory Position in Blight Stash is always invalid.
        public override int InventPosX => 0;
        public override int InventPosY => 0;

        public override RectangleF GetClientRect()
        {
            return Parent.GetClientRect();
        }
    }
}