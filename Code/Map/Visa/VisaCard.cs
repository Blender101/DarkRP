/// <summary>
/// Held visa "papers" carryable. The actual validity data lives on the
/// <see cref="VisaComponent"/> on this same GameObject - this carryable
/// just gives the player something to hold up at the border.
///
/// Lifetime is tied to the player's inventory: when the player dies,
/// <c>Player.GameObject.Destroy()</c> destroys every child carryable
/// (this card included), forcing the player to buy a new one.
///
/// The card is intentionally NOT job-locked, so Border Patrol can
/// confiscate it via the existing /confiscate flow if needed.
/// </summary>
public sealed class VisaCard : BaseCarryable
{
	[RequireComponent]
	public VisaComponent Visa { get; set; }
}
