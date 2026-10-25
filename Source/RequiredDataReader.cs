using System;
using System.Collections.Generic;
using System.Reflection;

namespace ItemIntelligence
{
    internal enum MemberReadStatus { Success, OwnerUnavailable, MissingMember, ReadFailed }

    internal struct MemberReadResult
    {
        internal readonly MemberReadStatus Status;
        internal readonly object Value;
        internal MemberReadResult(MemberReadStatus status, object value) { Status = status; Value = value; }
    }

    // Only required schema reads opt in. Optional probes remain quiet. Store schema
    // names and exception types, never live owners, values or exception instances.
    internal sealed class RequiredDataReader
    {
        private const int Limit = 32;
        private readonly HashSet<string> reported = new HashSet<string>(StringComparer.Ordinal);
        private readonly Func<Type, string, MemberInfo> lookup;
        private readonly Action<string> warn;
        private bool capped;
        internal int DistinctFailures { get { return reported.Count; } }
        internal bool LimitReached { get { return capped; } }

        internal RequiredDataReader(Func<Type, string, MemberInfo> lookup, Action<string> warn)
        {
            this.lookup = lookup; this.warn = warn;
        }

        internal void Reset() { reported.Clear(); capped = false; }

        internal MemberReadResult Read(object owner, string name)
        {
            if (owner == null) return new MemberReadResult(MemberReadStatus.OwnerUnavailable, null);
            Type type = owner.GetType();
            try
            {
                MemberInfo member = lookup(type, name);
                if (member == null)
                {
                    Report(type, name, MemberReadStatus.MissingMember, null);
                    return new MemberReadResult(MemberReadStatus.MissingMember, null);
                }
                FieldInfo field = member as FieldInfo;
                object value = field != null ? field.GetValue(owner) : ((PropertyInfo)member).GetValue(owner, null);
                return new MemberReadResult(MemberReadStatus.Success, value);
            }
            catch (Exception ex)
            {
                var invocation = ex as TargetInvocationException;
                Report(type, name, MemberReadStatus.ReadFailed,
                    invocation != null && invocation.InnerException != null ? invocation.InnerException.GetType() : ex.GetType());
                return new MemberReadResult(MemberReadStatus.ReadFailed, null);
            }
        }

        private void Report(Type owner, string member, MemberReadStatus status, Type error)
        {
            string key = owner.FullName + "." + member + ": " + status;
            if (reported.Contains(key)) return;
            if (reported.Count >= Limit)
            {
                if (!capped) warn("[ItemIntelligence] Required data diagnostics: further distinct failures suppressed for this session.");
                capped = true;
                return;
            }
            reported.Add(key);
            warn("[ItemIntelligence] Required data " + key + (error == null ? "." : " (" + error.Name + ")."));
        }
    }
}
