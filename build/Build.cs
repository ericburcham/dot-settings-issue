// -----------------------------------------------------------------------
// <copyright file="Build.cs" company="Enterprise Products Partners L.P.">
// For copyright details, see the COPYRIGHT file in the root of this repository.
// </copyright>
// -----------------------------------------------------------------------

using System.IO;
using System.Linq;
using System.Text;
using JetBrains.Annotations;
using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Tools.Git;
using Nuke.Common.Utilities.Collections;
using Serilog;
using Nuke.Common.Tools.ReSharper;
using Nuke.Common.Utilities;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

class Build : NukeBuild
{
    /// Support plugins are available for:
    /// - JetBrains ReSharper        https://nuke.build/resharper
    /// - JetBrains Rider            https://nuke.build/rider
    /// - Microsoft VisualStudio     https://nuke.build/visualstudio
    /// - Microsoft VSCode           https://nuke.build/vscode
    public static int Main() => Execute<Build>(x => x.Default);

    /// <summary>Gets the <see cref="Solution" /> information.</summary>
    [PublicAPI]
    [Required]
    [Solution]
    Solution Solution => null!;

    /// <summary>Gets the name of the ReSharper Code Cleanup profile.</summary>
    [PublicAPI]
    string Profile => "DEFAULT";

    Target Default =>
        td => td
            .Executes(() =>
            {
                Log.Information("Build succeeded!");
            })
            .After(CleanAll, Restore, Compile, Test);

    /// <summary>Gets the <see cref="AbsolutePath" /> to the artifacts directory.</summary>
    [PublicAPI]
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    /// <summary>Gets the <see cref="AbsolutePath" /> to the code inspection directory.</summary>
    [PublicAPI]
    AbsolutePath CodeInspectionDirectory => RootDirectory / "code-inspection";

    /// <summary>Gets the <see cref="AbsolutePath" /> to the source code directory.</summary>
    [PublicAPI]
    AbsolutePath SourceDirectory => RootDirectory / "src" / "code";

    /// <summary>Gets the <see cref="AbsolutePath" /> to the tests directory.</summary>
    [PublicAPI]
    AbsolutePath TestDirectory => RootDirectory / "src" / "tests";

    /// <summary>Gets a <see cref="Target" /> that runs tests.</summary>
    [PublicAPI]
    Target Test =>
        td => td
            .Executes(
                () =>
                {
                    Solution.NotNull();

                    Log.Information($"Running tests for: {Solution.Path}");
                    DotNetTest(
                        ts => ts.EnableNoBuild<DotNetTestSettings>()
                            .EnableNoRestore()
                            .SetConfiguration(Configuration)
                            .SetProjectFile(Solution));
                })
            .Requires(() => Configuration)
            .After(CleanupCode, EnforceCodeCleanup, InspectCode, Compile)
            .DependsOn(Compile);

    /// <summary>Gets a <see cref="Target" /> that fails the build if the user did not run ReSharper code clean up before pushing their changes.</summary>
    [PublicAPI]
    Target EnforceCodeCleanup =>
        td => td
            .DependsOn(CleanupCode)
            .Executes(
                () =>
                {
                    Log.Information("Enforcing ReSharper code clean up");
                    var gitPorcelainOutput = GitTasks.Git("status --porcelain");

                    if (gitPorcelainOutput.Any(x => !x.Text.IsNullOrWhiteSpace()))
                        Assert.Fail(
                            $"This repository adheres to strict code style rules.  Your code does not conform to those rules.  The \"{nameof(CleanupCode)}\" Nuke Target is provided"
                            + $" for your convenience.  In order to conform your source code to project standards, run the following command from the root of your repository:\n\n"
                            + $"  \"Nuke {nameof(CleanupCode)}\".\n\nOnce the command finishes, make a new commit taking all changes.");
                })
            .TriggeredBy(Compile);

    /// <summary>Gets a <see cref="Target" /> that executes ReSharper code inspection and displays the results.</summary>
    [PublicAPI]
    Target InspectCode =>
        td => td
            .DependsOn(Compile)
            .Executes(
                () =>
                {
                    CodeInspectionDirectory.NotNull();
                    Solution.NotNull();

                    Log.Information($"Running ReSharper code inspection for: {Solution.Path}");
                    var resultsPath = GetInspectionResultsPath(Solution);
                    ReSharperTasks.ReSharperInspectCode(
                        cs => cs.SetOutput<ReSharperInspectCodeSettings>(resultsPath)
                            .SetProcessArgumentConfigurator(
                                arguments =>
                                {
                                    arguments.Add("--no-build");

                                    return arguments;
                                })
                            .SetTargetPath(Solution.Path)
                            .SetVerbosity(ReSharperVerbosity.OFF));

                    if (!File.Exists(resultsPath)) return;

                    var sb = new StringBuilder($"Begin Code inspection results for {Solution.Path} ------------------------------\n\n");

                    var codeInspectionResults = File.ReadAllText(resultsPath);
                    sb.AppendLine(codeInspectionResults);

                    Log.Information(sb.ToString());
                    Log.Information($"End Code inspection results for {Solution.Path} --------------------------------");
                })
            .TriggeredBy(Compile);


    [PublicAPI]
    Target CleanupCode =>
        td => td
            .Executes(
                () =>
                {
                    Profile.NotNull();
                    Solution.NotNull();

                    Log.Information($"Running ReSharper code clean up for solution: {Solution.Path}");
                    ReSharperTasks.ReSharperCleanupCode(
                        cs => cs.SetProfile<ReSharperCleanupCodeSettings>(Profile)
                            .SetTargetPath(Solution.Path)
                            .SetVerbosity(ReSharperVerbosity.OFF));
                })
            .DependsOn(Compile);

    /// <summary>Gets a task that compiles a .NET solution.</summary>
    Target Compile =>
        td => td
            .Executes(
                () =>
                {
                    Solution.NotNull();

                    Log.Information($"Compiling solution: {Solution.Path}");
                    DotNetBuild(
                        bs => bs.EnableNoRestore<DotNetBuildSettings>()
                            .SetConfiguration(Configuration)
                            .SetProjectFile(Solution)
                            .SetProperty("GeneratePackageOnBuild", "False"));
                })
            .Requires(() => Configuration)
            .DependsOn(Restore);


    /// <summary>Gets a <see cref="Target" /> that runs NuGet package restore for .NET solutions.</summary>
    [PublicAPI]
    Target Restore =>
        td => td
            .Executes(
                () =>
                {
                    Solution.NotNull();

                    Log.Information($"Restoring NuGet packages for: {Solution.Path}");
                    DotNetRestore(rs => rs.SetProjectFile<DotNetRestoreSettings>(Solution));
                })
            .DependsOn(CleanAll);

    /// <summary>Gets a <see cref="Target" /> that cleans code test code bin and obj folders.</summary>
    [PublicAPI]
    Target CleanTestDirectories =>
        td => td
            .Executes(
                () =>
                {
                    TestDirectory.NotNull();

                    Log.Information($"Cleaning test bin and obj folders in: {TestDirectory}");
                    TestDirectory.GlobDirectories("**/bin", "**/obj")
                        .ForEach(
                            folder =>
                            {
                                Log.Information($"Deleting folder: {folder}");
                                folder.DeleteDirectory();
                            });
                });

    /// <summary>Gets a <see cref="Target" /> that cleans build artifacts, code inspection results, source code bin and obj folders, and test bin and object folders.</summary>
    [PublicAPI]
    Target CleanAll =>
        td => td
            .DependsOn(CleanArtifacts, CleanCodeInspectionResults, CleanSourceDirectories, CleanTestDirectories);

    /// <summary>Gets the <see cref="AbsolutePath" /> to the code inspection results file.</summary>
    [PublicAPI]
    AbsolutePath GetInspectionResultsPath(Solution solution) => CodeInspectionDirectory / $"{solution.Name}.results.xml";

    Target CleanSourceDirectories =>
        td => td
            .Executes(
                () =>
                {
                    SourceDirectory.NotNull();

                    Log.Information($"Cleaning source bin and obj folders in: {SourceDirectory}");
                    SourceDirectory.GlobDirectories("**/bin", "**/obj")
                        .ForEach(
                            folder =>
                            {
                                Log.Information($"Deleting folder: {folder}");
                                folder.DeleteDirectory();
                            });
                });

    /// <summary>Gets a <see cref="Target" /> that cleans build artifacts.</summary>
    [PublicAPI]
    Target CleanArtifacts =>
        td => td
            .Executes(
                () =>
                {
                    ArtifactsDirectory.NotNull();

                    Log.Information($"Cleaning artifacts directory: {ArtifactsDirectory}");
                    ArtifactsDirectory.CreateOrCleanDirectory();
                });

    /// <summary>Gets a <see cref="Target" /> that cleans code inspection results.</summary>
    [PublicAPI]
    Target CleanCodeInspectionResults =>
        td => td
            .Executes(
                () =>
                {
                    CodeInspectionDirectory.NotNull();

                    Log.Information($"Cleaning code inspection results directory: {CodeInspectionDirectory}");
                    CodeInspectionDirectory.CreateOrCleanDirectory();
                });


    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;
}