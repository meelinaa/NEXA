using System;
using Microsoft.Extensions.DependencyInjection;
using NEXA.Abstractions;
using NEXA.Adapters.Capture;
using NEXA.Adapters.Output;
using NEXA.Application;
using NEXA.Configuration;
using NEXA.Domain.Click;
using NEXA.Domain.EarsMute;
using NEXA.Domain.Grab;
using NEXA.Domain.Lock;
using NEXA.Domain.MonitorThrow;
using NEXA.Domain.Mute;
using NEXA.Domain.Scroll;
using NEXA.Domain.TwoHand;
using NEXA.Domain.Undo;
using NEXA.Domain.Volume;
using NEXA.Face;
using NEXA.Hand;
using NEXA.Object;
using NEXA.UI;

namespace NEXA.DependencyInjection;

/// <summary>
/// Service collection extension methods for registering all NEXA pipeline components, adapters, controllers, and renderers in the Dependency Injection container.
/// <para>
/// <b>What it is:</b> IoC service registration configurator for the NEXA architecture.
/// </para>
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers all core NEXA services, adapters, controllers, renderers, and the main execution engine.
    /// </summary>
    /// <param name="services">The service collection to register with.</param>
    /// <param name="palmModelPath">File path to palm_detection.onnx.</param>
    /// <param name="landmarkModelPath">File path to handpose_estimation.onnx.</param>
    /// <returns>The same service collection for method chaining.</returns>
    public static IServiceCollection AddNexaServices(
        this IServiceCollection services,
        string palmModelPath,
        string landmarkModelPath)
    {
        // 0. Configuration & Pipeline Channels
        services.AddSingleton<NexaConfiguration>();
        services.AddSingleton<FrameProcessingPipeline>();

        // 1. Output Sinks & Interop Adapters
        services.AddSingleton<IInputSink, Win32InputSink>();
        services.AddSingleton<IAudioSink, Win32AudioSink>();
        services.AddSingleton<IScreenshotSink, Win32ScreenshotSink>();
        services.AddSingleton<OpenCvFrameSource>();
        services.AddSingleton<RemoteStreamFrameSource>();
        services.AddSingleton<SwitchableFrameSource>();
        services.AddSingleton<IFrameSource>(sp => sp.GetRequiredService<SwitchableFrameSource>());
        services.AddSingleton<IDisplaySink, OpenCvDisplaySink>();
        services.AddSingleton<IKeyboardEventSource, OpenCvKeyboardEventSource>();

        // Concrete types for backward compatibility when injected explicitly
        services.AddSingleton(sp => (Win32InputSink)sp.GetRequiredService<IInputSink>());
        services.AddSingleton(sp => (Win32AudioSink)sp.GetRequiredService<IAudioSink>());
        services.AddSingleton(sp => (Win32ScreenshotSink)sp.GetRequiredService<IScreenshotSink>());
        services.AddSingleton(sp => sp.GetRequiredService<SwitchableFrameSource>().WebcamSource);
        services.AddSingleton(sp => (OpenCvDisplaySink)sp.GetRequiredService<IDisplaySink>());
        services.AddSingleton(sp => (OpenCvKeyboardEventSource)sp.GetRequiredService<IKeyboardEventSource>());

        // 2. Vision Models & Trackers
        services.AddSingleton<HandTracker>(sp => new HandTracker(palmModelPath, landmarkModelPath));
        services.AddSingleton<FaceTracker>();
        services.AddSingleton<IVisionPipeline, AsyncVisionPipeline>();

        // 3. Renderers & Visualizers
        services.AddSingleton<HandMeshRenderer>();
        services.AddSingleton<FaceMeshRenderer>();
        services.AddSingleton<HudRenderer>();
        services.AddSingleton<VirtualObjectRenderer>();
        services.AddSingleton<MouseFeedbackRenderer>();
        services.AddSingleton<ScrollFeedbackRenderer>();
        services.AddSingleton<VolumeFeedbackRenderer>();
        services.AddSingleton<LockSequenceRenderer>();
        services.AddSingleton<CircleUndoRenderer>();
        services.AddSingleton<ShhhMuteRenderer>();
        services.AddSingleton<HearNoEvilRenderer>();
        services.AddSingleton<MonitorThrowRenderer>();
        services.AddSingleton<WindowGrabRenderer>();
        services.AddSingleton<TwoHandGestureRenderer>();

        // 4. Domain Engines & Detectors
        services.AddSingleton<VirtualObjectGrabEngine>();
        services.AddSingleton<WindowResizeDetector>();
        services.AddSingleton<WindowSnapEngine>();
        services.AddSingleton<ScrollDetector>();
        services.AddSingleton<VolumeDetector>();
        services.AddSingleton<LockSequenceDetector>();
        services.AddSingleton<CircleUndoDetector>();
        services.AddSingleton<ShhhMuteDetector>();
        services.AddSingleton<HearNoEvilDetector>();
        services.AddSingleton<MonitorThrowDetector>();
        services.AddSingleton<TwoHandGestureDetector>();
        services.AddSingleton<TwoHandActionExecutor>();
        services.AddSingleton<WindowResizeCoordinator>();

        // 5. Domain Controllers
        services.AddSingleton<VirtualObjectController>();
        services.AddSingleton<MouseController>();
        services.AddSingleton<ScrollController>();
        services.AddSingleton<VolumeController>();
        services.AddSingleton<LockSequenceController>();
        services.AddSingleton<CircleUndoController>();
        services.AddSingleton<ShhhMuteController>();
        services.AddSingleton<HearNoEvilController>();
        services.AddSingleton<MonitorThrowController>();
        services.AddSingleton<TwoHandGestureController>();
        services.AddSingleton<WindowGrabController>();

        // 6. Application Loop, Hotkey Dispatcher & Controller Bundle
        services.AddSingleton<KeyboardCommandHandler>();
        services.AddSingleton<NexaControllerBundle>(sp =>
        {
            WindowGrabController grabCtrl = sp.GetRequiredService<WindowGrabController>();
            TwoHandGestureController twoHandCtrl = sp.GetRequiredService<TwoHandGestureController>();

            // Wire 3-second post-fist window trigger for two-hand maximize/minimize
            grabCtrl.OnFistReleased += () => twoHandCtrl.Detector.NotifyFistReleased();

            return new NexaControllerBundle(
                sp.GetRequiredService<MouseController>(),
                sp.GetRequiredService<ScrollController>(),
                grabCtrl,
                twoHandCtrl,
                sp.GetRequiredService<MonitorThrowController>(),
                sp.GetRequiredService<VolumeController>(),
                sp.GetRequiredService<LockSequenceController>(),
                sp.GetRequiredService<CircleUndoController>(),
                sp.GetRequiredService<ShhhMuteController>(),
                sp.GetRequiredService<HearNoEvilController>(),
                sp.GetRequiredService<VirtualObjectController>());
        });
        services.AddSingleton<NexaEngine>(sp =>
        {
            return new NexaEngine(
                sp.GetRequiredService<HandTracker>(),
                sp.GetRequiredService<FaceTracker>(),
                sp.GetRequiredService<IInputSink>(),
                sp.GetRequiredService<IAudioSink>(),
                sp.GetRequiredService<IScreenshotSink>(),
                sp.GetRequiredService<HandMeshRenderer>(),
                sp.GetRequiredService<FaceMeshRenderer>(),
                sp.GetRequiredService<NexaControllerBundle>(),
                sp.GetRequiredService<HudRenderer>(),
                sp.GetRequiredService<KeyboardCommandHandler>(),
                sp.GetRequiredService<IFrameSource>(),
                sp.GetRequiredService<IDisplaySink>(),
                sp.GetRequiredService<IKeyboardEventSource>(),
                sp.GetRequiredService<IVisionPipeline>()
            );
        });

        return services;
    }
}
