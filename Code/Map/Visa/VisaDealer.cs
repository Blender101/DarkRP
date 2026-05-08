using Sandbox.UI;

/// <summary>
/// Visa shop interactable. Toggle <see cref="IsBlackMarket"/> in the
/// inspector to switch between a legitimate visa office and a forged-papers
/// stall. All cash transactions and visa issuance run on the host.
///
/// Issuing a visa spawns a <see cref="VisaCard"/> prefab into the player's
/// inventory. The card is destroyed automatically when the player dies
/// (their GameObject is destroyed in <see cref="Player.Kill"/>), so death
/// forces them to come back for new papers.
/// </summary>
public sealed class VisaDealer : Component, Component.IPressable
{
	public const string VisaCardPrefab = "weapons/visacard/visa_card.prefab";

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

		var inventory = player.GetComponent<PlayerInventory>();
		if ( !inventory.IsValid() )
			return;

		var price = Price;
		if ( !player.CanAfford( price ) )
		{
			Notices.SendNotice( player.Network.Owner, "block", Color.Red, $"You need ${price:n0} for a {ItemLabel}.", 3 );
			return;
		}

		var existing = VisaComponent.CardFor( player );

		if ( !existing.IsValid() )
		{
			var slot = inventory.FindEmptySlot();
			if ( slot < 0 )
			{
				Notices.SendNotice( player.Network.Owner, "block", Color.Red, "No room for a visa - drop something first.", 4 );
				return;
			}

			if ( !player.TryTakeMoney( price ) )
				return;

			if ( !inventory.Pickup( VisaCardPrefab, slot, false ) )
			{
				player.GiveMoney( price );
				Notices.SendNotice( player.Network.Owner, "block", Color.Red, "Couldn't issue your visa - try again.", 4 );
				return;
			}

			existing = inventory.GetSlot( slot ) as VisaCard;
		}
		else
		{
			if ( !player.TryTakeMoney( price ) )
				return;
		}

		if ( !existing.IsValid() || !existing.Visa.IsValid() )
		{
			Notices.SendNotice( player.Network.Owner, "block", Color.Red, "Visa issuance failed.", 4 );
			return;
		}

		existing.Visa.IsFake = IsBlackMarket;
		existing.Visa.IssuerName = IssuerLabel;
		existing.Visa.ExpiryTime = DateTime.UtcNow + TimeSpan.FromMinutes( Math.Max( 1f, VisaDurationMinutes ) );
		existing.Visa.IsBurned = false;

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
