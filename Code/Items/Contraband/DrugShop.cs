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

		return !Contraband.IsCarrying( player ) && player.CanAfford( Price );
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

		Notices.SendNotice( player.Network.Owner, "shopping_bag", Color.Green, $"Picked up {ItemName}. Deliver it across the border.", 4 );
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
