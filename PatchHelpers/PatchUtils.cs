#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using static FTK_MultiMax_Rework_v2.Main;
using AccessTools = HarmonyLib.AccessTools;
using HarmonyMethod = HarmonyLib.HarmonyMethod;
using static FTK_MultiMax_Rework_v2.PatchHelpers.PatchPositions;

namespace FTK_MultiMax_Rework_v2.PatchHelpers
{
    public static class PatchUtils
    {
        private static void PatchMethod(Type target, PatchData data) {
            
            MethodInfo original = AccessTools.Method(target, data.patchedMethodName, data.parameters);
            HarmonyMethod method = new HarmonyMethod(data.patchMethod);
            Harmony.Patch(original,
                data.position == Prefix ? method : null,
                data.position == Postfix ? method : null,
                data.position == Transpiler ? method : null,
                data.position == Finalizer ? method : null,
                data.position == ILManipulator ? method : null
                );
        }

        private static bool TryGetAttribute<AttrType>(this Type type, out AttrType attribute) where AttrType : Attribute
        {
            AttrType[] attributes = (AttrType[]) type.GetCustomAttributes(typeof(AttrType), false);

            if (attributes.Length > 0)
            {
                attribute = attributes.First();
                return true;
            }

            attribute = null;
            return false;
        }
        
        private static AttrType? GetAttribute<AttrType>(this Type type) where AttrType : Attribute
        {
            return type.TryGetAttribute(out AttrType attribute) ? attribute : null;
        }
        
        private static bool TryGetAttribute<AttrType>(this MethodInfo method, out AttrType attribute) where AttrType : Attribute
        {
            AttrType[] attributes = (AttrType[]) method.GetCustomAttributes(typeof(AttrType), false);

            if (attributes.Length > 0)
            {
                attribute = attributes.First();
                return true;
            }

            attribute = null;
            return false;
        }
        
        private static AttrType? GetAttribute<AttrType>(this MethodInfo method) where AttrType : Attribute
        {
            return method.TryGetAttribute(out AttrType attribute) ? attribute : null;
        }

        public static Type[] GetTypesWithAttribute<AttrType>(this Assembly assembly) where AttrType : Attribute
        {
            return assembly.GetTypes().Where((type) => type.TryGetAttribute(out AttrType attribute)).ToArray();
        }

        private static List<PatchData> GetPatchMethods(this Type type)
        {
            MethodInfo[] allMethods = type.GetMethods(BindingFlags.Public | BindingFlags.Static);

            List<PatchData> result = new();

            foreach (var method in allMethods)
            {
                if (!method.TryGetAttribute(out PatchMethod patchMethod))
                    continue;

                PatchParams? patchParams = method.GetAttribute<PatchParams>();

                PatchPositions position = Prefix;
                if (method.TryGetAttribute(out PatchPosition patchPosition))
                    position = patchPosition.position;
                
                result.Add(new PatchData(patchMethod.methodName, position, method, patchParams?.parameters));
            }

            return result;
        }

        public static void PatchClass(Type type)
        {
            PatchType patchType = type.GetAttribute<PatchType>();
            Type patchedClass = patchType.type;
            Log($"Patching class {patchedClass.Name} with {type.Name}");
            
            foreach (PatchData patch in type.GetPatchMethods())
            {
                PatchMethod(patchedClass, patch);
                Log($"    Patched method {patch.patchedMethodName} with {patch.patchMethod.Name}");
            }
        }
    }

    struct PatchData
    {
        public PatchData(string patchedMethodName, PatchPositions position, MethodInfo patchMethod, Type[]? parameters)
        {
            this.patchedMethodName = patchedMethodName;
            this.patchMethod = patchMethod;
            this.parameters = parameters;
            this.position = position;
        }

        public string patchedMethodName;
        public MethodInfo patchMethod;
        public Type[]? parameters;
        public PatchPositions position;
    }
}