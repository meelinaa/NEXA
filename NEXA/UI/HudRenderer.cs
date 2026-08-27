using System;
using NEXA.Common;
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
using OpenCvSharp;

namespace NEXA.UI;

/// <summary>
/// Renderer responsible for drawing the semi-transparent telemetry HUD card with live FPS, filter states, and controller status indicators.
/// <para>
/// <b>What it is:</b> The visual telemetry HUD presenter for the top-left diagnostic overlay.
/// </para>
/// </summary>
public class HudRenderer
{
    /// <summary>
    /// Renders the complete telemetry HUD card onto the specified camera image frame.
    /// </summary>
    /// <param name="frame">The camera image frame to draw on.</param>
    /// <param name="fps">The current measured frames per second.</param>
    /// <param name="handsCount">Number of currently detected tracked hands.</param>
    /// <param name="smoothed">Whether OneEuroFilter smoothing is active.</param>
    /// <param name="objCtrl">Virtual test object controller.</param>
    /// <param name="mouseCtrl">Mouse controller.</param>
    /// <param name="scrollCtrl">Scroll controller.</param>
    /// <param name="grabCtrl">Window grab controller.</param>
    /// <param name="twoHandCtrl">Two-hand gesture controller.</param>
    /// <param name="throwCtrl">Monitor throw controller.</param>
    /// <param name="volCtrl">Volume controller.</param>
    /// <param name="lockCtrl">Lock sequence controller.</param>
    /// <param name="undoCtrl">Circle undo controller.</param>
    /// <param name="muteCtrl">Shhh microphone mute controller.</param>
    /// <param name="earsCtrl">Hear-no-evil speaker sound mute controller.</param>
    public void Render(
        Mat frame,
        double fps,
        int handsCount,
        bool smoothed,
        VirtualObjectController objCtrl,
        MouseController mouseCtrl,
        ScrollController scrollCtrl,
        WindowGrabController grabCtrl,
        TwoHandGestureController twoHandCtrl,
        MonitorThrowController throwCtrl,
        VolumeController volCtrl,
        LockSequenceController lockCtrl,
        CircleUndoController undoCtrl,
        ShhhMuteController muteCtrl,
        HearNoEvilController earsCtrl)
    {
        Rect hudRect = new(10, 10, 390, 256);
        using Mat overlay = frame.Clone();
        Cv2.Rectangle(overlay, hudRect, new Scalar(10, 10, 15), -1);
        Cv2.AddWeighted(overlay, 0.7, frame, 0.3, 0, frame);
        Cv2.Rectangle(frame, hudRect, new Scalar(0, 220, 255), 1);

        Cv2.PutText(frame, "NEXA HAND MOUSE & WINDOWS (ONNX)", new Point(20, 28),
            HersheyFonts.HersheySimplex, 0.48, new Scalar(0, 255, 255), 1, LineTypes.AntiAlias);

        Cv2.PutText(frame, $"FPS: {fps:F1} | Hands: {handsCount} | Filter: {(smoothed ? "ON" : "OFF")}", new Point(20, 48),
            HersheyFonts.HersheySimplex, 0.36, new Scalar(255, 255, 255), 1, LineTypes.AntiAlias);

        string mouseStatus = mouseCtrl.Enabled
            ? (mouseCtrl.DwellState.IsHovering && mouseCtrl.DwellState.HoverProgress > 0.05
                ? $"Verweilklick: {(int)(mouseCtrl.DwellState.HoverProgress * 100)}%"
                : "Aktiv (Zeigen)")
            : "AUS (Taste C)";
        Scalar mouseColor = mouseCtrl.Enabled ? new Scalar(0, 255, 120) : new Scalar(160, 160, 160);

        Cv2.PutText(frame, $"Maus (C): {mouseStatus}", new Point(20, 66),
            HersheyFonts.HersheySimplex, 0.36, mouseColor, 1, LineTypes.AntiAlias);

        string scrollStatus;
        Scalar scrollColor;

        if (!scrollCtrl.Enabled)
        {
            scrollStatus = "AUS (Taste W)";
            scrollColor = new Scalar(160, 160, 160);
        }
        else if (!scrollCtrl.IsWindowActive)
        {
            scrollStatus = "Gesperrt (Erst Zeigen)";
            scrollColor = new Scalar(120, 120, 120);
        }
        else if (scrollCtrl.State.WaitingForRest)
        {
            scrollStatus = $"Cooldown ({scrollCtrl.RemainingWindowSeconds:F1}s)";
            scrollColor = new Scalar(0, 180, 255);
        }
        else
        {
            scrollStatus = $"Bereit ({scrollCtrl.RemainingWindowSeconds:F1}s)";
            scrollColor = new Scalar(0, 255, 120);
        }

        Cv2.PutText(frame, $"Scroll (W): {scrollStatus}", new Point(20, 84),
            HersheyFonts.HersheySimplex, 0.36, scrollColor, 1, LineTypes.AntiAlias);

        string winGrabStatus;
        Scalar winGrabColor;
        if (!grabCtrl.Enabled)
        {
            winGrabStatus = "AUS (Taste G)";
            winGrabColor = new Scalar(160, 160, 160);
        }
        else if (grabCtrl.State.IsSnapped)
        {
            winGrabStatus = $"Docked ({grabCtrl.State.ActiveSnap})";
            winGrabColor = new Scalar(255, 160, 0);
        }
        else if (grabCtrl.ResizeState.IsActive)
        {
            winGrabStatus = $"Resize: {grabCtrl.ResizeState.CurrentWidth}x{grabCtrl.ResizeState.CurrentHeight} ({grabCtrl.ResizeState.CurrentScale:F2}x)";
            winGrabColor = new Scalar(0, 220, 255);
        }
        else if (grabCtrl.State.IsGrabbed)
        {
            winGrabStatus = $"Gegriffen [{TextSanitizer.ToSafeAscii(grabCtrl.State.CachedWindowTitle)}]";
            winGrabColor = new Scalar(0, 100, 255);
        }
        else if (grabCtrl.State.HoldDurationSeconds > 0)
        {
            winGrabStatus = $"Halte Faust ({grabCtrl.State.HoldDurationSeconds:F1}s / {grabCtrl.State.RequiredHoldSeconds:F1}s)";
            winGrabColor = new Scalar(0, 165, 255);
        }
        else
        {
            winGrabStatus = "Bereit (Faust 2s)";
            winGrabColor = new Scalar(0, 255, 120);
        }

        Cv2.PutText(frame, TextSanitizer.ToSafeAscii($"Fenster (G): {winGrabStatus}"), new Point(20, 102),
            HersheyFonts.HersheySimplex, 0.36, winGrabColor, 1, LineTypes.AntiAlias);

        string twoHandStatus;
        Scalar twoHandColor;
        if (!twoHandCtrl.Enabled)
        {
            twoHandStatus = "AUS (Taste T)";
            twoHandColor = new Scalar(160, 160, 160);
        }
        else if ((DateTime.Now - twoHandCtrl.State.LastMediaPlayPauseTime).TotalMilliseconds < 1500)
        {
            twoHandStatus = "> || PLAY / PAUSE gesendet!";
            twoHandColor = new Scalar(0, 220, 255);
        }
        else if (twoHandCtrl.State.IsCameraFrameActive)
        {
            twoHandStatus = "Kamera-Rahmen (2s Zusammenhalten)";
            twoHandColor = new Scalar(0, 255, 120);
        }
        else if ((DateTime.Now - twoHandCtrl.State.LastScreenshotTime).TotalMilliseconds < 1500)
        {
            twoHandStatus = "Screenshot gespeichert & kopiert!";
            twoHandColor = new Scalar(255, 255, 255);
        }
        else if (twoHandCtrl.State.InCooldown)
        {
            twoHandStatus = $"Cooldown ({twoHandCtrl.State.LastAction})";
            twoHandColor = new Scalar(0, 180, 255);
        }
        else if (twoHandCtrl.State.IsWindowActive)
        {
            twoHandStatus = $"Fenster ({twoHandCtrl.State.RemainingWindowSeconds:F1}s)";
            twoHandColor = new Scalar(0, 255, 120);
        }
        else
        {
            twoHandStatus = "Bereit (Klatsch >|| / Doppel-L / Faust)";
            twoHandColor = new Scalar(120, 120, 120);
        }

        Cv2.PutText(frame, TextSanitizer.ToSafeAscii($"Zwei-Hand (T): {twoHandStatus}"), new Point(20, 120),
            HersheyFonts.HersheySimplex, 0.36, twoHandColor, 1, LineTypes.AntiAlias);

        string throwStatus = throwCtrl.Enabled
            ? (throwCtrl.State.InCooldown ? "Cooldown (800ms)" : (throwCtrl.State.IsEdgeOnPosture ? "Handkante erkannt!" : "Bereit (Handkante Wisch)"))
            : "AUS (Taste M)";
        Scalar throwColor = throwCtrl.Enabled
            ? (throwCtrl.State.IsEdgeOnPosture ? new Scalar(255, 100, 200) : new Scalar(0, 255, 120))
            : new Scalar(160, 160, 160);

        Cv2.PutText(frame, TextSanitizer.ToSafeAscii($"Monitor (M): {throwStatus}"), new Point(20, 138),
            HersheyFonts.HersheySimplex, 0.36, throwColor, 1, LineTypes.AntiAlias);

        string volStatus;
        Scalar volColor;
        if (!volCtrl.Enabled)
        {
            volStatus = "AUS (Taste V)";
            volColor = new Scalar(160, 160, 160);
        }
        else if (volCtrl.State.IsActive)
        {
            volStatus = $"Aktiv: {(int)(volCtrl.State.SmoothedVolume * 100)}% ({volCtrl.State.AngleDelta:+0;-0;0} deg)";
            volColor = new Scalar(0, 255, 120);
        }
        else
        {
            volStatus = "Bereit (L-Geste + Drehung)";
            volColor = new Scalar(0, 255, 120);
        }

        Cv2.PutText(frame, TextSanitizer.ToSafeAscii($"Lautstaerke (V): {volStatus}"), new Point(20, 156),
            HersheyFonts.HersheySimplex, 0.36, volColor, 1, LineTypes.AntiAlias);

        string lockStatus;
        Scalar lockColor;
        if (!lockCtrl.Enabled)
        {
            lockStatus = "AUS (Taste L)";
            lockColor = new Scalar(160, 160, 160);
        }
        else if (lockCtrl.State.CurrentStep != LockSequenceStep.Idle)
        {
            lockStatus = $"Sequenz: {(int)lockCtrl.State.CurrentStep}/4 ({lockCtrl.State.StepTimeoutSeconds - lockCtrl.State.StepTimer.Elapsed.TotalSeconds:F1}s)";
            lockColor = new Scalar(0, 220, 255);
        }
        else
        {
            lockStatus = "Bereit (Offen-Faust-Offen-Faust)";
            lockColor = new Scalar(0, 255, 120);
        }

        Cv2.PutText(frame, TextSanitizer.ToSafeAscii($"Sperren (L): {lockStatus}"), new Point(20, 174),
            HersheyFonts.HersheySimplex, 0.36, lockColor, 1, LineTypes.AntiAlias);

        string undoStatus;
        Scalar undoColor;
        if (!undoCtrl.Enabled)
        {
            undoStatus = "AUS (Taste U)";
            undoColor = new Scalar(160, 160, 160);
        }
        else if (undoCtrl.State.IsTracking && Math.Abs(undoCtrl.State.AngleDeltaDeg) > 5.0)
        {
            string dir = undoCtrl.State.AngleDeltaDeg < 0.0 ? "Undo <--" : "Redo -->";
            string sign = undoCtrl.State.AngleDeltaDeg >= 0 ? "+" : "";
            undoStatus = $"{dir} ({sign}{undoCtrl.State.AngleDeltaDeg:F0} deg / 42 deg)";
            undoColor = undoCtrl.State.AngleDeltaDeg < 0.0 ? new Scalar(0, 220, 255) : new Scalar(255, 160, 0);
        }
        else
        {
            undoStatus = "Bereit (Peace Handgelenk-Dreh)";
            undoColor = new Scalar(0, 255, 120);
        }

        Cv2.PutText(frame, TextSanitizer.ToSafeAscii($"Undo/Redo (U): {undoStatus}"), new Point(20, 192),
            HersheyFonts.HersheySimplex, 0.36, undoColor, 1, LineTypes.AntiAlias);

        string muteStatus;
        Scalar muteColor;
        if (!muteCtrl.Enabled)
        {
            muteStatus = "AUS (Taste X)";
            muteColor = new Scalar(160, 160, 160);
        }
        else if (muteCtrl.State.IsInProximity)
        {
            muteStatus = $"Muten: {(int)(muteCtrl.State.HoldProgress * 100)}%";
            muteColor = new Scalar(0, 0, 255);
        }
        else
        {
            muteStatus = muteCtrl.State.IsMuted ? "STUMM (4 Finger vor Mund)" : "Aktiv (4 Finger vor Mund)";
            muteColor = muteCtrl.State.IsMuted ? new Scalar(0, 0, 255) : new Scalar(0, 255, 120);
        }

        Cv2.PutText(frame, TextSanitizer.ToSafeAscii($"Mikro (X): {muteStatus}"), new Point(20, 210),
            HersheyFonts.HersheySimplex, 0.36, muteColor, 1, LineTypes.AntiAlias);

        string soundStatus;
        Scalar soundColor;
        if (!earsCtrl.Enabled)
        {
            soundStatus = "AUS (Taste E)";
            soundColor = new Scalar(160, 160, 160);
        }
        else if (earsCtrl.State.IsInProximity)
        {
            soundStatus = $"Muten: {(int)(earsCtrl.State.HoldProgress * 100)}%";
            soundColor = new Scalar(0, 140, 255);
        }
        else
        {
            soundStatus = earsCtrl.State.IsSpeakerMuted ? "STUMM (Haende an Ohren)" : "Aktiv (Haende an Ohren)";
            soundColor = earsCtrl.State.IsSpeakerMuted ? new Scalar(0, 0, 255) : new Scalar(0, 255, 120);
        }

        Cv2.PutText(frame, TextSanitizer.ToSafeAscii($"Sound (E): {soundStatus}"), new Point(20, 228),
            HersheyFonts.HersheySimplex, 0.36, soundColor, 1, LineTypes.AntiAlias);

        string grabLabel = objCtrl.GrabState.Active ? "GRABBED" : (objCtrl.GrabState.HoldDurationSeconds > 0 ? $"HOLD {objCtrl.GrabState.HoldDurationSeconds:F1}s" : "Ready");
        string objStatus = $"Testobjekt: {grabLabel} | Zoom: {objCtrl.CurrentScale:F2}x (R)";
        Cv2.PutText(frame, TextSanitizer.ToSafeAscii(objStatus), new Point(20, 246),
            HersheyFonts.HersheySimplex, 0.34, new Scalar(200, 200, 200), 1, LineTypes.AntiAlias);
    }
}
