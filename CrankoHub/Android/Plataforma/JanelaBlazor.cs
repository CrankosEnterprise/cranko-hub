using CrankoHub.Linux;
using CrankoHub.Services;
using Microsoft.AspNetCore.Components.WebView.Maui;
using Microsoft.Extensions.FileProviders;

namespace CrankoHub.Movel;

/// <summary>
/// BlazorWebView que também enxerga a pasta das capas do Entretenimento em "capas/"
/// (o mesmo ArquivosDoApp da versão Linux).
/// </summary>
public class JanelaBlazor : BlazorWebView
{
    public override IFileProvider CreateFileProvider(string contentRootDir) =>
        new ArquivosDoApp(base.CreateFileProvider(contentRootDir), new PhysicalFileProvider(CapasService.Pasta));
}
