namespace Snakk.Api.Services;

using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using Google.Protobuf;
using Microsoft.Extensions.Caching.Hybrid;

// HybridCache's default serializer is System.Text.Json, which cannot round-trip
// protobuf-generated messages: repeated-field properties (e.g. Items) are get-only,
// so they serialize to JSON but deserialize back as EMPTY collections. Any proto
// value rehydrated from the distributed L2 tier (Valkey) would silently lose its
// list contents. This factory serializes IMessage types as protobuf binary instead;
// non-proto types keep the default JSON serializer.
public sealed class ProtobufHybridCacheSerializerFactory : IHybridCacheSerializerFactory
{
    public bool TryCreateSerializer<T>([NotNullWhen(true)] out IHybridCacheSerializer<T>? serializer)
    {
        if (typeof(IMessage).IsAssignableFrom(typeof(T)))
        {
            serializer = (IHybridCacheSerializer<T>)Activator.CreateInstance(
                typeof(ProtobufSerializer<>).MakeGenericType(typeof(T)))!;
            return true;
        }

        serializer = null;
        return false;
    }

    private sealed class ProtobufSerializer<T> : IHybridCacheSerializer<T>
        where T : IMessage<T>, new()
    {
        private static readonly MessageParser<T> Parser = new(() => new T());

        public T Deserialize(ReadOnlySequence<byte> source) => Parser.ParseFrom(source);

        public void Serialize(T value, IBufferWriter<byte> target) => value.WriteTo(target);
    }
}
