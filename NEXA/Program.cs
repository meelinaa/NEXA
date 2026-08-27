using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using NEXA.Application;
using NEXA.DependencyInjection;

// ====================================================================================================
// N.E.X.A. - Neural EXtended Augmented-Reality Gesture Controller (MediaPipe ONNX + OpenCV + Win32)
// Main Application Bootstrap & Entry Point (Configured via Dependency Injection)
// ====================================================================================================

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