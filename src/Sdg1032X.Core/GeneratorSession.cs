namespace Sdg1032X.Core;

public sealed class GeneratorSession : IAsyncDisposable
{
	private readonly SiglentGeneratorClient client;
	private readonly InstrumentRequestQueue requests=new();
	private readonly SemaphoreSlim ioGate=new(1,1);
	private readonly CancellationTokenSource cancellation=new();
	private readonly Task worker;
	private bool disposed;

	private GeneratorSession(SiglentGeneratorClient generatorClient,string identity)
	{
		client=generatorClient;
		Identity=identity;
		worker=Task.Run(ProcessWritesAsync);
	}

	public event EventHandler<Exception>? CommunicationFailed;
	public string Identity { get; }

	public static async Task<GeneratorSession> ConnectAsync(string host)
	{
		return await CreateAsync(()=>new Vxi11Transport(host));
	}

	public static async Task<GeneratorSession> CreateAsync(Func<IInstrumentTransport> transportFactory)
	{
		return await Task.Run(()=>
		{
			IInstrumentTransport transport=transportFactory();
			try
			{
				SiglentGeneratorClient client=new(transport);
				string identity=client.Initialize();
				return new GeneratorSession(client,identity);
			}
			catch
			{
				transport.Dispose();
				throw;
			}
		});
	}

	public Task SetLatestAsync(string key,string command)
	{
		ObjectDisposedException.ThrowIf(disposed,this);
		return requests.EnqueueLatest(key,command).Completion;
	}

	public Task WritePriorityAsync(string command)
	{
		ObjectDisposedException.ThrowIf(disposed,this);
		return requests.EnqueuePriority(command).Completion;
	}

	public async Task<ChannelSnapshot> ReadChannelAsync(int channel)
	{
		ObjectDisposedException.ThrowIf(disposed,this);
		await ioGate.WaitAsync(cancellation.Token);
		try
		{
			return await Task.Run(()=>client.ReadChannel(channel),cancellation.Token);
		}
		finally
		{
			ioGate.Release();
		}
	}

	public async ValueTask DisposeAsync()
	{
		if(disposed)
		{
			return;
		}
		disposed=true;
		cancellation.Cancel();
		try
		{
			await worker;
		}
		catch(OperationCanceledException)
		{
		}
		await ioGate.WaitAsync();
		try
		{
			client.Dispose();
		}
		finally
		{
			ioGate.Release();
			ioGate.Dispose();
			cancellation.Dispose();
		}
	}

	private async Task ProcessWritesAsync()
	{
		while(!cancellation.IsCancellationRequested)
		{
			await requests.WaitAsync(cancellation.Token);
			while(requests.TryTakeNext(out InstrumentRequest? request))
			{
				try
				{
					await ioGate.WaitAsync(cancellation.Token);
					try
					{
						await Task.Run(()=>client.Write(request.Command),cancellation.Token);
					}
					finally
					{
						ioGate.Release();
					}
					request.Succeed();
				}
				catch(OperationCanceledException)
				{
					request.Cancel();
					throw;
				}
				catch(Exception exception)
				{
					request.Fail(exception);
					CommunicationFailed?.Invoke(this,exception);
				}
			}
		}
	}
}
