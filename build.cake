#tool nuget:?package=NUnit.ConsoleRunner&version=3.8.0
#tool nuget:?package=NUnit.Extension.TeamCityEventListener&version=1.0.4

//////////////////////////////////////////////////////////////////////
// ARGUMENTS
//////////////////////////////////////////////////////////////////////

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Debug");
var msbuildPath = @"C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\MSBuild.exe";

// Special (optional) arguments for the script. You pass these
// through the Cake bootscrap script via the -ScriptArgs argument
// for example: 
//   ./build.ps1 -t RePackageNuget -ScriptArgs --nugetVersion="3.9.9"
//   ./build.ps1 -t RePackageNuget -ScriptArgs '--binaries="rel3.9.9" --nugetVersion="3.9.9"'
var nugetVersion = Argument("nugetVersion", (string)null);
var chocoVersion = Argument("chocoVersion", (string)null);
var binaries = Argument("binaries", (string)null);

//////////////////////////////////////////////////////////////////////
// SET PACKAGE VERSION
//////////////////////////////////////////////////////////////////////

var version = "1.0.10";
var modifier = "";

// Tuple(NUnit.Console version, NUnit version)
var versionsOfNunitCore = new [] {Tuple.Create("3.4.1", "3.4.1"), Tuple.Create("3.5", "3.5"), Tuple.Create("3.6", "3.6.1"), Tuple.Create("3.9", "3.8")};
// var versionsOfNunitCore = new [] {Tuple.Create("3.4.1", "3.4.1"), Tuple.Create("3.5", "3.5"), Tuple.Create("3.6", "3.6.1"), Tuple.Create("3.9", "3.8"), Tuple.Create("", "")};

var integrationTestsCategories = new List<string>();

var dbgSuffix = configuration == "Debug" ? "-dbg" : "";
var packageVersion = version + modifier + dbgSuffix;

if (BuildSystem.IsRunningOnAppVeyor)
{
    var tag = AppVeyor.Environment.Repository.Tag;

    if (tag.IsTag)
    {
        packageVersion = tag.Name;
    }
    else
    {
        var buildNumber = AppVeyor.Environment.Build.Number;
        packageVersion = version + "-CI-" + buildNumber + dbgSuffix;
        if (AppVeyor.Environment.PullRequest.IsPullRequest)
            packageVersion += "-PR-" + AppVeyor.Environment.PullRequest.Number;
        else
            packageVersion += "-" + AppVeyor.Environment.Repository.Branch;
    }

    AppVeyor.UpdateBuildVersion(packageVersion);
}

//////////////////////////////////////////////////////////////////////
// DEFINE RUN CONSTANTS
//////////////////////////////////////////////////////////////////////

// Directories
var TARGET_FRAMEWORK = "net20";
var PROJECT_DIR = Context.Environment.WorkingDirectory.FullPath + "/";
var PACKAGE_DIR = PROJECT_DIR + "package/";
var TOOLS_DIR = PROJECT_DIR + "tools/";
var BIN_CONFIG_DIR = PROJECT_DIR + "bin/" + configuration + "/";
var BIN_DIR = BIN_CONFIG_DIR + TARGET_FRAMEWORK + "/";
var BIN_SRC = BIN_CONFIG_DIR; // Source of binaries used in packaging
var TEST_NUNIT_DIR = PROJECT_DIR + "bin/nunit/";
var TEST_PACKAGES_DIR = PROJECT_DIR + "bin/packages/";
var TEST_TEAMCITY_EXT_DIR = TEST_NUNIT_DIR + "NUnit.Extension.TeamCityEventListener/tools/";

// Adjust BIN_SRC if --binaries option was given
if (binaries != null)
{
    BIN_SRC = binaries;
    if (!System.IO.Path.IsPathRooted(binaries))
    {
        BIN_SRC = PROJECT_DIR + binaries;
        if (!BIN_SRC.EndsWith("/"))
            BIN_SRC += "/";
    }
}

// Files
var SOLUTION_FILE = PROJECT_DIR + "teamcity-event-listener.sln";
var TEST_SOLUTION_FILE = PROJECT_DIR + "teamcity-event-listener-tests.sln";
var NUNIT3_CONSOLE = TOOLS_DIR + "NUnit.ConsoleRunner.3.8.0/tools/nunit3-console.exe";
var TEST_ASSEMBLY = BIN_DIR + "teamcity-event-listener.tests.dll";
var INTEGRATION_TEST_ASSEMBLY = BIN_CONFIG_DIR + "nunit.integration.tests.dll";

// MetaData used in the nuget and chocolatey packages
var GITHUB_SITE = "https://github.com/JetBrains/teamcity-event-listener";

var NUGET_ID = "NUnit.Extension.TeamCityEventListener";
var CHOCO_ID = "nunit-extension-teamcity-event-listener";

var TITLE = "TeamCity Event Listener Extension for NUnit";
var AUTHORS = new [] { "jetbrains", "teamcity", "Charlie Poole", "Nikolay Pianikov" };
var OWNERS = new [] { "jetbrains", "teamcity" };
var DESCRIPTION = "This extension sends specially formatted messages about test progress to TeamCity as each test executes, allowing TeamCity to monitor progress.";
var SUMMARY = "NUnit Team City Event Listener extension for TeamCity.";
var COPYRIGHT = "Copyright (c) 2017 - 2025 Charlie Poole, 2025 JetBrains";
var RELEASE_NOTES = new [] { "See https://raw.githubusercontent.com/JetBrains/teamcity-event-listener/master/CHANGES.txt" };
var TAGS = new [] { "nunit", "test", "testing", "tdd", "runner" };
var PROJECT_URL = new Uri("https://github.com/JetBrains/teamcity-event-listener");
var ICON_URL = new Uri("https://raw.githubusercontent.com/JetBrains/teamcity-event-listener/refs/heads/master/icon.png");
var LICENSE_URL = new Uri("https://github.com/JetBrains/teamcity-event-listener/raw/master/LICENSE.txt");
var PROJECT_SOURCE_URL = new Uri( GITHUB_SITE );
var PACKAGE_SOURCE_URL = new Uri( GITHUB_SITE );
var BUG_TRACKER_URL = new Uri(GITHUB_SITE + "/issues");

// Package sources for nuget restore
var PACKAGE_SOURCE = new string[]
    {
        "https://www.nuget.org/api/v2",
        "https://www.myget.org/F/nunit/api/v2"
    };

// Package sources for nuget restore
var PRERELEASE_PACKAGE_SOURCE = new string[]
    {
        "https://www.myget.org/F/nunit/api/v2",
        "https://www.nuget.org/api/v2",
    };

//////////////////////////////////////////////////////////////////////
// CLEAN
//////////////////////////////////////////////////////////////////////

Task("Clean")
    .Does(() =>
{
    CleanDirectory(BIN_DIR);
});


//////////////////////////////////////////////////////////////////////
// INITIALIZE FOR BUILD
//////////////////////////////////////////////////////////////////////

Task("NuGetRestore")
    .Does(() =>
    {
        NuGetRestore(SOLUTION_FILE, new NuGetRestoreSettings()
        {
            Source = PACKAGE_SOURCE
        });
    });

//////////////////////////////////////////////////////////////////////
// BUILD
//////////////////////////////////////////////////////////////////////

Task("Build")
    .IsDependentOn("NuGetRestore")
    .Does(() =>
    {
        if (binaries != null)
            throw new Exception("The --binaries option may only be specified when re-packaging an existing build.");

        if(IsRunningOnWindows())
        {
            MSBuild(SOLUTION_FILE, new MSBuildSettings{ ToolPath = msbuildPath }
                .SetConfiguration(configuration)
                .SetMSBuildPlatform(MSBuildPlatform.Automatic)
                .SetVerbosity(Verbosity.Minimal)
                .SetNodeReuse(false)
                .SetPlatformTarget(PlatformTarget.MSIL)
                .WithRestore()                
            );
        }
        else
        {
            XBuild(SOLUTION_FILE, new XBuildSettings()
                .WithTarget("Build")
                .WithProperty("Configuration", configuration)
                .SetVerbosity(Verbosity.Minimal)
            );
        }
    });

//////////////////////////////////////////////////////////////////////
// TEST
//////////////////////////////////////////////////////////////////////

Task("Test")
    .IsDependentOn("Build")
    .Does(() =>
    {
        int rc = StartProcess(
            NUNIT3_CONSOLE,
            new ProcessSettings()
            {
                Arguments = TEST_ASSEMBLY
            });

        if (rc != 0)
        {
            var message = rc > 0
                ? string.Format("Test failure: {0} tests failed", rc)
                : string.Format("Test exited with rc = {0}", rc);

            throw new CakeException(message);
        }
    });

//////////////////////////////////////////////////////////////////////
// INITIALIZE FOR BUILD
//////////////////////////////////////////////////////////////////////

Task("NuGetRestoreForIntegrationTests")
    .Does(() =>
    {
        CleanDirectory(TEST_NUNIT_DIR);
        NuGetRestore(TEST_SOLUTION_FILE, new NuGetRestoreSettings()
        {
            Source = PACKAGE_SOURCE
        });
    });

//////////////////////////////////////////////////////////////////////
// BUILD FOR INTEGRATION TEST
//////////////////////////////////////////////////////////////////////

Task("BuildForIntegrationTests")
    .IsDependentOn("NuGetRestoreForIntegrationTests")
    .Does(() =>
    {
        MSBuild(TEST_SOLUTION_FILE, new MSBuildSettings{ ToolPath = msbuildPath }
                        .SetConfiguration(configuration)
                        .SetVerbosity(Verbosity.Minimal)
                        .WithRestore()                
                    );               
    });

//////////////////////////////////////////////////////////////////////
// ADD TEAMCITY TEST CATEGORY
//////////////////////////////////////////////////////////////////////

Task("AddTeamCityTestCategory")
    .Does(() =>
    {
        integrationTestsCategories.Add("cat==teamcity");
    });

//////////////////////////////////////////////////////////////////////
// INTEGRATION TEST
//////////////////////////////////////////////////////////////////////

Task("IntegrationTest")
    .IsDependentOn("Build")
    .IsDependentOn("BuildForIntegrationTests")
    .Does(() =>
    {
        foreach(var nunitCoreVersion in versionsOfNunitCore)
        {
            EnsureDirectoryExists(TEST_NUNIT_DIR);
            EnsureDirectoryExists(TEST_PACKAGES_DIR);
            CleanDirectories(TEST_NUNIT_DIR + "**/*.*");
            CleanDirectories(TEST_PACKAGES_DIR + "**/*.*");		

            Information("Restoring basic packages to test");
            NuGetInstall(new [] {"NUnit" }, new NuGetInstallSettings()
            {
                Version = nunitCoreVersion.Item1 == string.Empty ? null : nunitCoreVersion.Item1,
                OutputDirectory = TEST_NUNIT_DIR,
                Source = nunitCoreVersion.Item1 == string.Empty ? PRERELEASE_PACKAGE_SOURCE : PACKAGE_SOURCE,
                Prerelease = (nunitCoreVersion.Item1 == string.Empty),
                NoCache = true
            });

            NuGetInstall(new [] {"NUnit.Console" }, new NuGetInstallSettings()
            {
                Version = nunitCoreVersion.Item2 == string.Empty ? null : nunitCoreVersion.Item2,
                OutputDirectory = TEST_NUNIT_DIR,
                Source = nunitCoreVersion.Item2 == string.Empty ? PRERELEASE_PACKAGE_SOURCE : PACKAGE_SOURCE,
                Prerelease = (nunitCoreVersion.Item2 == string.Empty),
                NoCache = true
            });

            Information("Restoring NUnit 2 packages");
            NuGetInstall(new [] {"NUnit"}, new NuGetInstallSettings()
            {
                Version = "2.6.4",
                OutputDirectory = TEST_PACKAGES_DIR,
                Source = PACKAGE_SOURCE,
                Prerelease = false,
                NoCache = true
            });

            CleanDirectories(TEST_NUNIT_DIR + "NUnit.Extension.TeamCityEventListener*");
            EnsureDirectoryExists(TEST_TEAMCITY_EXT_DIR);
            CopyFileToDirectory(BIN_DIR + "teamcity-event-listener.dll", TEST_TEAMCITY_EXT_DIR);

            var versionCategories = string.Join(
                "||",
                versionsOfNunitCore
                    .TakeWhile(i => i != nunitCoreVersion)
                    .Concat(Enumerable.Repeat(nunitCoreVersion, 1))
                    .Select(i => "cat==" + (string.IsNullOrEmpty(i.Item1) ? "dev" : i.Item1)));

            var categoriesList = 
                integrationTestsCategories
                .Concat(Enumerable.Repeat(versionCategories, 1))
                .Where(i => !string.IsNullOrEmpty(i))
                .Select(i => "(" + i + ")").ToList();
            
            var arguments = INTEGRATION_TEST_ASSEMBLY;
            if (categoriesList.Count!= 0)
            {
                arguments += " --where \"" + string.Join("&&", categoriesList) + "\"";
            }

            Information("NUnit arguments: " + arguments);
            int rc = StartProcess(
                NUNIT3_CONSOLE,
                new ProcessSettings()
                {
                    Arguments = arguments
                });

            if (rc != 0)
            {
                var message = rc > 0
                    ? string.Format("Test failure: {0} tests failed", rc)
                    : string.Format("Test exited with rc = {0}", rc);

                throw new CakeException(message);
            }

            try
            {
                using(var process = StartAndReturnProcess("TASKKILL", new ProcessSettings { Arguments = "/F /IM nunit-agent.exe /T" }))
                {
                    Information("Kill nunit-agent.exe");
                    process.WaitForExit();
                }
            }
            catch(Exception)
            {
            }

            try
            {
                using(var process = StartAndReturnProcess("TASKKILL", new ProcessSettings { Arguments = "/F /IM nunit-agent-x86.exe /T" }))
                {
                    Information("Kill nunit-agent-x86.exe");
                    process.WaitForExit();
                }
            }
            catch(Exception)
            {
            }
        }
    });


//////////////////////////////////////////////////////////////////////
// PACKAGE
//////////////////////////////////////////////////////////////////////

Task("RePackageNuGet")
    .IsDependentOn("Build")
    .Does(() => 
    {
        CreateDirectory(PACKAGE_DIR);

        NuGetPack(
            new NuGetPackSettings()
            {
                Id = NUGET_ID,
                Version = nugetVersion ?? packageVersion,
                Title = TITLE,
                Authors = AUTHORS,
                Owners = OWNERS,
                Description = DESCRIPTION,
                Summary = SUMMARY,
                ProjectUrl = PROJECT_URL,
                IconUrl = ICON_URL,
                LicenseUrl = LICENSE_URL,
                RequireLicenseAcceptance = false,
                Copyright = COPYRIGHT,
                ReleaseNotes = RELEASE_NOTES,
                Tags = TAGS,
                OutputDirectory = PACKAGE_DIR,
                Files = new [] {
                    new NuSpecContent { Source = PROJECT_DIR + "LICENSE.txt" },
                    new NuSpecContent { Source = PROJECT_DIR + "CHANGES.txt" },
                    new NuSpecContent { Source = PROJECT_DIR + "teamcity-event-listener.addins", Target = "tools" },
                    new NuSpecContent { Source = BIN_SRC + "net20/teamcity-event-listener.dll", Target = "tools/net20" },
                    new NuSpecContent { Source = BIN_SRC + "net20/nunit.engine.api.dll", Target = "tools/net20" },
                    new NuSpecContent { Source = BIN_SRC + "netstandard2.0/teamcity-event-listener.dll", Target = "tools/netstandard2.0" },
                    new NuSpecContent { Source = BIN_SRC + "netstandard2.0/nunit.engine.api.dll", Target = "tools/netstandard2.0" }
                }
            });
    });

Task("RePackageChocolatey")
    .IsDependentOn("Build")
    .Does(() =>
    {
        CreateDirectory(PACKAGE_DIR);

        ChocolateyPack(
            new ChocolateyPackSettings()
            {
                Id = CHOCO_ID,
                Version = chocoVersion ?? packageVersion,
                Title = TITLE,
                Authors = AUTHORS,
                Owners = OWNERS,
                Description = DESCRIPTION,
                Summary = SUMMARY,
                ProjectUrl = PROJECT_URL,
                IconUrl = ICON_URL,
                LicenseUrl = LICENSE_URL,
                RequireLicenseAcceptance = false,
                Copyright = COPYRIGHT,
                ProjectSourceUrl = PROJECT_SOURCE_URL,
                BugTrackerUrl = BUG_TRACKER_URL,
                PackageSourceUrl = PACKAGE_SOURCE_URL,
                ReleaseNotes = RELEASE_NOTES,
                Tags = TAGS,
                OutputDirectory = PACKAGE_DIR,
                Files = new [] {
                    new ChocolateyNuSpecContent { Source = PROJECT_DIR + "LICENSE.txt", Target = "tools" },
                    new ChocolateyNuSpecContent { Source = PROJECT_DIR + "CHANGES.txt", Target = "tools" },
                    new ChocolateyNuSpecContent { Source = PROJECT_DIR + "VERIFICATION.txt", Target = "tools" },
                    new ChocolateyNuSpecContent { Source = PROJECT_DIR + "teamcity-event-listener.addins", Target = "tools" },
                    new ChocolateyNuSpecContent { Source = BIN_SRC + "net20/teamcity-event-listener.dll", Target = "tools/net20" },
                    new ChocolateyNuSpecContent { Source = BIN_SRC + "net20/nunit.engine.api.dll", Target = "tools/net20" },
                    new ChocolateyNuSpecContent { Source = BIN_SRC + "netstandard2.0/teamcity-event-listener.dll", Target = "tools/netstandard2.0" },
                    new ChocolateyNuSpecContent { Source = BIN_SRC + "netstandard2.0/nunit.engine.api.dll", Target = "tools/netstandard2.0" }
                }
            });
    });
    
Task("RePackageZip")
    .IsDependentOn("Build")
    .Does(() =>
    {
        var zipRootDir = System.IO.Path.Combine(PACKAGE_DIR, "temp_zip_root");

        var eventListenerDir = System.IO.Path.Combine(zipRootDir, "NUnit.Extension.TeamCityEventListener");
        var toolsDir = System.IO.Path.Combine(eventListenerDir, "tools");
        var net20Dir = System.IO.Path.Combine(toolsDir, "net20");
        var netStandard20Dir = System.IO.Path.Combine(toolsDir, "netstandard2.0");

        CleanDirectory(zipRootDir);
        CreateDirectory(zipRootDir);
        CreateDirectory(eventListenerDir);
        CreateDirectory(toolsDir);
        CreateDirectory(net20Dir);
        CreateDirectory(netStandard20Dir);

        CopyFileToDirectory(BIN_SRC + "net20/teamcity-event-listener.dll", net20Dir);
        CopyFileToDirectory(BIN_SRC + "net20/nunit.engine.api.dll", net20Dir);
        CopyFileToDirectory(BIN_SRC + "netstandard2.0/teamcity-event-listener.dll", netStandard20Dir);
        CopyFileToDirectory(BIN_SRC + "netstandard2.0/nunit.engine.api.dll", netStandard20Dir);

        var zipOutputPath = System.IO.Path.Combine(PACKAGE_DIR, "NUnit.Extension.TeamCityEventListener.zip");
        Information($"Creating ZIP: {zipOutputPath}");
        Zip(zipRootDir, zipOutputPath);
    });

//////////////////////////////////////////////////////////////////////
// TASK TARGETS
//////////////////////////////////////////////////////////////////////

Task("Rebuild")
    .IsDependentOn("Clean")
    .IsDependentOn("Build");

Task("Appveyor")
    .IsDependentOn("Build")
    .IsDependentOn("Test")
    .IsDependentOn("AddTeamCityTestCategory")
    .IsDependentOn("IntegrationTest")
    .IsDependentOn("Package");

Task("CheckIntegration")
    .IsDependentOn("Build")
    .IsDependentOn("Test")	
    .IsDependentOn("IntegrationTest")
    .IsDependentOn("Package");

Task("Package")
    .IsDependentOn("Build")
    .IsDependentOn("RePackage");

Task("RePackage")
    .IsDependentOn("RePackageNuGet")
    .IsDependentOn("RePackageChocolatey");

Task("Travis")
    .IsDependentOn("Build")
    .IsDependentOn("Test");

Task("Default")
    .IsDependentOn("Build");

//////////////////////////////////////////////////////////////////////
// EXECUTION
//////////////////////////////////////////////////////////////////////

RunTarget(target);
