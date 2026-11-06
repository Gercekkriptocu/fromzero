using System.Windows;
using System.Windows.Controls;

namespace FromZero;

public partial class MainWindow
{
 private ReleaseDelta? availableFromZero;
 private TextBlock? fromZeroUpdateStatus;
 private Button? fromZeroInstallButton;
 private string fromZeroCheckStatus="Automatically checks GitHub for newer releases.";

 private void ShowFromZeroUpdateCard()
 {
  if(!SelfUpdateService.Supported)
  {
   Page.Children.Add(Card(Stack(T("FromZero app updates",17,"#B9E2A7",true),
    T("In-app updates are available for the standard Windows x64 installation.",12,"#B9CBC1"),
    Row(Link("FromZero releases","https://github.com/Gercekkriptocu/fromzero/releases/latest")))));
   return;
  }
  fromZeroUpdateStatus=T("",13,"#B9CBC1");
  fromZeroInstallButton=Action("Update FromZero and restart",InstallFromZeroUpdateAsync,true);
  var card=Stack(T("FromZero app updates · "+SelfUpdateService.InstalledVersion,17,"#B9E2A7",true),
   fromZeroUpdateStatus,Row(Action("Check for FromZero updates",CheckFromZeroUpdateAsync),fromZeroInstallButton));
  if(SelfUpdateService.LastFailure() is {} failure)
   card.Children.Add(PreparationExpander("The previous FromZero update did not finish",T(failure,12,"#EBC078")));
  Page.Children.Add(Card(card));
  RefreshFromZeroUpdateCard();
 }

 private void RefreshFromZeroUpdateCard()
 {
  if(fromZeroUpdateStatus!=null)fromZeroUpdateStatus.Text=availableFromZero is {} release
   ?Locale.Text("New version available")+ " · "+release.Manifest.Version
   :Locale.Text(fromZeroCheckStatus);
  if(fromZeroInstallButton!=null)fromZeroInstallButton.Visibility=availableFromZero==null?Visibility.Collapsed:Visibility.Visible;
  var version="v"+SelfUpdateService.InstalledVersion+" / ÖN İZLEME";
  VersionLabel.Text=Locale.Text(availableFromZero==null?version:version+" · "+Locale.Text("Update available"));
 }

 private async Task CheckFromZeroOnStartupAsync()
 {
  if(!SelfUpdateService.Supported)return;
  try{await CheckFromZeroUpdateAsync();}
  catch(Exception ex){Journal.Write("FromZero update check",ex.Message,false);}
 }

 private async Task CheckFromZeroUpdateAsync()
 {
  fromZeroCheckStatus="Checking for a FromZero update…";
  RefreshFromZeroUpdateCard();
  try
  {
   availableFromZero=await SelfUpdateService.CheckAsync(CancellationToken.None);
   fromZeroCheckStatus="FromZero is up to date.";
  }
  catch
  {
   fromZeroCheckStatus="Could not check for FromZero updates. Try again.";
   throw;
  }
  finally{RefreshFromZeroUpdateCard();}
 }

 private async Task InstallFromZeroUpdateAsync()
 {
  if(availableFromZero==null)return;
  if(gameService.Session!=null)throw new InvalidOperationException(Locale.Text("Finish the active game session before updating FromZero."));
  using var cancel=new CancellationTokenSource(TimeSpan.FromMinutes(10));
  updateCancellation=cancel;
  CancelOperation.Visibility=Visibility.Visible;
  OperationTitle.Text=Locale.Text("Verifying installed FromZero files…");
  try
  {
   await SelfUpdateService.PrepareAndScheduleAsync(availableFromZero,new Progress<string>(message=>OperationTitle.Text=Locale.Text(message)),cancel.Token);
   Application.Current.Shutdown();
  }
  finally
  {
   updateCancellation=null;
   CancelOperation.Visibility=Visibility.Collapsed;
  }
 }
}
