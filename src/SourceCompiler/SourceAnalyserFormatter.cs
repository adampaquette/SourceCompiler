using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace SourceCompiler
{
    public static class SourceAnalyserFormatter
    {
        public static string GetReferenceDepth(this IEnumerable<SourceProject> projs)
        {
            var sbOutput = new StringBuilder();
            var sequence = projs.OrderBy(p => p.BuildPriority).ThenBy(p => p.PartialName);

            foreach (var proj in sequence)
            {
                sbOutput.Append(String.Format("{0} - Priority : {1}\r\n", proj.PartialName, proj.BuildPriority.ToString()));
            }

            return sbOutput.ToString();
        }

        public static string GetTreeView(this IEnumerable<SourceProject> projs)
        {
            var sbOutput = new StringBuilder();
            var sequence = projs.OrderBy(p => p.BuildPriority).ThenBy(p=>p.PartialName);

            foreach (var proj in sequence)
            {
                GetProjectTreeView(proj, sbOutput, 0); 
            }

            return sbOutput.ToString();
        }

        private static void GetProjectTreeView(SourceProject proj, StringBuilder sb, int currentLevel)
        {
            sb.Append(new string(' ', currentLevel * 4)).Append(proj.PartialName).Append(Environment.NewLine);

            foreach (var subProj in proj.ReferencedAssemblies)
            {
                GetProjectTreeView(subProj, sb, currentLevel + 1);
            }
        }
    }
}
