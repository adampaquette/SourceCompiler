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
            try
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

                int idxNextArg;

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
                    idxNextArg = 2;
                    string buildPath = null;
                    bool stopBuildingOnFailure = false;
                    string configuration = null;

                    //If first param is not an arg, it's buildPath
                    if (nbArgs > idxNextArg && !args[idxNextArg].StartsWith("-"))
                    {
                        buildPath = args[idxNextArg];
                        idxNextArg++;
                    }

                    while (nbArgs > idxNextArg)
                    {
                        switch (args[idxNextArg])
                        {
                            case "-Configuration":
                                if (nbArgs > idxNextArg + 1)
                                {
                                    configuration = args[idxNextArg + 1];
                                    idxNextArg++;
                                }
                                break;
                            case "--StopBuildingOnFailure":
                                stopBuildingOnFailure = true;
                                break;
                            case "-v":
                                if (nbArgs > idxNextArg + 1)
                                {
                                    Int32.TryParse(args[idxNextArg + 1], out defaultVerbose);
                                    idxNextArg++;
                                }
                                break;
                            default:
                                break;
                        }
                        idxNextArg++;
                    }

                    _verbose = (Verbose)defaultVerbose;

                    Build(cacheFile, buildPath, stopBuildingOnFailure, configuration);
                }

                _engine.UnregisterAllLoggers();
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private static void PrintHelp()
        {
            string helpMsg =
            "SourceCompiler alpha 0.02\r\n" +
            "=========================\r\n" +
            "SourceCompiler cacheFile (-a (inputFolder|inputProject|inputSolution)[;...n]) | (-b [buildPath] [--StopBuildingOnFailure] [-Configuration (Release|Debug)]) [-v (value)]\r\n" +
            "    -a    Analyse mode\r\n" +
            "    -b    Build mode\r\n" +
            "        -Configuration (Release|Debug)\r\n" +
            "        --StopBuildingOnFailure    Stop building on first error\r\n" +
            "    -v    Verbose flag\r\n" +
            "        0    Quiet\r\n" +
            "        1    Console\r\n" +
            "        2    File\r\n" +
            "Help :\r\n" +
            "1 - You first need to analyse projects to set the builds priorities and save the result in a file.\r\n" +
            "2 - You can then build the assemblies from the cache file.\r\n" +
            "Example :\r\n" +
            "SourceCompiler c:/cache.sc -a c:/Source -v 3\r\n" +
            "SourceCompiler C:/cache.sc -a C:/Source/Project.sln\r\n" +
            "SourceCompiler C:/cache.sc -b C:/Source/\r\n" +
            "SourceCompiler C:/cache.sc -b --StopBuildingOnFailure -v 1\r\n";

            Console.WriteLine(helpMsg);
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

            if (_verbose.HasFlag(Verbose.Console))
            {
                Console.WriteLine("Processing : \r\n");
                sources.StatusChanged += new StatusChangedEventHandler(StatusChanged);
            }

            if (_verbose.HasFlag(Verbose.File))
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

        private void Build(string cacheFile, string buildPath, bool stopBuildingOnFailure, string configuration)
        {
            ConfigureEngine(buildPath, configuration);

            var builder = new SourceBuilder(_engine);
            builder.StatusChanged += new StatusChangedEventHandler(StatusChanged);
            builder.LoadCache(cacheFile);
            builder.StopBuildingOnFailure = stopBuildingOnFailure;
            builder.BuildAllProject();

            var nbSkippedBuilds = builder.AllAssenblies.Count() - (_nbSuccessBuilds + _nbFailedBuilds);

            WriteLineOutput(String.Format("========== Build: {0} succeeded, {1} failed, {2} skipped ==========",
                                          _nbSuccessBuilds.ToString(), _nbFailedBuilds.ToString(),
                                          nbSkippedBuilds.ToString()));
        }

        private void ConfigureEngine(string buildPath, string configuration)
        {
            _engine.GlobalProperties.SetProperty("Configuration", configuration);
            _engine.GlobalProperties.SetProperty("Platform", "AnyCPU");

            if (!String.IsNullOrEmpty(buildPath))
            {
                _engine.GlobalProperties.SetProperty("OutDir", buildPath + "/");
                _engine.GlobalProperties.SetProperty("OutputPath", buildPath);

                if (!Directory.Exists(buildPath))
                    Directory.CreateDirectory(buildPath);
            }
        }

        private void WriteLineOutput()
        {
            WriteLineOutput(null);
        }

        private void WriteLineOutput(string msg)
        {
            if (_verbose.HasFlag(Verbose.Console))
                using (var sw = new StreamWriter(Console.OpenStandardOutput()))
                    sw.WriteLine(msg);

            if (_verbose.HasFlag(Verbose.File))
                using (var sw = File.AppendText(_outputLog))
                    sw.WriteLine(msg);
        }
    }
}

