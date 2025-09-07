using MelonLoader;
using System.Reflection;


[assembly: AssemblyTitle(FsOptimizer.BuildInfo.Description)]
[assembly: AssemblyDescription(FsOptimizer.BuildInfo.Description)]
[assembly: AssemblyCompany(FsOptimizer.BuildInfo.Company)]
[assembly: AssemblyProduct(FsOptimizer.BuildInfo.Name)]
[assembly: AssemblyCopyright("Created by " + FsOptimizer.BuildInfo.Author)]
[assembly: AssemblyTrademark(FsOptimizer.BuildInfo.Company)]
[assembly: AssemblyVersion(FsOptimizer.BuildInfo.Version)]
[assembly: AssemblyFileVersion(FsOptimizer.BuildInfo.Version)]
[assembly: MelonInfo(typeof(FsOptimizer.FsOptimizer), FsOptimizer.BuildInfo.Name, FsOptimizer.BuildInfo.Version, FsOptimizer.BuildInfo.Author, FsOptimizer.BuildInfo.DownloadLink)]
[assembly: MelonColor(0, 255, 255, 0)]

// Create and Setup a MelonGame Attribute to mark a Melon as Universal or Compatible with specific Games.
// If no MelonGame Attribute is found or any of the Values for any MelonGame Attribute on the Melon is null or empty it will be assumed the Melon is Universal.
// Values for MelonGame Attribute can be found in the Game's app.info file or printed at the top of every log directly beneath the Unity version.
[assembly: MelonGame()]