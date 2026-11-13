using FluentAssertions;
using System.IO;
using System.Net;
using System.Net.Http;
using Zeiterfassung.Integration;

namespace Zeiterfassung.Tests.Integration;

public class HolidayApiClientTests
{
    private sealed class StubHandler(Func<HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond());
    }

    private static HolidayApiClient CreateClient(Func<HttpResponseMessage> respond) =>
        new(new HttpClient(new StubHandler(respond)) { BaseAddress = new Uri(HolidayApiClient.BaseUrl) });

    [Fact]
    public async Task Liefert_normale_Antwort_unveraendert()
    {
        var client = CreateClient(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("[{\"date\":\"2027-01-01\"}]")
        });

        var json = await client.GetPublicHolidaysJsonAsync(2027);

        json.Should().Be("[{\"date\":\"2027-01-01\"}]");
    }

    [Fact]
    public async Task Lehnt_zu_grosse_Antwort_mit_bekanntem_Laengenheader_ab()
    {
        var client = CreateClient(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent(new byte[HolidayApiClient.MaxResponseBytes + 1])
        });

        var act = () => client.GetPublicHolidaysJsonAsync(2027);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task Bricht_ab_wenn_Antwort_ohne_Laengenheader_zu_gross_wird()
    {
        var client = CreateClient(() => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(new InfiniteStream())
        });

        var act = () => client.GetPublicHolidaysJsonAsync(2027);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    /// <summary>Liefert unendlich viele Bytes ohne Längenangabe.</summary>
    private sealed class InfiniteStream : Stream
    {
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => 0; set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override int Read(byte[] buffer, int offset, int count)
        {
            Array.Fill(buffer, (byte)'a', offset, count);
            return count;
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
