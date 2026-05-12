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

	protected override string BuildResultDetail( Player player, bool? result )
	{
		if ( !Networking.IsHost || result is null )
			return "";

		var target = player.TraceForInspectionTarget();
		if ( !target.IsValid() || target == player )
			return "";

		var visa = VisaComponent.For( target );
		if ( !visa.IsValid() || !visa.HasIssuedVisa )
			return result == false ? "No visa" : "";

		if ( result == false )
		{
			if ( visa.IsExpired )
				return "Expired visa";
			if ( visa.IsBurned )
				return "Forgery flag";
			return "Forgery check failed";
		}

		var minutes = Math.Max( 0, (int)Math.Ceiling( (visa.ExpiryTime - DateTime.UtcNow).TotalMinutes ) );
		return $"{visa.IssuerName} · {minutes}m";
	}
}
