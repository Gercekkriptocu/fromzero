namespace FromZero;
public record UpdateAdvice(string Title,string Reason,string Action,bool Recommended,string Color);
public static class UpdateAssessment
{
 public static UpdateAdvice For(ApplicationRelease release)=>release.Installed.Length==0?
  new("OPTIONAL · NOT INSTALLED","This app is not installed. It is not required to keep Windows or your drivers up to date.","Install only if you need this app",false,"#9AAFAE"):
  release.NeedsUpdate?new("UPDATE AVAILABLE","An installed version is older than the verified stable release.","Update existing app",true,"#EBC078"):
  release.State=="Güncel veya daha yeni"?new("UP TO DATE","Your installed version is equal to or newer than this source. No installation is needed.","No action needed",false,"#B9E2A7"):
  new("VERSION NOT VERIFIED","The installed version or release could not be compared reliably. This is not proof that an update is required.","Review version details",false,"#C7C2A0");
}
