using System;

namespace NEXA.Domain.MonitorThrow;

/// <summary>
/// Domain decision record representing an evaluated multi-monitor window transfer event.
/// <para>
/// <b>What it is:</b> An immutable value record returned when a monitor throw gesture threshold is reached.
/// </para>
/// <para>
/// <b>What it does:</b> Encapsulates the target window handle and the transfer direction (Left or Right).
/// </para>
/// <para>
/// <b>Why it is used:</b> Decouples edge-on swipe kinematics from operating system display transfer calls.
/// </para>
/// </summary>
/// <param name="Direction">The target transfer direction (Left or Right).</param>
/// <param name="TargetHwnd">The window handle to transfer.</param>
public record MonitorThrowDecision(MonitorThrowDirection Direction, IntPtr TargetHwnd);
