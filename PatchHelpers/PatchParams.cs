using System;

namespace FTK_MultiMax_Rework.PatchHelpers;

public class PatchParams : Attribute
{
    public Type[] parameters;

    public PatchParams(params Type[] parameters)
    {
        this.parameters = parameters;
    }
}