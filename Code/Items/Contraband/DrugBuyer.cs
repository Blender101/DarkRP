using Sandbox.UI;

/// <summary>
/// US-side buyer. Players press E with a <see cref="Contraband"/>
/// component on them to deliver the shipment, removing the component
/// and paying out cash. All mutations run on the host.
/// </summary>
public sealed class DrugBuyer : Component, Component.IPressable
{
	[Property, Range( 1, 100000 )]
	public int Payout { get; set; } = 1000;

	IPressable.Tooltip? IPressable.GetTooltip( IPressable.Event e )
	{
		var player = GetPlayer( e.Source.GameObject );
		if ( !player.IsValid() )
			return null;

		if ( !Contraband.IsCarrying( player ) )
			return new IPressable.Tooltip( "No shipment to deliver", "block", "Pick up contraband from a cartel shop first." );

		var contraband = player.GameObject.GetComponent<Contraband>();
		return new IPressable.Tooltip( $"Deliver {contraband.ItemName} (+${Payout:n0})", "$", "Drop off the shipment for cash." );
	}

	bool IPressable.CanPress( IPressable.Event e )
	{
		var player = GetPlayer( e.Source.GameObject );
		return Contraband.IsCarrying( player );
	}

	bool IPressable.Press( IPressable.Event e )
	{
		DeliverContraband( e.Source.GameObject );
		return true;
	}

	[Rpc.Host]
	void DeliverContraband( GameObject delivererObject )
	{
		var player = GetPlayer( delivererObject );
		if ( !player.IsValid() || Rpc.Caller != player.Network.Owner )
			return;

		var contraband = player.GameObject.GetComponent<Contraband>();
		if ( contraband is null )
		{
			Notices.SendNotice( player.Network.Owner, "block", Color.Red, "You're not carrying anything to sell.", 3 );
			return;
		}

		var itemName = contraband.ItemName;
		contraband.Destroy();

		player.GiveMoney( Payout );
		Notices.SendNotice( player.Network.Owner, "$", Color.Green, $"Shipment Delivered: {itemName} (+${Payout:n0})", 4 );
		PlayDeliveryEffects();
	}

	[Rpc.Broadcast]
	void PlayDeliveryEffects()
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
