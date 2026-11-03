using FromZero;
internal static class ApplicationChecks
{
 public static void Run(Action<bool,string> check)
 {
  check(ApplicationCatalog.State(["1.9.0"],"1.10.0")=="Güncelleme var","App version comparison is numeric rather than lexical");
  check(ApplicationCatalog.State(["2.0.0","2.2.0"],"2.1.0")=="Güncel veya daha yeni","App catalog preserves newer installed releases");
  check(ApplicationCatalog.State([],"2.0")=="Kurulu değil"&&ApplicationCatalog.State(["unknown"],"2.0")=="Sürüm karşılaştırılamadı","App catalog distinguishes missing and unknown installed versions");
  var xna=AppDownloads.Recipes.Single(r=>r.Id=="xna");check(xna.Extension==".msi"&&xna.Publisher=="Microsoft Corporation","XNA has a typed Microsoft MSI recipe");
  var hash=new string('a',64);var recipe=new DownloadRecipe("7-Zip","https://github.com/ip7z/7zip/releases/download/26.05/7z2605-x64.exe","Igor Pavlov",["github.com"],Id:"app-7zip",ExpectedSha256:hash,PublisherHashOnly:true);
  check(AppDownloads.HashOnlyAllowed(recipe)&&!AppDownloads.HashOnlyAllowed(recipe with{Id="app-git"})&&!AppDownloads.HashOnlyAllowed(recipe with{ExpectedSha256=""}),"Unsigned installer hash validation is restricted to the official 7-Zip release asset");
  check(ApplicationSources.Count==600&&ApplicationSources.Github.Select(s=>s.Id).Distinct().Count()==21,"Expanded catalog declares six hundred distinct application tracks");
  check(ApplicationCatalog.State(["1.2"],"1.2.0.0")=="Güncel veya daha yeni","Application comparison normalizes absent numeric version components");
  var edge=new InstalledApp("Microsoft Edge WebView2 Runtime","1","Microsoft","","","test");check(!ApplicationCatalog.Matches(edge,["Microsoft Edge"])&&!ApplicationCatalog.Matches(edge with{Name="Microsoft Edge Update"},["Microsoft Edge"]),"Browser detection excludes updater and WebView subcomponents");
  check(ApplicationCatalog.Matches(edge with{Name="Mozilla Firefox (x64 tr)"},["Mozilla Firefox"]),"Application matching retains vendor name and architecture suffix");
  check(!ApplicationCatalog.Matches(edge with{Name="GitHub Desktop"},["Git"]),"Short application names cannot match a different product prefix");
  var sharex=recipe with{Id="app-sharex",Url="https://github.com/ShareX/ShareX/releases/download/v21.0.0/ShareX-21.0.0-setup-x64.exe"};var mpc=recipe with{Id="app-mpchc",Url="https://github.com/clsid2/mpc-hc/releases/download/2.8.4/MPC-HC.2.8.4.x64.exe"};check(AppDownloads.HashOnlyAllowed(sharex)&&AppDownloads.HashOnlyAllowed(mpc)&&!AppDownloads.HashOnlyAllowed(sharex with{Url="https://github.com/unrelated/project/releases/download/v21/ShareX.exe"})&&!AppDownloads.HashOnlyAllowed(mpc with{ExpectedSha256=""}),"ShareX and MPC-HC hash validation is restricted to exact manufacturer repositories and digests");
 }
}

