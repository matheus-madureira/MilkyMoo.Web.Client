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
string authBaseUrl = builder.Configuration["Api:BaseUrl"]
    ?? throw new InvalidOperationException("Api:BaseUrl não configurado.");

ApiOptions apiOptions = new(
    authBaseUrl,
    builder.Configuration["Api:Tenant"] ?? throw new InvalidOperationException("Api:Tenant não configurado."))
{
    // Ausente, a API de negócio compartilha a base da autenticação — é o destino, e hoje a exceção.
    ProductsBaseUrl = builder.Configuration["Api:ProductsBaseUrl"] ?? authBaseUrl
};

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

// Mesmo handler do cliente de auth, e de propósito: ele é quem põe o Bearer guardado no navegador, renova o
// token vencido antes de enviar e repete a chamada uma vez no 401. Duplicar isso num handler só de produtos
// duplicaria a parte mais delicada da sessão.
builder.Services.AddScoped(sp => new ProductsApiClient(
    new HttpClient(new AuthTokenHandler(sp.GetRequiredService<AuthSession>(), apiOptions)
    {
        InnerHandler = new HttpClientHandler()
    })
    {
        BaseAddress = new Uri(apiOptions.ProductsBaseUrl)
    }));

builder.Services.AddScoped<AuthService>();

builder.Services.AddScoped<ProductsService>();

// A política de admin lê o papel pelo indexador, como o resto da configuração: Get<T>() depende de
// reflexão e o publish do WASM roda com trimming. Vazio hoje = todo autenticado passa (AdminPolicy).
builder.Services.AddAuthorizationCore(options =>
    options.AddPolicy(AdminPolicy.Name, AdminPolicy.Build(builder.Configuration["Auth:AdminRoles"])));

builder.Services.AddScoped<AuthenticationStateProvider, MilkyMooAuthenticationStateProvider>();

WebAssemblyHost host = builder.Build();

// A sessão é restaurada antes da primeira renderização: sem isso, quem já estava logado veria a tela de
// login piscar antes de o app decidir que há sessão.
await host.Services.GetRequiredService<AuthService>().RestoreSessionAsync();

await host.RunAsync();
