using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.BuildEngine;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace SourceCompiler
{
    public sealed class SourceAnalyser
    {
        public Engine Engine { get; set; }
        public List<SourceProject> AllAssenblies { get { return _allAssemblies.ToList(); } }
        public bool AnalyseOnlyProject { get; set; }
        public event StatusChangedEventHandler StatusChanged;
        public event ErrorEventHandler Error;

        private HashSet<SourceProject> _allAssemblies = new HashSet<SourceProject>();

        public SourceAnalyser(Engine engine)
        {
            Engine = engine;
            AnalyseOnlyProject = true;
        }

        #region Public methods

        public void AnalyseInputs(string[] inputs)
        {
            foreach (var input in inputs)
                AppendInput(input);

            //Processing
            var projs = new List<SourceProject>(_allAssemblies);
            int i = 1;
            int nbProjs = projs.Count;

            foreach (var project in projs)
            {
                if (StatusChanged != null)
                    StatusChanged(this, new StatusChangedEventArgs(Status.AnalysingProject, project.PartialName, i++, nbProjs));
                AnalyseProject(project);
            }
        }

        public void ApplyBuildsPriority()
        {
            int i = 1;
            int nbProjs = _allAssemblies.Count;

            foreach (var project in _allAssemblies)
            {
                if (StatusChanged != null)
                    StatusChanged(this, new StatusChangedEventArgs(Status.ProcessingBuildPriority, project.PartialName, i++, nbProjs));
                SetBuildPriority(project);
            }
        }

        public void SaveCache(string path)
        {
            var formatter = new BinaryFormatter();
            using (var stream = File.Create(path))
            {
                formatter.Serialize(stream, _allAssemblies);
            }
        }

        public void LoadCache(string path)
        {
            var formatter = new BinaryFormatter();
            using (var stream = File.OpenRead(path))
            {
                _allAssemblies = (HashSet<SourceProject>)formatter.Deserialize(stream);
            }
        }

        #endregion

        #region Private methods

        private void AppendInput(string path)
        {
            if (File.Exists(path))
            {
                if (Path.GetExtension(path) == ".sln")
                    AppendSolution(path);
                else
                    AppendProject(path, false);
            }
            else if (Directory.Exists(path))
            {
                AppendProjects(Directory.GetFiles(path, "*.vbproj", SearchOption.AllDirectories));
                AppendProjects(Directory.GetFiles(path, "*.csproj", SearchOption.AllDirectories));
            }
            else
            {
                if (Path.HasExtension(path))
                    throw new FileNotFoundException(path);
                else
                    throw new DirectoryNotFoundException(path);
            }
        }

        private void AppendSolution(string solutionFile)
        {
            var basePath = Path.GetDirectoryName(solutionFile);
            var sln = new Onion.SolutionParser.Parser.SolutionParser(solutionFile).Parse();

            foreach (var project in sln.Projects)
                AppendProject(Path.Combine(basePath, project.Path), false);
        }

        private void AppendProjects(string[] projectsFile)
        {
            foreach (var file in projectsFile)
                AppendProject(file, false);
        }

        private SourceProject AppendProject(string projectFile, bool analyse)
        {
            var project = new Project(Engine);
            SourceProject asmProj = null;

            try
            {
                project.Load(projectFile);
                string partialName = GetPartialNameAssembly(project);
                asmProj = _allAssemblies.Where(a => a.PartialName == partialName).FirstOrDefault();

                //Projet non existant
                if (asmProj == null)
                {
                    asmProj = new SourceProject();
                    asmProj.PartialName = partialName;
                    asmProj.ProjectPath = projectFile;

                    _allAssemblies.Add(asmProj);
                    if (analyse)
                        AnalyseProject(asmProj);
                }
            }
            catch (Exception ex)
            {
                if (Error != null)
                    Error(this, new ErrorEventArgs(ex));
            }
            return asmProj;
        }

        private SourceProject AnalyseProject(string fullName)
        {
            string partialName = GetPartialNameAssembly(fullName);
            SourceProject asmProj = _allAssemblies.Where(a => a.PartialName == partialName).FirstOrDefault();

            //Il existe un projet pour cette DLL
            if (asmProj != null)
            {
                if (!String.IsNullOrEmpty(asmProj.ProjectPath) && asmProj.BuildPriority == (int)PriorityCode.NotAnalysed)
                    AnalyseProject(asmProj);
            }
            else
            {
                if (!AnalyseOnlyProject)
                {
                    //TODO : Décompiler la DLL au besoin
                    asmProj = new SourceProject();
                    asmProj.PartialName = partialName;
                    _allAssemblies.Add(asmProj);
                }
            }
            return asmProj;
        }

        private void AnalyseProject(SourceProject proj)
        {
            var project = new Project(Engine);
            project.Load(proj.ProjectPath);

            proj.BuildPriority = (int)PriorityCode.Analysing;

            try
            {
                foreach (BuildItem item in project.EvaluatedItems)
                {
                    SourceProject subProj = null;

                    switch (item.Name)
                    {
                        case "Reference":
                            subProj = AnalyseProject(item.FinalItemSpec);
                            break;
                        case "ProjectReference":
                            var path = Path.Combine(Path.GetDirectoryName(proj.ProjectPath), item.FinalItemSpec);
                            subProj = AppendProject(path, true);
                            break;
                    }
                    if (subProj != null && !proj.ReferencedAssemblies.Contains(subProj))
                        proj.ReferencedAssemblies.Add(subProj);
                }
            }
            catch (Exception ex)
            {
                if (Error != null)
                    Error(this, new ErrorEventArgs(ex));
            }
        }

        private string GetPartialNameAssembly(Project proj)
        {
            string assemblyName, version;
            assemblyName = version = "";

            foreach (BuildProperty item in proj.EvaluatedProperties)
            {
                switch (item.Name)
                {
                    case "AssemblyName":
                        assemblyName = item.Value;
                        break;
                    case "Version":
                        version = item.Value;
                        break;
                }
            }

            var sb = new StringBuilder(assemblyName);
            if (!String.IsNullOrEmpty(version))
                sb.Append(", Version=").Append(version);
            return sb.ToString();
        }

        private string GetPartialNameAssembly(string fullName)
        {
            if (fullName == null)
                throw new ArgumentException("fullName");

            int pos1 = fullName.IndexOf(',', 0);
            //Pas de version spécifique ex: MySql.Data
            if (pos1 == -1)
                return fullName;
            int pos2 = fullName.IndexOf(',', pos1 + 1);
            //Version spécifique ex: MySql.Data, Version=6.8.3.0
            if (pos2 == -1)
                return fullName;
            else //FullName ex: MySql.Data, Version=6.8.3.0, Culture=neutral, PublicKeyToken=c5687fc88969c44d, processorArchitecture=MSIL
                return fullName.Substring(0, pos2);
        }

        private void SetBuildPriority(SourceProject proj)
        {
            var stack = new Stack<SourceProject>();
            stack.Push(proj);
            GetBuildPriority(stack);
        }

        private Int32 GetBuildPriority(Stack<SourceProject> stack)
        {
            if (stack == null)
                return 0;

            var proj = stack.Peek();

            if (proj == null)
                return 0;
            if (proj.BuildPriority != (int)PriorityCode.NotAnalysed &&
                proj.BuildPriority != (int)PriorityCode.Analysing)
                return proj.BuildPriority;

            var currentLevel = stack.Count - 1;
            int maxLevel = 0;

            foreach (var asm in proj.ReferencedAssemblies)
            {
                //Assembly déjà analysé et en erreur. 
                if (asm.BuildPriority == (int)PriorityCode.CircularReference ||
                    asm.BuildPriority == (int)PriorityCode.CircularReferenceCollateral)
                {
                    //On s'assure de ne pas écraser la priorité du projet qui est la 
                    //source de la référence circulaire lors du dépilage de la stack. 
                    //On met l'assembly courant comme étant un dommage collatéral du 
                    //vrai problème.
                    if (proj.BuildPriority != (int)PriorityCode.CircularReference)
                        proj.BuildPriority = (int)PriorityCode.CircularReferenceCollateral;
                    return proj.BuildPriority;
                }

                //Déjà analisé
                else if (asm.BuildPriority > (int)PriorityCode.NotAnalysed)
                {
                    //Comme c'est un sous assembly, on incrémente pour la priorité courante.
                    if (maxLevel < asm.BuildPriority + 1)
                        maxLevel = asm.BuildPriority + 1;
                }

                //Erreur : référence circulaire
                else if (stack.Contains(asm))
                {
                    //Les deux derniers assemblies sont la cause du problème
                    maxLevel = (int)PriorityCode.CircularReference;
                    asm.BuildPriority = maxLevel;

                    if (Error != null)
                    {
                        stack.Push(asm);
                        string error = String.Join(" ->" + Environment.NewLine, stack.Reverse().Select(p => p.PartialName).ToArray());
                        stack.Pop();
                        Error(this, new ErrorEventArgs(new CircularReferenceException(error)));
                    }

                    break;
                }

                //On poursuit
                else
                {
                    stack.Push(asm);
                    int bp = GetBuildPriority(stack);
                    if (maxLevel < bp)
                        maxLevel = bp;
                    stack.Pop();
                }
            }

            //Si l'assembly est la source du problème on ne veux pas, qu'en dépilant jusqu'à 
            //la racine, écraser l'erreur CircularReference pour CircularReferenceCollateral.
            if (proj.BuildPriority != (int)PriorityCode.CircularReference)
            {
                //Le projet courant ne sera plus traité
                proj.BuildPriority = maxLevel;
            }

            //Si nous avons trouvé une erreur de référence circulaire.
            if (maxLevel == (int)PriorityCode.CircularReference)
                maxLevel = (int)PriorityCode.CircularReferenceCollateral;

            //Si nous somme dans un projet CircularReferenceCollateral, toute la chaîne doit être 
            //assignée à cette valeur donc on ne change rien. Sinon on récupère la vrai priorité.
            else if (maxLevel != (int)PriorityCode.CircularReferenceCollateral)
            {
                //Si on est au fond de la pile, c'est la vrai valeur car on empile depuis l'appelant.
                if (maxLevel < currentLevel)
                    maxLevel = currentLevel;
                //Sinon, on a récupéré la valeur d'un sous assembly donc on incrémente pour l'appelant
                else
                    maxLevel++;
            }

            return maxLevel;
        }

        #endregion

    }
}
