using System.Text.Json;
using MassTransit;
using MassTransit.Testing;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using TechTalk.SpecFlow;

namespace Dcc.SpecFlow.MassTransit;

public abstract class MassTransitScenario<TStartup> : ContextScenario where TStartup : class {
    protected virtual (HttpClient HttpClient, WebApplicationFactory<TStartup> Factory, ITestHarness Harness) DoSetup(ScenarioContext context, Action<IServiceCollection>? configureServices = null, Action<IBusRegistrationConfigurator>? configureMassTransit = null) {
        var factory = new WebApplicationFactory<TStartup>();
        factory = factory.WithWebHostBuilder(builder => builder.ConfigureServices(services => {
            services.ReplaceMassTransitWithTestHarness(context, configureMassTransit);
            configureServices?.Invoke(services);
        }));

        var httpClient = context.ScenarioInfo.Tags.Contains("noAutoRedirect")
            ? factory.CreateClient(new() {
                AllowAutoRedirect = false
            })
            : factory.CreateClient();

        //? фактически таймауты могут быть заданы отдельными консумерами,
        //? т.к. здесь они не добавляются в коллекцию сервисов заново
        var harness = factory.Services.GetRequiredService<ITestHarness>();
        harness.TestTimeout = TimeSpan.FromSeconds(10);

        context.Set(factory);
        context.Set(factory.Services);
        context.Set(httpClient);
        context.Set(harness);
        context.Set(factory.Services.GetRequiredService<JsonSerializerOptions>());

        return (httpClient, factory, harness);
    }
}
