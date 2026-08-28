using System;
using System.IO;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using global::NEXA.Adapters.Capture;
using global::NEXA.Application;
using global::NEXA.DependencyInjection;
using Serilog;

// ====================================================================================================
// N.E.X.A. - Neural EXtended Augmented-Reality Gesture Controller (MediaPipe ONNX + OpenCV + Win32)
// Main Application Bootstrap & Entry Point (Configured via Dependency Injection)
// ====================================================================================================

// 0. Bootstrap Serilog before any component is created
string logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
Directory.CreateDirectory(logDirectory);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: Path.Combine(logDirectory, "nexa-.log"),
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}",
        retainedFileCountLimit: 7)
    .CreateLogger();

try
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("==========================================================");
    Console.WriteLine("  N.E.X.A. - Neural Extended Augmented Reality Platform  ");
    Console.WriteLine("==========================================================");
    Console.ResetColor();

    // 1. Resolve ONNX Model Paths with multi-directory fallback
    string palmModelPath = FindModelPath("palm_detection.onnx");
    string landmarkModelPath = FindModelPath("handpose_estimation.onnx");

    if (string.IsNullOrEmpty(palmModelPath) || string.IsNullOrEmpty(landmarkModelPath))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine("[ERROR] MediaPipe ONNX Models not found!");
        Console.WriteLine($"Searched locations: {Path.Combine(AppContext.BaseDirectory, "models")}");
        Console.ResetColor();
        return;
    }

    Console.WriteLine($"[INFO] Palm Detection Model:     {palmModelPath}");
    Console.WriteLine($"[INFO] Hand Landmark Model:      {landmarkModelPath}");
    Console.WriteLine("Initializing Dependency Injection Container & Services...");

    // 2. Build and Configure the IoC Dependency Injection Container
    ServiceCollection services = new();
    services.AddNexaServices(palmModelPath, landmarkModelPath);

    using ServiceProvider serviceProvider = services.BuildServiceProvider();

    // 3. Parse Command-Line Options
    int webcamIndex = 0;
    string? customStreamUrl = null;
    bool forcePhoneStream = false;

    for (int i = 0; i < args.Length; i++)
    {
        string arg = args[i].ToLowerInvariant();
        if (arg is "--phone" or "--smartphone" or "--remote")
        {
            forcePhoneStream = true;
        }
        else if (arg is "--url" && i + 1 < args.Length)
        {
            customStreamUrl = args[++i];
        }
        else if (arg is "--cam" or "--camera" && i + 1 < args.Length && int.TryParse(args[++i], out int idx))
        {
            webcamIndex = idx;
        }
    }

    SwitchableFrameSource frameSource = serviceProvider.GetRequiredService<SwitchableFrameSource>();

    if (!string.IsNullOrEmpty(customStreamUrl))
    {
        Console.WriteLine($"[INFO] Connecting directly to stream URL: {customStreamUrl}");
        frameSource.ConnectToRemoteStream(customStreamUrl);
    }
    else if (forcePhoneStream)
    {
        Console.WriteLine("[INFO] Activating Wireless Smartphone Camera Stream mode...");
        frameSource.SwitchMode(CameraSourceMode.SmartphoneStream);
    }

    // 4. Resolve and Run Main Execution Engine
    NexaEngine engine = serviceProvider.GetRequiredService<NexaEngine>();
    engine.Run(webcamIndex);
}
catch (Exception ex)
{
    Console.ForegroundColor = ConsoleColor.Red;
    Console.WriteLine($"[FATAL ERROR] {ex.Message}");
    Console.ResetColor();
    Log.Fatal(ex, "NEXA terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}

static string FindModelPath(string fileName)
{
    string[] candidatePaths =
    [
        Path.Combine(AppContext.BaseDirectory, "models", fileName),
        Path.Combine(Directory.GetCurrentDirectory(), "models", fileName),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "models", fileName),
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "NEXA", "models", fileName),
        Path.Combine(Directory.GetCurrentDirectory(), "NEXA", "models", fileName)
    ];

    foreach (string path in candidatePaths)
    {
        if (File.Exists(path))
        {
            return Path.GetFullPath(path);
        }
    }

    return string.Empty;
}