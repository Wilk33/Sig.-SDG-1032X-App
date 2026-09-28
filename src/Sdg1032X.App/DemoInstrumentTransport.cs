using System.Globalization;
using System.Text;
using Sdg1032X.Core;

namespace Sdg1032X.App;

internal sealed class DemoInstrumentTransport : IInstrumentTransport
{
	private readonly object gate=new();
	private readonly DemoChannel[] channels=
	[
		new()
		{
			Waveform="SINE",
			Frequency=1000,
			Amplitude=2,
			Offset=0,
			Phase=0,
			Duty=50,
			Symmetry=50,
			PulseWidth=0.0005,
			StandardDeviation=0.5,
			Mean=0,
			Output=true
		},
		new()
		{
			Waveform="SQUARE",
			Frequency=10000,
			Amplitude=3.3,
			Offset=1.65,
			Phase=0,
			Duty=50,
			Symmetry=50,
			PulseWidth=0.00005,
			StandardDeviation=0.5,
			Mean=0,
			Output=false
		}
	];
	private bool disposed;

	public void Write(string command)
	{
		ObjectDisposedException.ThrowIf(disposed,this);
		lock(gate)
		{
			if(!TryChannel(command,out int channel,out string body))
			{
				return;
			}
			DemoChannel state=channels[channel-1];
			if(body.StartsWith("BSWV ",StringComparison.OrdinalIgnoreCase))
			{
				string[] values=body[5..].Split(',',StringSplitOptions.TrimEntries);
				if(values.Length<2)
				{
					return;
				}
				string parameter=values[0].ToUpperInvariant();
				if(parameter == "WVTP")
				{
					state.Waveform=values[1].ToUpperInvariant();
					return;
				}
				if(!double.TryParse(values[1],NumberStyles.Float,CultureInfo.InvariantCulture,out double value))
				{
					return;
				}
				switch(parameter)
				{
					case "FRQ": state.Frequency=value; break;
					case "AMP": state.Amplitude=value; break;
					case "OFST": state.Offset=value; break;
					case "PHSE": state.Phase=value; break;
					case "DUTY": state.Duty=value; break;
					case "SYM": state.Symmetry=value; break;
					case "WIDTH": state.PulseWidth=value; break;
					case "STDEV": state.StandardDeviation=value; break;
					case "MEAN": state.Mean=value; break;
				}
			}
			else if(body.StartsWith("OUTP ",StringComparison.OrdinalIgnoreCase))
			{
				string value=body[5..].Trim();
				if(value.Equals("ON",StringComparison.OrdinalIgnoreCase))
				{
					state.Output=true;
				}
				else if(value.Equals("OFF",StringComparison.OrdinalIgnoreCase))
				{
					state.Output=false;
				}
				else if(value.StartsWith("LOAD,",StringComparison.OrdinalIgnoreCase))
				{
					state.HighImpedance=value[5..].Equals("HZ",StringComparison.OrdinalIgnoreCase);
				}
				else if(value.StartsWith("PLRT,",StringComparison.OrdinalIgnoreCase))
				{
					state.Inverted=value[5..].Equals("INVT",StringComparison.OrdinalIgnoreCase);
				}
			}
		}
	}

	public byte[] Query(string command)
	{
		ObjectDisposedException.ThrowIf(disposed,this);
		lock(gate)
		{
			if(command.Equals("*IDN?",StringComparison.OrdinalIgnoreCase))
			{
				return Bytes("SIGLENT,SDG1032X,DEMO,0.1.0");
			}
			if(!TryChannel(command,out int channel,out string body))
			{
				return Bytes(string.Empty);
			}
			DemoChannel state=channels[channel-1];
			if(body.Equals("BSWV?",StringComparison.OrdinalIgnoreCase))
			{
				return Bytes(
					$"C{channel}:BSWV WVTP,{state.Waveform},"+
					$"FRQ,{Number(state.Frequency)}HZ,AMP,{Number(state.Amplitude)}V,"+
					$"OFST,{Number(state.Offset)}V,PHSE,{Number(state.Phase)},"+
					$"DUTY,{Number(state.Duty)},SYM,{Number(state.Symmetry)},"+
					$"WIDTH,{Number(state.PulseWidth)}S,STDEV,{Number(state.StandardDeviation)}V,"+
					$"MEAN,{Number(state.Mean)}V");
			}
			if(body.Equals("OUTP?",StringComparison.OrdinalIgnoreCase))
			{
				return Bytes(
					$"C{channel}:OUTP {(state.Output ? "ON" : "OFF")},"+
					$"LOAD,{(state.HighImpedance ? "HZ" : "50")},"+
					$"PLRT,{(state.Inverted ? "INVT" : "NOR")}");
			}
			return Bytes(string.Empty);
		}
	}

	public void Dispose()
	{
		disposed=true;
	}

	private static bool TryChannel(string command,out int channel,out string body)
	{
		channel=0;
		body=string.Empty;
		if(command.Length<4 || command[0] != 'C' || command[2] != ':' ||
			!int.TryParse(command[1].ToString(),out channel) || channel is not (1 or 2))
		{
			return false;
		}
		body=command[3..];
		return true;
	}

	private static byte[] Bytes(string text)
	{
		return Encoding.ASCII.GetBytes(text);
	}

	private static string Number(double value)
	{
		return value.ToString("G17",CultureInfo.InvariantCulture);
	}

	private sealed class DemoChannel
	{
		public required string Waveform { get; set; }
		public double Frequency { get; set; }
		public double Amplitude { get; set; }
		public double Offset { get; set; }
		public double Phase { get; set; }
		public double Duty { get; set; }
		public double Symmetry { get; set; }
		public double PulseWidth { get; set; }
		public double StandardDeviation { get; set; }
		public double Mean { get; set; }
		public bool Output { get; set; }
		public bool HighImpedance { get; set; }=true;
		public bool Inverted { get; set; }
	}
}
