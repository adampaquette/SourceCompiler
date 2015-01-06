SourceCompiler
==============

A smart API to compile multiple .NET projects for extremely large solutions.
It analyse the references and order the projects by build priority. After, they can be built.

It handle circular references and can show somes reports : 
```
Reference Depth : 
ProjectA, Version=1.0.0.0 - Priority : 1
ProjectB, Version=1.0.0.0 - Priority : 2
ProjectC, Version=1.0.0.0 - Priority : 3
```

### C# exemples
___

##### Build sources in a two step process for performance

```C#
var engine = new Microsoft.Build.BuildEngine.Engine();
engine.GlobalProperties.SetProperty("Configuration", "Debug");
engine.GlobalProperties.SetProperty("Platform", "AnyCPU");

//Step 1 : Analyse build priorities (Required only the first time when using cache)
var sources = new SourceCompiler.SourceAnalyser(engine);
sources.AnalyseInputs(new string[]{"C:\solution.sln","C:\folderWithProjects", "C:\project.vbproj"});
sources.ApplyBuildsPriority();
sources.SaveCache(cacheFile);

//Step 2 : Build
var builder = new SourceBuilder(engine);
builder.LoadCache(cacheFile);
builder.BuildAllProject();
```

##### Build sources in a one step process (without cache)

```C#
var engine = new Microsoft.Build.BuildEngine.Engine();
engine.GlobalProperties.SetProperty("Configuration", "Debug");
engine.GlobalProperties.SetProperty("Platform", "AnyCPU");

//Step 1 : Analyse build priorities
var sources = new SourceCompiler.SourceAnalyser(engine);
sources.AnalyseInputs(new string[]{"C:\solution.sln","C:\folderWithProjects", "C:\project.vbproj"});
sources.ApplyBuildsPriority();

//Step 2 : Build
var builder = new SourceBuilder(engine);
builder.AllAssenblies = sources.AllAssenblies;
builder.BuildAllProject();
```

### Command line exemples 
___

##### Build opened solution with a batch file as external tool within Visual Studio

```batch
@echo off
REM Param 1 : $(SolutionDir) 
REM Param 2 : $(SolutionDir)$(SolutionFileName)
echo Build started
%~dp0\SourceCompiler.exe %1\sln.sc -a %2 -v 0
%~dp0\SourceCompiler.exe %1\sln.sc -b --StopBuildingOnFailure
```

##### Build all projects from a folder

```batch
@echo off
echo Build started
SourceCompiler.exe C:\Sources\Projects\cache.sc -a C:\Sources\Projects -v 3
SourceCompiler.exe C:\Sources\Projects\cache.sc -b
echo Build completed
pause
```
