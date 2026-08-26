using System;

namespace NEXA.Domain.TwoHand;

/// <summary>
/// Domain decision record representing an evaluated two-hand window action.
/// <para>
/// <b>What it is:</b> An immutable value record returned by <see cref="TwoHandGestureDetector"/> when a two-hand gesture threshold is satisfied.
/// </para>
/// <para>
/// <b>What it does:</b> Encapsulates the target window handle and the action to perform (Maximize, Minimize).
/// </para>
/// <para>
/// <b>Why it is used:</b> Decouples gesture pattern recognition from operating system window manipulation calls.
/// </para>
/// </summary>
/// <param name="Action">The evaluated action (Maximize or Minimize).</param>
/// <param name="TargetHwnd">The window handle to apply the action to.</param>
public record TwoHandGestureDecision(TwoHandAction Action, IntPtr TargetHwnd);
