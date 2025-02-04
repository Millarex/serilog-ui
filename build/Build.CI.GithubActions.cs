using Nuke.Common;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.GitHub;
using Nuke.Common.Tools.SonarScanner;
using static CustomGithubActionsAttribute;

/**
 * Interesting ref to make the build script executable on server:
 * https://blog.dangl.me/archive/executing-nuke-build-scripts-on-linux-machines-with-correct-file-permissions/
 * https://stackoverflow.com/a/40979016/15129749
 */
[CustomGithubActions("DotNET-build",
    GitHubActionsImage.UbuntuLatest,
    AddGithubActions = new[] { GithubAction.Backend_Artifact },
    AutoGenerate = true,
    EnableGitHubToken = true,
    FetchDepth = 0,
    ImportSecrets = new[] { nameof(SonarToken) },
    InvokedTargets = new[] { nameof(Backend_SonarScan_End) },
    OnPushBranches = new[] { "master", "dev" },
    OnPullRequestBranches = new[] { "master", "dev" }
)]
[CustomGithubActions("JS-build",
    GitHubActionsImage.UbuntuLatest,
    AddGithubActions = new[] { GithubAction.Frontend_SonarScan_Task, GithubAction.Frontend_Artifact },
    AutoGenerate = true,
    EnableGitHubToken = true,
    FetchDepth = 0,
    ImportSecrets = new[] { nameof(SonarTokenUi) },
    InvokedTargets = new[] { nameof(Frontend_Tests_Ci) },
    OnPushBranches = new[] { "master", "dev" },
    OnPullRequestBranches = new[] { "master", "dev" }
)]
[CustomGithubActions("Release",
    GitHubActionsImage.UbuntuLatest,
    AddGithubActions = new[] { GithubAction.Backend_Reporter, GithubAction.Frontend_SonarScan_Task, GithubAction.Frontend_Reporter },
    AutoGenerate = true,
    EnableGitHubToken = true,
    FetchDepth = 0,
    ImportSecrets = new[] { nameof(SonarTokenUi), nameof(SonarToken), nameof(NugetApiKey) },
    InvokedTargets = new[] { nameof(Publish) },
    OnWorkflowDispatchRequiredInputs = new[] {
        nameof(ElasticProvider),
        nameof(MongoProvider),
        nameof(MsSqlProvider),
        nameof(MySqlProvider),
        nameof(PostgresProvider),
        nameof(Ui),
    }
)]
partial class Build : NukeBuild
{
    [Parameter][Secret] readonly string SonarToken;
    [Parameter][Secret] readonly string SonarTokenUi;
    [Parameter][Secret] readonly string NugetApiKey;
    [Parameter] readonly string ElasticProvider = string.Empty;
    [Parameter] readonly string MongoProvider = string.Empty;
    [Parameter] readonly string MsSqlProvider = string.Empty;
    [Parameter] readonly string MySqlProvider = string.Empty;
    [Parameter] readonly string PostgresProvider = string.Empty;
    [Parameter] readonly string Ui = string.Empty;

    public ReleaseParams[] ReleaseInfos() => new ReleaseParams[]
    {
        new(nameof(ElasticProvider), ElasticProvider, "Serilog.Ui.ElasticSearchProvider"),
        new(nameof(MongoProvider), MongoProvider, "Serilog.Ui.MongoDbProvider"),
        new(nameof(MsSqlProvider), MsSqlProvider, "Serilog.Ui.MsSqlServerProvider"),
        new(nameof(MySqlProvider), MySqlProvider, "Serilog.Ui.MySqlProvider"),
        new(nameof(PostgresProvider), PostgresProvider, "Serilog.Ui.PostgreSqlProvider"),
        new(nameof(Ui), Ui, "Serilog.Ui.Web"),
    };

    public bool OnGithubActionRun = GitHubActions.Instance != null &&
            !string.IsNullOrWhiteSpace(GitHubActions.Instance.RunId.ToString());

    public bool IsPr = GitHubActions.Instance != null &&
        GitHubActions.Instance.IsPullRequest;

    Target Backend_SonarScan_Start => _ => _
        .DependsOn(Backend_Restore)
        .OnlyWhenStatic(() => OnGithubActionRun && !IsPr &&
            !string.IsNullOrWhiteSpace(SonarCloudInfo.Organization) &&
            !string.IsNullOrWhiteSpace(SonarCloudInfo.BackendProjectKey)
        )
        .Executes(() =>
        {
            SonarScannerTasks.SonarScannerBegin(new SonarScannerBeginSettings()
                .SetExcludeTestProjects(true)
                .SetFramework("net5.0")
                .SetLogin(SonarToken)
                .SetOrganization(SonarCloudInfo.Organization)
                .SetProjectKey(SonarCloudInfo.BackendProjectKey)
                .SetServer("https://sonarcloud.io")
                .SetSourceInclusions("src/**/*")
                .SetSourceExclusions(
                    "src/Serilog.Ui.Web/assets/**/*",
                    "src/Serilog.Ui.Web/wwwroot/**/*",
                    "src/Serilog.Ui.Web/node_modules/**/*",
                    "src/Serilog.Ui.Web/*.js",
                    "src/Serilog.Ui.Web/*.json")
                .SetVisualStudioCoveragePaths("**/coverage.xml")
                .SetProcessEnvironmentVariable("GITHUB_TOKEN", GitHubActions.Instance.Token)
                .SetProcessEnvironmentVariable("SONAR_TOKEN", SonarToken)
            );
        });

    Target Backend_SonarScan_End => _ => _
        .DependsOn(Backend_Test_Ci)
        .OnlyWhenStatic(() => OnGithubActionRun && !IsPr &&
            !string.IsNullOrWhiteSpace(SonarCloudInfo.Organization) &&
            !string.IsNullOrWhiteSpace(SonarCloudInfo.BackendProjectKey)
        )
        .Executes(() =>
        {
            SonarScannerTasks.SonarScannerEnd(new SonarScannerEndSettings()
                .SetFramework("net5.0")
                .SetLogin(SonarToken)
                .SetProcessEnvironmentVariable("GITHUB_TOKEN", GitHubActions.Instance.Token)
                .SetProcessEnvironmentVariable("SONAR_TOKEN", SonarToken));
        });
}

public readonly record struct ReleaseParams(string Key, string ShouldPublish, string Project)
{
    public bool Publish() => ShouldPublish.Equals("true");
}