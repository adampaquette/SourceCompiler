using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.BuildEngine;
using System.Runtime.Serialization.Formatters.Binary;

namespace SourceCompiler
{
    public sealed class SourceBuilder
    {
        public Engine Engine { get; set; }
        public List<SourceProject> AllAssenblies { get { return _allAssemblies.ToList(); } }
        public event StatusChangedEventHandler StatusChanged;

        private HashSet<SourceProject> _allAssemblies = new HashSet<SourceProject>();

        public SourceBuilder(Engine engine)
        {
            Engine = engine;
        }

        #region Public methods

        public void LoadCache(string path)
        {
            var formatter = new BinaryFormatter();
            using (var stream = File.OpenRead(path))
            {
                _allAssemblies = (HashSet<SourceProject>)formatter.Deserialize(stream);
            }
        }

        public void BuildAllProject()
        {
            var sequenceGrp = AllAssenblies.Where(a => !String.IsNullOrEmpty(a.ProjectPath))
                                           .GroupBy(a => a.BuildPriority)
                                           .OrderBy(grp => grp.Key);
            int i = 1;
            int nbProjs = AllAssenblies.Count();           

            foreach (var group in sequenceGrp)
            {
                //Parallel.ForEach(group, (proj) =>
                foreach (var proj in group)
                {
                    Status status;
                    var project = new Project(Engine);
                    project.BuildEnabled = true;
                    project.Load(proj.ProjectPath);

                    status = project.Build() ? Status.BuildSucced : Status.BuildFailed;

                    if (StatusChanged != null)
                        StatusChanged(this, new StatusChangedEventArgs(status, proj.PartialName, i++, nbProjs));
                }
            }
        }

        #endregion

        #region Private methods

       
        
        #endregion
    }
}
