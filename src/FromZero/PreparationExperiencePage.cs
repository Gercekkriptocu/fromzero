using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
namespace FromZero;
public partial class MainWindow
{
 private string preparationView="setup";
 private string preparationRecoveryId="";
 private bool preparationSync;private Action? preparationSelectionRefresh;
 private PreparationSnapshot? preparationSnapshot;
 private ReadinessReport? preparationReadiness;
 private StackPanel? preparationOptionsPanel;
 private StackPanel? preparationPreflightPanel;
 private void Preparation()
 {
  if(preparationJob?.State!="running")World.PreparationWork(-1,"idle");
  Header("FROMZERO / HAZIRLIK","Bilgisayarın, sana hazır.","Donanımını tanıyan kurulum. Ölçülen sonuçlar. Geri alınabilir tercihler.");
  Page.Children.Add(Row(Action("Hazırla",()=>PreparationViewAsync("setup"),preparationView=="setup"),Action("Durumu kontrol et",()=>PreparationViewAsync("check"),preparationView=="check"),Action("USB ve kurtarma",()=>PreparationViewAsync("recovery"),preparationView=="recovery")));
  if(preparationView=="check"){PreparationCheckView();return;}if(preparationView=="recovery"){PreparationRecoveryView();return;}
  preparationPreflightPanel=Stack();Page.Children.Add(preparationPreflightPanel);RenderPreparationPreflight();
  var profiles=new ComboBox{MinWidth=190,MaxWidth=340,HorizontalAlignment=HorizontalAlignment.Left,ItemsSource=PreparationExperience.Profiles,DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=preparationOptions.Profile};
  var profileDescription=T(PreparationExperience.Profiles.FirstOrDefault(p=>p.Id==preparationOptions.Profile)?.Description??"Özel seçimler",13,"#B9CBC1");
  profiles.SelectionChanged+=(_,_)=>{if(profiles.SelectedValue is not string id)return;preparationOptions=PreparationExperience.Profile(id);PreparationSelectionChanged();Navigate("prepare");};
  Page.Children.Add(Card(Stack(T("Nasıl kullanacaksın?",20,"#D8E8D3",true),profiles,profileDescription)));
  var pending=preparationJob!=null&&PreparationPolicy.Pending(preparationJob);
  Page.Children.Add(Row(Action(pending?"Hazırlamaya devam et":"Bilgisayarımı hazırla",StartPreparationAsync,true),Action("Planı incele",ReviewPreparationAsync)));
  preparationStatus=T("",13,"#B9CBC1");preparationRows=Stack();Page.Children.Add(Card(preparationStatus));Page.Children.Add(preparationRows);RenderPreparationJob();
  preparationOptionsPanel=Stack();void Toggle(StackPanel panel,string label,bool value,Action<bool> change){var box=new CheckBox{Content=label,IsChecked=value,Margin=new Thickness(0,4,0,6)};box.Checked+=(_,_)=>{change(true);PreparationSelectionChanged();};box.Unchecked+=(_,_)=>{change(false);PreparationSelectionChanged();};panel.Children.Add(box);}
  var apps=Stack(T(PreparationExperience.Apps.Count+" uygulamada kontrollü otomatik kurulum / güncelleme. Zaten güncel olanlar atlanır.",13,"#B9CBC1"));
  var filter=new TextBox{ToolTip="Uygulama ara",Margin=new Thickness(0,4,0,10)};apps.Children.Add(filter);List<(PreparationApp App,CheckBox Box)> appBoxes=[];
  foreach(var category in PreparationExperience.Apps.GroupBy(a=>a.Category)){apps.Children.Add(T(category.Key,13,"#B9E2A7",true));foreach(var app in category){var box=new CheckBox{Content=app.Name,IsChecked=preparationOptions.Apps.Contains(app.Id),Margin=new Thickness(0,2,0,5)};void Set(bool selected){if(preparationSync)return;var ids=preparationOptions.Apps.ToList();ids.Remove(app.Id);if(selected)ids.Add(app.Id);preparationOptions=preparationOptions with{Apps=ids};PreparationSelectionChanged();}box.Checked+=(_,_)=>Set(true);box.Unchecked+=(_,_)=>Set(false);apps.Children.Add(box);appBoxes.Add((app,box));}}
  filter.TextChanged+=(_,_)=>{foreach(var entry in appBoxes)entry.Box.Visibility=(entry.App.Name+" "+entry.App.Category).Contains(filter.Text.Trim(),StringComparison.CurrentCultureIgnoreCase)?Visibility.Visible:Visibility.Collapsed;};
  var appsExpander=PreparationExpander("Uygulamalar · "+preparationOptions.Apps.Count+" seçili",apps);preparationOptionsPanel.Children.Add(appsExpander);
  var games=Stack(T("Seçtiğin oyunların başlatıcısı, kütüphanesi ve gereksinimleri kontrol edilir. Oyun indirmesi / hesap girişi kendiliğinden başlatılmaz.",13,"#B9CBC1"));
  foreach(var game in PreparationExperience.Games)Toggle(games,game.Name,preparationOptions.Games.Contains(game.Id),selected=>{preparationOptions=PreparationExperience.ChooseGame(preparationOptions,game.Id,selected);});
  games.Children.Add(Action("Inspect a game executable",()=>InspectGameDependenciesAsync(true)));
  if(preparationOptions.RequiredLibraries.Count>0)games.Children.Add(Stack(T(string.Join(", ",preparationOptions.RequiredLibraries),13,"#B9E2A7"),Action("Clear runtime selection",()=>{preparationOptions=preparationOptions with{RequiredLibraries=[]};PreparationSelectionChanged();Navigate("prepare");return Task.CompletedTask;})));
  var gamesExpander=PreparationExpander("Oyunlar · "+preparationOptions.Games.Count+" seçili",games);preparationOptionsPanel.Children.Add(gamesExpander);
  preparationSelectionRefresh=()=>{preparationSync=true;try{appsExpander.Header="Uygulamalar · "+preparationOptions.Apps.Count+" seçili";gamesExpander.Header="Oyunlar · "+preparationOptions.Games.Count+" seçili";foreach(var item in appBoxes)item.Box.IsChecked=preparationOptions.Apps.Contains(item.App.Id);}finally{preparationSync=false;}};
  var preferences=Stack(T("Bu tercihler yalnız seçildiğinde uygulanır. Güç planı ve desteklenen oyun dosyaları önce yedeklenir.",13,"#B9CBC1"));
  var power=new ComboBox{ItemsSource=new[]{new{Id="none",Name="Güç planını değiştirme"},new{Id="balanced",Name="Dengeli"},new{Id="performance",Name="Yüksek performans · adaptör gerekli"},new{Id="quiet",Name="Güç tasarrufu"}},DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=preparationOptions.PowerPreference,HorizontalAlignment=HorizontalAlignment.Stretch};
  power.SelectionChanged+=(_,_)=>{preparationOptions=preparationOptions with{PowerPreference=(string)power.SelectedValue};PreparationSelectionChanged();};preferences.Children.Add(power);
  var graphics=new ComboBox{Margin=new Thickness(0,8,0,4),ItemsSource=new[]{new{Id="none",Name="Oyun görüntü ayarını değiştirme"},new{Id="balanced",Name="Seçili CS2 / VALORANT · dengeli"},new{Id="fps",Name="Seçili CS2 / VALORANT · rekabetçi FPS"}},DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=preparationOptions.GraphicsPreference,HorizontalAlignment=HorizontalAlignment.Stretch};
  graphics.SelectionChanged+=(_,_)=>{preparationOptions=preparationOptions with{GraphicsPreference=(string)graphics.SelectedValue};PreparationSelectionChanged();};preferences.Children.Add(graphics);preferences.Children.Add(T("Modern Standby / üretici planları farklı olabilir. Grafik dosyası için oyun kapalı olmalı ve daha önce açılmış olmalı. FPS kazancı ölçülmeden sayı verilmez.",12,"#9AAFAE"));
  preferences.Children.Add(Row(Action("GPU ve ekran ayarları",()=>{Navigate("performance");return Task.CompletedTask;}),Action("Oyun merkezi",()=>{Navigate("gaming");return Task.CompletedTask;})));
  preparationOptionsPanel.Children.Add(PreparationExpander("Güç ve görüntü tercihleri",preferences));
  var system=Stack();Toggle(system,"Donanıma uygun sürücüleri kur",preparationOptions.Drivers,v=>preparationOptions=preparationOptions with{Drivers=v});Toggle(system,"Windows güvenlik ve kararlılık güncellemeleri",preparationOptions.WindowsUpdates,v=>preparationOptions=preparationOptions with{WindowsUpdates=v});Toggle(system,"Eski DirectX ve Visual C++ oyun bileşenleri",preparationOptions.GamingLibraries,v=>preparationOptions=preparationOptions with{GamingLibraries=v});Toggle(system,"Ekran kartının resmi uygulamasını kur / kontrol et",preparationOptions.GpuApps,v=>preparationOptions=preparationOptions with{GpuApps=v});Toggle(system,"Uyumlu üretici aracını kullan",preparationOptions.OemDrivers,v=>preparationOptions=preparationOptions with{OemDrivers=v});Toggle(system,"Yeniden açılışta kaldığım yeri göster",preparationOptions.ResumeAtLogon,v=>preparationOptions=preparationOptions with{ResumeAtLogon=v});
  Toggle(system,"Visual C++ v14 compatibility libraries (x64 and x86)",preparationOptions.CoreLibraries,v=>preparationOptions=preparationOptions with{CoreLibraries=v});
  system.Children.Add(Row(Action("USB sürücü klasörü seç",()=>{var folder=PickFolder("FromZero USB paketinin kök klasörünü veya sürücü klasörünü seç");if(folder!=null){preparationOptions=preparationOptions with{OfflineFolder=folder};PreparationSelectionChanged();Navigate("prepare");}return Task.CompletedTask;}),Action("USB seçimini kaldır",()=>{preparationOptions=preparationOptions with{OfflineFolder=""};PreparationSelectionChanged();Navigate("prepare");return Task.CompletedTask;})));
  if(preparationOptions.OfflineFolder.Length>0)system.Children.Add(T(preparationOptions.OfflineFolder,12,"#B9CBC1"));preparationOptionsPanel.Children.Add(PreparationExpander("Sistem ve kurulum seçenekleri",system));Page.Children.Add(preparationOptionsPanel);
  if(preparationSnapshot!=null)PreparationHardwareView(preparationSnapshot);
 }
 private static Expander PreparationExpander(string label,UIElement content)=>new(){Header=label,Content=content,Foreground=B("#D8E8D3"),Margin=new Thickness(0,6,0,10),Padding=new Thickness(0,8,0,8)};
 private static string PreparationPowerLabel(string id)=>id switch{"balanced"=>"dengeli","performance"=>"yüksek performans","quiet"=>"güç tasarrufu",_=>"değiştirme"};
 private static string PreparationGraphicsLabel(string id)=>id switch{"balanced"=>"dengeli","fps"=>"rekabetçi FPS",_=>"değiştirme"};
 private Task PreparationViewAsync(string view){preparationView=view;Navigate("prepare");return Task.CompletedTask;}
 private void PreparationSelectionChanged(){if(preparationSync)return;preparationJob=null;preparationReadiness=null;preparationSelectionRefresh?.Invoke();RenderPreparationPreflight();RenderPreparationJob();}
 private void RenderPreparationPreflight()
 {
  if(preparationPreflightPanel==null)return;preparationPreflightPanel.Children.Clear();var report=PreparationBackend.InspectPreflight(preparationOptions);
  if(report.Ready){preparationPreflightPanel.Children.Add(T("Başlatmaya hazır • "+report.SpaceLabel,13,"#B9E2A7"));return;}
  var content=Stack(T("Hazırlama henüz başlayamıyor",18,"#EBC078",true),T(report.Message,14,"#E8EEE5"));var actions=Row(Action("Tekrar kontrol et",()=>{RenderPreparationPreflight();return Task.CompletedTask;}));
  if(report.MissingBytes>0){actions.Children.Add(Action("Temizlikle alan aç",()=>{Navigate("cleanup");return Task.CompletedTask;}));actions.Children.Add(Action("Windows depolama",()=>{Commands.Open("ms-settings:storagesense");return Task.CompletedTask;}));}
  if(!report.Network&&!report.Offline)actions.Children.Add(Action("Wi-Fi ve ağ",()=>{Navigate("network");return Task.CompletedTask;}));content.Children.Add(actions);preparationPreflightPanel.Children.Add(Card(content,"#342C25"));
 }
 private void RenderPreparationJob()
 {
  if(preparationStatus==null||preparationRows==null)return;preparationRows.Children.Clear();var job=preparationJob;if(job==null){preparationStatus.Text="Profilini ve isteğe bağlı tercihlerini seç. Planı inceleyebilir veya hazırlamayı başlatabilirsin.";return;}
  var finished=job.Steps.Count(PreparationPolicy.Done);preparationStatus.Text=(job.State=="attention"?"Son deneme durdu • ":"")+finished+" / "+job.Steps.Count+" adım tamamlandı • "+job.Message;
  var current=job.Steps.FirstOrDefault(s=>s.State=="running");World.PreparationWork(current==null?-1:PreparationExperience.Sector(current),job.State=="complete"?"done":job.State=="attention"?"attention":current!=null?"working":"idle");
  foreach(var phase in job.Steps.GroupBy(PreparationExperience.Phase)){
   var rows=Stack();foreach(var step in phase){var state=step.State switch{"done"=>"Tamamlandı","skipped"=>"Gerekmedi","running"=>"Çalışıyor","failed"=>"Başarısız","attention"=>"Kontrol gerekli","reboot"=>"Yeniden başlat",_=>"Sırada"};var row=Stack(T(step.Title,14,"#D8E8D3",true),T(state,12,step.State is "failed" or "attention"?"#EBC078":"#B9CBC1"));if(step.Detail.Length>0)row.Children.Add(T(step.Detail,12,"#9AAFAE"));rows.Children.Add(Card(row));}
   preparationRows.Children.Add(new Expander{Header=phase.Key+" · "+phase.Count(PreparationPolicy.Done)+" / "+phase.Count(),IsExpanded=phase.Any(s=>s.State is "running" or "attention" or "failed" or "reboot"),Foreground=B("#D8E8D3"),Content=rows,Margin=new Thickness(0,2,0,7)});
  }
  if(preparationOptionsPanel!=null)preparationOptionsPanel.IsEnabled=job.State!="running";
 }
 private async Task PlanPreparationAsync()
 {
  PreparationPolicy.ValidateOptions(preparationOptions);preparationSnapshot=await PreparationDiagnostics.CaptureAsync();preparationJob=PreparationPolicy.Create(preparationOptions,PreparationBackend.Sid,Environment.ProcessPath!);
  preparationJob.Message=preparationSnapshot.Manufacturer+" "+preparationSnapshot.Model+" • "+preparationSnapshot.Devices.Count(d=>d.Present)+" aygıt • "+preparationOptions.Apps.Count(id=>!PreparationPackages.Present(PreparationPackages.RecipeId(id),preparationSnapshot.Apps))+" seçili uygulama eksik • güç: "+PreparationPowerLabel(preparationOptions.PowerPreference)+" • görüntü: "+PreparationGraphicsLabel(preparationOptions.GraphicsPreference);PreparationStore.Save(preparationJob);Navigate("prepare");
 }
 private async Task ReviewPreparationAsync()
 {
  if(preparationJob==null)await PlanPreparationAsync();
  Detail("Preparation plan details",await PreparationPlanDescriptionAsync(preparationJob!));
 }
 private async Task<string> PreparationPlanDescriptionAsync(PreparationJob job)
 {
  preparationSnapshot??=await PreparationDiagnostics.CaptureAsync();
  return PreparationAdvice.Describe(PreparationAdvice.Build(job,preparationSnapshot,GameComponents.Scan(preparationSnapshot.Apps)));
 }
 private async Task InspectOfflineDriversAsync()
 {
  var folder=PickFolder("Select an offline driver kit or driver folder");if(folder==null)return;
  var actual=await inventory.DevicesAsync();var index=OfflineDriverCatalog.Read(folder)??await OfflineDriverCatalog.BuildAsync(folder,actual);
  var errors=await OfflineDriverCatalog.VerifyAsync(folder,index,actual);
  errors.AddRange(await OfflineDriverCatalog.VerifyCapsuleAsync(folder));
  Detail("Offline driver coverage",OfflineDriverCatalog.Describe(index,actual)+(errors.Count==0?"":"\n\n"+string.Join("\n",errors.Take(20))));
 }
 private void PreparationHardwareView(PreparationSnapshot snapshot)
 {
  var recipe=HardwareRecipes.Build(snapshot.Manufacturer,snapshot.Model,snapshot.Portable,snapshot.Devices);var rows=Stack(T(recipe.Model,16,"#B9E2A7",true),T(recipe.Kind,12,"#9AAFAE"),T("Önce yedek → varsa eşleşen USB ağ sürücüsü → Windows sürücü teklifleri → desteklenen üretici aracı → Windows / bileşen / uygulama → son kontrol. USB paketlerinde tam donanım kimliği ve imzalı CAT aranır; Windows çevrimiçi teklifleri kendi uyumluluk sıralamasını yapar.",13,"#B9CBC1"));foreach(var item in recipe.Items)rows.Children.Add(Card(Stack(T(item.Area+" · "+item.Choice,14,"#D8E8D3",true),T(item.Basis,12,"#9AAFAE"))));Page.Children.Add(PreparationExpander("Bu bilgisayara özel kurulum tarifi",rows));
 }
 private void PreparationCheckView()
 {
  Page.Children.Add(Card(Stack(T("Gerçek durumunu kontrol et",22,"#B9E2A7",true),T("Kurulum başlatmadan seçili profilin bağlantı, sürücü, ses, uygulama ve oyun durumunu tara.",13,"#B9CBC1"),Row(Action("Şimdi kontrol et",async()=>{preparationSnapshot=await PreparationDiagnostics.CaptureAsync();preparationReadiness=await PreparationDiagnostics.InspectAsync(preparationOptions,preparationSnapshot);if(preparationJob!=null)await PreparationDiagnostics.SaveReportAsync(preparationJob,preparationSnapshot,preparationReadiness);Navigate("prepare");},true)))));
  var report=preparationReadiness??(preparationJob!=null?PreparationRecovery.Read<ReadinessReport>(preparationJob,"readiness"):null);
  if(report!=null){Page.Children.Add(T(report.Passed+" doğrulandı · "+report.Attention+" ilgilenilmeli · "+report.Manual+" kullanıcı testi / "+report.Checked.ToLocalTime().ToString("g"),14,"#B9E2A7",true));foreach(var group in report.Checks.GroupBy(c=>c.Area)){
   var rows=Stack();foreach(var check in group){var state=check.State switch{"pass"=>"Doğrulandı","attention"=>"İlgilenilmeli","manual"=>"Kullanıcı testi","not-applicable"=>"Uygulanamaz",_=>"Okunamadı"};rows.Children.Add(Stack(T(check.Label+" · "+state,14,check.State=="pass"?"#B9E2A7":"#EBC078",true),T(check.Evidence,12,"#B9CBC1")));}Page.Children.Add(new Expander{Header=group.Key+" · "+group.Count(),Content=rows,IsExpanded=group.Any(c=>c.State=="attention"),Foreground=B("#D8E8D3"),Margin=new Thickness(0,8,0,8)});
  }}
  Page.Children.Add(PreparationExpander("Elle doğrula ve düzelt",Stack(T("Ses testi yalnız sen doğruladığında duyuldu olarak kaydedilir. Mikrofon sesi kaydedilmez.",13,"#B9CBC1"),Row(Action("Test sesini çal",()=>{SystemSounds.Asterisk.Play();return Task.CompletedTask;}),Action("Sesi duydum",()=>SavePreparationAudioTestAsync(true)),Action("Ses gelmedi",()=>SavePreparationAudioTestAsync(false))),Row(Action("Ağ onarımına git",()=>{Navigate("network");return Task.CompletedTask;}),Action("GPU / ekran",()=>{Navigate("performance");return Task.CompletedTask;}),Action("Oyun ve CS2 seçenekleri",()=>{Navigate("gaming");return Task.CompletedTask;})))));
  if(preparationSnapshot!=null)PreparationHardwareView(preparationSnapshot);
 }
 private async Task SavePreparationAudioTestAsync(bool heard)
 {
  if(preparationReadiness==null)throw new InvalidOperationException("Önce Şimdi kontrol et ile durumu tara.");var checks=preparationReadiness.Checks.Where(c=>c.Id!="audio-heard").ToList();checks.Add(new("audio-heard","Ses","Sesi duyma testi",heard?"pass":"attention","Kullanıcı bildirimi: "+(heard?"ses duyuldu":"ses gelmedi")+" / "+DateTimeOffset.Now.ToString("g")));preparationReadiness=new(DateTimeOffset.Now,checks);if(preparationJob!=null)await PreparationRecovery.WriteAsync(preparationJob,"readiness",preparationReadiness);Navigate("prepare");
 }
 private void PreparationRecoveryView()
 {
  Page.Children.Add(Card(Stack(T("USB hazırlama kiti",22,"#B9E2A7",true),T("Mevcut sürücüleri ve seçili desteklenen kurucuları dış diske hazırla. Eksik indirilen paketler raporda ayrı belirtilir.",13,"#B9CBC1"),Row(Action("USB kiti oluştur",ExportPreparationAsync,true),Action("Inspect offline driver kit",InspectOfflineDriversAsync)))));
  var jobs=PreparationStore.Recent(PreparationBackend.Sid);var job=jobs.FirstOrDefault(j=>j.Id==preparationRecoveryId)??preparationJob??jobs.FirstOrDefault();
  if(job!=null&&jobs.Count>1){var history=new ComboBox{ItemsSource=jobs.Select(j=>new{j.Id,Name=j.Created.ToLocalTime().ToString("g")+" · "+(PreparationExperience.Profiles.FirstOrDefault(p=>p.Id==j.Options.Profile)?.Name??"Özel")+" · "+j.Steps.Count(PreparationPolicy.Done)+" / "+j.Steps.Count}),DisplayMemberPath="Name",SelectedValuePath="Id",SelectedValue=job.Id,Margin=new Thickness(0,4,0,12)};history.SelectionChanged+=(_,_)=>{preparationRecoveryId=(string)history.SelectedValue;Navigate("prepare");};Page.Children.Add(history);}
  if(job==null){Page.Children.Add(T("Hazırlama planı oluşturduğunda yedekler ve önce / sonra farkları burada görünür.",14,"#B9CBC1"));return;}
  var recoveryActions=Row(Action("Rapor klasörünü aç",()=>{Commands.Open(PreparationStore.Folder(job.Id));return Task.CompletedTask;}),Action("Oyun ayarlarını geri al",()=>{Navigate("gaming");return Task.CompletedTask;}));if(PreparationRecovery.Read<PreparationPowerBackup>(job,"power-backup")!=null)recoveryActions.Children.Add(Action("Güç planını geri al",async()=>{Detail("Güç planı",await PreparationRecovery.RestorePowerAsync(job));Navigate("prepare");}));Page.Children.Add(recoveryActions);
  Page.Children.Add(T("Sürücü yedeği yalnız dışa aktarılmış üçüncü taraf paketlerini içerir. Otomatik genel sürücü / Windows geri alma yapılmaz. Önce aygıtın kimliğini kontrol et; Sürücüler ekranından eşleşen imzalı INF kullan veya Windows Aygıt Yöneticisi'nde mevcutsa Sürücüyü Geri Al seçeneğini aç.",13,"#9AAFAE"));
  Page.Children.Add(Row(Action("Sürücü kurtarma",()=>{Navigate("drivers");return Task.CompletedTask;}),Action("Aygıt Yöneticisi",()=>{System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"devmgmt.msc")){UseShellExecute=true});return Task.CompletedTask;}),Action("Ağ kontrolü",()=>{Navigate("network");return Task.CompletedTask;})));
  recoveryActions.Children.Add(Action("Attempt history",()=>{var attempts=PreparationAudit.Read(job);Detail("Attempt history",attempts.Count==0?"No attempts recorded by this version.":string.Join("\n\n",attempts.Select(a=>$"{a.Checked:g} · {a.Step} · {a.Attempt}\n{a.Phase} / {a.State}\n{a.Detail}")));return Task.CompletedTask;}));
  var checkpoints=PreparationRecovery.Checkpoints(job);var baseline=PreparationRecovery.Read<List<PreparationChange>>(job,"before-after");
  if(baseline!=null)Page.Children.Add(PreparationChangesView("Kurulum öncesi / son durum",baseline));foreach(var checkpoint in checkpoints)Page.Children.Add(PreparationChangesView(checkpoint.Step+" sonrası / "+checkpoint.Checked.ToLocalTime().ToString("g"),checkpoint.Changes));
  if(checkpoints.Count==0&&baseline==null)Page.Children.Add(T("Henüz karşılaştırma kaydı yok. Kurulum başladığında hassas adımların öncesi ve sonrası kaydedilir.",13,"#B9CBC1"));Page.Children.Add(T("Bir adımdan sonra görülen fark, o adımın kesin neden olduğunu göstermez. USB aygıtının çıkarılması gibi dış değişiklikler de rapora yansır.",12,"#9AAFAE"));
 }
 private static Expander PreparationChangesView(string title,List<PreparationChange> changes){var rows=Stack();foreach(var change in changes)rows.Children.Add(Stack(T(change.Area+" · "+change.Title+(change.Regression?" · kontrol et":""),14,change.Regression?"#EBC078":"#D8E8D3",true),T(change.Before+" → "+change.After,12,"#9AAFAE")));if(changes.Count==0)rows.Children.Add(T("Bu karşılaştırmada durum değişikliği görülmedi.",13,"#B9CBC1"));return new(){Header=title+" · "+changes.Count+" fark",Content=rows,IsExpanded=changes.Any(c=>c.Regression),Foreground=B("#D8E8D3"),Margin=new Thickness(0,8,0,8)};}
}





