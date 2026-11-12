using AbsolutePathHelpers.Build;
using Nuke.Common;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using NukeBuildHelpers;
using NukeBuildHelpers.Common.Attributes;
using NukeBuildHelpers.Common.Enums;
using NukeBuildHelpers.Entry;
using NukeBuildHelpers.Entry.Extensions;
using NukeBuildHelpers.Runner.Abstraction;
using System;
using System.Linq;

class Build : BaseNukeBuildHelpers
{
    public static int Main() => Execute<Build>(x => x.Interactive);

    public override string[] EnvironmentBranches { get; } = ["prerelease", "master"];

    public override string MainEnvironmentBranch => "master";

    [SecretVariable("GITHUB_TOKEN")]
    readonly string? GithubToken;

    BuildEntry AbsolutePathHelpersBuild => _ => _
        .AppId("absolute_path_helpers")
        .RunnerOS(RunnerOS.Ubuntu2204)
        .Execute(context =>
        {
            var app = context.Apps.Values.First();
            var projectPath = RootDirectory / "AbsolutePathHelpers" / "AbsolutePathHelpers.csproj";
            var version = app.AppVersion.Version.ToString()!;
            var releaseNotes = "";

            if (app.BumpVersion != null)
            {
                version = app.BumpVersion.Version.ToString();
                releaseNotes = app.BumpVersion.ReleaseNotes;
            }
            DotNetTasks.DotNetClean(_ => _
                .SetProject(projectPath));
            DotNetTasks.DotNetBuild(_ => _
                .SetProjectFile(projectPath)
                .SetConfiguration("Release"));
            DotNetTasks.DotNetPack(_ => _
                .SetProject(projectPath)
                .SetConfiguration("Release")
                .SetNoRestore(true)
                .SetNoBuild(true)
                .SetIncludeSymbols(true)
                .SetSymbolPackageFormat("snupkg")
                .SetVersion(version)
                .SetPackageReleaseNotes(NormalizeReleaseNotes(releaseNotes))
                .SetOutputDirectory(app.OutputDirectory));
        });

    TestEntry AbsolutePathHelpersTest => _ => _
        .AppId("absolute_path_helpers")
        .Execute(context =>
        {
            var projectPath = RootDirectory / "AbsolutePathHelpers.UnitTest" / "AbsolutePathHelpers.UnitTest.csproj";
            DotNetTasks.DotNetClean(_ => _
                .SetProject(projectPath));
            DotNetTasks.DotNetTest(_ => _
                .SetProcessAdditionalArguments(
                    "--logger \"GitHubActions;summary.includePassedTests=true;summary.includeSkippedTests=true\" " +
                    "-- " +
                    "RunConfiguration.CollectSourceInformation=true " +
                    "DataCollectionRunSettings.DataCollectors.DataCollector.Configuration.Format=opencovere ")
                .SetProjectFile(projectPath));
            DotNetTasks.DotNetTest(_ => _
                .SetProcessAdditionalArguments(
                    "--logger \"GitHubActions;summary.includePassedTests=true;summary.includeSkippedTests=true\"")
                .SetProjectFile(RootDirectory / "build" / "WorkflowPatcher.Tests" / "WorkflowPatcher.Tests.csproj")
                .SetConfiguration("Release"));
        })
        .Matrix([ new { Id = "LINUX", RunnerOS = RunnerOS.Ubuntu2204 }, new { Id = "WINDOWS", RunnerOS = RunnerOS.Windows2022 }],
            (_, osMatrix) => _
                .RunnerOS(osMatrix.RunnerOS)
                .WorkflowId($"TEST_{osMatrix.Id}"));

    PublishEntry AbsolutePathHelpersPublish => _ => _
        .AppId("absolute_path_helpers")
        .RunnerOS(RunnerOS.Ubuntu2204)
        .Execute(async context =>
        {
            var app = context.Apps.Values.First();
            if (app.BumpVersion != null)
            {
                // The publish job mints this through NuGet/login (OIDC); it is
                // only required on the bump path, so pull-request runs without
                // an OIDC token still work.
                var nuGetAuthToken = Environment.GetEnvironmentVariable("NUKE_NUGET_AUTH_TOKEN");
                if (string.IsNullOrEmpty(nuGetAuthToken))
                {
                    throw new InvalidOperationException(
                        "NUKE_NUGET_AUTH_TOKEN is empty. The publish job must run the " +
                        "NuGet/login (OIDC) step before the bump to mint a short-lived " +
                        "nuget.org token; see build/README.md.");
                }

                DotNetTasks.DotNetNuGetPush(_ => _
                    .SetSource("https://nuget.pkg.github.com/kiryuumaru/index.json")
                    .SetApiKey(GithubToken)
                    .SetTargetPath(app.OutputDirectory / "**"));
                DotNetTasks.DotNetNuGetPush(_ => _
                    .SetSource("https://api.nuget.org/v3/index.json")
                    .SetApiKey(nuGetAuthToken)
                    .SetTargetPath(app.OutputDirectory / "**"));
                await AddReleaseAsset(app.OutputDirectory, app.AppId);
            }
        });

    // REMOVE WHEN: NukeBuildHelpers can emit job-level `permissions`, a
    // job-level PR gate, a pre-step NuGet/login (OIDC) step, and step-level
    // env directly from its GitHub pipeline builder. Then delete this target,
    // WorkflowPatcher.cs, WorkflowPatcher.Tests, the _build.csproj compile
    // exclude, the solution entry, and the WorkflowPatcher.Tests DotNetTest
    // block; see build/README.md for the checklist.
    public Target PatchGithubWorkflow => _ => _
        .Description("Patch generated GitHub workflow for NuGet Trusted Publishing")
        .TriggeredBy(GithubWorkflow)
        .Unlisted()
        .Executes(() =>
        {
            WorkflowPatcher.PatchFile(RootDirectory / ".github" / "workflows" / "nuke-cicd.yml");
        });

    private string? NormalizeReleaseNotes(string? releaseNotes)
    {
        return releaseNotes?
            .Replace(",", "%2C")?
            .Replace(":", "%3A")?
            .Replace(";", "%3B");
    }
}
