using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace SourceCompiler
{
    [Serializable]
    public sealed class SourceProject : IEquatable<SourceProject>
    {
        public string ProjectPath { get; set; }
        public string PartialName { get; set; }
        public int BuildPriority { get; set; }
        public HashSet<SourceProject> ReferencedAssemblies { get; set; }

        public SourceProject()
        {
            BuildPriority = (int)PriorityCode.NotAnalysed;
            ReferencedAssemblies = new HashSet<SourceProject>();
        }

        public override int GetHashCode()
        {
            return PartialName.GetHashCode();
        }

        public override bool Equals(object obj)
        {
            if (!(obj is SourceProject)) return false;
            var proj = (SourceProject)obj;
            return PartialName.Equals(proj.PartialName, StringComparison.Ordinal);
        }

        public bool Equals(SourceProject proj)
        {
            return PartialName.Equals(proj.PartialName, StringComparison.Ordinal);
        }

        public static bool operator ==(SourceProject a, SourceProject b)
        {
            if (Object.ReferenceEquals(a, b)) return true;
            if ((object)a == null || (object)b == null) return false;
            return a.PartialName.Equals(b.PartialName);
        }

        public static bool operator !=(SourceProject a, SourceProject b)
        {
            return !(a == b);
        }
    }

    public enum PriorityCode
    {
        NotAnalysed = -1,
        Analysing = -2,
        CircularReference = Int32.MaxValue,
        CircularReferenceCollateral = Int32.MaxValue -1
    }
}
