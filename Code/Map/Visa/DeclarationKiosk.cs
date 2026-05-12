using Sandbox.UI;

/// <summary>
/// Border declaration terminal: carriers register contraband before crossing.
/// Host-only; writes to the nearest <see cref="BorderGateTerminal"/> audit log.
/// </summary>
public sealed class DeclarationKiosk : Component, Component.IPressable
{
	[Property]
	public string TerminalLabel { get; set; } = "DECLARATION";

	[Property, Range( 0, 10000 )]
	public int AmnestyPayout { get; set; } = 25;

	[Property]
	public SoundEvent DeclareSound { get; set; }

	[Sync( SyncFlags.FromHost )]
	public string LastActor { get; set; } = "---";

	[Sync( SyncFlags.FromHost )]
	public string LastActionLabel { get; set; } = "STANDBY";

	IPressable.Tooltip? IPressable.GetTooltip( IPressable.Event e )
	{
		return null;
	}

	bool IPressable.CanPress( IPressable.Event e )
	{
		var player = GetPlayer( e.Source.GameObject );
		return player.IsValid() && Contraband.IsCarrying( player );
	}

	bool IPressable.Press( IPressable.Event e )
	{
		RequestDeclare( e.Source.GameObject );
		return true;
	}

	[Rpc.Host]
	void RequestDeclare( GameObject source )
	{
		var player = GetPlayer( source );
		if ( !player.IsValid() || Rpc.Caller != player.Network.Owner )
			return;

		var contraband = player.GameObject.GetComponent<Contraband>();
		if ( !contraband.IsValid() )
			return;

		if ( contraband.DeclaredAtBorder )
		{
			Notices.SendNotice( player.Network.Owner, "check_circle", Color.Yellow, "You already filed a declaration for this shipment.", 3 );
			return;
		}

		contraband.DeclaredAtBorder = true;
		LastActor = player.DisplayName;
		LastActionLabel = "DECLARED";

		if ( AmnestyPayout > 0 )
			player.GiveMoney( AmnestyPayout );

		var line = $"{contraband.ItemName} ${contraband.PurchasePrice}";
		BorderGateTerminal.FindNearest( WorldPosition, 8000f )?.AppendLog( player.DisplayName, $"DECLARED {line}" );

		Notices.SendNotice( player.Network.Owner, "verified", Color.Green,
			AmnestyPayout > 0
				? $"Declaration filed. Amnesty stipend: ${AmnestyPayout:n0}."
				: "Declaration filed.", 4 );

		PlayDeclareEffects();
	}

	[Rpc.Broadcast]
	void PlayDeclareEffects()
	{
		if ( Application.IsDedicatedServer )
			return;

		if ( DeclareSound is not null )
			Sound.Play( DeclareSound, WorldPosition );
	}

	static Player GetPlayer( GameObject source )
	{
		return source?.Root.GetComponent<Player>();
	}
}
