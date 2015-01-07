using System;
using System.IO;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Text;
using System.Globalization;

namespace SourceCompiler
{
    // This logger will derive from the Microsoft.Build.Utilities.Logger class, 
    // which provides it with getters and setters for Verbosity and Parameters, 
    // and a default empty Shutdown() implementation. 
    public sealed class ConsoleLogger : Logger
    {
        private StreamWriter _streamWriter;
        private int _indent;

        /// <summary> 
        /// Initialize is guaranteed to be called by MSBuild at the start of the build 
        /// before any events are raised. 
        /// </summary> 
        public override void Initialize(IEventSource eventSource)
        {
            _streamWriter = new StreamWriter(Console.OpenStandardOutput(), Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage));
            _streamWriter.AutoFlush = true;

            // For brevity, we'll only register for certain event types. Loggers can also 
            // register to handle TargetStarted/Finished and other events.
            //eventSource.ProjectStarted += new ProjectStartedEventHandler(eventSource_ProjectStarted);
            eventSource.TaskStarted += new TaskStartedEventHandler(eventSource_TaskStarted);
            eventSource.MessageRaised += new BuildMessageEventHandler(eventSource_MessageRaised);
            eventSource.WarningRaised += new BuildWarningEventHandler(eventSource_WarningRaised);
            eventSource.ErrorRaised += new BuildErrorEventHandler(eventSource_ErrorRaised);
            //eventSource.ProjectFinished += new ProjectFinishedEventHandler(eventSource_ProjectFinished);
        }

        void eventSource_ErrorRaised(object sender, BuildErrorEventArgs e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.BackgroundColor = ConsoleColor.DarkRed;
            
            // BuildErrorEventArgs adds LineNumber, ColumnNumber, File, amongst other parameters 
            string line = String.Format(": ERROR {0}({1},{2}): ", e.File, e.LineNumber, e.ColumnNumber);
            WriteLineWithSenderAndMessage(line, e);

            Console.ResetColor();
        }

        void eventSource_WarningRaised(object sender, BuildWarningEventArgs e)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.BackgroundColor = ConsoleColor.DarkYellow;
            
            // BuildWarningEventArgs adds LineNumber, ColumnNumber, File, amongst other parameters 
            string line = String.Format(": Warning {0}({1},{2}): ", e.File, e.LineNumber, e.ColumnNumber);
            WriteLineWithSenderAndMessage(line, e);

            Console.ResetColor();
        }

        void eventSource_MessageRaised(object sender, BuildMessageEventArgs e)
        {
            // BuildMessageEventArgs adds Importance to BuildEventArgs 
            // Let's take account of the verbosity setting we've been passed in deciding whether to log the message 
            if ((e.Importance == MessageImportance.High && IsVerbosityAtLeast(LoggerVerbosity.Minimal))
                || (e.Importance == MessageImportance.Normal && IsVerbosityAtLeast(LoggerVerbosity.Normal))
                || (e.Importance == MessageImportance.Low && IsVerbosityAtLeast(LoggerVerbosity.Detailed))
                )
            {
                WriteLineWithSenderAndMessage(String.Empty, e);
            }
        }

        void eventSource_TaskStarted(object sender, TaskStartedEventArgs e)
        {
            // TaskStartedEventArgs adds ProjectFile, TaskFile, TaskName 
            // To keep this log clean, this logger will ignore these events.
        }

        //void eventSource_ProjectStarted(object sender, ProjectStartedEventArgs e)
        //{
        //    // ProjectStartedEventArgs adds ProjectFile, TargetNames 
        //    // Just the regular message string is good enough here, so just display that.
        //    WriteLine(String.Empty, e);
        //    _indent++;
        //}

        //void eventSource_ProjectFinished(object sender, ProjectFinishedEventArgs e)
        //{
        //    // The regular message string is good enough here too.
        //    _indent--;
        //    WriteLine(String.Empty, e);
        //}

        /// <summary> 
        /// Write a line to the log, adding the SenderName and Message 
        /// (these parameters are on all MSBuild event argument objects) 
        /// </summary> 
        private void WriteLineWithSenderAndMessage(string line, BuildEventArgs e)
        {
            if (0 == String.Compare(e.SenderName, "MSBuild", true /*ignore case*/))
            {
                // Well, if the sender name is MSBuild, let's leave it out for prettiness
                WriteLine(line, e);
            }
            else
            {
                WriteLine(e.SenderName + ": " + line, e);
            }
        }

        /// <summary> 
        /// Just write a line to the log 
        /// </summary> 
        private void WriteLine(string line, BuildEventArgs e)
        {
            for (int i = _indent; i > 0; i--)
            {
                _streamWriter.Write("\t");
            }
            _streamWriter.WriteLine(line + e.Message);
        }

        /// <summary> 
        /// Shutdown() is guaranteed to be called by MSBuild at the end of the build, after all  
        /// events have been raised. 
        /// </summary> 
        public override void Shutdown()
        {
            _streamWriter.Close();
        }
    }
}
