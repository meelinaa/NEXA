namespace NEXA.Domain.Scroll;

/// <summary>
/// Domain decision record representing an evaluated scroll wheel command.
/// <para>
/// <b>What it is:</b> An immutable value record returned by <see cref="ScrollDetector"/> when a scroll threshold or momentum step is met.
/// </para>
/// <para>
/// <b>What it does:</b> Encapsulates the signed integer mouse wheel delta value to dispatch to the operating system.
/// </para>
/// <para>
/// <b>Why it is used:</b> Decouples calculation and physics momentum logic in the Domain layer from the Win32 input injection sink in the Adapter layer.
/// </para>
/// <para>
/// <b>Consequence:</b> Allows domain scroll logic to be easily tested in isolation without triggering physical OS scroll events.
/// </para>
/// </summary>
/// <param name="WheelDelta">The signed wheel delta (multiples of standard 120 WHEEL_DELTA; positive = scroll UP, negative = scroll DOWN).</param>
public record ScrollDecision(int WheelDelta);
