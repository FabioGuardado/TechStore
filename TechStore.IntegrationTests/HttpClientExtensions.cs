using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc.Testing;

namespace TechStore.IntegrationTests;

public static class HttpClientExtensions
{
    public static HttpClient CreateClientSinRedirecciones(this TechStoreFactory factory)
    {
        return factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    public static string Ruta(this HttpResponseMessage response)
    {
        var location = response.Headers.Location!;
        return location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;
    }

    public static async Task<string> LeerHtmlAsync(this HttpResponseMessage response)
    {
        return WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
    }

    public static async Task<string> ObtenerTokenAsync(this HttpClient client, string url)
    {
        var html = await client.GetStringAsync(url);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, $"No se encontró el token antifalsificación en {url}.");
        return match.Groups[1].Value;
    }

    public static async Task<HttpResponseMessage> PostFormularioAsync(
        this HttpClient client,
        string url,
        string urlDelToken,
        Dictionary<string, string> campos)
    {
        var token = await client.ObtenerTokenAsync(urlDelToken);
        campos["__RequestVerificationToken"] = token;
        return await client.PostAsync(url, new FormUrlEncodedContent(campos));
    }

    public static async Task IniciarSesionAsync(this HttpClient client, string usuario, string password)
    {
        var respuesta = await client.PostFormularioAsync("/Cuenta/Login", "/Cuenta/Login", new Dictionary<string, string>
        {
            ["UsuarioOCorreo"] = usuario,
            ["Password"] = password
        });

        Assert.Equal(HttpStatusCode.Redirect, respuesta.StatusCode);
    }
}
