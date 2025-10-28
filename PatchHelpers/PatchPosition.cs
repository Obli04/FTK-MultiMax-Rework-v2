using System;

namespace FTK_MultiMax_Rework.PatchHelpers;

public class PatchPosition : Attribute
{
    public PatchPosition(PatchPositions position)
    {
        this.position = position;
    }

    public PatchPositions position;
}