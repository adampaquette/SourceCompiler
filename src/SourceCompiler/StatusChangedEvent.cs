using System;

namespace SourceCompiler
{
    public delegate void StatusChangedEventHandler(object sender, StatusChangedEventArgs e);
    public sealed class StatusChangedEventArgs : EventArgs
    {
        readonly Status _status;
        readonly string _partialNameAssembly;
        readonly int _currentIndex;
        readonly int _maxIndex;

        public Status Status { get { return _status; } }
        public string PartialNameAssembly { get { return _partialNameAssembly; } }
        public int CurrentIndex { get { return _currentIndex; } }
        public int MaxIndex { get { return _maxIndex; } }

        public StatusChangedEventArgs(Status status, string partialNameAssembly, int currentIndex, int maxIndex)
        {
            _status = status;
            _partialNameAssembly = partialNameAssembly;
            _currentIndex = currentIndex;
            _maxIndex = maxIndex;
        }
    }

    public enum Status
    {
        AnalysingProject,
        ProcessingBuildPriority,
        BuildSucced,
        BuildFailed
    }
}
