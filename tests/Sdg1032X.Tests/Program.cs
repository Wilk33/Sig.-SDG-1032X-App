using System.Globalization;
using System.Text;
using Sdg1032X.App;
using Sdg1032X.App.Controls;
using Sdg1032X.Core;

int failed=0;
int passed=0;

void Test(string name,Action action)
{
	try
	{
		action();
		passed++;
		Console.WriteLine("PASS "+name);
	}
	catch(Exception exception)
	{
		failed++;
		Console.WriteLine("FAIL "+name+": "+exception.Message);
	}
}

void Equal<T>(T expected,T actual)
{
	if(!EqualityComparer<T>.Default.Equals(expected,actual))
	{
		throw new Exception($"Oczekiwano {expected}, otrzymano {actual}");
	}
}

void Close(double expected,double actual,double tolerance=1e-9)
{
	if(Math.Abs(expected-actual)>tolerance)
	{
		throw new Exception($"Oczekiwano {expected}, otrzymano {actual}");
	}
}

void Reject(Action action)
{
	try
	{
		action();
	}
	catch(InvalidDataException)
	{
		return;
	}
	throw new Exception("Nieprawidłowe dane zostały zaakceptowane");
}

Test("Lista przebiegów zawiera wyłącznie sześć podstawowych typów",()=>
{
	BasicWaveform[] values=Enum.GetValues<BasicWaveform>();
	Equal(6,values.Length);
	Equal("SINE",SiglentProtocol.WaveformCode(BasicWaveform.Sine));
	Equal("SQUARE",SiglentProtocol.WaveformCode(BasicWaveform.Square));
	Equal("RAMP",SiglentProtocol.WaveformCode(BasicWaveform.Ramp));
	Equal("PULSE",SiglentProtocol.WaveformCode(BasicWaveform.Pulse));
	Equal("NOISE",SiglentProtocol.WaveformCode(BasicWaveform.Noise));
	Equal("DC",SiglentProtocol.WaveformCode(BasicWaveform.Dc));
});

Test("Polecenia SCPI używają kanału i formatu niezależnego od kultury",()=>
{
	CultureInfo.CurrentCulture=new("pl-PL");
	Equal("C1:BSWV WVTP,SINE",SiglentProtocol.WaveformCommand(1,BasicWaveform.Sine));
	Equal("C2:BSWV FRQ,1234.5",SiglentProtocol.ParameterCommand(2,GeneratorParameter.Frequency,1234.5));
	Equal("C1:OUTP LOAD,HZ",SiglentProtocol.LoadCommand(1,OutputLoad.HighImpedance));
	Equal("C2:OUTP PLRT,INVT",SiglentProtocol.PolarityCommand(2,OutputPolarity.Inverted));
	Equal("C1:OUTP OFF",SiglentProtocol.OutputCommand(1,false));
});

Test("Parser odczytuje podstawowy przebieg i stan wyjścia",()=>
{
	ChannelSnapshot value=SiglentProtocol.ParseSnapshot(
		1,
		"C1:BSWV WVTP,SQUARE,FRQ,1.25KHZ,AMP,3.3V,OFST,1.65V,PHSE,90,DUTY,40",
		"C1:OUTP ON,LOAD,HZ,PLRT,NOR");
	Equal(BasicWaveform.Square,value.Waveform);
	Close(1250,value.FrequencyHz);
	Close(3.3,value.AmplitudeVpp);
	Close(1.65,value.OffsetVolts);
	Close(90,value.PhaseDegrees);
	Close(40,value.DutyPercent);
	Equal(true,value.OutputEnabled);
	Equal(OutputLoad.HighImpedance,value.Load);
	Equal(OutputPolarity.Normal,value.Polarity);
});

Test("Parser odczytuje parametry szumu",()=>
{
	ChannelSnapshot value=SiglentProtocol.ParseSnapshot(
		2,
		"C2:BSWV WVTP,NOISE,STDEV,500MV,MEAN,-25MV",
		"C2:OUTP OFF,LOAD,50,PLRT,INVT");
	Equal(BasicWaveform.Noise,value.Waveform);
	Close(0.5,value.NoiseStandardDeviation);
	Close(-0.025,value.NoiseMean);
	Equal(false,value.OutputEnabled);
	Equal(OutputLoad.Ohms50,value.Load);
	Equal(OutputPolarity.Inverted,value.Polarity);
});

Test("Nieznany przebieg z urządzenia jest odrzucany",()=>
{
	Reject(()=>SiglentProtocol.ParseSnapshot(
		1,
		"C1:BSWV WVTP,ARB",
		"C1:OUTP OFF,LOAD,HZ,PLRT,NOR"));
});

Test("Edytor wartości przyjmuje polski separator i skaluje jednostkę",()=>
{
	Close(0.0000025,EngineeringValue.ParseDisplay("2,5",0.000001));
	Equal("2,500",EngineeringValue.FormatDisplay(0.0000025,0.000001,3));
	Reject(()=>EngineeringValue.ParseDisplay("abc",1));
});

Test("Kolejka zastępuje starszą wartość tego samego parametru",()=>
{
	InstrumentRequestQueue queue=new();
	queue.EnqueueLatest("C1:FRQ","C1:BSWV FRQ,100");
	queue.EnqueueLatest("C1:FRQ","C1:BSWV FRQ,200");
	InstrumentRequest request=queue.TakeNext();
	Equal("C1:BSWV FRQ,200",request.Command);
	Equal(0,queue.PendingCount);
});

Test("Polecenie wyjścia ma priorytet przed ustawieniami",()=>
{
	InstrumentRequestQueue queue=new();
	queue.EnqueueLatest("C1:AMP","C1:BSWV AMP,2");
	queue.EnqueuePriority("C1:OUTP OFF");
	Equal("C1:OUTP OFF",queue.TakeNext().Command);
	Equal("C1:BSWV AMP,2",queue.TakeNext().Command);
});

Test("Zastąpione polecenie kończy oczekiwanie jako anulowane",()=>
{
	InstrumentRequestQueue queue=new();
	InstrumentRequest older=queue.EnqueueLatest("C1:FRQ","C1:BSWV FRQ,100");
	queue.EnqueueLatest("C1:FRQ","C1:BSWV FRQ,200");
	if(!older.Completion.IsCanceled)
	{
		throw new Exception("Starsze polecenie nadal oczekuje");
	}
});

Test("Kodowanie XDR zachowuje liczby i dopełnione bajty",()=>
{
	Xdr writer=new();
	writer.Put(0x12345678);
	writer.PutBytes([1,2,3]);
	Xdr reader=new(writer.Bytes());
	Equal(0x12345678u,reader.Get());
	if(!reader.GetBytes().SequenceEqual(new byte[]{1,2,3}))
	{
		throw new Exception("Dane XDR zostały zmienione");
	}
});

Test("Klient akceptuje tylko generator SDG1032X",()=>
{
	using ScriptedTransport transport=new("SIGLENT,SDG1032X,123456,1.0");
	using SiglentGeneratorClient client=new(transport);
	Equal("SIGLENT,SDG1032X,123456,1.0",client.Initialize());
	using ScriptedTransport other=new("SIGLENT,SDG2042X,123456,1.0");
	using SiglentGeneratorClient rejected=new(other);
	Reject(()=>rejected.Initialize());
});

Test("Metadane aplikacji zachowują autora, wersję i licencję",()=>
{
	Equal("Mateusz Skipor",ProductInformation.AuthorName);
	Equal("Inżynier technik elektroniki",ProductInformation.AuthorProfession);
	Equal("mskiporsklep@op.pl",ProductInformation.AuthorEmail);
	Equal("0.1.0",ProductInformation.Version);
	if(!ProductInformation.GetWindowTitle(true).EndsWith(" - DEMO",StringComparison.Ordinal))
	{
		throw new Exception("Brak oznaczenia trybu demonstracyjnego");
	}
	string license=ProductInformation.LoadLicenseText();
	if(!license.Contains("PolyForm Noncommercial License 1.0.0",StringComparison.Ordinal))
	{
		throw new Exception("Brak właściwego tekstu licencji");
	}
});

Test("Pozycja przebiegu udostępnia czytelną nazwę dla UI Automation",()=>
{
	WaveformChoice choice=new("Prostokąt",BasicWaveform.Square);
	Equal("Prostokąt",choice.ToString());
});

Console.WriteLine($"Wynik: {passed} zaliczonych, {failed} niezaliczonych");
return failed == 0 ? 0 : 1;

sealed class ScriptedTransport(string identity) : IInstrumentTransport
{
	public void Write(string command)
	{
	}

	public byte[] Query(string command)
	{
		return Encoding.ASCII.GetBytes(identity);
	}

	public void Dispose()
	{
	}
}
