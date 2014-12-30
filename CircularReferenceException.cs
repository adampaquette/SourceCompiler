using System;

namespace SourceCompiler
{
    public sealed class CircularReferenceException : Exception
    {
        public CircularReferenceException() : base() { }
        public CircularReferenceException(string message) : base(message) { }
        public CircularReferenceException(string message, Exception innerExeption) : base(message, innerExeption) { }
    }
}
