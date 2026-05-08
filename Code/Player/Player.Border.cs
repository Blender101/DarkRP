using Sandbox.UI;

public sealed partial class Player
{
	public const float BorderInspectionDistance = 200.0f;

	/// <summary>
	/// Border Patrol Agent or Border Patrol Chief - the only jobs allowed
	/// to inspect papers, frisk for contraband, and operate the border gate.
	/// Reuses the existing law-enforcement classification.
	/// </summary>
	public bool IsBorderPatrol => CanArrestPlayers;

	public static IEnumerable<Player> AllBorderPatrol()
	{
		if ( Game.ActiveScene is null )
			yield break;

		foreach ( var player in Game.ActiveScene.GetAllComponents<Player>() )
		{
			if ( player.IsValid() && player.IsBorderPatrol )
				yield return player;
		}
	}

	/// <summary>
	/// Aim-trace from the patrol's eye to find the player they're inspecting.
	/// Server-side only so the result can't be spoofed.
	/// </summary>
	Player TraceForInspectionTarget()
	{
		if ( !Controller.IsValid() )
			return null;

		var eye = EyeTransform;
		var trace = Scene.Trace
			.Ray( eye.ForwardRay, BorderInspectionDistance )
			.IgnoreGameObjectHierarchy( GameObject )
			.WithoutTags( "playercontroller" )
			.UseHitboxes()
			.Run();

		if ( !trace.GameObject.IsValid() )
			return null;

		return trace.GameObject.Root.GetComponent<Player>();
	}

	public void CheckVisaOnAimedPlayer()
	{
		if ( !Networking.IsHost || !IsBorderPatrol )
			return;

		var target = TraceForInspectionTarget();
		if ( !target.IsValid() || target == this )
		{
			Notices.SendNotice( Network.Owner, "block", Color.Orange, "Aim at someone to check their papers.", 3 );
			return;
		}

		var visa = VisaComponent.For( target );
		if ( !visa.IsValid() )
		{
			ReportInspection( target, "No visa on record.", "block", Color.Red );
			return;
		}

		if ( visa.IsExpired )
		{
			ReportInspection( target, $"Visa EXPIRED ({visa.IssuerName}).", "schedule", Color.Orange );
			return;
		}

		if ( visa.IsBurned )
		{
			ReportInspection( target, $"Visa flagged as forgery ({visa.IssuerName}).", "warning", Color.Red );
			return;
		}

		var passed = visa.CheckValidity();
		if ( !passed )
		{
			ReportInspection( target, $"Visa REJECTED - forgery detected ({visa.IssuerName}).", "warning", Color.Red );
			Notices.SendNotice( target.Network.Owner, "warning", Color.Red, $"{DisplayName} flagged your papers as forged.", 5 );
			return;
		}

		var minutes = Math.Max( 0, (int)Math.Ceiling( (visa.ExpiryTime - DateTime.UtcNow).TotalMinutes ) );
		ReportInspection( target, $"Visa valid - {visa.IssuerName} - {minutes}m remaining.", "verified", Color.Green );
	}

	public void FriskAimedPlayer()
	{
		if ( !Networking.IsHost || !IsBorderPatrol )
			return;

		var target = TraceForInspectionTarget();
		if ( !target.IsValid() || target == this )
		{
			Notices.SendNotice( Network.Owner, "block", Color.Orange, "Aim at someone to frisk them.", 3 );
			return;
		}

		var contraband = target.GameObject.GetComponent<Contraband>();
		if ( contraband.IsValid() )
		{
			ReportInspection( target, $"Carrying contraband: {contraband.ItemName}.", "warning", Color.Red );
			Notices.SendNotice( target.Network.Owner, "warning", Color.Orange, $"{DisplayName} frisked you.", 3 );
			return;
		}

		ReportInspection( target, "Clean - no contraband.", "check_circle", Color.Green );
		Notices.SendNotice( target.Network.Owner, "check_circle", Color.Yellow, $"{DisplayName} frisked you.", 3 );
	}

	public void ConfiscateFromAimedPlayer()
	{
		if ( !Networking.IsHost || !IsBorderPatrol )
			return;

		var target = TraceForInspectionTarget();
		if ( !target.IsValid() || target == this )
		{
			Notices.SendNotice( Network.Owner, "block", Color.Orange, "Aim at someone to confiscate.", 3 );
			return;
		}

		var contraband = target.GameObject.GetComponent<Contraband>();
		if ( !contraband.IsValid() )
		{
			ReportInspection( target, "Nothing to confiscate.", "block", Color.Orange );
			return;
		}

		var itemName = contraband.ItemName;
		contraband.Destroy();

		Notices.SendNotice( Network.Owner, "inventory_2", Color.Green, $"Confiscated {itemName} from {target.DisplayName}.", 4 );
		Notices.SendNotice( target.Network.Owner, "warning", Color.Red, $"{DisplayName} confiscated your {itemName}.", 5 );

		Scene.Get<Chat>()?.AddSystemText( $"{DisplayName} confiscated contraband from {target.DisplayName}.", "🛂" );
	}

	void ReportInspection( Player target, string finding, string icon, Color color )
	{
		if ( Network.Owner is not { } connection )
			return;

		Notices.SendNotice( connection, icon, color, $"{target.DisplayName}: {finding}", 5 );
	}
}
