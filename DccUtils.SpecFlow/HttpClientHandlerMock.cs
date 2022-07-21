using System.Collections.Concurrent;
using Dcc.Extensions;
using Moq;
using Moq.Protected;

namespace Dcc.SpecFlow;

public class HttpClientHandlerMock : Mock<HttpClientHandler> {

    readonly ConcurrentBag<AnswerContainer> _answers = new();

    public ConcurrentBag<HttpRequestMessage> Requests { get; } = new();

    public ConcurrentBag<(HttpRequestMessage Request, HttpResponseMessage Response)> Messages { get; } = new();

    public int ResultTimeout { get; set; } = 5000;

    public class AnswerContainer {
        public Func<HttpRequestMessage, bool> RequestFilter { get; set; } = null!;
        public Func<Task<HttpResponseMessage>> GetResponseMessage { get; set; } = null!;
        public TaskCompletionSource<HttpRequestMessage> Completion { get; } = new();
    }


    public HttpClientHandlerMock() : base(MockBehavior.Strict) {
        this.Protected().Setup<Task<HttpResponseMessage>>(
            "SendAsync",
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>()
        ).Returns<HttpRequestMessage, CancellationToken>(async (message, token) => {
            Requests.Add(message);
            var answer = await _answers.AwaitItem(x => x.RequestFilter.Invoke(message), ResultTimeout, cancellationToken:token);
            if (answer == null) {
                var content = message.Content != null
                    ? await message.Content.ReadAsStringAsync()
                    : null;

                throw new InvalidOperationException($"Не найден подходящий ответ под запрос {message.Method.Method} {message.RequestUri} {content}");
            }

            var response = await answer.GetResponseMessage();

            Messages.Add((message, response));

#pragma warning disable CS4014
            //? комплишн выставляется с небольшой задержкой, чтобы клиент авейтящий метод Answer получил управление 100% после возвращения респонса
            Task.Delay(10, token).ContinueWith(_ => answer.Completion.SetResult(message), token);
#pragma warning restore CS4014

            return response;
        }).Verifiable();
    }


    public void SetFutureAnswer(AnswerContainer answer) {
        _answers.Add(answer);
    }

    public async Task<HttpRequestMessage?> Answer(AnswerContainer answer, CancellationToken cancellationToken = default) {
        _answers.Add(answer);
        await Task.WhenAny(Task.Delay(ResultTimeout, cancellationToken), answer.Completion.Task);
        if (answer.Completion.Task.IsCompletedSuccessfully) {
            return await answer.Completion.Task;
        }

        return null;
    }

    public async Task<HttpRequestMessage?> Answer(Func<HttpRequestMessage, bool> requestFilter, HttpResponseMessage message, CancellationToken cancellationToken = default) {
        return await Answer(requestFilter, () => Task.FromResult(message), cancellationToken);
    }

    public async Task<HttpRequestMessage?> Answer(Func<HttpRequestMessage, bool> requestFilter, Func<HttpResponseMessage> getResponseCallback, CancellationToken cancellationToken = default) {
        return await Answer(requestFilter, () => Task.FromResult(getResponseCallback.Invoke()), cancellationToken);
    }

    public async Task<HttpRequestMessage?> Answer(Func<HttpRequestMessage, bool> requestFilter, Func<Task<HttpResponseMessage>> getResponseCallback, CancellationToken cancellationToken = default) {
        return await Answer(new() {
            RequestFilter = requestFilter,
            GetResponseMessage = getResponseCallback
        }, cancellationToken);
    }

    public async Task<HttpRequestMessage?> WaitRequest(Func<HttpRequestMessage, bool> requestFilter, CancellationToken cancellationToken = default) {
        return await Requests.AwaitItem(requestFilter, ResultTimeout, cancellationToken: cancellationToken);
    }
}
