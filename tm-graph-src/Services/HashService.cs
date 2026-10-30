using System.Security.Cryptography;
using System.Text;

namespace tm_graph_src.Services;

public class HashService : IHashService
{
    public string Hash<T>(T data)
    {
        var bytes = MessagePack.MessagePackSerializer.Serialize(data);
        return Convert.ToHexString(SHA256.HashData(bytes));
    }

    public bool Verify<T>(T data, string hash)
    {
        var dataHash = Hash(data);
        return CryptographicOperations.FixedTimeEquals(MessagePack.MessagePackSerializer.Serialize(dataHash), MessagePack.MessagePackSerializer.Serialize(hash));
    }
}

public interface IHashService
{
    public string Hash<T>(T data);
    public bool Verify<T>(T data, string hash);
}