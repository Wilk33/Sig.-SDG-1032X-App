using System.Buffers.Binary;
using System.Net.Sockets;
using System.Text;

namespace Sdg1032X.Core;

public sealed class Xdr
{
	private const int MaximumMessageSize=32*1024*1024;
	private readonly MemoryStream stream;

	public Xdr()
	{
		stream=new();
	}

	public Xdr(byte[] bytes)
	{
		stream=new(bytes,false);
	}

	public void Put(uint value)
	{
		Span<byte> buffer=stackalloc byte[4];
		BinaryPrimitives.WriteUInt32BigEndian(buffer,value);
		stream.Write(buffer);
	}

	public uint Get()
	{
		Span<byte> buffer=stackalloc byte[4];
		stream.ReadExactly(buffer);
		return BinaryPrimitives.ReadUInt32BigEndian(buffer);
	}

	public void PutBytes(byte[] bytes)
	{
		Put((uint)bytes.Length);
		stream.Write(bytes);
		for(int index=bytes.Length;index%4 != 0;index++)
		{
			stream.WriteByte(0);
		}
	}

	public byte[] GetBytes()
	{
		uint size=Get();
		if(size>MaximumMessageSize)
		{
			throw new InvalidDataException("Zbyt duży blok RPC.");
		}
		byte[] bytes=new byte[size];
		stream.ReadExactly(bytes);
		int padding=(4-(int)size%4)%4;
		Span<byte> pad=stackalloc byte[3];
		stream.ReadExactly(pad[..padding]);
		return bytes;
	}

	public byte[] Bytes()
	{
		return stream.ToArray();
	}
}

internal sealed class RpcConnection : IDisposable
{
	private const int MaximumMessageSize=32*1024*1024;
	private readonly TcpClient client=new();
	private uint sequence;

	public RpcConnection(string host,int port)
	{
		try
		{
			client.ConnectAsync(host,port).WaitAsync(TimeSpan.FromSeconds(5)).GetAwaiter().GetResult();
			client.ReceiveTimeout=15000;
			client.SendTimeout=15000;
			client.NoDelay=true;
		}
		catch
		{
			client.Dispose();
			throw;
		}
	}

	public Xdr Call(uint program,uint version,uint procedure,Action<Xdr> arguments)
	{
		Xdr call=new();
		uint id=++sequence;
		foreach(uint value in new[]{id,0u,2u,program,version,procedure,0u,0u,0u,0u})
		{
			call.Put(value);
		}
		arguments(call);
		byte[] payload=call.Bytes();
		NetworkStream network=client.GetStream();
		byte[] header=new byte[4];
		BinaryPrimitives.WriteUInt32BigEndian(header,0x80000000u|(uint)payload.Length);
		network.Write(header);
		network.Write(payload);
		using MemoryStream reply=new();
		for(int fragments=0;;fragments++)
		{
			network.ReadExactly(header);
			uint marker=BinaryPrimitives.ReadUInt32BigEndian(header);
			int count=(int)(marker&0x7fffffffu);
			if(fragments>4096 || count>MaximumMessageSize || reply.Length+count>MaximumMessageSize)
			{
				throw new InvalidDataException("Zbyt duża odpowiedź RPC.");
			}
			byte[] part=new byte[count];
			network.ReadExactly(part);
			reply.Write(part);
			if((marker&0x80000000u) != 0)
			{
				break;
			}
		}
		Xdr result=new(reply.ToArray());
		if(result.Get() != id || result.Get() != 1 || result.Get() != 0)
		{
			throw new IOException("Serwer odrzucił wywołanie RPC.");
		}
		result.Get();
		result.GetBytes();
		if(result.Get() != 0)
		{
			throw new IOException("Błąd procedury RPC.");
		}
		return result;
	}

	public void Dispose()
	{
		client.Dispose();
	}
}

public sealed class Vxi11Transport : IInstrumentTransport
{
	private const uint Program=395183;
	private const int MaximumMessageSize=32*1024*1024;
	private readonly RpcConnection core;
	private readonly uint link;
	private readonly int maximumWrite;
	private bool disposed;

	public Vxi11Transport(string host,int mapperPort=111)
	{
		using(RpcConnection mapper=new(host,mapperPort))
		{
			Xdr port=mapper.Call(100000,2,3,xdr=>
			{
				xdr.Put(Program);
				xdr.Put(1);
				xdr.Put(6);
				xdr.Put(0);
			});
			uint number=port.Get();
			if(number == 0 || number>65535)
			{
				throw new IOException("VXI-11 jest niedostępne pod tym adresem.");
			}
			core=new(host,(int)number);
		}
		try
		{
			Xdr response=core.Call(Program,1,10,xdr=>
			{
				xdr.Put((uint)Random.Shared.Next(1,int.MaxValue));
				xdr.Put(0);
				xdr.Put(10000);
				xdr.PutBytes(Encoding.ASCII.GetBytes("inst0"));
			});
			Check(response.Get());
			link=response.Get();
			response.Get();
			uint size=response.Get();
			maximumWrite=(int)Math.Clamp(size,1u,1048576u);
		}
		catch
		{
			core.Dispose();
			throw;
		}
	}

	public void Write(string command)
	{
		ObjectDisposedException.ThrowIf(disposed,this);
		byte[] data=Encoding.ASCII.GetBytes(command+"\n");
		for(int offset=0;offset<data.Length;)
		{
			int count=Math.Min(maximumWrite,data.Length-offset);
			byte[] chunk=data.AsSpan(offset,count).ToArray();
			uint flags=offset+count == data.Length ? 8u : 0u;
			Xdr reply=core.Call(Program,1,11,xdr=>
			{
				xdr.Put(link);
				xdr.Put(10000);
				xdr.Put(10000);
				xdr.Put(flags);
				xdr.PutBytes(chunk);
			});
			Check(reply.Get());
			if(reply.Get() != count)
			{
				throw new IOException("Niepełny zapis VXI-11.");
			}
			offset+=count;
		}
	}

	public byte[] Query(string command)
	{
		Write(command);
		using MemoryStream output=new();
		while(true)
		{
			Xdr reply=core.Call(Program,1,12,xdr=>
			{
				xdr.Put(link);
				xdr.Put(1048576);
				xdr.Put(10000);
				xdr.Put(10000);
				xdr.Put(0);
				xdr.Put(0);
			});
			Check(reply.Get());
			uint reason=reply.Get();
			byte[] data=reply.GetBytes();
			if(data.Length == 0 && (reason&4) == 0)
			{
				throw new IOException("Pusta odpowiedź VXI-11.");
			}
			if(output.Length+data.Length>MaximumMessageSize)
			{
				throw new IOException("Przekroczony limit danych.");
			}
			output.Write(data);
			if((reason&4) != 0)
			{
				return output.ToArray();
			}
			if((reason&2) != 0)
			{
				throw new IOException("Nieoczekiwane zakończenie transferu.");
			}
		}
	}

	public void Dispose()
	{
		if(disposed)
		{
			return;
		}
		disposed=true;
		try
		{
			core.Call(Program,1,23,xdr=>xdr.Put(link));
		}
		catch
		{
		}
		core.Dispose();
	}

	private static void Check(uint error)
	{
		if(error != 0)
		{
			throw new IOException(
				$"Błąd VXI-11: {error}"+(error == 15 ? " - przekroczony czas odpowiedzi." : "."));
		}
	}
}
