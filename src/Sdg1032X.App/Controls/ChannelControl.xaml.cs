using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Sdg1032X.Core;

namespace Sdg1032X.App.Controls;

public sealed record WaveformChoice(string Name,BasicWaveform Value)
{
	public override string ToString()
	{
		return Name;
	}
}

public partial class ChannelControl : UserControl
{
	private Func<GeneratorSession?> sessionProvider=()=>null;
	private Action<string,bool> showStatus=(_, _)=>{ };
	private int channel;
	private bool outputEnabled;
	private bool updating;
	private OutputLoad confirmedLoad=OutputLoad.HighImpedance;
	private double? confirmedLoadOhms;

	public ChannelControl()
	{
		InitializeComponent();
		WaveformSelector.ItemsSource=new[]
		{
			new WaveformChoice("Sinus",BasicWaveform.Sine),
			new WaveformChoice("Prostokąt",BasicWaveform.Square),
			new WaveformChoice("Rampa",BasicWaveform.Ramp),
			new WaveformChoice("Impuls",BasicWaveform.Pulse),
			new WaveformChoice("Szum",BasicWaveform.Noise),
			new WaveformChoice("DC",BasicWaveform.Dc)
		};
		WaveformSelector.SelectedIndex=0;
		LoadSelector.SelectedIndex=0;
		PolaritySelector.SelectedIndex=0;
		UpdateFieldVisibility(BasicWaveform.Sine);
	}

	public event EventHandler<bool>? OutputStateChanged;

	public void Configure(
		int channelNumber,
		Func<GeneratorSession?> currentSession,
		Action<string,bool> statusHandler)
	{
		channel=channelNumber;
		sessionProvider=currentSession;
		showStatus=statusHandler;
		ChannelTitle.Text="Kanał "+channel;
	}

	public void ApplySnapshot(ChannelSnapshot snapshot)
	{
		updating=true;
		try
		{
			WaveformSelector.SelectedItem=((WaveformChoice[])WaveformSelector.ItemsSource)
				.Single(choice=>choice.Value == snapshot.Waveform);
			FrequencyEditor.Value=snapshot.FrequencyHz;
			AmplitudeEditor.Value=snapshot.AmplitudeVpp;
			OffsetEditor.Value=snapshot.OffsetVolts;
			PhaseEditor.Value=snapshot.PhaseDegrees;
			DutyEditor.Value=snapshot.DutyPercent;
			SymmetryEditor.Value=snapshot.SymmetryPercent;
			PulseWidthEditor.Value=snapshot.PulseWidthSeconds;
			NoiseDeviationEditor.Value=snapshot.NoiseStandardDeviation;
			NoiseMeanEditor.Value=snapshot.NoiseMean;
			DcLevelEditor.Value=snapshot.OffsetVolts;
			confirmedLoad=snapshot.Load;
			confirmedLoadOhms=snapshot.LoadOhms;
			ShowLoadSelection(confirmedLoad,confirmedLoadOhms);
			PolaritySelector.SelectedIndex=snapshot.Polarity == OutputPolarity.Normal ? 0 : 1;
			SetOutputState(snapshot.OutputEnabled);
			UpdateFieldVisibility(snapshot.Waveform);
		}
		finally
		{
			updating=false;
		}
	}

	private async void WaveformSelectionChanged(object sender,SelectionChangedEventArgs eventArgs)
	{
		if(WaveformSelector.SelectedItem is not WaveformChoice choice)
		{
			return;
		}
		UpdateFieldVisibility(choice.Value);
		if(updating || sessionProvider() is not GeneratorSession session)
		{
			return;
		}
		try
		{
			await session.SetLatestAsync(
				$"C{channel}:WVTP",
				SiglentProtocol.WaveformCommand(channel,choice.Value));
			showStatus($"CH{channel}: ustawiono przebieg {choice.Name}.",false);
			await RefreshAsync();
		}
		catch(TaskCanceledException)
		{
		}
		catch(Exception exception)
		{
			showStatus(exception.Message,true);
		}
	}

	private void FrequencyCommitted(object sender,NumericValueCommittedEventArgs eventArgs)
	{
		Commit(GeneratorParameter.Frequency,eventArgs.Value);
	}

	private void AmplitudeCommitted(object sender,NumericValueCommittedEventArgs eventArgs)
	{
		Commit(GeneratorParameter.Amplitude,eventArgs.Value);
	}

	private void OffsetCommitted(object sender,NumericValueCommittedEventArgs eventArgs)
	{
		Commit(GeneratorParameter.Offset,eventArgs.Value);
	}

	private void PhaseCommitted(object sender,NumericValueCommittedEventArgs eventArgs)
	{
		Commit(GeneratorParameter.Phase,eventArgs.Value);
	}

	private void DutyCommitted(object sender,NumericValueCommittedEventArgs eventArgs)
	{
		Commit(GeneratorParameter.Duty,eventArgs.Value);
	}

	private void SymmetryCommitted(object sender,NumericValueCommittedEventArgs eventArgs)
	{
		Commit(GeneratorParameter.Symmetry,eventArgs.Value);
	}

	private void PulseWidthCommitted(object sender,NumericValueCommittedEventArgs eventArgs)
	{
		Commit(GeneratorParameter.PulseWidth,eventArgs.Value);
	}

	private void NoiseDeviationCommitted(object sender,NumericValueCommittedEventArgs eventArgs)
	{
		Commit(GeneratorParameter.NoiseStandardDeviation,eventArgs.Value);
	}

	private void NoiseMeanCommitted(object sender,NumericValueCommittedEventArgs eventArgs)
	{
		Commit(GeneratorParameter.NoiseMean,eventArgs.Value);
	}

	private void DcLevelCommitted(object sender,NumericValueCommittedEventArgs eventArgs)
	{
		Commit(GeneratorParameter.Offset,eventArgs.Value);
	}

	private void Commit(GeneratorParameter parameter,double value)
	{
		if(updating || sessionProvider() is not GeneratorSession session)
		{
			return;
		}
		string command=SiglentProtocol.ParameterCommand(channel,parameter,value);
		_ = ObserveLatestAsync(
			session.SetLatestAsync($"C{channel}:{parameter}",command),
			$"CH{channel}: {ParameterName(parameter)} = {value:G6}");
	}

	private async void LoadSelectionChanged(object sender,SelectionChangedEventArgs eventArgs)
	{
		if(LoadSelector.SelectedItem is ComboBoxItem item && item.Tag?.ToString() == "Custom")
		{
			return;
		}
		if(updating || sessionProvider() is not GeneratorSession session)
		{
			return;
		}
		OutputLoad load=LoadSelector.SelectedIndex == 0
			? OutputLoad.HighImpedance
			: OutputLoad.Ohms50;
		try
		{
			await session.SetLatestAsync(
				$"C{channel}:LOAD",
				SiglentProtocol.LoadCommand(channel,load));
			confirmedLoad=load;
			confirmedLoadOhms=load == OutputLoad.Ohms50 ? 50 : null;
			ShowLoadSelection(confirmedLoad,confirmedLoadOhms);
			showStatus($"CH{channel}: zmieniono obciążenie.",false);
		}
		catch(TaskCanceledException)
		{
		}
		catch(Exception exception)
		{
			ShowLoadSelection(confirmedLoad,confirmedLoadOhms);
			showStatus(exception.Message,true);
		}
	}

	private void ShowLoadSelection(OutputLoad load,double? loadOhms)
	{
		bool wasUpdating=updating;
		updating=true;
		try
		{
			while(LoadSelector.Items.Count > 2)
			{
				LoadSelector.Items.RemoveAt(2);
			}
			if(load == OutputLoad.Custom)
			{
				LoadSelector.Items.Add(new ComboBoxItem
				{
					Content=$"{loadOhms:G} Ω (odczyt)",
					Tag="Custom"
				});
				LoadSelector.SelectedIndex=2;
			}
			else
			{
				LoadSelector.SelectedIndex=load == OutputLoad.HighImpedance ? 0 : 1;
			}
		}
		finally
		{
			updating=wasUpdating;
		}
	}

	private async void PolaritySelectionChanged(object sender,SelectionChangedEventArgs eventArgs)
	{
		if(updating || sessionProvider() is not GeneratorSession session)
		{
			return;
		}
		OutputPolarity polarity=PolaritySelector.SelectedIndex == 0
			? OutputPolarity.Normal
			: OutputPolarity.Inverted;
		await ObserveLatestAsync(
			session.SetLatestAsync($"C{channel}:PLRT",SiglentProtocol.PolarityCommand(channel,polarity)),
			$"CH{channel}: zmieniono polaryzację.");
	}

	private async void OutputClick(object sender,RoutedEventArgs eventArgs)
	{
		if(sessionProvider() is not GeneratorSession session)
		{
			return;
		}
		bool requested=!outputEnabled;
		OutputButton.IsEnabled=false;
		try
		{
			Task operation=requested
				? session.WriteOrderedAsync(SiglentProtocol.OutputCommand(channel,true))
				: session.WritePriorityAsync(SiglentProtocol.OutputCommand(channel,false));
			await operation;
			SetOutputState(requested);
			showStatus($"CH{channel}: wyjście "+(requested ? "włączone." : "wyłączone."),false);
		}
		catch(OperationCanceledException)
		{
		}
		catch(Exception exception)
		{
			showStatus(exception.Message,true);
		}
		finally
		{
			OutputButton.IsEnabled=true;
		}
	}

	private async void RefreshClick(object sender,RoutedEventArgs eventArgs)
	{
		await RefreshAsync();
	}

	private async Task RefreshAsync()
	{
		if(sessionProvider() is not GeneratorSession session)
		{
			return;
		}
		RefreshButton.IsEnabled=false;
		try
		{
			ChannelSnapshot snapshot=await session.ReadChannelAsync(channel);
			ApplySnapshot(snapshot);
			showStatus($"CH{channel}: odczytano ustawienia.",false);
		}
		catch(Exception exception)
		{
			showStatus(exception.Message,true);
		}
		finally
		{
			RefreshButton.IsEnabled=true;
		}
	}

	private async Task ObserveLatestAsync(Task task,string success)
	{
		try
		{
			await task;
			showStatus(success,false);
		}
		catch(TaskCanceledException)
		{
		}
		catch(Exception exception)
		{
			showStatus(exception.Message,true);
		}
	}

	private void SetOutputState(bool enabled)
	{
		outputEnabled=enabled;
		OutputButton.Content=enabled ? "Wyjście ON" : "Wyjście OFF";
		OutputButton.Background=enabled
			? new SolidColorBrush(Color.FromRgb(22,135,70))
			: new SolidColorBrush(Color.FromRgb(70,70,70));
		OutputButton.Foreground=Brushes.White;
		OutputStateChanged?.Invoke(this,enabled);
	}

	private void UpdateFieldVisibility(BasicWaveform waveform)
	{
		FrequencyEditor.Visibility=Visible(waveform is BasicWaveform.Sine or BasicWaveform.Square or BasicWaveform.Ramp or BasicWaveform.Pulse);
		AmplitudeEditor.Visibility=Visible(waveform is BasicWaveform.Sine or BasicWaveform.Square or BasicWaveform.Ramp or BasicWaveform.Pulse);
		OffsetEditor.Visibility=Visible(waveform is BasicWaveform.Sine or BasicWaveform.Square or BasicWaveform.Ramp or BasicWaveform.Pulse);
		PhaseEditor.Visibility=Visible(waveform is BasicWaveform.Sine or BasicWaveform.Square or BasicWaveform.Ramp);
		DutyEditor.Visibility=Visible(waveform == BasicWaveform.Square);
		SymmetryEditor.Visibility=Visible(waveform == BasicWaveform.Ramp);
		PulseWidthEditor.Visibility=Visible(waveform == BasicWaveform.Pulse);
		NoiseDeviationEditor.Visibility=Visible(waveform == BasicWaveform.Noise);
		NoiseMeanEditor.Visibility=Visible(waveform == BasicWaveform.Noise);
		DcLevelEditor.Visibility=Visible(waveform == BasicWaveform.Dc);
	}

	private static Visibility Visible(bool visible)
	{
		return visible ? Visibility.Visible : Visibility.Collapsed;
	}

	private static string ParameterName(GeneratorParameter parameter)
	{
		return parameter switch
		{
			GeneratorParameter.Frequency=>"częstotliwość",
			GeneratorParameter.Amplitude=>"amplituda",
			GeneratorParameter.Offset=>"offset",
			GeneratorParameter.Phase=>"faza",
			GeneratorParameter.Duty=>"wypełnienie",
			GeneratorParameter.Symmetry=>"symetria",
			GeneratorParameter.PulseWidth=>"szerokość impulsu",
			GeneratorParameter.NoiseStandardDeviation=>"odchylenie",
			GeneratorParameter.NoiseMean=>"średnia",
			_=>parameter.ToString()
		};
	}
}
