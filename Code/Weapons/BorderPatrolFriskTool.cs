/// <summary>
/// Border Patrol frisk tool. Left-click frisks the aimed-at player for contraband.
/// The toolgun's viewmodel screen plays a typewriter "FRISKING..." sweep and
/// snaps to a green CLEAN or red FOUND result. The actual check runs on the
/// host via <see cref="Player.FriskAimedPlayer"/>.
/// </summary>
public sealed class BorderPatrolFriskTool : BorderPatrolScreenWeapon
{
	protected override string ScreenIcon => "🚓";
	protected override string ScreenTitle => "FRISK";
	protected override string WorkingLabel => "FRISKING";
	protected override string PassLabel => "CLEAN";
	protected override string FailLabel => "FOUND";

	protected override bool? PerformServerAction( Player player )
	{
		return player.FriskAimedPlayer();
	}

	protected override string BuildResultDetail( Player player, bool? result )
	{
		if ( !Networking.IsHost || result != false )
			return "";

		var target = player.TraceForInspectionTarget();
		if ( !target.IsValid() || target == player )
			return "";

		var c = target.GameObject.GetComponent<Contraband>();
		if ( !c.IsValid() )
			return "";

		return $"CARRYING: {c.ItemName} (${c.PurchasePrice:n0} shipment)";
	}
}
