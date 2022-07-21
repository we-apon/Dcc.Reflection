using Dcc.Reflection.TypeResolver;
using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using TechTalk.SpecFlow;

namespace Dcc.SpecFlow.MassTransit;

public static class ServiceCollectionExtensions {
    public static void ReplaceMassTransitWithTestHarness(this IServiceCollection services, ScenarioContext? context = null, Action<IBusRegistrationConfigurator>? configure = null) {
        var massTransitServices = services.Where(x => x.ServiceType.Namespace?.StartsWith("MassTransit") == true).ToList();
        foreach (var service in massTransitServices) {
            services.Remove(service);
        }

        services.AddMassTransitTestHarness(configurator => {
            if (context != null) {
                foreach (var consumerMockTypeName in context.ScenarioInfo.Tags.Where(x => x.StartsWith(nameof(ConsumerMock<object>)))) {
                    var type = TypeResolver.Resolve(consumerMockTypeName);
                    type.Should().NotBeNull($"Scenario has tag {consumerMockTypeName}");
                    configurator.AddConsumer(type);
                }
            }

            configure?.Invoke(configurator);
        });
    }
}
