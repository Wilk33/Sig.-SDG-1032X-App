using System.Globalization;
using System.Text.RegularExpressions;

namespace Sdg1032X.Core;

public static class EngineeringValue
{
	public static double ParseDisplay(string text,double scale)
	{
		if(!double.IsFinite(scale) || scale <= 0)
		{
			throw new ArgumentOutOfRangeException(nameof(scale));
		}
		string normalized=text.Trim().Replace(',','.');
		if(!double.TryParse(normalized,NumberStyles.Float,CultureInfo.InvariantCulture,out double value) ||
			!double.IsFinite(value*scale))
		{
			throw new InvalidDataException("Nieprawidłowa wartość liczbowa.");
		}
		return value*scale;
	}

	public static string FormatDisplay(double value,double scale,int decimals)
	{
		if(!double.IsFinite(value) || !double.IsFinite(scale) || scale <= 0)
		{
			throw new InvalidDataException("Nieprawidłowa wartość liczbowa.");
		}
		return (value/scale).ToString("F"+decimals,CultureInfo.CurrentCulture);
	}
}

public static class SiglentProtocol
{
	public static string WaveformCode(BasicWaveform waveform)
	{
		return waveform switch
		{
			BasicWaveform.Sine=>"SINE",
			BasicWaveform.Square=>"SQUARE",
			BasicWaveform.Ramp=>"RAMP",
			BasicWaveform.Pulse=>"PULSE",
			BasicWaveform.Noise=>"NOISE",
			BasicWaveform.Dc=>"DC",
			_=>throw new ArgumentOutOfRangeException(nameof(waveform))
		};
	}

	public static string WaveformCommand(int channel,BasicWaveform waveform)
	{
		ValidateChannel(channel);
		return $"C{channel}:BSWV WVTP,{WaveformCode(waveform)}";
	}

	public static string ParameterCommand(int channel,GeneratorParameter parameter,double value)
	{
		ValidateChannel(channel);
		if(!double.IsFinite(value))
		{
			throw new ArgumentOutOfRangeException(nameof(value));
		}
		string code=parameter switch
		{
			GeneratorParameter.Frequency=>"FRQ",
			GeneratorParameter.Amplitude=>"AMP",
			GeneratorParameter.Offset=>"OFST",
			GeneratorParameter.Phase=>"PHSE",
			GeneratorParameter.Duty=>"DUTY",
			GeneratorParameter.Symmetry=>"SYM",
			GeneratorParameter.PulseWidth=>"WIDTH",
			GeneratorParameter.NoiseStandardDeviation=>"STDEV",
			GeneratorParameter.NoiseMean=>"MEAN",
			_=>throw new ArgumentOutOfRangeException(nameof(parameter))
		};
		return $"C{channel}:BSWV {code},{value.ToString("G17",CultureInfo.InvariantCulture)}";
	}

	public static string LoadCommand(int channel,OutputLoad load)
	{
		ValidateChannel(channel);
		return $"C{channel}:OUTP LOAD,"+(load == OutputLoad.HighImpedance ? "HZ" : "50");
	}

	public static string PolarityCommand(int channel,OutputPolarity polarity)
	{
		ValidateChannel(channel);
		return $"C{channel}:OUTP PLRT,"+(polarity == OutputPolarity.Normal ? "NOR" : "INVT");
	}

	public static string OutputCommand(int channel,bool enabled)
	{
		ValidateChannel(channel);
		return $"C{channel}:OUTP "+(enabled ? "ON" : "OFF");
	}

	public static ChannelSnapshot ParseSnapshot(int channel,string basicWave,string output)
	{
		ValidateChannel(channel);
		Dictionary<string,string> wave=Pairs(basicWave,"BSWV");
		Dictionary<string,string> state=Pairs(output,"OUTP");
		BasicWaveform waveform=wave.GetValueOrDefault("WVTP")?.ToUpperInvariant() switch
		{
			"SINE"=>BasicWaveform.Sine,
			"SQUARE"=>BasicWaveform.Square,
			"RAMP"=>BasicWaveform.Ramp,
			"PULSE"=>BasicWaveform.Pulse,
			"NOISE"=>BasicWaveform.Noise,
			"DC"=>BasicWaveform.Dc,
			string unknown=>throw new InvalidDataException("Nieobsługiwany przebieg: "+unknown),
			null=>throw new InvalidDataException("Brak typu przebiegu w odpowiedzi.")
		};
		string outputState=state.GetValueOrDefault("STATE") ??
			throw new InvalidDataException("Brak stanu wyjścia w odpowiedzi.");
		return new()
		{
			Channel=channel,
			Waveform=waveform,
			FrequencyHz=Number(wave,"FRQ"),
			AmplitudeVpp=Number(wave,"AMP"),
			OffsetVolts=Number(wave,"OFST"),
			PhaseDegrees=Number(wave,"PHSE"),
			DutyPercent=Number(wave,"DUTY"),
			SymmetryPercent=Number(wave,"SYM"),
			PulseWidthSeconds=Number(wave,"WIDTH"),
			NoiseStandardDeviation=Number(wave,"STDEV"),
			NoiseMean=Number(wave,"MEAN"),
			OutputEnabled=outputState.Equals("ON",StringComparison.OrdinalIgnoreCase),
			Load=state.GetValueOrDefault("LOAD")?.Equals("HZ",StringComparison.OrdinalIgnoreCase) == true
				? OutputLoad.HighImpedance
				: OutputLoad.Ohms50,
			Polarity=state.GetValueOrDefault("PLRT")?.Equals("INVT",StringComparison.OrdinalIgnoreCase) == true
				? OutputPolarity.Inverted
				: OutputPolarity.Normal
		};
	}

	private static void ValidateChannel(int channel)
	{
		if(channel is not (1 or 2))
		{
			throw new ArgumentOutOfRangeException(nameof(channel));
		}
	}

	private static Dictionary<string,string> Pairs(string response,string command)
	{
		string[] tokens=response.Trim().Split(',',StringSplitOptions.TrimEntries);
		if(tokens.Length < 2)
		{
			throw new InvalidDataException("Niepełna odpowiedź "+command+".");
		}
		string[] header=tokens[0].Split(' ',StringSplitOptions.RemoveEmptyEntries);
		if(header.Length < 2 || !header[0].Contains(command,StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException("Nieprawidłowa odpowiedź "+command+".");
		}
		Dictionary<string,string> result=new(StringComparer.OrdinalIgnoreCase);
		int start;
		if(command == "OUTP")
		{
			result["STATE"]=header[^1];
			start=1;
		}
		else
		{
			result[header[^1]]=tokens[1];
			start=2;
		}
		for(int index=start;index+1<tokens.Length;index+=2)
		{
			result[tokens[index]]=tokens[index+1];
		}
		return result;
	}

	private static double Number(Dictionary<string,string> values,string key)
	{
		return values.TryGetValue(key,out string? value) ? ParseScpiNumber(value) : 0;
	}

	private static double ParseScpiNumber(string text)
	{
		Match match=Regex.Match(
			text.Trim(),
			@"^([+-]?(?:\d+(?:\.\d*)?|\.\d+)(?:[Ee][+-]?\d+)?)([GMKkmunpµ]?)([A-Za-z%]*)$");
		if(!match.Success ||
			!double.TryParse(match.Groups[1].Value,NumberStyles.Float,CultureInfo.InvariantCulture,out double number))
		{
			throw new InvalidDataException("Nieprawidłowa wartość SCPI: "+text);
		}
		string prefix=match.Groups[2].Value;
		string unit=match.Groups[3].Value;
		double multiplier=prefix switch
		{
			"G"=>1e9,
			"M" when unit.StartsWith("V",StringComparison.OrdinalIgnoreCase)=>1e-3,
			"M"=>1e6,
			"K" or "k"=>1e3,
			"m"=>1e-3,
			"u" or "µ"=>1e-6,
			"n"=>1e-9,
			"p"=>1e-12,
			_=>1
		};
		double result=number*multiplier;
		if(!double.IsFinite(result))
		{
			throw new InvalidDataException("Nieprawidłowa wartość SCPI: "+text);
		}
		return result;
	}
}
