using System;

namespace FTK_MultiMax_Rework_v2.PatchHelpers
{
    public class PatchType: Attribute
    {
        public Type type;

        public PatchType(Type type)
        {
            this.type = type;
        }
    }
}