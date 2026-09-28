using System.Diagnostics.CodeAnalysis;

namespace Sdg1032X.Core;

public sealed class InstrumentRequest
{
	private readonly TaskCompletionSource completion=
		new(TaskCreationOptions.RunContinuationsAsynchronously);

	public InstrumentRequest(string command)
	{
		Command=command;
	}

	public string Command { get; }
	public Task Completion => completion.Task;

	internal void Succeed()
	{
		completion.TrySetResult();
	}

	internal void Fail(Exception exception)
	{
		completion.TrySetException(exception);
	}

	internal void Cancel()
	{
		completion.TrySetCanceled();
	}
}

public sealed class InstrumentRequestQueue
{
	private readonly object gate=new();
	private readonly Queue<InstrumentRequest> priority=[];
	private readonly LinkedList<(string Key,InstrumentRequest Request)> latest=[];
	private readonly Queue<InstrumentRequest> ordered=[];
	private readonly Dictionary<string,LinkedListNode<(string Key,InstrumentRequest Request)>> byKey=[];
	private readonly SemaphoreSlim signal=new(0,1);

	public int PendingCount
	{
		get
		{
			lock(gate)
			{
				return priority.Count+latest.Count+ordered.Count;
			}
		}
	}

	public InstrumentRequest EnqueueLatest(string key,string command)
	{
		InstrumentRequest request=new(command);
		lock(gate)
		{
			if(byKey.TryGetValue(key,out LinkedListNode<(string Key,InstrumentRequest Request)>? node))
			{
				node.Value.Request.Cancel();
				node.Value=(key,request);
			}
			else
			{
				byKey[key]=latest.AddLast((key,request));
			}
		}
		Signal();
		return request;
	}

	public InstrumentRequest EnqueuePriority(string command)
	{
		InstrumentRequest request=new(command);
		lock(gate)
		{
			priority.Enqueue(request);
		}
		Signal();
		return request;
	}

	public InstrumentRequest EnqueueOrdered(string command)
	{
		InstrumentRequest request=new(command);
		lock(gate)
		{
			ordered.Enqueue(request);
		}
		Signal();
		return request;
	}

	public void CancelAll()
	{
		lock(gate)
		{
			while(priority.TryDequeue(out InstrumentRequest? request))
			{
				request.Cancel();
			}
			foreach((string _,InstrumentRequest request) in latest)
			{
				request.Cancel();
			}
			latest.Clear();
			byKey.Clear();
			while(ordered.TryDequeue(out InstrumentRequest? request))
			{
				request.Cancel();
			}
		}
	}

	public InstrumentRequest TakeNext()
	{
		if(!TryTakeNext(out InstrumentRequest? request))
		{
			throw new InvalidOperationException("Kolejka jest pusta.");
		}
		return request ?? throw new InvalidOperationException("Kolejka zwróciła pusty element.");
	}

	public bool TryTakeNext([NotNullWhen(true)] out InstrumentRequest? request)
	{
		lock(gate)
		{
			if(priority.TryDequeue(out request))
			{
				return true;
			}
			LinkedListNode<(string Key,InstrumentRequest Request)>? node=latest.First;
			if(node is not null)
			{
				latest.RemoveFirst();
				byKey.Remove(node.Value.Key);
				request=node.Value.Request;
				return true;
			}
			return ordered.TryDequeue(out request);
		}
	}

	public ValueTask WaitAsync(CancellationToken cancellationToken)
	{
		return new(signal.WaitAsync(cancellationToken));
	}

	private void Signal()
	{
		try
		{
			signal.Release();
		}
		catch(SemaphoreFullException)
		{
		}
	}
}
