using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Extensions;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace ElmTime.Platform.WebService;

public static class Asp
{
    private class ClientsRateLimitStateContainer
    {
        public readonly ConcurrentDictionary<string, IMutableRateLimit> RateLimitFromClientId = new();
    }

    public static void ConfigureServices(IServiceCollection services)
    {
        services.AddSingleton(new ClientsRateLimitStateContainer());
    }

    public static async Task MiddlewareFromWebServiceConfig(
        WebServiceConfigJson? serverConfig, HttpContext context, Func<Task> next) =>
        await RateLimitMiddlewareFromWebServiceConfig(serverConfig, context, next);

    private static async Task RateLimitMiddlewareFromWebServiceConfig(
        WebServiceConfigJson? serverConfig,
        HttpContext context,
        Func<Task> next)
    {
        const string DefaultClientId = "MapToIPv4-failed";

        string ClientId()
        {
            try
            {
                return context.Connection.RemoteIpAddress?.MapToIPv4().ToString() ?? DefaultClientId;
            }
            catch
            {
                return DefaultClientId;
            }
        }

        var rateLimitFromClientId =
            context.RequestServices.GetService<ClientsRateLimitStateContainer>()?.RateLimitFromClientId;

        var clientRateLimitState =
            rateLimitFromClientId?
            .GetOrAdd(ClientId(), _ => BuildRateLimitContainerForClient(serverConfig));

        if (clientRateLimitState?.AttemptPass(Configuration.GetDateTimeOffset(context).ToUnixTimeMilliseconds()) ?? true)
        {
            await next.Invoke();
            return;
        }

        context.Response.StatusCode = 429;
        await context.Response.WriteAsync("");
    }

    private static IMutableRateLimit BuildRateLimitContainerForClient(WebServiceConfigJson? jsonStructure)
    {
        if (jsonStructure?.singleRateLimitWindowPerClientIPv4Address is not { } singleRateLimitWindowPerClientIPv4Address)
            return new MutableRateLimitAlwaysPassing();

        return new RateLimitMutableContainer(new RateLimitStateSingleWindow
        (
            limit: singleRateLimitWindowPerClientIPv4Address.limit,
            windowSize: singleRateLimitWindowPerClientIPv4Address.windowSizeInMs,
            passes: []
        ));
    }

    public static async Task<Pine.Elm.Platform.WebServiceInterface.HttpRequestProperties>
        AsInterfaceHttpRequestAsync(HttpRequest httpRequest)
    {
        var httpHeaders = new List<Pine.Elm.Platform.WebServiceInterface.HttpHeader>(httpRequest.Headers.Count);

        // Convert the headers to the interface representation.
        for (var i = 0; i < httpRequest.Headers.Count; i++)
        {
            var header = httpRequest.Headers.ElementAt(i);

            var values = new List<string>(header.Value.Count);

            for (var j = 0; j < header.Value.Count; j++)
            {
                if (header.Value[j] is { } value)
                    values.Add(value);
            }

            httpHeaders.Add(
                new Pine.Elm.Platform.WebServiceInterface.HttpHeader(
                    Name: header.Key,
                    Values: values));
        }

        var httpRequestBody = await CopyRequestBodyAsync(httpRequest);

        return new Pine.Elm.Platform.WebServiceInterface.HttpRequestProperties
        (
            Method: httpRequest.Method,
            Uri: httpRequest.GetDisplayUrl(),
            Body: httpRequestBody,
            Headers: httpHeaders
        );
    }

    public static async Task<ReadOnlyMemory<byte>> CopyRequestBodyAsync(HttpRequest httpRequest)
    {
        httpRequest.EnableBuffering(bufferThreshold: 100_000);
        httpRequest.Body.Position = 0;

        using var memoryStream = new MemoryStream();

        await httpRequest.Body.CopyToAsync(memoryStream);

        httpRequest.Body.Position = 0;

        return memoryStream.ToArray();
    }
}
