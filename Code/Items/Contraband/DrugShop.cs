using Sandbox.UI;

/// <summary>
/// Cartel-side shop. Players press E to purchase a contraband shipment;
/// the host charges the configured Price and tags the player's GameObject
/// with a <see cref="Contraband"/> component to carry.
/// </summary>
public sealed class DrugShop : Component, Component.IPressable
{
	[Property]
	public string ItemName { get; set; } = "Contraband";

	[Property, Range( 1, 100000 )]
	public int Price { get; set; } = 500;

	/// <summary>
	/// Delivery window the buyer expects this shipment within. Past this
	/// the carrier enters a 30-second grace window with half payout, after
	/// which the shipment spoils.
	/// </summary>
	[Property, Range( 30f, 1800f )]
	public float DeliveryWindowSeconds { get; set; } = 300f;

	[Property, Group( "Tips" ), Range( 0f, 1f )]
	public float AnonymousTipChanceAtReferencePrice { get; set; } = 0.12f;

	[Property, Group( "Tips" ), Range( 0f, 1f )]
	public float AnonymousTipChanceMax { get; set; } = 0.55f;

	[Property, Group( "Tips" ), Range( 1, 50000 )]
	public int AnonymousTipReferencePrice { get; set; } = 500;

	[Property, Group( "Tips" ), Range( 0.1f, 3f )]
	public float AnonymousTipPriceExponent { get; set; } = 0.7f;

	IPressable.Tooltip? IPressable.GetTooltip( IPressable.Event e )
	{
		var player = GetPlayer( e.Source.GameObject );
		if ( !player.IsValid() )
			return null;

		if ( Contraband.IsCarrying( player ) )
			return new IPressable.Tooltip( "Already carrying a shipment", "block", "Deliver it before buying more." );

		var description = player.CanAfford( Price )
			? $"Pick up a shipment of {ItemName}."
			: $"You need ${Price:n0} to buy {ItemName}.";

		return new IPressable.Tooltip( $"Buy {ItemName} (${Price:n0})", "shopping_bag", description );
	}

	bool IPressable.CanPress( IPressable.Event e )
	{
		var player = GetPlayer( e.Source.GameObject );
		if ( !player.IsValid() )
			return false;

		// Do not gate on money here — the HUD treats CanPress as "can use" and would hide
		// the prompt for everyone who cannot afford yet. Affordability is enforced in
		// BuyContraband on the host with a notice.
		return !Contraband.IsCarrying( player );
	}

	bool IPressable.Press( IPressable.Event e )
	{
		BuyContraband( e.Source.GameObject );
		return true;
	}

	[Rpc.Host]
	void BuyContraband( GameObject buyerObject )
	{
		var player = GetPlayer( buyerObject );
		if ( !player.IsValid() || Rpc.Caller != player.Network.Owner )
			return;

		if ( Contraband.IsCarrying( player ) )
		{
			Notices.SendNotice( player.Network.Owner, "block", Color.Red, "You're already carrying a shipment.", 3 );
			return;
		}

		if ( !player.TryTakeMoney( Price ) )
		{
			Notices.SendNotice( player.Network.Owner, "block", Color.Red, $"You need ${Price:n0} for {ItemName}.", 3 );
			return;
		}

		var contraband = player.GameObject.AddComponent<Contraband>();
		contraband.ItemName = ItemName;
		contraband.PurchasePrice = Price;
		contraband.Expiry = DeliveryWindowSeconds;
		contraband.DeclaredAtBorder = false;

		var buyer = FindNearestBuyer();
		contraband.BuyerId = buyer.IsValid() ? buyer.GameObject.Id : Guid.Empty;

		var minutes = Math.Max( 1, (int)Math.Ceiling( DeliveryWindowSeconds / 60f ) );
		Notices.SendNotice( player.Network.Owner, "shopping_bag", Color.Green,
			$"Picked up {ItemName}. Deliver within {minutes}m.", 4 );

		PlayPurchaseEffects();

		MaybeDispatchAnonymousTip( player, Price );
	}

	void MaybeDispatchAnonymousTip( Player buyer, int purchasePrice )
	{
		if ( !Networking.IsHost )
			return;

		var refPrice = Math.Max( 1, AnonymousTipReferencePrice );
		var ratio = purchasePrice / (float)refPrice;
		var t = MathF.Pow( ratio, AnonymousTipPriceExponent );
		var chance = (AnonymousTipChanceAtReferencePrice + (AnonymousTipChanceMax - AnonymousTipChanceAtReferencePrice) * t).Clamp( 0f, AnonymousTipChanceMax );
		if ( Random.Shared.NextSingle() > chance )
			return;

		var patrol = Player.AllBorderPatrol()
			.Where( p => p.IsValid() && p.Network.Owner is not null && p != buyer )
			.ToArray();

		if ( patrol.Length == 0 )
			return;

		var pick = Random.Shared.FromArray( patrol );
		if ( pick.Network.Owner is not { } conn )
			return;

		Notices.SendNotice( conn, "shield", Color.Orange,
			$"Anonymous tip: possible {ItemName} pickup activity in the area.", 6 );
	}

	DrugBuyer FindNearestBuyer()
	{
		var origin = WorldPosition;
		DrugBuyer best = null;
		var bestDistSq = float.MaxValue;

		foreach ( var buyer in Scene.GetAllComponents<DrugBuyer>() )
		{
			if ( !buyer.IsValid() )
				continue;

			var dSq = Vector3.DistanceBetweenSquared( origin, buyer.WorldPosition );
			if ( dSq < bestDistSq )
			{
				bestDistSq = dSq;
				best = buyer;
			}
		}

		return best;
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
