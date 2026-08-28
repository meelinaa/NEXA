using System;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using NEXA.Adapters.Capture;
using OpenCvSharp;
using Xunit;

namespace NEXA.Tests.Adapters;

/// <summary>
/// Right-BICEP unit tests for <see cref="RemoteStreamFrameSource"/> validating network stream URL handling,
/// embedded smartphone HTTP receiver server, and binary frame ingestion.
/// </summary>
public class RemoteStreamFrameSourceTests : IDisposable
{
    private readonly RemoteStreamFrameSource _source = new();

    public void Dispose()
    {
        _source.Dispose();
    }

    // [R]IGHT-BICEP: Ingesting a valid encoded JPEG image frame decodes into OpenCV Mat and reads out correctly.
    [Fact]
    public void IngestEncodedFrame_ValidJpegBytes_DecodesAndReadsMatchingMat()
    {
        // Arrange
        using Mat testImage = new(480, 640, MatType.CV_8UC3, new Scalar(0, 120, 255));
        Cv2.ImEncode(".jpg", testImage, out byte[] jpegBytes);

        // Act
        bool ingested = _source.IngestEncodedFrame(jpegBytes);
        using Mat readMat = new();
        bool readSuccess = _source.Read(readMat);

        // Assert
        Assert.True(ingested);
        Assert.True(readSuccess);
        Assert.False(readMat.Empty());
        Assert.Equal(480, readMat.Rows);
        Assert.Equal(640, readMat.Cols);
    }

    // RIGHT-[B]ICEP: Empty or null byte buffers return false gracefully without throwing.
    [Fact]
    public void IngestEncodedFrame_NullOrEmptyBytes_ReturnsFalse()
    {
        Assert.False(_source.IngestEncodedFrame(null!));
        Assert.False(_source.IngestEncodedFrame(Array.Empty<byte>()));
    }

    // RIGHT-B[I]CEP: Starting embedded server opens receiver, and Disposing closes it.
    [Fact]
    public void StartEmbeddedReceiver_AndDispose_InvertsReceiverState()
    {
        // Act
        bool started = _source.StartEmbeddedReceiver(port: 18090);

        // Assert
        Assert.True(started);
        Assert.True(_source.IsReceiverRunning);
        Assert.True(_source.IsOpened());
        Assert.NotNull(_source.SmartphoneConnectUrl);

        // Act (Inverse)
        _source.Dispose();

        // Assert
        Assert.False(_source.IsReceiverRunning);
    }

    // RIGHT-BI[C]EP: Cross-checks HTTP GET request to embedded server returning HTML mobile streamer web page.
    [Fact]
    public async Task EmbeddedReceiver_HttpEndpoint_ServesMobileStreamerHtml()
    {
        // Arrange
        int testPort = 18091;
        bool started = _source.StartEmbeddedReceiver(testPort);
        Assert.True(started);

        using HttpClient client = new();
        string url = $"http://localhost:{testPort}/";

        // Act
        HttpResponseMessage response = await client.GetAsync(url);
        string content = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("NEXA Camera Bridge", content);
        Assert.Contains("getUserMedia", content);
    }

    // RIGHT-BIC[E]P: Passing empty stream URL throws ArgumentException.
    [Fact]
    public void OpenStreamUrl_EmptyUrl_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _source.OpenStreamUrl(""));
        Assert.Throws<ArgumentException>(() => _source.OpenStreamUrl("   "));
    }
}
