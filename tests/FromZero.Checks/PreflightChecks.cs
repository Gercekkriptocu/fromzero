using FromZero;
static class PreflightChecks
{
 public static void Run(Action<bool,string> check)
 {
  var required=PreparationPreflight.MinimumSystemBytes;var low=PreparationPreflight.Evaluate("C:\\",required-1,true,true,false);
  check(!low.Ready&&low.MissingBytes==1&&low.Blockers.Count==1,"Preflight blocks even a one-byte system-disk deficit rather than rounding it into success");
  check(low.Message.Contains("C:\\")&&low.Message.Contains("MB")&&low.Message.Contains("gerekli"),"Low-space feedback names the actual system drive, required size and missing space");
  check(PreparationPreflight.Evaluate("C:\\",required,true,true,false).Ready,"The exact five-GiB system-space boundary is accepted without lowering the existing requirement");
  check(!PreparationPreflight.Evaluate("C:\\",(long)(4.98*1024*1024*1024),true,true,false).Ready,"The user's 4.98-GiB condition is diagnosed as a disk blocker, not a permission or worker-startup failure");
  check(!PreparationPreflight.Evaluate("C:\\",required,true,false,false).Ready&&PreparationPreflight.Evaluate("C:\\",required,true,false,true).Ready,"An offline source can satisfy the connectivity prerequisite while preserving the disk rule");
  check(!PreparationPreflight.Evaluate("C:\\",required-1,true,false,true).Ready,"An offline folder or a different disk cannot bypass the Windows system-space guard");
  check(PreparationPreflight.Evaluate("C:\\",required,false,false,false).Blockers.Count==2,"Unsupported architecture and missing connectivity are reported together");
  check(PreparationPreflight.Evaluate("C:\\",-100,true,true,false).AvailableBytes==0,"Invalid negative free-space input fails closed");
  check(PreparationPreflight.Evaluate("C:\\",required+1024,true,true,false).Message.Contains("kaynak"),"A local gateway is not advertised as verified Internet access");
 }
}
