#addin Cake.GitVersioning

var target = Argument("target", "Default");
var configuration = Argument("configuration", "Release");

// Repository-specific variables for build tasks
var buildNumber = GitVersioningGetVersion().SemVer2;
var artifactsDir = "./artifacts";
var projectToPublish = "./src/KubernetesClient/KubernetesClient.csproj";
var solution = "./kubernetes-client.sln";
var feedzKey = EnvironmentVariable("NUGET_FEEDZ_API_KEY");

// Shared build tasks that hopefully should be copy-pastable
Information($"Running on TeamCity: {TeamCity.IsRunningOnTeamCity}");
Information($"Building: {buildNumber}");

Task("Clean")
    .Does(() =>
{
    CleanDirectory(artifactsDir);
});

Task("Restore")
    .IsDependentOn("Clean")
    .Does(() => {
        DotNetCoreRestore(solution);
    });

Task("Build")
    .IsDependentOn("Restore")
    .Does(() => {
        DotNetCoreBuild(solution, new DotNetCoreBuildSettings
        {
            Configuration = configuration
        });
    });

Task("Test")
    .IsDependentOn("Build")
    .Does(() => {
        var settings = new DotNetCoreTestSettings
        {
            Configuration = configuration,
            ArgumentCustomization = args =>
                args.Append("/p:Include=\"[KubernetesClient]*\"")
                    .Append("/p:Exclude=\"[KubernetesClient]k8s.Models.*\"")
                    .Append("/p:Exclude=\"[KubernetesClient]k8s.Internal.*\"")
        };

        var projectFiles = GetFiles("./tests/**/*.csproj");
        foreach (var file in projectFiles)
        {
            DotNetCoreTest(file.FullPath, settings);
        }
    });

Task("Pack-NuGet")
    .IsDependentOn("Test")
    .Does(() => {
        DotNetCorePack(projectToPublish, new DotNetCorePackSettings {
            Configuration = configuration,
            OutputDirectory = artifactsDir
        });
    });

Task("Push-NuGet")
    .IsDependentOn("Pack-NuGet")
    .Does(() => {
        if (!String.IsNullOrEmpty (feedzKey)) {
            Information("Have a feedz key so pushing package");

            DotNetCoreNuGetPush($"./{artifactsDir}/Gearset.KubernetesClient.{buildNumber}.nupkg", new DotNetCoreNuGetPushSettings {
                Source = "https://f.feedz.io/gearsethq/gearset-kubernetes-client/nuget",
                ApiKey = feedzKey
            });
        } else {
            Information("No Feedz key so skipping package push");
        }
    });

Task("Default")
    .IsDependentOn("Push-NuGet");

RunTarget(target);
