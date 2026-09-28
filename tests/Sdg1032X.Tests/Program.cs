using System.Buffers.Binary;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
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
		Console.WriteLine("FAIL "+name+": "+exception);
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

void RunSta(Action action)
{
	Exception? failure=null;
	Thread thread=new(()=>
	{
		try
		{
			action();
		}
		catch(Exception exception)
		{
			failure=exception;
		}
	});
	thread.SetApartmentState(ApartmentState.STA);
	thread.Start();
	thread.Join();
	if(failure is not null)
	{
		throw failure;
	}
}

IEnumerable<T> VisualChildren<T>(DependencyObject parent) where T : DependencyObject
{
	for(int index=0;index<VisualTreeHelper.GetChildrenCount(parent);index++)
	{
		DependencyObject child=VisualTreeHelper.GetChild(parent,index);
		if(child is T typed)
		{
			yield return typed;
		}
		foreach(T descendant in VisualChildren<T>(child))
		{
			yield return descendant;
		}
	}
}

IEnumerable<T> LogicalChildren<T>(DependencyObject parent) where T : DependencyObject
{
	foreach(object childValue in LogicalTreeHelper.GetChildren(parent))
	{
		if(childValue is not DependencyObject child)
		{
			continue;
		}
		if(child is T typed)
		{
			yield return typed;
		}
		foreach(T descendant in LogicalChildren<T>(child))
		{
			yield return descendant;
		}
	}
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

Test("Nagłówek kanału używa skróconych jednostek inżynierskich",()=>
{
	CultureInfo.CurrentCulture=new("pl-PL");
	Equal("3kHz",ChannelSummaryFormatter.FormatEngineering(3000,"Hz"));
	Equal("2,5mVpp",ChannelSummaryFormatter.FormatEngineering(0.0025,"Vpp"));
	Equal("CH1  ON\n3kHz  4Vpp",ChannelSummaryFormatter.Format(1,true,3000,4));
});

Test("Nagłówek kanału odrzuca nieprawidłowy numer i wartości",()=>
{
	try
	{
		ChannelSummaryFormatter.Format(3,false,1000,1);
	}
	catch(ArgumentOutOfRangeException)
	{
		try
		{
			ChannelSummaryFormatter.Format(1,false,double.NaN,1);
		}
		catch(ArgumentOutOfRangeException)
		{
			return;
		}
	}
	throw new Exception("Nieprawidłowe dane nagłówka zostały zaakceptowane");
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

Test("Włączenie wyjścia czeka na wcześniejsze nastawy",()=>
{
	InstrumentRequestQueue queue=new();
	queue.EnqueueLatest("C1:AMP","C1:BSWV AMP,2");
	queue.EnqueueOrdered("C1:OUTP ON");
	Equal("C1:BSWV AMP,2",queue.TakeNext().Command);
	Equal("C1:OUTP ON",queue.TakeNext().Command);
});

Test("Anulowanie kolejki kończy wszystkie oczekujące zadania",()=>
{
	InstrumentRequestQueue queue=new();
	InstrumentRequest setting=queue.EnqueueLatest("C1:AMP","C1:BSWV AMP,2");
	InstrumentRequest output=queue.EnqueueOrdered("C1:OUTP ON");
	InstrumentRequest off=queue.EnqueuePriority("C2:OUTP OFF");
	queue.CancelAll();
	Equal(0,queue.PendingCount);
	if(!setting.Completion.IsCanceled || !output.Completion.IsCanceled || !off.Completion.IsCanceled)
	{
		throw new Exception("Nie wszystkie zadania zostały anulowane");
	}
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

Test("VXI-11 wykonuje pełny lokalny przepływ RPC",()=>
{
	using FakeVxi11Server server=new("SIGLENT,SDG1032X,FAKE,1.0");
	using(Vxi11Transport transport=new("127.0.0.1",server.MapperPort))
	{
		string response=Encoding.ASCII.GetString(transport.Query("*IDN?")).Trim();
		Equal("SIGLENT,SDG1032X,FAKE,1.0",response);
	}
	server.Wait();
	Equal("*IDN?\n",Encoding.ASCII.GetString(server.WrittenBytes));
	if(server.WriteCalls < 2)
	{
		throw new Exception("Zapis nie został podzielony według maxRecvSize");
	}
	if(!server.Destroyed)
	{
		throw new Exception("Łącze VXI-11 nie zostało zamknięte");
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

Test("Niestandardowe obciążenie nie jest przedstawiane jako 50 omów",()=>
{
	ChannelSnapshot value=SiglentProtocol.ParseSnapshot(
		1,
		"C1:BSWV WVTP,SINE,FRQ,1KHZ,AMP,1V,OFST,0V,PHSE,0",
		"C1:OUTP OFF,LOAD,75,PLRT,NOR");
	Equal(OutputLoad.Custom,value.Load);
	Close(75,value.LoadOhms ?? double.NaN);
	try
	{
		SiglentProtocol.LoadCommand(1,OutputLoad.Custom);
	}
	catch(ArgumentOutOfRangeException)
	{
		return;
	}
	throw new Exception("Próba zapisu niestandardowego obciążenia nie została odrzucona");
});

Test("Metadane aplikacji zachowują autora, wersję i licencję",()=>
{
	Equal("Mateusz Skipor",ProductInformation.AuthorName);
	Equal("Inżynier technik elektroniki",ProductInformation.AuthorProfession);
	Equal("mskiporsklep@op.pl",ProductInformation.AuthorEmail);
	Equal("0.1.2",ProductInformation.Version);
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

Test("Interfejs zachowuje kompaktowy rozmiar i pełne pola klikalne",()=>
{
	RunSta(()=>
	{
		Sdg1032X.App.App application=new();
		application.InitializeComponent();
		MainWindow window=new();
		window.Measure(new Size(350,749));
		window.Arrange(new Rect(0,0,350,749));
		window.ApplyTemplate();

		Equal(749d,window.Height);
		MenuItem about=LogicalChildren<MenuItem>(window)
			.First(item=>item.Items.Count > 0);
		Equal("O Aplikacji",about.Header);
		TextBox host=LogicalChildren<TextBox>(window)
			.First(textBox=>textBox.Name == "HostEditor");
		Equal("192.168.200.132",host.Text);
		if(host.Padding.Top>2 || host.Padding.Bottom>2)
		{
			throw new Exception("Pole IP ma zbyt duży pionowy margines wewnętrzny");
		}
		if(LogicalChildren<Button>(window)
			.Any(button=>Equals(button.Content,"Odczytaj kanał")))
		{
			throw new Exception("Pozostał zbędny przycisk odczytu kanału");
		}

		ComboBox combo=LogicalChildren<ComboBox>(window).First();
		combo.Measure(new Size(300,30));
		combo.Arrange(new Rect(0,0,300,30));
		combo.ApplyTemplate();
		ToggleButton toggle=VisualChildren<ToggleButton>(combo).Single();
		if(toggle.ActualWidth+1<combo.ActualWidth)
		{
			throw new Exception("Lista otwiera się tylko po kliknięciu strzałki");
		}

		InfoWindow author=new("Autor","Autor testowy",false);
		author.Measure(new Size(author.Width,author.Height));
		author.Arrange(new Rect(0,0,author.Width,author.Height));
		author.ApplyTemplate();
		if(LogicalChildren<Button>(author).Any())
		{
			throw new Exception("Okno autora nadal zawiera przycisk Zamknij");
		}
		if(!LogicalChildren<TextBlock>(author).Any(textBlock=>textBlock.Text == "Autor testowy"))
		{
			throw new Exception("Informacje o autorze nie używają właściwego tła okna");
		}

		InfoWindow license=new("Licencja","Treść licencji",true);
		license.Measure(new Size(license.Width,license.Height));
		license.Arrange(new Rect(0,0,license.Width,license.Height));
		license.ApplyTemplate();
		if(LogicalChildren<Button>(license).Any())
		{
			throw new Exception("Okno licencji nadal zawiera przycisk Zamknij");
		}
	});
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

sealed class FakeVxi11Server : IDisposable
{
	private const uint Program=395183;
	private readonly byte[] response;
	private readonly TcpListener mapper=new(IPAddress.Loopback,0);
	private readonly TcpListener core=new(IPAddress.Loopback,0);
	private readonly MemoryStream written=new();
	private readonly Task mapperTask;
	private readonly Task coreTask;

	public FakeVxi11Server(string identity)
	{
		response=Encoding.ASCII.GetBytes(identity+"\n");
		core.Start();
		mapper.Start();
		MapperPort=((IPEndPoint)mapper.LocalEndpoint).Port;
		mapperTask=Task.Run(ServeMapper);
		coreTask=Task.Run(ServeCore);
	}

	public int MapperPort { get; }
	public int WriteCalls { get; private set; }
	public bool Destroyed { get; private set; }
	public byte[] WrittenBytes => written.ToArray();

	public void Wait()
	{
		if(!Task.WaitAll([mapperTask,coreTask],TimeSpan.FromSeconds(5)))
		{
			throw new TimeoutException("Lokalny serwer VXI-11 nie zakończył pracy");
		}
	}

	public void Dispose()
	{
		mapper.Stop();
		core.Stop();
		try
		{
			Task.WaitAll([mapperTask,coreTask],TimeSpan.FromSeconds(1));
		}
		catch(AggregateException)
		{
		}
		written.Dispose();
	}

	private void ServeMapper()
	{
		using TcpClient client=mapper.AcceptTcpClient();
		NetworkStream network=client.GetStream();
		(uint id,uint procedure,Xdr call)=ReadCall(network);
		if(procedure != 3)
		{
			throw new InvalidDataException("Nieoczekiwana procedura portmapper");
		}
		call.Get();
		call.Get();
		call.Get();
		call.Get();
		SendReply(network,id,xdr=>xdr.Put((uint)((IPEndPoint)core.LocalEndpoint).Port),false);
	}

	private void ServeCore()
	{
		using TcpClient client=core.AcceptTcpClient();
		NetworkStream network=client.GetStream();
		while(true)
		{
			(uint id,uint procedure,Xdr call)=ReadCall(network);
			switch(procedure)
			{
				case 10:
					call.Get();
					call.Get();
					call.Get();
					call.GetBytes();
					SendReply(network,id,xdr=>
					{
						xdr.Put(0);
						xdr.Put(123);
						xdr.Put(0);
						xdr.Put(4);
					},false);
					break;
				case 11:
					call.Get();
					call.Get();
					call.Get();
					call.Get();
					byte[] part=call.GetBytes();
					written.Write(part);
					WriteCalls++;
					SendReply(network,id,xdr=>
					{
						xdr.Put(0);
						xdr.Put((uint)part.Length);
					},false);
					break;
				case 12:
					for(int index=0;index<6;index++)
					{
						call.Get();
					}
					SendReply(network,id,xdr=>
					{
						xdr.Put(0);
						xdr.Put(4);
						xdr.PutBytes(response);
					},true);
					break;
				case 23:
					call.Get();
					Destroyed=true;
					SendReply(network,id,xdr=>xdr.Put(0),false);
					return;
				default:
					throw new InvalidDataException("Nieoczekiwana procedura VXI-11: "+procedure);
			}
		}
	}

	private static (uint Id,uint Procedure,Xdr Arguments) ReadCall(NetworkStream network)
	{
		Xdr call=new(ReadRecord(network));
		uint id=call.Get();
		if(call.Get() != 0 || call.Get() != 2)
		{
			throw new InvalidDataException("Nieprawidłowe wywołanie RPC");
		}
		call.Get();
		call.Get();
		uint procedure=call.Get();
		call.Get();
		call.GetBytes();
		call.Get();
		call.GetBytes();
		return (id,procedure,call);
	}

	private static byte[] ReadRecord(NetworkStream network)
	{
		using MemoryStream result=new();
		byte[] markerBytes=new byte[4];
		while(true)
		{
			network.ReadExactly(markerBytes);
			uint marker=BinaryPrimitives.ReadUInt32BigEndian(markerBytes);
			byte[] part=new byte[marker&0x7fffffffu];
			network.ReadExactly(part);
			result.Write(part);
			if((marker&0x80000000u) != 0)
			{
				return result.ToArray();
			}
		}
	}

	private static void SendReply(NetworkStream network,uint id,Action<Xdr> body,bool split)
	{
		Xdr reply=new();
		reply.Put(id);
		reply.Put(1);
		reply.Put(0);
		reply.Put(0);
		reply.PutBytes([]);
		reply.Put(0);
		body(reply);
		byte[] bytes=reply.Bytes();
		if(!split)
		{
			SendFragment(network,bytes,true);
			return;
		}
		int middle=bytes.Length/2;
		SendFragment(network,bytes[..middle],false);
		SendFragment(network,bytes[middle..],true);
	}

	private static void SendFragment(NetworkStream network,byte[] bytes,bool last)
	{
		byte[] marker=new byte[4];
		uint value=(uint)bytes.Length|(last ? 0x80000000u : 0);
		BinaryPrimitives.WriteUInt32BigEndian(marker,value);
		network.Write(marker);
		network.Write(bytes);
	}
}
