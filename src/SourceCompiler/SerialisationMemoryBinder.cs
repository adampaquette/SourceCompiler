using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.Runtime.Serialization;

namespace SourceCompiler
{
    //Used when loaded through a Visual Studio Addin.
    //http://stackoverflow.com/questions/5794686/serializationbinder-with-listt
    //https://social.msdn.microsoft.com/Forums/vstudio/en-US/e5f0c371-b900-41d8-9a5b-1052739f2521/deserialize-unable-to-find-an-assembly-?forum=netfxbcl
    class SerialisationMemoryBinder : SerializationBinder
    {
        public override Type BindToType(string assemblyName, string typeName) 
        {
            return Type.GetType(String.Format("{0}, {1}", typeName, assemblyName));
        }
    }
}
