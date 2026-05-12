using Sandbox.UI;

/// <summary>
/// Self-service visa kiosk. Any player can press E to buy a real visa,
/// no NPC, no job restriction. Pairs with the <c>VisaKioskScreen</c> Razor
/// UI rendered on a worldspace screen.
///
/// All cash transactions and visa issuance run on the host; the screen reads
/// the synced fields each frame so every client sees the same readout.
/// </summary>
public sealed class VisaPurchaseTerminal : Component, Component.IPressable
{
	[Property]
	public string TerminalLabel { get; set; } = "VISA KIOSK";

	[Property, Range( 1, 10000 )]
	public int VisaPrice { get; set; } = 200;

	[Property, Range( 1, 240 )]
	public float VisaDurationMinutes { get; set; } = 30f;

	[Property, Range( 0.1f, 30f )]
	public float CooldownSeconds { get; set; } = 1.0f;

	[Property]
	public SoundEvent PurchaseSound { get; set; }

	[Property]
	public SoundEvent DenySound { get; set; }

	[Sync( SyncFlags.FromHost )]
	public TimeUntil Cooldown { get; set; }

	/// <summary>Last action banner shown on the screen.</summary>
	[Sync( SyncFlags.FromHost )]
	public string LastActionLabel { get; set; } = "STANDBY";

	[Sync( SyncFlags.FromHost )]
	public string LastActor { get; set; } = "---";

	[Sync( SyncFlags.FromHost )]
	public TimeSince TimeSinceLastAction { get; set; }

	public bool IsCoolingDown => Cooldown > 0f;

	IPressable.Tooltip? IPressable.GetTooltip( IPressable.Event e )
	{
		// The kiosk's worldspace screen already shows price, status, and the
		// [ PRESS E ] prompt, so don't double up by drawing the central HUD tooltip.
		return null;
	}

	bool IPressable.CanPress( IPressable.Event e )
	{
		var player = GetPlayer( e.Source.GameObject );
		return player.IsValid() && !IsCoolingDown;
	}

	bool IPressable.Press( IPressable.Event e )
	{
		RequestBuy( e.Source.GameObject );
		return true;
	}

	[Rpc.Host]
	void RequestBuy( GameObject buyerObject )
	{
		var player = GetPlayer( buyerObject );
		if ( !player.IsValid() || Rpc.Caller != player.Network.Owner )
			return;

		if ( IsCoolingDown )
			return;

		Cooldown = CooldownSeconds;
		LastActor = player.DisplayName;
		TimeSinceLastAction = 0f;

		if ( !player.CanAfford( VisaPrice ) )
		{
			Deny( player, "FUNDS DECLINED", $"You need ${VisaPrice:n0}." );
			return;
		}

		var visa = VisaComponent.For( player );
		if ( !visa.IsValid() )
		{
			Deny( player, "ISSUANCE FAILED" );
			return;
		}

		if ( !player.TryTakeMoney( VisaPrice ) )
		{
			Deny( player, "FUNDS DECLINED" );
			return;
		}

		visa.IsFake = false;
		visa.IssuerName = TerminalLabel;
		visa.ExpiryTime = DateTime.UtcNow + TimeSpan.FromMinutes( Math.Max( 1f, VisaDurationMinutes ) );
		visa.IsBurned = false;

		LastActionLabel = "VISA ISSUED";

		Notices.SendNotice( player.Network.Owner, "verified", Color.Green,
			$"Visa issued. Valid for {VisaDurationMinutes:0} minutes.", 4 );

		PlayPurchaseEffects();
	}

	void Deny( Player player, string label, string message = null )
	{
		LastActionLabel = label;

		Notices.SendNotice( player.Network.Owner, "block", Color.Red,
			string.IsNullOrEmpty( message ) ? label : message, 3 );

		PlayDenyEffects();
	}

	[Rpc.Broadcast]
	void PlayPurchaseEffects()
	{
		if ( Application.IsDedicatedServer )
			return;

		if ( PurchaseSound is not null )
			Sound.Play( PurchaseSound, WorldPosition );
		else
			Sound.Play( "sounds/ui/ui.spawn.sound", WorldPosition );
	}

	[Rpc.Broadcast]
	void PlayDenyEffects()
	{
		if ( Application.IsDedicatedServer )
			return;

		if ( DenySound is not null )
			Sound.Play( DenySound, WorldPosition );
	}

	static Player GetPlayer( GameObject source )
	{
		return source?.Root.GetComponent<Player>();
	}
}
