using System.Windows;
using System.Windows.Media;
using Sdg1032X.Core;

namespace Sdg1032X.App;

public partial class MainWindow : Window
{
	private readonly bool demoMode;
	private GeneratorSession? session;
	private bool connecting;

	public MainWindow()
	{
		InitializeComponent();
		demoMode=Environment.GetCommandLineArgs()
			.Any(argument=>argument.Equals("--demo",StringComparison.OrdinalIgnoreCase));
		Title=ProductInformation.GetWindowTitle(demoMode);
		SystemTheme.UseImmersiveDarkMode(this);
		Channel1.Configure(1,()=>session,ShowStatus);
		Channel2.Configure(2,()=>session,ShowStatus);
		Channel1.SummaryChanged+=(_, eventArgs)=>Channel1Header.Text=eventArgs.Text;
		Channel2.SummaryChanged+=(_, eventArgs)=>Channel2Header.Text=eventArgs.Text;
		SetControlsEnabled(false);
		Loaded+=MainWindowLoaded;
		Closed+=MainWindowClosed;
	}

	private async void MainWindowLoaded(object sender,RoutedEventArgs eventArgs)
	{
		if(demoMode)
		{
			HostEditor.Text="DEMO";
			await ConnectAsync();
		}
	}

	private async void ConnectionClick(object sender,RoutedEventArgs eventArgs)
	{
		if(session is null)
		{
			await ConnectAsync();
		}
		else
		{
			await DisconnectAsync();
		}
	}

	private async Task ConnectAsync()
	{
		if(connecting)
		{
			return;
		}
		string host=HostEditor.Text.Trim();
		if(!demoMode && string.IsNullOrWhiteSpace(host))
		{
			ShowStatus("Wprowadź adres IP generatora.",true);
			return;
		}
		connecting=true;
		ConnectionButton.IsEnabled=false;
		ConnectionButton.Content="Łączenie";
		ShowStatus(demoMode ? "Uruchamianie trybu demonstracyjnego..." : "Łączenie z "+host+"...",false);
		try
		{
			session=demoMode
				? await GeneratorSession.CreateAsync(()=>new DemoInstrumentTransport())
				: await GeneratorSession.ConnectAsync(host);
			session.CommunicationFailed+=SessionCommunicationFailed;
			ChannelSnapshot[] snapshots=await Task.WhenAll(
				session.ReadChannelAsync(1),
				session.ReadChannelAsync(2));
			Channel1.ApplySnapshot(snapshots[0]);
			Channel2.ApplySnapshot(snapshots[1]);
			SetControlsEnabled(true);
			HostEditor.IsEnabled=false;
			ConnectionButton.Content="Online";
			ConnectionButton.Background=new SolidColorBrush(Color.FromRgb(22,135,70));
			ConnectionButton.Foreground=Brushes.White;
			ShowStatus("Połączono: "+session.Identity,false);
		}
		catch(Exception exception)
		{
			if(session is not null)
			{
				await session.DisposeAsync();
				session=null;
			}
			ShowStatus("Błąd połączenia: "+exception.Message,true);
			ConnectionButton.Content="Offline";
		}
		finally
		{
			connecting=false;
			ConnectionButton.IsEnabled=true;
		}
	}

	private async Task DisconnectAsync()
	{
		GeneratorSession? current=session;
		session=null;
		SetControlsEnabled(false);
		HostEditor.IsEnabled=!demoMode;
		ConnectionButton.IsEnabled=false;
		if(current is not null)
		{
			current.CommunicationFailed-=SessionCommunicationFailed;
			await current.DisposeAsync();
		}
		ConnectionButton.Content="Offline";
		ConnectionButton.Background=(Brush)FindResource("LightBrush");
		ConnectionButton.Foreground=Brushes.Black;
		ConnectionButton.IsEnabled=!demoMode;
		ShowStatus("Stan generatora: OFFLINE",false);
	}

	private async void MainWindowClosed(object? sender,EventArgs eventArgs)
	{
		if(session is not null)
		{
			GeneratorSession current=session;
			session=null;
			await current.DisposeAsync();
		}
	}

	private void SessionCommunicationFailed(object? sender,Exception exception)
	{
		Dispatcher.Invoke(()=>ShowStatus("Błąd komunikacji: "+exception.Message,true));
	}

	private void SetControlsEnabled(bool enabled)
	{
		Channel1.IsEnabled=enabled;
		Channel2.IsEnabled=enabled;
	}

	private void ShowStatus(string message,bool error)
	{
		StatusText.Text=message;
		StatusText.Foreground=error
			? new SolidColorBrush(Color.FromRgb(255,128,128))
			: (Brush)FindResource("MutedTextBrush");
	}

	private void AuthorClick(object sender,RoutedEventArgs eventArgs)
	{
		new InfoWindow(
			"Autor - "+ProductInformation.DisplayName,
			ProductInformation.AuthorText,
			false)
		{
			Owner=this
		}.ShowDialog();
	}

	private void LicenseClick(object sender,RoutedEventArgs eventArgs)
	{
		new InfoWindow(
			"Licencja - "+ProductInformation.DisplayName,
			ProductInformation.LoadLicenseText(),
			true)
		{
			Owner=this
		}.ShowDialog();
	}
}
