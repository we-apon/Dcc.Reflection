using System.Text.Json;
using Dcc.Reflection.TypeResolver;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TechTalk.SpecFlow;

namespace Dcc.SpecFlow;

public abstract class WebApplicationFactoryScenario<TStartup, TTypeResolver> : ContextScenario<TTypeResolver> where TStartup : class where TTypeResolver : ITypeResolver {


    protected virtual (HttpClient HttpClient, WebApplicationFactory<TStartup> Factory) SetupWebAppFactory(ScenarioContext context, Action<IWebHostBuilder>? configure = null) {
        var factory = new WebApplicationFactory<TStartup>();
        factory = factory.WithWebHostBuilder(builder => configure?.Invoke(builder));

        var httpClient = context.ScenarioInfo.Tags.Contains("noAutoRedirect")
            ? factory.CreateClient(new() {
                AllowAutoRedirect = false
            })
            : factory.CreateClient();

        context.Set(factory);
        context.Set(factory.Services);
        context.Set(httpClient);
        context.Set(factory.Services.GetRequiredService<JsonSerializerOptions>());
        context.Set(factory.Services.GetRequiredService<IConfiguration>());

        var mediatrType = TTypeResolver.ResolveByFullName("MediatR.IMediator");
        if (mediatrType != null) {
            var mediatr = factory.Services.GetService(mediatrType);
            if (mediatr != null) {
                context.TryAdd("MediatR.IMediator", mediatr);
                context.TryAdd("IMediator", mediatr);
            }
        }

        return (httpClient, factory);
    }
}
