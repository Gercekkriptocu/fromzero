using FromZero;

internal static class SilentUpdateChecks
{
 public static void Run(Action<bool,string> check)
 {
  DownloadRecipe Recipe(string id,string extension=".exe")=>new("Fixture","https://example.test/setup"+extension,"Fixture",["example.test"],Id:id,Extension:extension);
  var code=UpdatePackageInstaller.Definition(Recipe("app-vscode"),null);
  check(code!=null&&code.Arguments.Contains("/VERYSILENT")&&code.Arguments.Contains("/CURRENTUSER")&&
   code.Arguments.Contains("/MERGETASKS=!runcode")&&code.ProcessName=="Code",
   "Visual Studio Code user setup has a silent recipe that preserves user scope and prevents launch");
  var systemCode=UpdatePackageInstaller.Definition(Recipe("prepare-vscode"),null);
  var vivaldiUser=UpdatePackageInstaller.Definition(Recipe("app-vivaldi"),null,false);
  var vivaldiMachine=UpdatePackageInstaller.Definition(Recipe("app-vivaldi"),null,true);
  check(systemCode?.Arguments.Contains("/ALLUSERS")==true&&vivaldiUser?.Arguments.Contains("--vivaldi-silent")==true&&
   !vivaldiUser.Arguments.Contains("--system-level")&&vivaldiMachine?.Arguments.Contains("--system-level")==true,
   "System Code setup and Vivaldi keep their explicit installation scopes");
  DownloadRecipe Community(string slug)
  {
   var package=CommunityPackageCatalog.Definitions.Single(package=>package.Slug==slug);
   return new(package.Name,package.Url,"",package.Hosts,Id:"app-"+package.Id,ExpectedSha256:package.Hash,
    Extension:System.IO.Path.GetExtension(new Uri(package.Url).AbsolutePath),CatalogSigned:true);
  }
  var transmission=UpdatePackageInstaller.Definition(Community("transmission"),null);
  var copilot=UpdatePackageInstaller.Definition(Community("github-copilot"),null);
  var zed=UpdatePackageInstaller.Definition(Community("zed"),null);
  var zoom=UpdatePackageInstaller.Definition(Community("zoom"),null);
  check(transmission?.Arguments.Contains("/qn")==true&&copilot?.Arguments.Contains("/qn")==true&&
   zed?.Arguments.Contains("/VERYSILENT")==true&&zed.Arguments.Contains("/NOCLOSEAPPLICATIONS")&&
   zoom?.Arguments.SequenceEqual(["/silent"])==true,
   "Signed catalogue MSI, Zed and Zoom packages have explicit unattended installer recipes");
  check(UpdatePackageInstaller.Definition(Community("zed") with{ExpectedSha256=""},null)==null&&
   UpdatePackageInstaller.Definition(Recipe("app-community-unknown") with{CatalogSigned=true},null)==null,
   "Catalogue automation refuses unregistered packages and missing source hashes");
 }
}
