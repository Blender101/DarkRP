using Sandbox.UI;

/// <summary>
/// Visa shop interactable. Toggle <see cref="IsBlackMarket"/> in the
/// inspector to switch between a legitimate visa office and a forged-papers
/// stall. All cash transactions and visa issuance run on the host.
///
/// Issuing a visa updates the buyer's <see cref="VisaComponent"/> directly
/// on their player object. When the player dies their pawn is destroyed,
/// so they must buy new papers after respawn.
/// </summary>
public sealed class VisaDealer : Component, Component.IPressable
{
	[Property]
	public bool IsBlackMarket { get; set; }

	[Property, Range( 1, 10000 )]
	public int RealVisaPrice { get; set; } = 200;

	[Property, Range( 1, 10000 )]
	public int FakeVisaPrice { get; set; } = 50;

	[Property, Range( 1, 240 )]
	public float VisaDurationMinutes { get; set; } = 30f;

	int Price => IsBlackMarket ? FakeVisaPrice : RealVisaPrice;
	string IssuerLabel => IsBlackMarket ? "Black Market" : "Border Authority";
	string ItemLabel => IsBlackMarket ? "Fake Visa" : "Visa";
	string IconName => IsBlackMarket ? "warning" : "verified";

	IPressable.Tooltip? IPressable.GetTooltip( IPressable.Event e )
	{
		var player = GetPlayer( e.Source.GameObject );
		if ( !player.IsValid() )
			return null;

		var description = player.CanAfford( Price )
			? $"{IssuerLabel} - valid for {VisaDurationMinutes:0} minutes."
			: $"You need ${Price:n0} for a {ItemLabel}.";

		return new IPressable.Tooltip( $"Buy {ItemLabel} (${Price:n0})", IconName, description );
	}

	bool IPressable.CanPress( IPressable.Event e )
	{
		var player = GetPlayer( e.Source.GameObject );
		return player.IsValid() && player.CanAfford( Price );
	}

	bool IPressable.Press( IPressable.Event e )
	{
		BuyVisa( e.Source.GameObject );
		return true;
	}

	[Rpc.Host]
	void BuyVisa( GameObject buyerObject )
	{
		var player = GetPlayer( buyerObject );
		if ( !player.IsValid() || Rpc.Caller != player.Network.Owner )
			return;

		var price = Price;
		if ( !player.CanAfford( price ) )
		{
			Notices.SendNotice( player.Network.Owner, "block", Color.Red, $"You need ${price:n0} for a {ItemLabel}.", 3 );
			return;
		}

		var visa = VisaComponent.For( player );
		if ( !visa.IsValid() )
		{
			Notices.SendNotice( player.Network.Owner, "block", Color.Red, "Visa issuance failed.", 4 );
			return;
		}

		if ( !player.TryTakeMoney( price ) )
			return;

		visa.IsFake = IsBlackMarket;
		visa.IssuerName = IssuerLabel;
		visa.ExpiryTime = DateTime.UtcNow + TimeSpan.FromMinutes( Math.Max( 1f, VisaDurationMinutes ) );
		visa.IsBurned = false;

		var color = IsBlackMarket ? Color.Yellow : Color.Green;
		Notices.SendNotice( player.Network.Owner, IconName, color,
			$"{ItemLabel} issued. Valid for {VisaDurationMinutes:0} minutes.", 4 );

		PlayPurchaseEffects();
	}

	[Rpc.Broadcast]
	void PlayPurchaseEffects()
	{
		if ( Application.IsDedicatedServer )
			return;

		Sound.Play( "sounds/ui/ui.spawn.sound", WorldPosition );
	}

	static Player GetPlayer( GameObject source )
	{
		return source?.Root.GetComponent<Player>();
	}
}
