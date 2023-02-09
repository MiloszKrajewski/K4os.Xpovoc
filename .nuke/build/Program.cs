using System;
using System.Linq;
using NuGet.Versioning;
using Nuke.Common;
using Nuke.Common.ChangeLog;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

// ReSharper disable UnusedMember.Local

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
	
	Target Test => _ => _
		.After(Build)
		.Executes(() =>
		{
			DotNetTest(s => s
				.SetProjectFile(Solution)
				.SetConfiguration(Configuration));
		});
}