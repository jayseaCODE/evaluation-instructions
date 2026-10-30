using System.Net;

namespace CompanyApi.Tests.Support;

/// <summary>
/// An <see cref="HttpMessageHandler"/> that returns canned responses and records the URIs it was asked for.
/// </summary>
public sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly List<Uri?> _requestUris = [];

    public IReadOnlyList<Uri?> RequestUris => _requestUris;

    private Func<CancellationToken, Task<HttpResponseMessage>> Responder { get; set; } =
        _ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));

    public void Respond(HttpStatusCode statusCode, string body = "") =>
        Responder = _ => Task.FromResult(new HttpResponseMessage(statusCode) { Content = new StringContent(body) });

    public void Throw(Exception exception) => Responder = _ => throw exception;

    public void NeverRespond() =>
        Responder = async cancellationToken =>
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        };

    // Sends the headers promptly and then never completes the body, like a server that stalls mid-response.
    public void StallBody() =>
        Responder = _ => Task.FromResult(
            new HttpResponseMessage(HttpStatusCode.OK) { Content = new StreamContent(new StalledStream()) });

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        _requestUris.Add(request.RequestUri);
        return Responder(cancellationToken);
    }

    private sealed class StalledStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await Task.Delay(Timeout.Infinite, cancellationToken);
            return 0;
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        public override void Flush() { }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
