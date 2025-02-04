using Nuke.Common;
using Nuke.Common.IO;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using System.Diagnostics.CodeAnalysis;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.IO.PathConstruction;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

partial class Build : NukeBuild
{
    [SuppressMessage("Performance", "CA1822:Mark members as static", Justification = "Not necessary")]
    Target Backend_Clean => _ => _
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            TestsDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            DotNetClean();
        });

    Target Backend_Restore => _ => _
        .DependsOn(Clean, Frontend_Build)
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution)
            );
        });

    Target Backend_Compile => _ => _
        .DependsOn(Backend_Restore, Backend_SonarScan_Start)
        .Executes(() =>
        {
            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoRestore());
        });
}