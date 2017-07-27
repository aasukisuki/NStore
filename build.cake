//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////

// Define directories.
var artifactsDir    = Directory("./artifacts");
var solution        = "./src/NStore.sln";

//////////////////////////////////////////////////////////////////////
// TASKS
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    CleanDirectory(artifactsDir);
});

Task("restore-packages")
    .IsDependentOn("Clean")
    .Does(() =>
{
    DotNetCoreRestore(solution);
    NuGetRestore(solution);
});

Task("Build")
    .IsDependentOn("restore-packages")
    .Does(() =>
{
    var settings = new DotNetCoreBuildSettings
	{
		Configuration = configuration
	};

	DotNetCoreBuild(solution, settings);
});

Task("CreateMsSqlDatabase")
    .Does(()=>
{

});


Task("RunTests")
    .IsDependentOn("Build")
    .Does(() =>
{
    var testProjects = new string[] {
        "NStore.Tests", 
        "NStore.Persistence.Tests",  
        "NStore.Persistence.Mongo.Tests",
        "NStore.Persistence.MsSql.Tests",
        "NStore.Sample.Tests" 
    };

    foreach(var testProject in testProjects)
	{
        var projectDir = "./src/"+ testProject + "/";
        var settings = new ProcessSettings
        {
            Arguments = "xunit",
            WorkingDirectory = projectDir
        };
        StartProcess("dotnet", settings);
    }
});

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("RunTests");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
