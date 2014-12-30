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
        private static Engine _engine;
        private static string _outputLog;
        private static string _errorLog;
        private static string _buildLog;
        private Verbose _verbose;
        private int _nbSuccessBuilds;
        private int _nbFailedBuilds;

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
            var prog = new Program();
            prog.MainCommandLine(args);
        }

        public void MainCommandLine(string[] args)
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

            _engine = new Engine();
            _nbSuccessBuilds = 0;
            _nbFailedBuilds = 0;
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
                _verbose = (Verbose)defaultVerbose;

                AnalyseInputs(inputs, cacheFile);
            }

            //Build mode
            else if (mode == "-b")
            {
                string buildPath = null;

                //if not an arg
                if (nbArgs >= 3 && !args[2].StartsWith("-"))
                    buildPath = args[2];

                if (nbArgs == 4)
                    Int32.TryParse(args[3], out defaultVerbose);
                _verbose = (Verbose)defaultVerbose;

                Build(cacheFile, buildPath);
            }

            _engine.UnregisterAllLoggers();
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

        private void StatusChanged(object sender, StatusChangedEventArgs e)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.BackgroundColor = ConsoleColor.DarkBlue;

            WriteLineOutput(e.PartialNameAssembly);
            WriteLineOutput(String.Format("{0} of {1} projects.", e.CurrentIndex.ToString(), e.MaxIndex.ToString()));
            WriteLineOutput(e.Status.ToString());

            if (e.Status == Status.BuildSucced)
                _nbSuccessBuilds++;
            else if (e.Status == Status.BuildFailed)
                _nbFailedBuilds++;

            Console.ResetColor();
        }

        private void AnalyseInputs(string[] inputs, string cacheFile)
        {
            var sources = new SourceAnalyser(_engine);
            sources.AnalyseOnlyProject = true;

            if ((_verbose & Verbose.Console) == Verbose.Console)
            {
                Console.WriteLine("Processing : \r\n");
                sources.StatusChanged += new StatusChangedEventHandler(StatusChanged);
            }

            if ((_verbose & Verbose.File) == Verbose.File)
            {
                sources.Error += (sender, e) =>
                {
                    using (var sw = File.AppendText(_errorLog))
                        sw.WriteLine(e.GetException().ToString());
                };
            }

            sources.AnalyseInputs(inputs);
            sources.ApplyBuildsPriority();
            sources.SaveCache(cacheFile);

            WriteLineOutput();
            WriteLineOutput("Reference Depth : ");
            WriteLineOutput(sources.AllAssenblies.GetReferenceDepth());
        }

        private void Build(string cacheFile, string buildPath)
        {
            var builder = CreateBuilder(cacheFile, buildPath);
            builder.BuildAllProject();

            WriteLineOutput(String.Format("========== Build: {0} succeeded , {1} failed ==========",
                                          _nbSuccessBuilds.ToString(), _nbFailedBuilds.ToString()));
        }

        private SourceBuilder CreateBuilder(string cacheFile, string buildPath)
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
            builder.StatusChanged += new StatusChangedEventHandler(StatusChanged);
            builder.LoadCache(cacheFile);

            return builder;
        }

        private void WriteLineOutput()
        {
            WriteLineOutput(null);
        }

        private void WriteLineOutput(string msg)
        {
            if ((_verbose & Verbose.Console) == Verbose.Console)
                using(var sw = new StreamWriter(Console.OpenStandardOutput()))
                    sw.WriteLine(msg);
            
            if ((_verbose & Verbose.File) == Verbose.File)
                using(var sw = File.AppendText(_errorLog))
                    sw.WriteLine(msg);
        }
    }
}

