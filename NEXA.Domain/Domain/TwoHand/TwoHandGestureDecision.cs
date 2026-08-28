using System;
using OpenCvSharp;

namespace NEXA.Domain.TwoHand;

/// <summary>
/// Domain decision record representing an evaluated two-hand window or screenshot action.
/// <para>
/// <b>What it is:</b> An immutable value record returned by <see cref="TwoHandGestureDetector"/> when a two-hand gesture threshold is satisfied.
/// </para>
/// <para>
/// <b>What it does:</b> Encapsulates the action to perform (Maximize, Minimize, Screenshot), target window handle, and optional camera-space bounding box rectangle.
/// </para>
/// </summary>
/// <param name="Action">The evaluated action (Maximize, Minimize, or Screenshot).</param>
/// <param name="TargetHwnd">The window handle to apply window actions to.</param>
/// <param name="CropRect">Optional 2D camera coordinates bounding rectangle for screenshot captures.</param>
public record TwoHandGestureDecision(TwoHandAction Action, IntPtr TargetHwnd, Rect2f? CropRect = null);
