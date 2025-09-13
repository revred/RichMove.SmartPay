using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using RichMove.SmartPay.Api.Constants;

namespace RichMove.SmartPay.Api.Handlers;

public sealed class CorrelationIdHandler : DelegatingHandler
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CorrelationIdHandler(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var correlationId = _httpContextAccessor.HttpContext?.Request.Headers[HeaderNames.CorrelationId].ToString();
        if (!string.IsNullOrWhiteSpace(correlationId))
        {
            request.Headers.TryAddWithoutValidation(HeaderNames.CorrelationId, correlationId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}