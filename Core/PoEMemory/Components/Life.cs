using System;
using System.Collections.Generic;
using ExileCore.Shared.Cache;
using ExileCore.Shared.Helpers;
using GameOffsets;
using JM.LinqFaster;

namespace ExileCore.PoEMemory.Components
{
    public class Life : Component
    {
        private readonly CachedValue<LifeComponentOffsets> _life;

        public Life()
        {
            _life = new FrameCache<LifeComponentOffsets>(() => Address == 0 ? default : M.Read<LifeComponentOffsets>(Address));
        }

        public new long OwnerAddress => LifeComponentOffsetsStruct.Owner;
        private LifeComponentOffsets LifeComponentOffsetsStruct => _life.Value;
        public int MaxHP => Address != 0 ? LifeComponentOffsetsStruct.MaxHP : 1;
        public int CurHP => Address != 0 ? LifeComponentOffsetsStruct.CurHP : 0;
        public double ReservedPercentHP => LifeComponentOffsetsStruct.ReservedPercentHP / 100d;
        public int ReservedFlatHP => Address != 0 ? LifeComponentOffsetsStruct.ReservedFlatHP : 0;
        public int TotalReservedHP => Address == 0 ? 0 :
            (int)(LifeComponentOffsetsStruct.ReservedFlatHP + (LifeComponentOffsetsStruct.MaxHP * (LifeComponentOffsetsStruct.ReservedPercentHP / 10000d)));
        public int MaxMana => Address != 0 ? LifeComponentOffsetsStruct.MaxMana : 1;
        public int CurMana => Address != 0 ? LifeComponentOffsetsStruct.CurMana : 1;
        public double ReservedPercentMana => Address != 0 ? LifeComponentOffsetsStruct.ReservedPercentMana / 100d : 0d;
        public int ReservedFlatMana => (int)Math.Round(MaxMana * ReservedPercentMana / 100);
        public int MaxES => LifeComponentOffsetsStruct.MaxES;
        public int CurES => LifeComponentOffsetsStruct.CurES;
        public float HPPercentage => CurHP / (float) (MaxHP - ReservedFlatHP);
        public float MPPercentage => CurMana / (float) (MaxMana - ReservedFlatMana);
        public float ESPercentage => MaxES == 0 ? 0 : CurES / (float) MaxES;

    }
}
