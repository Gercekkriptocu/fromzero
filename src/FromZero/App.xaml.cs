using System.Windows;
namespace FromZero;
public partial class App : Application
{
 private Mutex? instance;
 private Mutex? previousInstance;
 protected override async void OnStartup(StartupEventArgs e)
 {
  if(e.Args.FirstOrDefault() is "--preparation-worker" or "--preparation-worker-check"){
   ShutdownMode=ShutdownMode.OnExplicitShutdown;var id=e.Args.ElementAtOrDefault(1)??"";
   try{
    if(e.Args.Length!=2)throw new System.IO.InvalidDataException("Hazırlama başlangıç parametreleri geçersiz.");
    var checkOnly=e.Args[0]=="--preparation-worker-check";
    // Let WPF finish its startup before checking that the worker has no UI.
    await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
    if(Windows.Count!=0)throw new InvalidOperationException("Hazırlama çalışanı ayrı bir uygulama penceresi açmamalı.");
    await PreparationWorker.RunAsync(id,checkOnly);
    if(Windows.Count!=0)throw new InvalidOperationException("Hazırlama çalışanında beklenmeyen pencere açıldı.");Shutdown(0);
   }catch(Exception ex){PreparationWorker.RecordStartupFailure(id,ex);Shutdown(1);}return;
  }
  instance = new Mutex(true, "Local\\FromZero-" + System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value, out var first);
  if(!first&&e.Args.Contains("--elevated")){try{first=instance.WaitOne(TimeSpan.FromSeconds(12));}catch(AbandonedMutexException){first=true;}}
  if (!first) { AppMessage.Show("FromZero zaten açık. Görev çubuğundaki mevcut pencereyi kullan.", "FromZero"); Shutdown(); return; }
  previousInstance=new Mutex(true,"Local\\"+BrandMigration.PreviousName+"-"+System.Security.Principal.WindowsIdentity.GetCurrent().User?.Value,out var oldAvailable);
  if(!oldAvailable){AppMessage.Show("Önceki ön izleme uygulaması hâlâ açık. Çalışan işlemi tamamlayıp kapattıktan sonra FromZero'yu aç.","FromZero");Shutdown();return;}
  BrandMigration.CopySettings();
  UiTheme.Start();
  base.OnStartup(e);
  EventManager.RegisterClassHandler(typeof(Window),FrameworkElement.LoadedEvent,new RoutedEventHandler((sender,_)=>{if(sender is Window window)PixelTheme.Apply(window);}));
  DispatcherUnhandledException += (_, args) => {
   Journal.Write("Uygulama hatası", args.Exception.ToString(), false);
   AppMessage.Show(args.Exception.Message, "FromZero", MessageBoxButton.OK, MessageBoxImage.Error);
   args.Handled = true;
  };
  MainWindow=new MainWindow();MainWindow.Show();
 }
 protected override void OnExit(ExitEventArgs e) { previousInstance?.Dispose(); instance?.Dispose(); base.OnExit(e); }
}
