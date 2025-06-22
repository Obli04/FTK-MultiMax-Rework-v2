using System;

namespace FTK_MultiMax_Rework_v2.PatchHelpers
{
    public class PatchMethod: Attribute
    {
        public string methodName;

        public PatchMethod(string methodName)
        {
            this.methodName = methodName;
        }
    }
}