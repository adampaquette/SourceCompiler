using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Build.BuildEngine;
using System.Xml.Serialization;
using System.Runtime.Serialization;
using System.IO;

namespace SourceCompiler
{
    public class Program
    {
        private static Engine _engine = new Engine();
        private static string _outputLog;
        private static string _errorLog;
        private static string _buildLog;

        [Flags]
        private enum Verbose : int
        {
            Quiet,
            Console,
            File
        }

        [STAThread]
        static void Main(string[] args)
        {          
            if (!VerifiyParams(args))
            {
                PrintHelp();
                return;
            }

            int nbArgs = args.Length;
            string cacheFile = args[0].ToLower();
            string mode = args[1].ToLower();
            string filesPath = Path.GetDirectoryName(cacheFile);
            int defaultVerbose = (int)(Verbose.Console | Verbose.File);
            Verbose verbose;

            _outputLog = Path.Combine(filesPath, "output.txt");
            _errorLog = Path.Combine(filesPath, "errors.txt");
            _buildLog = Path.Combine(filesPath, "buildLog.txt");

            File.Delete(_outputLog);
            File.Delete(_errorLog);
            File.Delete(_buildLog);

            var fileLogger = new FileLogger();
            fileLogger.Parameters = _buildLog;
            _engine.RegisterLogger(fileLogger);

            var consoleLogger = new ConsoleLogger();
            _engine.RegisterLogger(consoleLogger);

            //Analyse mode
            if (mode == "-a")
            {
                string[] inputs = args[2].Split(';');

                if (nbArgs == 5)
                    Int32.TryParse(args[4], out defaultVerbose);
                verbose = (Verbose)defaultVerbose;

                AnalyseInputs(inputs, cacheFile, verbose);
            }

            //Build mode
            else if (mode == "-b")
            {
                string buildPath = null;
                
                //if not an arg
                if(nbArgs >= 3 &&  !args[2].StartsWith("-"))
                    buildPath = args[2];

                if (nbArgs == 4)
                    Int32.TryParse(args[3], out defaultVerbose);
                verbose = (Verbose)defaultVerbose;

                Build(cacheFile, buildPath, verbose);
            }
        }

        private static bool VerifiyParams(string[] args)
        {
            int nbArgs = args.Length;

            if (nbArgs < 2)
                return false;

            string mode = args[1].ToLower();

            //Analyse mode
            if (mode == "-a")
            {
                if (nbArgs < 3)
                    return false;
            }
            //Build mode
            else if (mode == "-b")
            {
                
            }
            else
                return false;
            return true;
        }

        private static void StatusChangedToConsole(object sender, StatusChangedEventArgs e)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.BackgroundColor = ConsoleColor.DarkBlue;

            Console.WriteLine(e.PartialNameAssembly);
            Console.WriteLine(String.Format("{0} of {1} projects.", e.CurrentIndex.ToString(), e.MaxIndex.ToString()));
            Console.WriteLine(e.Status.ToString());

            Console.ResetColor();
        }

        private static void PrintHelp()
        {
            string helpMsg =
            "SourceCompiler alpha 0.01" +
            "=========================" +
            "SourceCompiler cacheFile [-a [inputFolder|inputProject|inputSolution][;...n]] | [-b [buildPath]] [-v (value)]" +
            "    -a    Analyse mode" +
            "    -b    Build mode" +
            "    -v    Verbose flag" +
            "        0    Quiet" +
            "        1    Console" +
            "        2    File" +
            "Help :" +
            "1 - You first need to analyse projects to set the builds priorities and save the result in a file." +
            "2 - You can then build the assemblies from the cache file." +
            "Example :" +
            "SourceCompiler c:/cache.sc -a c:/Source -v 3" +
            "SourceCompiler C:/cache.sc -a C:/Source/Project.sln" +
            "SourceCompiler C:/cache.sc -b C:/Source/" +
            "SourceCompiler C:/cache.sc -b -v 1";            

            Console.WriteLine(helpMsg);
        }

        private static void AnalyseInputs(string[] inputs, string cacheFile, Verbose verbose)
        {
            var sources = new SourceAnalyser(_engine);
            sources.AnalyseOnlyProject = true;

            if ((verbose & Verbose.Console) == Verbose.Console)
            {
                Console.WriteLine("Processing : \r\n");
                sources.StatusChanged += new StatusChangedEventHandler(StatusChangedToConsole);
            }

            if ((verbose & Verbose.File) == Verbose.File)
            {
                sources.Error += (sender, e) =>
                {
                    using (var sw = File.AppendText(_errorLog))
                    {
                        sw.WriteLine(e.GetException().ToString());
                    }
                };
            }

            sources.AnalyseInputs(inputs);
            sources.ApplyBuildsPriority();
            sources.SaveCache(cacheFile);

            if ((verbose & Verbose.File) == Verbose.File)
            {
                using (var sw = File.AppendText(_outputLog))
                {
                    Console.WriteLine();
                    sw.WriteLine("Reference Depth : ");
                    sw.WriteLine(sources.AllAssenblies.GetReferenceDepth());
                    /*sw.WriteLine("Reference Tree view : ");
                    sw.WriteLine(sources.AllAssenblies.GetTreeView());*/
                }
            }

            if ((verbose & Verbose.Console) == Verbose.Console)
            {
                Console.WriteLine();
                Console.WriteLine("Reference Depth : ");
                Console.WriteLine(sources.AllAssenblies.GetReferenceDepth());              
            }
        }

        private static void Build(string cacheFile, string buildPath, Verbose verbose)
        {
            var builder = CreateBuilder(cacheFile, buildPath, verbose);          
            builder.BuildAllProject();
        }

        private static SourceBuilder CreateBuilder(string cacheFile, string buildPath, Verbose verbose)
        {
            _engine.GlobalProperties.SetProperty("Configuration", "Debug");
            _engine.GlobalProperties.SetProperty("Platform", "AnyCPU");
            
            if (!String.IsNullOrEmpty(buildPath))
            {
                _engine.GlobalProperties.SetProperty("OutDir", buildPath + "/");
                _engine.GlobalProperties.SetProperty("OutputPath", buildPath);
                
                if (!Directory.Exists(buildPath))
                    Directory.CreateDirectory(buildPath);
            }

            var builder = new SourceBuilder(_engine);
            builder.StatusChanged += new StatusChangedEventHandler(StatusChangedToConsole);
            builder.LoadCache(cacheFile);

            return builder;
        }
    }
}

