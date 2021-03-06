#addin "Cake.SqlTools"

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////
var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

//////////////////////////////////////////////////////////////////////
// PREPARATION
//////////////////////////////////////////////////////////////////////
var msSqlServerConnectionString = RemoveQuotes(GetVariable("NSTORE_MSSQL_INSTANCE")) ?? "Server=localhost,1433;User Id=sa;Password=NStoreD0ck3r";
var msSqlDatabaseConnectionString  = msSqlServerConnectionString +";Database=NStore";
var testOutput = GetVariable("testoutput");

private string GetVariable(string key)
{
    var variable = Argument<string>(key, "___");
    if(variable == "___")
    {
        variable = EnvironmentVariable(key);
    }
    Information("Variable "+key+" is <" + (variable == null ? "null" : variable) + ">");
    return variable;
}

private string RemoveQuotes(string cstring)
{
    if(cstring == null)
        return null;
 
    if (cstring.StartsWith("\""))
        cstring = cstring.Substring(1);

    if (cstring.EndsWith("\""))
        cstring = cstring.Substring(0, cstring.Length - 1);

    return cstring;
}

private void RunTest(string testProject, IDictionary<string,string> env = null)
{
    var projectDir = "./src/"+ testProject + "/";
    var to = GetVariable("testoutput");
    var output = to == null ? "" :  "-xml " + to + "/" + testProject + ".xml";

/*
    var settings = new ProcessSettings
    {
//        Arguments = "xunit -parallel none",
        Arguments = "xunit " + output,
        WorkingDirectory = projectDir,
        EnvironmentVariables = env
    };
//    var result = StartProcess("dotnet", settings);
*/
    var settings = new DotNetCoreToolSettings {
        WorkingDirectory = projectDir,
        EnvironmentVariables = env
    };

    DotNetCoreTool(projectDir +"/"+ testProject, "xunit", output, settings);
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

    if( testOutput != null )
    {
        CleanDirectory(testOutput);
        EnsureDirectoryExists(testOutput);
    }
});

Task("restore-packages")
    .IsDependentOn("Clean")
    .Does(() =>
{
    DotNetCoreRestore(solution);
   // NuGetRestore(solution);
});


Task("TestMsSql")
    .ContinueOnError()
    .IsDependentOn("TestLibrary")
    .Does(()=>
{
    var dropdb = @"USE master
    IF EXISTS(select * from sys.databases where name='NStore')
    DROP DATABASE NStore
    ";

    var createdb = @"USE master 
    CREATE DATABASE NStore";

    var settings =  new SqlQuerySettings()
    {
        Provider = "MsSql",
        ConnectionString = msSqlServerConnectionString
    };

    Information("Connected to sql server instance " + msSqlServerConnectionString);

    ExecuteSqlQuery(dropdb, settings);
    ExecuteSqlQuery(createdb, settings);

    var env = new Dictionary<string, string>{
        { "NSTORE_MSSQL", msSqlDatabaseConnectionString},
    };
    
    RunTest("NStore.Persistence.MsSql.Tests",env);

    ExecuteSqlQuery(dropdb, settings);
});

Task("TestMongoDb")
    .ContinueOnError()
    .IsDependentOn("TestLibrary")
    .Does(() =>
{
    var env = new Dictionary<string, string>{
        { "NSTORE_MONGODB", "mongodb://localhost/nstore-tests"},
    };

    RunTest("NStore.Persistence.Mongo.Tests",env);
});

Task("TestInMemory")
    .IsDependentOn("TestLibrary")
    .ContinueOnError()
    .Does(() =>
{
    var env = new Dictionary<string, string>{};
    RunTest("NStore.Persistence.Tests",env);
});


Task("TestSample")
    .ContinueOnError()
    .IsDependentOn("TestLibrary")
    .Does(() =>
{
    var env = new Dictionary<string, string>{
        { "xx", "val"},
    };

    RunTest("NStore.Sample.Tests",env);
});

Task("TestLibrary")
    .ContinueOnError()
    .IsDependentOn("restore-packages")
    .Does(() =>
{
    var env = new Dictionary<string, string>{};

    RunTest("NStore.Tests",env);
});

Task("TestAll")
    .IsDependentOn("TestInMemory")
    .IsDependentOn("TestMongoDb")
    .IsDependentOn("TestMsSql")
    .IsDependentOn("TestSample")
    .Does(() =>
{
});

Task("ReleaseBuild")
    .IsDependentOn("TestAll")
    .Does(() =>
{
    Information("Building configuration "+configuration);
    var settings = new DotNetCoreBuildSettings
	{
		Configuration = configuration
	};

	DotNetCoreBuild(solution, settings);
});


//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Default")
    .IsDependentOn("TestAll");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
