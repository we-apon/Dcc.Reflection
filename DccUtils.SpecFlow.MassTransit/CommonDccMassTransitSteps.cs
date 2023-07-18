using Dcc.Extensions;
using Dcc.Reflection.TypeResolver;
using FluentAssertions;
using MassTransit.Testing;
using TechTalk.SpecFlow;

namespace Dcc.SpecFlow.MassTransit;

#if NET7_0_OR_GREATER

[Binding]
public class CommonDccMassTransitSteps : CommonDccMassTransitSteps<TypeResolver> {  }

public class CommonDccMassTransitSteps<TTypeResolver> : ContextScenario<TTypeResolver> where TTypeResolver : ITypeResolver {

#else

[Binding]
public class CommonDccMassTransitSteps : ContextScenario {

#endif

    [When(@"в шину поступает сообщение ""(.*)"", содержащее")]
    [Then(@"в шину поступает сообщение ""(.*)"", содержащее")]
    public async Task ThenВШинуПоступаетСообщениеGetPaymentStateRequest(string typeName, Table table) {
        var type = GetTypeByName(typeName);

        var testHarness = Get<ITestHarness>();
        var message = await testHarness.Published.SelectAsync(context => context.MessageType == type, new CancellationTokenSource(Timeout).Token).FirstOrDefault();
        message.Should().NotBeNull();

        ShouldBeAsTable(message.MessageObject, table);
    }

    [When(@"из шины в ответ на ""(.*)"" возвращается ""(.*)"", содержащий")]
    [Then(@"из шины в ответ на ""(.*)"" возвращается ""(.*)"", содержащий")]
    public async Task GivenИзШиныВОтветНаGetPaymentStateRequestВозвращаетсяResponseСодержащий(string requestTypeName, string responseTypeName, Table table) {
        var harness = Get<ITestHarness>();

        var requestType = GetTypeByName(requestTypeName);
        var request = await Get<ITestHarness>().Published.SelectAsync(context => context.MessageType == requestType, new CancellationTokenSource(Timeout).Token).FirstOrDefault();
        request.Should().NotBeNull();

        var responseType = GetTypeByName(responseTypeName);
        var response = await harness.Sent.SelectAsync(context => context.MessageType == responseType).FirstOrDefault();
        response.Should().NotBeNull();

        response.Context.ConversationId.Should().Be(request.Context.ConversationId);

        ShouldBeAsTable(response.MessageObject, table);
    }


    [Given(@"ConsumerMock в ответ на ""(.*)"" c корреляцией по ""(.*)"" (.*) возвращает ""(.*)"", содержащий")]
    public void GivenConsumerMockВОтветНаВозвращает(string requestTypeName, string correlationPropName, string correlationValue, string responseTypeName, Table table) {
        var requestType = GetTypeByName(requestTypeName);
        requestType.Should().NotBeNull();
        ConsumerMock.SetupCallback(requestType!, new() {
            CanConsume = (context, message) => message.GetPropertyValue(correlationPropName)?.ToString()?.Equals(correlationValue, StringComparison.InvariantCultureIgnoreCase) == true,
            Consume = (context, message) => GetFromTable(responseTypeName, table)
        });
    }
}
