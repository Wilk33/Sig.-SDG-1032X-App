using System.Text;

namespace Sdg1032X.Core;

public interface IInstrumentTransport : IDisposable
{
	void Write(string command);
	byte[] Query(string command);
}

public sealed class SiglentGeneratorClient(IInstrumentTransport transport) : IDisposable
{
	public string Initialize()
	{
		string identity=Encoding.ASCII.GetString(transport.Query("*IDN?")).Trim();
		string[] parts=identity.Split(',',StringSplitOptions.TrimEntries);
		if(parts.Length < 2 ||
			!parts[0].Contains("SIGLENT",StringComparison.OrdinalIgnoreCase) ||
			!parts[1].Equals("SDG1032X",StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidDataException(
				"Ta wersja aplikacji obsługuje SIGLENT SDG1032X. Odpowiedź: "+identity);
		}
		return identity;
	}

	public ChannelSnapshot ReadChannel(int channel)
	{
		string basic=Encoding.ASCII.GetString(transport.Query($"C{channel}:BSWV?")).Trim();
		string output=Encoding.ASCII.GetString(transport.Query($"C{channel}:OUTP?")).Trim();
		return SiglentProtocol.ParseSnapshot(channel,basic,output);
	}

	public void Write(string command)
	{
		transport.Write(command);
	}

	public void Dispose()
	{
		transport.Dispose();
	}
}
