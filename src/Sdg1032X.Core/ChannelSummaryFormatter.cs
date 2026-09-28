using System.Globalization;

namespace Sdg1032X.Core;

public static class ChannelSummaryFormatter
{
	public static string Format(int channel,bool outputEnabled,double frequencyHz,double amplitudeVpp)
	{
		if(channel is not (1 or 2))
		{
			throw new ArgumentOutOfRangeException(nameof(channel));
		}
		return $"CH{channel}  {(outputEnabled ? "ON" : "OFF")}\n"+
			FormatEngineering(frequencyHz,"Hz")+"  "+
			FormatEngineering(amplitudeVpp,"Vpp");
	}

	public static string FormatEngineering(double value,string unit)
	{
		if(!double.IsFinite(value))
		{
			throw new ArgumentOutOfRangeException(nameof(value));
		}
		ArgumentException.ThrowIfNullOrWhiteSpace(unit);
		double absolute=Math.Abs(value);
		(double factor,string prefix)=absolute switch
		{
			>=1e9=>(1e9,"G"),
			>=1e6=>(1e6,"M"),
			>=1e3=>(1e3,"k"),
			>=1=>(1,""),
			>=1e-3=>(1e-3,"m"),
			>=1e-6=>(1e-6,"µ"),
			>0=>(1e-9,"n"),
			_=>(1,"")
		};
		return (value/factor).ToString("0.###",CultureInfo.CurrentCulture)+prefix+unit;
	}
}
