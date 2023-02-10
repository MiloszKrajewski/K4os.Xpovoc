using System;
using System.Linq;
using NuGet.Versioning;
using Nuke.Common;
using Nuke.Common.ChangeLog;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

// ReSharper disable UnusedMember.Local

[GitHubActions(
	"continuous",
	GitHubActionsImage.UbuntuLatest,
	On = new[] { GitHubActionsTrigger.Push },
	InvokedTargets = new[] { nameof(Release) })]
class Program: NukeBuild
{
	public static int Main() => Execute<Program>(x => x.Build);

	[Parameter("Use debug configuration")] readonly bool Debug;

	Configuration Configuration =>
		!IsLocalBuild ? Configuration.Release :
		Debug ? Configuration.Debug :
		Configuration.Release;

	[Solution] readonly Solution Solution;

	static readonly AbsolutePath NukeDirectory = RootDirectory / ".nuke";
	static readonly AbsolutePath OutputDirectory = RootDirectory / ".output";

	readonly ReleaseNotes[] ReleaseNotes = ChangelogTasks
		.ReadReleaseNotes(RootDirectory / "CHANGES.md")
		.ToArray();

	NuGetVersion PackageVersion =>
		ReleaseNotes.FirstOrDefault()?.Version ??
		throw new ArgumentException("No release notes found");

	Target Clean => _ => _
		.Before(Restore)
		.Executes(() =>
		{
			RootDirectory
				.GlobDirectories("**/bin", "**/obj", "packages")
				.Where(p => !NukeDirectory.Contains(p))
				.ForEach(DeleteDirectory);
		});

	Target Restore => _ => _
		.After(Clean)
		.Executes(() =>
		{
			DotNetRestore(s => s
				.SetProjectFile(Solution));
		});

	Target Build => _ => _
		.DependsOn(Restore)
		.Executes(() =>
		{
			DotNetBuild(s => s
				.SetProjectFile(Solution)
				.SetConfiguration(Configuration)
				.SetVersion(PackageVersion.ToString())
				.EnableNoRestore());
		});

	Target Rebuild => _ => _
		.DependsOn(Build).DependsOn(Clean)
		.Executes(() => { });

	Target Release => _ => _
		.DependsOn(Rebuild)
		.Produces(OutputDirectory / "*.nupkg")
		.Executes(() =>
		{
			DotNetPack(s => s
				.SetProject(Solution)
				.SetConfiguration(Configuration)
				.SetVersion(PackageVersion.ToString())
				.SetOutputDirectory(OutputDirectory)
				.EnableNoRestore()
				.EnableNoBuild());
		});

	Target Publish => _ => _
		.DependsOn(Release)
		.Executes(() =>
		{
			var apiKey = GetNugetApiKey();

			DotNetNuGetPush(s => s
				.SetTargetPath(OutputDirectory / $"*.{PackageVersion}.nupkg")
				.SetSource("https://api.nuget.org/v3/index.json")
				.SetApiKey(apiKey));
		});

	static string GetNugetApiKey()
	{
		var key = EnvironmentInfo.GetVariable<string>("NUGET_API_KEY");
		if (string.IsNullOrWhiteSpace(key))
			throw new Exception("NUGET_API_KEY is not set");

		return key;
	}

	static (string Login, string Token) GetGitHubApi()
	{
		var key = EnvironmentInfo.GetVariable<string>("GITHUB_API_KEY");
		if (string.IsNullOrWhiteSpace(key))
			throw new Exception("GITHUB_API_KEY is not set, expecting '<login>:<token>'");

		var (login, token) = key.Split(':');
		return (login, token);
	}

	Target Test => _ => _
		.After(Build)
		.Executes(() =>
		{
			DotNetTest(s => s
				.SetProjectFile(Solution)
				.SetConfiguration(Configuration));
		});
}
