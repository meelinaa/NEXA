using System.Collections;
using System.Collections.Generic;
using NEXA.Abstractions;
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
using NEXA.Object;

namespace NEXA.Application;

/// <summary>
/// Aggregate parameter object grouping all gesture-domain controllers injected into <see cref="NexaEngine"/>.
/// <para>
/// <b>What it is:</b> A Facade/Aggregate that collapses the 11 domain controller constructor parameters into a single,
/// cohesive object – eliminating Constructor-Overinjection while preserving full individual testability of each controller.
/// </para>
/// <para>
/// <b>Why it is used:</b> The DI container resolves each controller independently; the bundle only exists as a transport
/// object to keep the <see cref="NexaEngine"/> constructor signature lean and readable.
/// </para>
/// </summary>
public sealed class NexaControllerBundle : IEnumerable<IHudStatusProvider>
{
    /// <summary>Gets the mouse pointer and dwell-click controller.</summary>
    public MouseController Mouse { get; }

    /// <summary>Gets the physics-based palm swipe scroll controller.</summary>
    public ScrollController Scroll { get; }

    /// <summary>Gets the window grab, auto-resize, and 8-zone snap-dock controller.</summary>
    public WindowGrabController WindowGrab { get; }

    /// <summary>Gets the two-hand gesture controller (play/pause, screenshot, maximize/minimize).</summary>
    public TwoHandGestureController TwoHand { get; }

    /// <summary>Gets the monitor-throw controller for cross-display window transfer.</summary>
    public MonitorThrowController MonitorThrow { get; }

    /// <summary>Gets the L-gesture rotary volume dial controller.</summary>
    public VolumeController Volume { get; }

    /// <summary>Gets the four-step gesture lock-sequence controller.</summary>
    public LockSequenceController Lock { get; }

    /// <summary>Gets the wrist-rotation undo/redo controller.</summary>
    public CircleUndoController CircleUndo { get; }

    /// <summary>Gets the Shhh-gesture microphone mute controller.</summary>
    public ShhhMuteController ShhhMute { get; }

    /// <summary>Gets the hear-no-evil speaker mute controller.</summary>
    public HearNoEvilController HearNoEvil { get; }

    /// <summary>Gets the AR virtual test object controller.</summary>
    public VirtualObjectController VirtualObject { get; }

    /// <summary>
    /// Gets the ordered list of all domain controllers implementing <see cref="IHudStatusProvider"/>.
    /// </summary>
    public IReadOnlyList<IHudStatusProvider> StatusProviders { get; }

    /// <summary>
    /// Gets the ordered list of all domain controllers implementing <see cref="IFrameProcessor"/>.
    /// </summary>
    public IReadOnlyList<IFrameProcessor> Processors { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="NexaControllerBundle"/> class.
    /// </summary>
    public NexaControllerBundle(
        MouseController mouse,
        ScrollController scroll,
        WindowGrabController windowGrab,
        TwoHandGestureController twoHand,
        MonitorThrowController monitorThrow,
        VolumeController volume,
        LockSequenceController lockSeq,
        CircleUndoController circleUndo,
        ShhhMuteController shhhMute,
        HearNoEvilController hearNoEvil,
        VirtualObjectController virtualObject)
    {
        Mouse = mouse;
        Scroll = scroll;
        WindowGrab = windowGrab;
        TwoHand = twoHand;
        MonitorThrow = monitorThrow;
        Volume = volume;
        Lock = lockSeq;
        CircleUndo = circleUndo;
        ShhhMute = shhhMute;
        HearNoEvil = hearNoEvil;
        VirtualObject = virtualObject;

        StatusProviders = new List<IHudStatusProvider>
        {
            mouse,
            scroll,
            windowGrab,
            twoHand,
            monitorThrow,
            volume,
            lockSeq,
            circleUndo,
            shhhMute,
            hearNoEvil,
            virtualObject
        }.AsReadOnly();

        Processors = new List<IFrameProcessor>
        {
            mouse,
            scroll,
            windowGrab,
            twoHand,
            monitorThrow,
            volume,
            lockSeq,
            circleUndo,
            shhhMute,
            hearNoEvil,
            virtualObject
        }.AsReadOnly();
    }

    /// <inheritdoc/>
    public IEnumerator<IHudStatusProvider> GetEnumerator() => StatusProviders.GetEnumerator();

    /// <inheritdoc/>
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

