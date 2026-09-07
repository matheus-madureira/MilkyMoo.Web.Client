using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using MilkyMoo;
using MilkyMoo.Services;

WebAssemblyHostBuilder? builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");

builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Configuração lida pelo indexador, e não por Get<T>(): o binder depende de reflexão e o publish do WASM
// roda com trimming, onde esse caminho é o primeiro a quebrar em silêncio.
ApiOptions apiOptions = new(
    builder.Configuration["Api:BaseUrl"] ?? throw new InvalidOperationException("Api:BaseUrl não configurado."),
    builder.Configuration["Api:Tenant"] ?? throw new InvalidOperationException("Api:Tenant não configurado."));

builder.Services.AddSingleton(apiOptions);

builder.Services.AddScoped<TokenStore>();

// Cliente cru da sessão: sem o AuthTokenHandler, senão um 401 na renovação dispararia outra renovação.
builder.Services.AddScoped(sp => new AuthSession(
    new HttpClient { BaseAddress = new Uri(apiOptions.BaseUrl) },
    sp.GetRequiredService<TokenStore>(),
    apiOptions));

builder.Services.AddScoped(sp => new ApiHttpClient(
    new HttpClient(new AuthTokenHandler(sp.GetRequiredService<AuthSession>(), apiOptions)
    {
        InnerHandler = new HttpClientHandler()
    })
    {
        BaseAddress = new Uri(apiOptions.BaseUrl)
    }));

builder.Services.AddScoped<AuthService>();

builder.Services.AddAuthorizationCore();
builder.Services.AddScoped<AuthenticationStateProvider, MilkyMooAuthenticationStateProvider>();

WebAssemblyHost host = builder.Build();

// A sessão é restaurada antes da primeira renderização: sem isso, quem já estava logado veria a tela de
// login piscar antes de o app decidir que há sessão.
await host.Services.GetRequiredService<AuthService>().RestoreSessionAsync();

await host.RunAsync();
