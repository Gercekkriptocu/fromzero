using System.Text;
using FromZero;
internal static class GraphicsChecks
{
 public static async Task Run(string root,Action<bool,string> check)
 {
  var cfg=Path.Combine(root,"Graphics");Directory.CreateDirectory(cfg);var file=Path.Combine(cfg,"cs2_video.txt");
  var text="\"video.cfg\"\r\n{\r\n\t\"Version\" \"16\"\r\n\t\"setting.cpu_level\" \"3\"\r\n\t\"setting.gpu_level\" \"3\"\r\n\t\"setting.gpu_mem_level\" \"3\"\r\n\t\"setting.msaa_samples\" \"4\"\r\n\t\"setting.mat_vsync\" \"1\"\r\n\t\"setting.defaultres\" \"1920\"\r\n\t\"setting.refreshrate_numerator\" \"144\"\r\n}\r\n";
  var original=new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(text)).ToArray();await File.WriteAllBytesAsync(file,original);
  var target=new GraphicsTarget("cs2","fixture",cfg,file);HashSet<string> running=new(StringComparer.OrdinalIgnoreCase);var service=new GameGraphicsService(Path.Combine(root,"GraphicsBackups"),()=>running,[target]);
  var plan=service.Preview(target,"competitive");check(plan.Changes.Count==4&&File.ReadAllBytes(file).SequenceEqual(original),"Graphics preview recognizes CS2 CRLF keys without changing files");
  var updated=Encoding.UTF8.GetString(plan.Updated);check(updated.Contains("\"setting.defaultres\" \"1920\"")&&updated.Contains("\"setting.refreshrate_numerator\" \"144\"")&&updated.Contains("\"setting.mat_vsync\" \"1\""),"CS2 profile preserves resolution, refresh and VSync by default");
  var backup=await service.ApplyAsync(plan);check(service.Active(target)!=null&&File.ReadAllBytes(file).SequenceEqual(plan.Updated),"CS2 profile saves recovery state before applying actual file changes");
  check(service.ProfileMatches(backup,false),"Profile benchmark can verify the applied values in the actual settings format");
  var result=await service.RestoreAsync(backup);check(result.Exact&&File.ReadAllBytes(file).SequenceEqual(original),"Graphics restore reproduces original CS2 bytes and UTF-8 BOM exactly");
  check(!service.ProfileMatches(backup,false),"A restored configuration cannot be imported as an after-profile capture");
  plan=service.Preview(target,"balanced",true);backup=await service.ApplyAsync(plan);await File.WriteAllTextAsync(file,File.ReadAllText(file).Replace("\"setting.cpu_level\" \"1\"","\"setting.cpu_level\" \"2\""));result=await service.RestoreAsync(backup);check(result.Preserved==1&&File.ReadAllText(file).Contains("\"setting.cpu_level\" \"2\""),"Graphics restore preserves a later user setting while restoring applied values");
  await File.WriteAllBytesAsync(file,original);plan=service.Preview(target,"competitive");await File.AppendAllTextAsync(file,"// user edited\r\n");bool stale=false;try{await service.ApplyAsync(plan);}catch(IOException){stale=true;}check(stale&&service.Active(target)==null,"Graphics apply rejects files changed after preview");
  await File.WriteAllBytesAsync(file,original);running.Add("cs2.exe");bool blocked=false;try{service.Preview(target,"competitive");}catch(InvalidOperationException){blocked=true;}check(blocked,"Graphics profile refuses to edit a running game");running.Clear();
  var ini=Path.Combine(cfg,"GameUserSettings.ini");var valorant="[ScalabilityGroups]\nsg.ResolutionQuality=100.000000\nsg.TextureQuality=3\nsg.EffectsQuality=3\nsg.ShadowQuality=2\n[/Script/ShooterGame.ShooterGameUserSettings]\nbUseVSync=True\nResolutionSizeX=1920\nSensitivity=0.5\n[Other]\nsg.TextureQuality=3\n";await File.WriteAllTextAsync(ini,valorant);
  var vtarget=new GraphicsTarget("valorant","fixture-v",cfg,ini);var vservice=new GameGraphicsService(Path.Combine(root,"ValorantBackup"),()=>running,[vtarget]);var vplan=vservice.Preview(vtarget,"competitive",true);
  var value=Encoding.UTF8.GetString(vplan.Updated);check(vplan.Changes.Count==4&&value.Contains("sg.ResolutionQuality=100.000000")&&value.Contains("Sensitivity=0.5")&&value.Contains("[Other]\nsg.TextureQuality=3"),"Valorant profile targets only known quality sections and preserves scaling and input values");
  var vb=await vservice.ApplyAsync(vplan);await vservice.RestoreAsync(vb);check(File.ReadAllText(ini)==valorant,"Valorant profile can apply and restore a generated INI fixture");
  bool duplicate=false;try{GameGraphicsService.Patch("cs2","competitive",text.Replace("\"Version\" \"16\"","\"setting.cpu_level\" \"2\""));}catch(InvalidDataException){duplicate=true;}check(duplicate,"Graphics parser refuses duplicate CS2 setting keys");
  File.SetAttributes(file,FileAttributes.ReadOnly);bool readOnly=false;try{service.Preview(target,"competitive");}catch(IOException){readOnly=true;}finally{File.SetAttributes(file,FileAttributes.Normal);}check(readOnly,"Graphics optimizer never bypasses a read-only game configuration");
  var live=new GameGraphicsService();var targets=live.Discover();foreach(var current in targets.Where(t=>t.Game=="cs2")){try{var before=File.ReadAllBytes(current.Path);var preview=live.Preview(current,"competitive");check(File.ReadAllBytes(current.Path).SequenceEqual(before),"Live CS2 preview reads the real schema without changing user settings");Console.WriteLine("CS2 preview: "+preview.Changes.Count+" changes available; "+preview.Unavailable.Count+" unsupported keys skipped.");}catch(InvalidOperationException){Console.WriteLine("Live CS2 preview skipped because game is running.");}}
 }
}
