using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dcc.Reflection.TypeFormatting;
using MassTransit;
using Moq;

namespace Dcc.SpecFlow.MassTransit;

public static class ConsumerMock {
    static readonly ConcurrentDictionary<Type, ConcurrentBag<Callback>> CallbackMapping = new();

    public class Callback {
        public delegate bool CanConsumeDelegate(ConsumeContext context, object message);

        public delegate object ConsumeDelegate(ConsumeContext context, object message);

        public CanConsumeDelegate CanConsume { get; set; }

        public ConsumeDelegate Consume { get; set; }
    }


    public static Callback? GetCallback(Type messageType, ConsumeContext context, object message) {
        if (!CallbackMapping.TryGetValue(messageType, out var bag))
            return null;

        return bag.SingleOrDefault(x => x.CanConsume(context, message));
    }

    public static void SetupCallback(Type messageType, Callback callback) {
        if (!CallbackMapping.TryAdd(messageType, new() {callback})) {
            var bag = CallbackMapping[messageType];
            bag.Add(callback);
        }
    }

}

public class ConsumerMock<TMessage> : Mock<IConsumer<TMessage>>, IConsumer<TMessage> where TMessage : class{
    static readonly Type MessageType = typeof(TMessage);

    static string Describe(object message) {
        try {
            return JsonSerializer.Serialize(message, DefaultJsonSerializerOptions.Instance);
        }
        catch {
            return message.ToString()!;
        }
    }

    // static readonly ConcurrentBag<ConsumerMockCallback> Callbacks = new();

    public ConsumerMock() {
        Setup(x => x.Consume(It.IsAny<ConsumeContext<TMessage>>()))
            .Callback<ConsumeContext<TMessage>>(AwaitResponse);
    }

    async void AwaitResponse(ConsumeContext<TMessage> context) {
        var watch = new Stopwatch();

        while (true) {
            context.CancellationToken.ThrowIfCancellationRequested();

            if (watch.Elapsed > TimeSpan.FromSeconds(50)) {
                throw new Exception($"{nameof(ConsumerMock<TMessage>)} {nameof(ConsumerMock.Callback)} is missing for message {MessageType.GetNestedName()} {Describe(context.Message)}");
            }
            var callback = ConsumerMock.GetCallback(MessageType, context, context.Message);
            if (callback == null) {
                await Task.Delay(10);
                continue;
            }

            var result = callback.Consume(context, context.Message);
            await context.RespondAsync(result, result.GetType());
            return;
        }
    }

    public async Task Consume(ConsumeContext<TMessage> context) {
        context.CancellationToken.ThrowIfCancellationRequested();
        await Object.Consume(context);
    }

    public static void SetupCallback(ConsumerMock.Callback callback) => ConsumerMock.SetupCallback(MessageType, callback);
}

