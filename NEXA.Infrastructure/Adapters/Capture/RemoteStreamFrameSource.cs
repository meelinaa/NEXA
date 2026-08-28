using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NEXA.Abstractions;
using OpenCvSharp;
using Serilog;

namespace NEXA.Adapters.Capture;

/// <summary>
/// Remote stream and smartphone camera frame source supporting both direct network stream URLs (RTSP, HTTP, MJPEG, WebRTC stream feeds)
/// and a built-in zero-install embedded smartphone camera receiver server.
/// </summary>
public class RemoteStreamFrameSource : IFrameSource
{
    private static readonly ILogger Logger = Log.ForContext<RemoteStreamFrameSource>();

    private VideoCapture? _capture;
    private HttpListener? _httpListener;
    private CancellationTokenSource? _serverCts;
    private readonly object _frameLock = new();
    private Mat? _latestRemoteFrame;
    private DateTime _lastFrameReceived = DateTime.MinValue;
    private bool _isReceiverRunning;
    private int _listeningPort;

    /// <summary>
    /// Gets the current stream URL if connected to an external stream.
    /// </summary>
    public string? StreamUrl { get; private set; }

    /// <summary>
    /// Gets a value indicating whether the embedded smartphone receiver server is currently active.
    /// </summary>
    public bool IsReceiverRunning => _isReceiverRunning;

    /// <summary>
    /// Gets the local web URL for smartphone connection (e.g. http://192.168.1.100:8080/).
    /// </summary>
    public string? SmartphoneConnectUrl { get; private set; }

    /// <inheritdoc/>
    public bool Open(int index)
    {
        // When opened with an integer, start the built-in smartphone camera receiver on default port (8080 + index)
        int port = 8080 + Math.Max(0, index);
        return StartEmbeddedReceiver(port);
    }

    /// <summary>
    /// Opens an external network stream URL (RTSP, HTTP, HTTPS, MJPEG, or IP Webcam stream).
    /// </summary>
    /// <param name="streamUrl">The network stream URI (e.g. "http://192.168.1.50:8080/video" or "rtsp://...").</param>
    /// <returns><c>true</c> if the stream was opened successfully; otherwise, <c>false</c>.</returns>
    public bool OpenStreamUrl(string streamUrl)
    {
        if (string.IsNullOrWhiteSpace(streamUrl))
        {
            throw new ArgumentException("Stream URL cannot be null or empty.", nameof(streamUrl));
        }

        StopReceiver();
        _capture?.Dispose();

        StreamUrl = streamUrl;
        Logger.Information("Connecting to remote video stream URL: {StreamUrl}", streamUrl);

        _capture = new VideoCapture(streamUrl, VideoCaptureAPIs.ANY);
        bool isOpened = _capture.IsOpened();
        if (isOpened)
        {
            Logger.Information("Successfully connected to remote stream: {StreamUrl}", streamUrl);
        }
        else
        {
            Logger.Warning("Failed to open remote video stream: {StreamUrl}", streamUrl);
        }

        return isOpened;
    }

    /// <summary>
    /// Starts the embedded zero-install smartphone camera receiver server.
    /// Smartphones on the local Wi-Fi can navigate to the displayed URL and stream their camera feed directly into NEXA.
    /// </summary>
    /// <param name="port">The local port to listen on (default: 8080).</param>
    /// <returns><c>true</c> if the server started successfully; otherwise, <c>false</c>.</returns>
    public bool StartEmbeddedReceiver(int port = 8080)
    {
        StopReceiver();
        _capture?.Dispose();
        _capture = null;

        _listeningPort = port;
        _serverCts = new CancellationTokenSource();

        try
        {
            _httpListener = new HttpListener();
            _httpListener.Prefixes.Add($"http://*:{port}/");
            _httpListener.Start();
            _isReceiverRunning = true;

            string localIp = GetLocalIpAddress();
            SmartphoneConnectUrl = $"http://{localIp}:{port}/";

            Logger.Information("=================================================================");
            Logger.Information("  SMARTPHONE CAMERA RECEIVER STARTED                             ");
            Logger.Information("  Open on your mobile browser: {ConnectUrl}", SmartphoneConnectUrl);
            Logger.Information("=================================================================");

            Task.Run(() => ListenLoopAsync(_httpListener, _serverCts.Token));
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to start HTTP listener on port {Port}, trying localhost prefix fallback...", port);

            try
            {
                _httpListener?.Close();
                _httpListener = new HttpListener();
                _httpListener.Prefixes.Add($"http://localhost:{port}/");
                _httpListener.Prefixes.Add($"http://127.0.0.1:{port}/");
                _httpListener.Start();
                _isReceiverRunning = true;
                SmartphoneConnectUrl = $"http://localhost:{port}/";

                Task.Run(() => ListenLoopAsync(_httpListener, _serverCts.Token));
                return true;
            }
            catch (Exception fallbackEx)
            {
                Logger.Error(fallbackEx, "Failed to start smartphone camera receiver server.");
                _isReceiverRunning = false;
                return false;
            }
        }
    }

    /// <inheritdoc/>
    public bool IsOpened()
    {
        if (_capture != null && _capture.IsOpened())
        {
            return true;
        }

        lock (_frameLock)
        {
            if (_latestRemoteFrame != null && !_latestRemoteFrame.Empty())
            {
                return true;
            }
        }

        return _isReceiverRunning;
    }

    /// <inheritdoc/>
    public bool Read(Mat image)
    {
        lock (_frameLock)
        {
            if (_latestRemoteFrame != null && !_latestRemoteFrame.Empty())
            {
                _latestRemoteFrame.CopyTo(image);
                return true;
            }
        }

        if (_capture != null && _capture.IsOpened())
        {
            return _capture.Read(image);
        }

        if (_isReceiverRunning)
        {
            // If no smartphone frame received yet, provide a neutral waiting indicator frame
            using Mat placeholder = CreateWaitingPlaceholder();
            placeholder.CopyTo(image);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Ingests a raw encoded JPEG/PNG frame payload directly into the frame pipeline (e.g. from tests or network packets).
    /// </summary>
    /// <param name="frameBytes">Binary image bytes.</param>
    /// <returns><c>true</c> if frame was decoded successfully; otherwise, <c>false</c>.</returns>
    public bool IngestEncodedFrame(byte[] frameBytes)
    {
        if (frameBytes == null || frameBytes.Length == 0) return false;

        try
        {
            Mat decoded = Cv2.ImDecode(frameBytes, ImreadModes.Color);
            if (decoded.Empty())
            {
                decoded.Dispose();
                return false;
            }

            lock (_frameLock)
            {
                _latestRemoteFrame?.Dispose();
                _latestRemoteFrame = decoded;
                _lastFrameReceived = DateTime.UtcNow;
            }
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Failed to decode incoming remote frame.");
            return false;
        }
    }

    /// <inheritdoc/>
    public bool Set(VideoCaptureProperties property, double value)
    {
        return _capture?.Set(property, value) ?? true;
    }

    private async Task ListenLoopAsync(HttpListener listener, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested && listener.IsListening)
        {
            try
            {
                HttpListenerContext context = await listener.GetContextAsync().ConfigureAwait(false);
                _ = ProcessRequestAsync(context);
            }
            catch (HttpListenerException) when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.Warning(ex, "Error processing incoming smartphone HTTP connection.");
            }
        }
    }

    private async Task ProcessRequestAsync(HttpListenerContext context)
    {
        HttpListenerRequest request = context.Request;
        HttpListenerResponse response = context.Response;

        try
        {
            // Enable Cross-Origin Resource Sharing (CORS)
            response.Headers.Add("Access-Control-Allow-Origin", "*");
            response.Headers.Add("Access-Control-Allow-Methods", "GET, POST, OPTIONS");
            response.Headers.Add("Access-Control-Allow-Headers", "Content-Type");

            if (request.HttpMethod == "OPTIONS")
            {
                response.StatusCode = 200;
                response.Close();
                return;
            }

            string rawUrl = request.RawUrl ?? "/";

            if (request.HttpMethod == "POST" && rawUrl.StartsWith("/frame"))
            {
                // Ingest incoming camera frame from mobile browser
                using MemoryStream ms = new();
                await request.InputStream.CopyToAsync(ms).ConfigureAwait(false);
                byte[] frameBytes = ms.ToArray();

                if (IngestEncodedFrame(frameBytes))
                {
                    response.StatusCode = 200;
                    byte[] okBytes = Encoding.UTF8.GetBytes("OK");
                    await response.OutputStream.WriteAsync(okBytes).ConfigureAwait(false);
                }
                else
                {
                    response.StatusCode = 400;
                }
                response.Close();
                return;
            }

            if (request.HttpMethod == "GET")
            {
                // Serve mobile camera streamer web interface
                string html = GenerateMobileWebCamHtml();
                byte[] htmlBytes = Encoding.UTF8.GetBytes(html);

                response.ContentType = "text/html; charset=utf-8";
                response.ContentLength64 = htmlBytes.Length;
                response.StatusCode = 200;
                await response.OutputStream.WriteAsync(htmlBytes).ConfigureAwait(false);
                response.Close();
                return;
            }

            response.StatusCode = 404;
            response.Close();
        }
        catch (Exception ex)
        {
            Logger.Warning(ex, "Error in HTTP request handling for remote stream.");
            try { response.Close(); } catch { }
        }
    }

    private Mat CreateWaitingPlaceholder()
    {
        Mat mat = new(720, 1280, MatType.CV_8UC3, new Scalar(25, 25, 25));
        Cv2.PutText(mat, "NEXA SMARTPHONE CAMERA STREAM", new Point(100, 200),
            HersheyFonts.HersheySimplex, 1.2, new Scalar(0, 220, 255), 3, LineTypes.AntiAlias);

        string urlText = SmartphoneConnectUrl ?? "http://<LOCAL_IP>:8080/";
        Cv2.PutText(mat, $"Open in mobile browser: {urlText}", new Point(100, 300),
            HersheyFonts.HersheySimplex, 0.9, Scalar.White, 2, LineTypes.AntiAlias);

        Cv2.PutText(mat, "Waiting for video connection...", new Point(100, 420),
            HersheyFonts.HersheySimplex, 0.8, new Scalar(150, 150, 150), 2, LineTypes.AntiAlias);

        return mat;
    }

    private static string GetLocalIpAddress()
    {
        try
        {
            using Socket socket = new(AddressFamily.InterNetwork, SocketType.Dgram, 0);
            socket.Connect("8.8.8.8", 65530);
            if (socket.LocalEndPoint is IPEndPoint endPoint)
            {
                return endPoint.Address.ToString();
            }
        }
        catch { }

        return "127.0.0.1";
    }

    private static string GenerateMobileWebCamHtml()
    {
        return """
        <!DOCTYPE html>
        <html lang="en">
        <head>
            <meta charset="UTF-8">
            <meta name="viewport" content="width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no">
            <title>NEXA Mobile Camera Streamer</title>
            <style>
                body { margin: 0; background: #0f172a; color: #f8fafc; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif; display: flex; flex-direction: column; align-items: center; justify-content: center; min-height: 100vh; text-align: center; }
                h1 { font-size: 1.5rem; margin-bottom: 0.5rem; color: #38bdf8; }
                p { color: #94a3b8; font-size: 0.9rem; margin-top: 0; }
                #preview { width: 90vw; max-width: 480px; aspect-ratio: 4/3; background: #000; border-radius: 12px; border: 2px solid #38bdf8; object-fit: cover; }
                .btn { margin-top: 15px; padding: 12px 24px; font-size: 1rem; font-weight: bold; background: #0284c7; color: white; border: none; border-radius: 8px; cursor: pointer; }
                .btn:active { background: #0369a1; }
                #status { margin-top: 10px; font-size: 0.85rem; color: #4ade80; }
            </style>
        </head>
        <body>
            <h1>NEXA Camera Bridge</h1>
            <p>Point phone camera at your hand or face for gesture control.</p>
            <video id="preview" autoplay playsinline muted></video>
            <button id="startBtn" class="btn" onclick="startCamera()">Start Camera Stream</button>
            <div id="status">Ready to connect</div>

            <canvas id="canvas" style="display: none;"></canvas>

            <script>
                let streaming = false;
                let video = document.getElementById('preview');
                let canvas = document.getElementById('canvas');
                let status = document.getElementById('status');
                let startBtn = document.getElementById('startBtn');

                async function startCamera() {
                    try {
                        const stream = await navigator.mediaDevices.getUserMedia({
                            video: { facingMode: 'user', width: { ideal: 1280 }, height: { ideal: 720 }, frameRate: { ideal: 30 } },
                            audio: false
                        });
                        video.srcObject = stream;
                        video.onloadedmetadata = () => {
                            video.play();
                            canvas.width = video.videoWidth || 640;
                            canvas.height = video.videoHeight || 480;
                            streaming = true;
                            startBtn.style.display = 'none';
                            status.innerText = 'Streaming LIVE to NEXA (30 FPS)';
                            streamLoop();
                        };
                    } catch (err) {
                        status.innerText = 'Camera Error: ' + err.message;
                        status.style.color = '#ef4444';
                    }
                }

                async function streamLoop() {
                    if (!streaming) return;
                    const ctx = canvas.getContext('2d');
                    ctx.drawImage(video, 0, 0, canvas.width, canvas.height);
                    
                    canvas.toBlob(async (blob) => {
                        if (blob) {
                            try {
                                await fetch('/frame', { method: 'POST', body: blob });
                            } catch (e) {}
                        }
                        if (streaming) {
                            setTimeout(streamLoop, 33); // ~30 FPS
                        }
                    }, 'image/jpeg', 0.7);
                }
            </script>
        </body>
        </html>
        """;
    }

    private void StopReceiver()
    {
        _serverCts?.Cancel();
        try
        {
            _httpListener?.Stop();
            _httpListener?.Close();
        }
        catch { }

        _httpListener = null;
        _serverCts?.Dispose();
        _serverCts = null;
        _isReceiverRunning = false;
        SmartphoneConnectUrl = null;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        StopReceiver();
        _capture?.Dispose();
        _capture = null;

        lock (_frameLock)
        {
            _latestRemoteFrame?.Dispose();
            _latestRemoteFrame = null;
        }

        GC.SuppressFinalize(this);
    }
}
