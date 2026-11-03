using FromZero;
static class CrosshairChecks
{
 public static void Run(Action<bool,string> check)
 {
  const string known="CSvbPubOq37zTGqtsPTP5QTrp5CB4xFXiKRLfzJsm49ZRe";var decoded=CrosshairCode.Decode(known);
  check(decoded.Style==2&&decoded.Red==255&&decoded.Green==0&&decoded.Blue==0&&decoded.Dot&&decoded.Length==5&&decoded.Thickness==1&&decoded.ScreenHeight==768,"CS2 codec decodes the upstream reference vector");
  check(CrosshairCode.Encode(decoded)==known,"CS2 code encoding matches upstream canonical reference exactly");
  var changed=decoded with{Gap=-7,Outline=2,OutlineRed=15,OutlineGreen=40,OutlineBlue=80,Recoil=true,TStyle=true,ScopeColor=true,ScopeScale=1.75,ScreenHeight=1440};
  check(CrosshairCode.Decode(CrosshairCode.Encode(changed))==changed,"Crosshair code preserves signed gap, outline, scope preferences and flags");
  bool Reject(Action action){try{action();return false;}catch(InvalidDataException){return true;}}
  check(Reject(()=>CrosshairCode.Decode("CS"+new string('A',44)))&&Reject(()=>CrosshairCode.Decode(known[..^1]+"A")),"Crosshair decoder refuses unsupported version and checksum corruption");
  check(Reject(()=>CrosshairCode.Decode("CS"+new string('9',44)))&&Reject(()=>CrosshairCode.Decode("CSGO-b57wT-uvT3A-LJdaE-uSduy-f8KvM")),"Crosshair decoder refuses overflow and legacy containers");
  check(Reject(()=>CrosshairCode.Encode(decoded with{InnerAlpha=double.NaN}))&&Reject(()=>CrosshairCode.Encode(decoded with{ScreenHeight=0})),"Crosshair encoder rejects invalid ranges and nonfinite values");
  var entries=CrosshairCatalog.Read();check(entries.Count==50&&entries.Any(e=>e.Snapshot.Id=="xantares")&&entries.Any(e=>e.Snapshot.Name=="woxic"),"Bundled catalog loads fifty sourced pro players including Turkish players");
  check(entries.All(e=>CrosshairCode.Decode(e.Code)==e.Settings)&&entries.Select(e=>e.Snapshot.Id).Distinct().Count()==entries.Count,"Every catalog code round-trips to exactly the preview parameters");
  check(entries.All(e=>!string.IsNullOrWhiteSpace(e.Snapshot.Team))&&
   CrosshairCatalog.Entry(entries[0].Snapshot with{Team=""}).Snapshot.Team.Length>0,
   "All CS2 pro profiles have team data, including older cached snapshots");
  var donk=entries.Single(e=>e.Snapshot.Id=="donk");check(donk.Settings is{Red:0,Green:255,Blue:0,Length:2,Thickness:2,Gap:1,ScreenHeight:960},"Donk catalog snapshot matches current source fields");
  check(Reject(()=>CrosshairCatalog.Entry(donk.Snapshot with{Source="https://example.invalid/"})),"Catalog rejects a substituted player source");
  var fields=new Dictionary<string,string>(donk.Snapshot.Fields);fields.Remove("cl_crosshair_screen_height");check(Reject(()=>CrosshairCatalog.Settings(fields)),"Incomplete source tables cannot silently create crosshair codes");
  check(Reject(()=>CrosshairCatalog.Parse("donk","<html>schema changed</html>")),"Changed source markup is refused without replacing the prior catalog entry");
  var faceit=FaceitCrosshairs.Read();var ranks=FaceitCatalog.Read();
  check(faceit.Count==11&&faceit.Any(c=>c.Nickname=="KAD1M")&&FaceitCrosshairs.MatchPlayer(faceit,new(1,"changed-name",4800,"EU","","","",Guid.Parse("89aee03d-1762-4b48-99b9-61ff5827dd3f")))?.Nickname=="KAD1M","KAD1M is sourced and remains matched after a nickname change");
  check(faceit.All(c=>CrosshairCode.Encode(CrosshairCode.Decode(c.Code))==c.Code),"All FACEIT codes pass checksum validation and canonical round-trip");
  check(FaceitCrosshairs.TeamFor(faceit.Single(c=>c.Nickname=="KAD1M"))==("LPH","2026-11-04")&&
   FaceitCrosshairs.TeamFor(faceit.Single(c=>c.Nickname=="bluewh1te")).Team=="BET-M"&&
   FaceitCrosshairs.TeamFor(null)==("",""),"FACEIT team names and dates come from verified records only");
  check(faceit.Single(c=>c.Nickname=="CEMEN_BAKIN").Slug=="kyousuke"&&faceit.Single(c=>c.Nickname=="donk666").Slug=="donk"&&faceit.Single(c=>c.Nickname=="nipl").Slug=="npl","FACEIT account aliases map explicitly to the correct source players");
  var account=faceit[0];
  string Page(string name,string code)=>"\"alternateName\": \""+name+"\", \"dateModified\": \"2026-10-26\"<code class=\"pl-x-code\">"+code+"</code><p class=\"pl-x-seen\">Seen in a match <span>Oct 26, 2026</span></p>";
  check(FaceitCrosshairs.Parse(account,Page(account.PlayerName,account.Code)).Code==account.Code,"FACEIT refresh reads the featured code rather than a historical crosshair");
  check(Reject(()=>FaceitCrosshairs.Parse(account,Page("different-player",account.Code))),"FACEIT refresh rejects a changed player identity");
  check(Reject(()=>FaceitCrosshairs.Parse(account,Page(account.PlayerName,"CSGO-b57wT-uvT3A-LJdaE-uSduy-f8KvM"))),"FACEIT refresh refuses unsupported legacy codes");
  check(Reject(()=>FaceitCrosshairs.Validate(account with{Source="https://example.invalid/"})),"FACEIT crosshair cache rejects source substitution");
  check(Reject(()=>FaceitCrosshairs.Parse(account,"schema changed")),"Changed FACEIT crosshair markup preserves the prior record");
  check(ValorantCrosshairs.All.Count==50&&ValorantCrosshairs.All.All(e=>e.Code.StartsWith("0;"))&&Reject(()=>ValorantCrosshairs.Decode("0;P;0l;")),"VALORANT catalogue contains 50 validated import codes and rejects truncated profiles");
  check(ValorantCrosshairs.All.All(e=>!string.IsNullOrWhiteSpace(e.Team))&&
   ValorantCrosshairs.All.Any(e=>e.Id=="f0rsaken"&&e.Team=="Paper Rex")&&
   ValorantCrosshairs.All.Any(e=>e.Id=="ethos"&&e.Team=="Content Creator"),
   "Every VALORANT record includes a sourced team or profile status");
  check(WorldStations.ForPage("crosshairs").Page=="gaming"&&WorldStations.ForPage("valorant-crosshairs").Page=="gaming","Inline crosshair catalog keeps the gaming world destination");
 }
}
