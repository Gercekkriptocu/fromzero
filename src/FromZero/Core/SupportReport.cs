namespace FromZero;
public static class SupportReport
{
 public static object Build(string appVersion,IReadOnlyList<DeviceInfo> devices,IReadOnlyList<DriverInfo> drivers,int apps,PreparationJob? job,IReadOnlyList<SourceAttempt> sources,NetworkQualityReport? quality)
 {
  var present=devices.Where(d=>d.Present).ToArray();
  var classes=new[]{"Net","Display","Bluetooth","MEDIA","System","USB"};
  return new
  {
   Schema=1,Created=DateTimeOffset.Now,AppVersion=appVersion,WindowsBuild=Environment.OSVersion.Version.Build,Architecture=System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString(),
   Hardware=new{Present=present.Length,ByClass=classes.ToDictionary(c=>c,c=>present.Count(d=>d.Class.Equals(c,StringComparison.OrdinalIgnoreCase))),ProblemCodes=present.Where(d=>d.Problem!=0).GroupBy(d=>d.Problem).ToDictionary(g=>g.Key,g=>g.Count()),Drivers=drivers.Count,SignedDrivers=drivers.Count(d=>d.IsSigned)},
   InstalledAppCount=apps,
   Preparation=job==null?null:new{State=job.State switch{"running"=>"running","paused"=>"paused","reboot"=>"reboot","attention"=>"attention","complete"=>"complete",_=>"other"},Steps=job.Steps.Count,Completed=job.Steps.Count(PreparationPolicy.Done),Failed=job.Steps.Count(s=>s.State is "attention" or "failed"),job.RebootRequired},
   Sources=new{Checked=sources.Count,Verified=sources.Count(s=>s.Status=="Verified"),Unavailable=sources.Count(s=>s.Status=="Unavailable")},
   Connection=quality==null?null:new{quality.Checked,GatewayReplies=quality.Gateway.Received,TcpReplies=quality.DirectTcp.Received,DnsReplies=quality.Dns.Received,GatewayMedianMs=quality.Gateway.MedianMs}
  };
 }
}
