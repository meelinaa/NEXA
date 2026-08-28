using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using NEXA.Application;
using NEXA.DependencyInjection;
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
    int webcamIndex = 0; // Index 0 = Webcam. Change if external camera is used.

    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine("==============================");
    Console.WriteLine("  N.E.X.A. - Hand Tracking    ");
    Console.WriteLine("==============================");
    Console.ResetColor();

    string palmModelPath = Path.Combine(AppContext.BaseDirectory, "models", "palm_detection.onnx");
    string landmarkModelPath = Path.Combine(AppContext.BaseDirectory, "models", "handpose_estimation.onnx");

    if (!File.Exists(palmModelPath) || !File.Exists(landmarkModelPath))
    {
        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"[ERROR] Models not found in: {Path.Combine(AppContext.BaseDirectory, "models")}");
        Console.ResetColor();
        return;
    }

    Console.WriteLine("Loading MediaPipe ONNX Models & Initializing DI Services...");

    // 1. Build and Configure the IoC Dependency Injection Container
    ServiceCollection services = new();
    services.AddNexaServices(palmModelPath, landmarkModelPath);

    using ServiceProvider serviceProvider = services.BuildServiceProvider();

    // 2. Resolve and Run Main Execution Engine
    NexaEngine engine = serviceProvider.GetRequiredService<NexaEngine>();
    engine.Run(webcamIndex);
}
catch (Exception ex)
{
    Log.Fatal(ex, "NEXA terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}