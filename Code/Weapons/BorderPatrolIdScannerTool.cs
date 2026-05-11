/// <summary>
/// Border Patrol ID scanner. Left-click checks the aimed-at player's visa.
/// The toolgun's viewmodel screen plays a typewriter "SCANNING..." sweep and
/// snaps to a green PASS or red DENY result. Validation runs on the host via
/// <see cref="Player.CheckVisaOnAimedPlayer"/>, so the client can't spoof it.
/// </summary>
public sealed class BorderPatrolIdScannerTool : BorderPatrolScreenWeapon
{
	protected override string ScreenIcon => "🆔";
	protected override string ScreenTitle => "SCAN";
	protected override string WorkingLabel => "SCANNING";
	protected override string PassLabel => "PASS";
	protected override string FailLabel => "DENY";

	protected override bool? PerformServerAction( Player player )
	{
		return player.CheckVisaOnAimedPlayer();
	}
}
