using System.IO.Compression;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace RichMove.SmartPay.Api.Middleware;

/// <summary>
/// Response compression middleware with Gzip/Brotli support
/// Provides content-type filtering and size thresholds
/// </summary>
public sealed partial class CompressionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<CompressionMiddleware> _logger;
    private readonly CompressionOptions _options;
    private readonly HashSet<string> _compressibleTypes;

    public CompressionMiddleware(RequestDelegate next, ILogger<CompressionMiddleware> logger, IOptions<CompressionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _next = next;
        _logger = logger;
        _options = options.Value;
        _compressibleTypes = new HashSet<string>(_options.CompressibleContentTypes, StringComparer.OrdinalIgnoreCase);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!ShouldCompress(context))
        {
            await _next(context);
            return;
        }

        var originalStream = context.Response.Body;
        var compressionType = GetBestCompressionType(context);

        if (compressionType == CompressionType.None)
        {
            await _next(context);
            return;
        }

        using var compressionStream = CreateCompressionStream(originalStream, compressionType);
        var compressionWrapper = new CompressionStreamWrapper(compressionStream, originalStream, _logger, _options.MinimumBytes);

        context.Response.Body = compressionWrapper;

        try
        {
            await _next(context);

            // Ensure all data is written and compressed
            await compressionWrapper.FlushAsync();

            if (compressionWrapper.ShouldCompress)
            {
                SetCompressionHeaders(context, compressionType, compressionWrapper.OriginalBytes, compressionWrapper.CompressedBytes);
                Log.ResponseCompressed(_logger, compressionType.ToString(), compressionWrapper.OriginalBytes, compressionWrapper.CompressedBytes, GetCompressionRatio(compressionWrapper.OriginalBytes, compressionWrapper.CompressedBytes));
            }
            else
            {
                // Fallback to original stream for small responses
                context.Response.Body = originalStream;
                await compressionWrapper.CopyOriginalToResponse();
                Log.CompressionSkipped(_logger, compressionWrapper.OriginalBytes, _options.MinimumBytes);
            }
        }
        catch (Exception ex)
        {
            Log.CompressionError(_logger, ex);
            // Ensure original stream is restored
            context.Response.Body = originalStream;
            throw;
        }
        finally
        {
            context.Response.Body = originalStream;
        }
    }

    private static bool ShouldCompress(HttpContext context)
    {
        var request = context.Request;
        var response = context.Response;

        // Check if client accepts compression
        if (request.Headers.AcceptEncoding.Count == 0)
        {
            return false;
        }

        // Skip if already compressed
        if (response.Headers.ContentEncoding.Count > 0)
        {
            return false;
        }

        // Skip for specific status codes
        if (response.StatusCode < 200 || response.StatusCode >= 300)
        {
            return false;
        }

        // Check method
        var method = request.Method;
        if (!string.Equals(method, "GET", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(method, "POST", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return true;
    }

    private CompressionType GetBestCompressionType(HttpContext context)
    {
        var acceptEncoding = context.Request.Headers.AcceptEncoding.ToString();

        if (string.IsNullOrEmpty(acceptEncoding))
        {
            return CompressionType.None;
        }

        // Parse Accept-Encoding with quality values
        var encodings = ParseAcceptEncoding(acceptEncoding);

        // Prefer Brotli if available and enabled
        if (_options.EnableBrotli && encodings.TryGetValue("br", out var brQuality) && brQuality > 0)
        {
            return CompressionType.Brotli;
        }

        // Fall back to Gzip
        if (_options.EnableGzip && encodings.TryGetValue("gzip", out var gzipQuality) && gzipQuality > 0)
        {
            return CompressionType.Gzip;
        }

        return CompressionType.None;
    }

    private static Dictionary<string, float> ParseAcceptEncoding(string acceptEncoding)
    {
        var result = new Dictionary<string, float>(StringComparer.OrdinalIgnoreCase);
        var parts = acceptEncoding.Split(',', StringSplitOptions.RemoveEmptyEntries);

        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            var qIndex = trimmed.IndexOf(';', StringComparison.Ordinal);

            if (qIndex == -1)
            {
                result[trimmed] = 1.0f;
            }
            else
            {
                var encoding = trimmed[..qIndex].Trim();
                var qPart = trimmed[(qIndex + 1)..].Trim();

                if (qPart.StartsWith("q=", StringComparison.OrdinalIgnoreCase) &&
                    float.TryParse(qPart[2..], out var quality))
                {
                    result[encoding] = quality;
                }
                else
                {
                    result[encoding] = 1.0f;
                }
            }
        }

        return result;
    }

    private static Stream CreateCompressionStream(Stream output, CompressionType type)
    {
        return type switch
        {
            CompressionType.Gzip => new GZipStream(output, CompressionLevel.Fastest, leaveOpen: true),
            CompressionType.Brotli => new BrotliStream(output, CompressionLevel.Fastest, leaveOpen: true),
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };
    }

    private void SetCompressionHeaders(HttpContext context, CompressionType type, long originalBytes, long compressedBytes)
    {
        var response = context.Response;

        response.Headers.ContentEncoding = type switch
        {
            CompressionType.Gzip => "gzip",
            CompressionType.Brotli => "br",
            _ => throw new ArgumentOutOfRangeException(nameof(type))
        };

        response.Headers.Vary = "Accept-Encoding";

        // Remove Content-Length since compressed size is different
        response.Headers.Remove("Content-Length");

        // Add compression statistics as custom header if enabled
        if (_options.IncludeCompressionStats)
        {
            response.Headers["X-Compression-Ratio"] = GetCompressionRatio(originalBytes, compressedBytes).ToString("F2", System.Globalization.CultureInfo.InvariantCulture);
            response.Headers["X-Original-Size"] = originalBytes.ToString(System.Globalization.CultureInfo.InvariantCulture);
            response.Headers["X-Compressed-Size"] = compressedBytes.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }
    }

    private bool IsCompressibleContentType(string? contentType)
    {
        if (string.IsNullOrEmpty(contentType))
        {
            return false;
        }

        // Handle content type with charset (e.g., "application/json; charset=utf-8")
        var mainType = contentType.Split(';')[0].Trim();
        return _compressibleTypes.Contains(mainType);
    }

    private static double GetCompressionRatio(long originalBytes, long compressedBytes)
    {
        if (originalBytes == 0) return 0;
        return (1.0 - (double)compressedBytes / originalBytes) * 100;
    }

    private static partial class Log
    {
        [LoggerMessage(EventId = 6201, Level = LogLevel.Information, Message = "Response compressed using {CompressionType}: {OriginalSize} -> {CompressedSize} bytes ({CompressionRatio:F1}% reduction)")]
        public static partial void ResponseCompressed(ILogger logger, string compressionType, long originalSize, long compressedSize, double compressionRatio);

        [LoggerMessage(EventId = 6202, Level = LogLevel.Debug, Message = "Compression skipped: response size {ResponseSize} below minimum {MinimumSize}")]
        public static partial void CompressionSkipped(ILogger logger, long responseSize, int minimumSize);

        [LoggerMessage(EventId = 6203, Level = LogLevel.Error, Message = "Compression error occurred")]
        public static partial void CompressionError(ILogger logger, Exception exception);
    }
}

/// <summary>
/// Compression stream wrapper with size tracking
/// </summary>
internal sealed class CompressionStreamWrapper : Stream
{
    private readonly Stream _compressionStream;
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2213:Disposable fields should be disposed", Justification = "_originalStream is not owned by this class")]
    private readonly Stream _originalStream;
    private readonly ILogger _logger;
    private readonly int _minimumBytes;
    private readonly MemoryStream _buffer;
    private bool _disposed;

    public long OriginalBytes => _buffer.Length;
    public long CompressedBytes { get; private set; }
    public bool ShouldCompress => OriginalBytes >= _minimumBytes;

    public CompressionStreamWrapper(Stream compressionStream, Stream originalStream, ILogger logger, int minimumBytes)
    {
        _compressionStream = compressionStream;
        _originalStream = originalStream;
        _logger = logger;
        _minimumBytes = minimumBytes;
        _buffer = new MemoryStream();
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        await _buffer.WriteAsync(buffer.AsMemory(offset, count), cancellationToken);
    }

    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        await _buffer.WriteAsync(buffer, cancellationToken);
    }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        if (ShouldCompress)
        {
            _buffer.Position = 0;
            await _buffer.CopyToAsync(_compressionStream, cancellationToken);
            await _compressionStream.FlushAsync(cancellationToken);

            if (_compressionStream is GZipStream gzipStream)
            {
                gzipStream.Close();
            }
            else if (_compressionStream is BrotliStream brotliStream)
            {
                brotliStream.Close();
            }

            // Track compressed size
            CompressedBytes = _originalStream.Length;
        }
    }

    public async Task CopyOriginalToResponse()
    {
        _buffer.Position = 0;
        await _buffer.CopyToAsync(_originalStream);
        await _originalStream.FlushAsync();
    }

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => _buffer.Length;
    public override long Position
    {
        get => _buffer.Position;
        set => _buffer.Position = value;
    }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => _buffer.SetLength(value);
    public override void Write(byte[] buffer, int offset, int count) => _buffer.Write(buffer, offset, count);
    public override void Flush() => _buffer.Flush();

    protected override void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            _buffer.Dispose();
            _compressionStream.Dispose();
            _disposed = true;
        }
        base.Dispose(disposing);
    }
}

/// <summary>
/// Compression types supported
/// </summary>
internal enum CompressionType
{
    None,
    Gzip,
    Brotli
}

/// <summary>
/// Compression configuration options
/// </summary>
public sealed class CompressionOptions
{
    public bool EnableGzip { get; set; } = true;
    public bool EnableBrotli { get; set; } = true;
    public int MinimumBytes { get; set; } = 1024;
    public bool IncludeCompressionStats { get; set; }

    public IReadOnlyList<string> CompressibleContentTypes { get; set; } = new[]
    {
        "application/json",
        "application/xml",
        "text/plain",
        "text/html",
        "text/css",
        "text/javascript",
        "application/javascript",
        "application/x-javascript",
        "text/xml",
        "application/atom+xml",
        "application/rss+xml",
        "image/svg+xml"
    };
}