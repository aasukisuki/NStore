#addin "Cake.Docker"
#addin "Cake.SqlTools"
//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");
var msSqlConnectionString = EnvironmentVariable("NSTORE_MSSQL") ?? "Server=localhost,1433;User Id=sa;Password=NStoreD0ck3r";
//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////
private void RunTest(string testProject, IDictionary<string,string> env = null)
{
    var projectDir = "./src/"+ testProject + "/";
    var settings = new ProcessSettings
    {
        Arguments = "xunit",
        WorkingDirectory = projectDir,
        EnvironmentVariables = env
    };

    StartProcess("dotnet", settings);
}

// Define Settings.
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
   // NuGetRestore(solution);
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

Task("RunMsSqlTests")
    .IsDependentOn("Build")
    .Does(()=>
{
var sql = @"USE master
IF EXISTS(select * from sys.databases where name='NStore')
DROP DATABASE NStore

CREATE DATABASE NStore";

    ExecuteSqlQuery(sql, new SqlQuerySettings()
    {
        Provider = "MsSql",
        ConnectionString = msSqlConnectionString
    });
    
    var env = new Dictionary<string, string>{
        { "NSTORE_MSSQL", msSqlConnectionString },
    };
    
    RunTest("NStore.Persistence.MsSql.Tests",env);
});

Task("RunTests")
    .IsDependentOn("Build")
    .Does(() =>
{
    var env = new Dictionary<string, string>{
        { "NSTORE_MSSQL", msSqlConnectionString },
    };

    var testProjects = new string[] {
        "NStore.Tests", 
        "NStore.Persistence.Tests",  
        "NStore.Persistence.Mongo.Tests",
        "NStore.Persistence.MsSql.Tests",
        "NStore.Sample.Tests" 
    };

    foreach(var testProject in testProjects)
	{
        RunTest(testProject,env);
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
