using UnityEngine;

namespace ItemIntelligence
{
    public static partial class ModMain
    {
        private static readonly RequiredDataReader RequiredData = new RequiredDataReader(
            (type, name) => FindCachedMember(type, name, false), message => Debug.LogWarning(message));

        private static object GetRequiredMember(object owner, string name)
        {
            return RequiredData.Read(owner, name).Value;
        }
    }
}
