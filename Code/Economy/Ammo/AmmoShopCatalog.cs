public sealed class AmmoShopItemDefinition
{
	public AmmoShopItemDefinition( string prefabPath, string title, int price, string description, bool gunDealerOnly = false )
	{
		PrefabPath = prefabPath;
		Title = title;
		Price = price;
		Description = description;
		GunDealerOnly = gunDealerOnly;
	}

	public string PrefabPath { get; }
	public string Title { get; }
	public int Price { get; }
	public string Description { get; }
	public bool GunDealerOnly { get; }
}

public static class AmmoShopCatalog
{
	static readonly AmmoShopItemDefinition[] Items =
	[
		new( "entities/pickup/ammo_9mm.prefab", "Pistol Ammo", 250, "A 30-round pistol ammo pack." ),
		new( "entities/pickup/ammo_rifle.prefab", "Rifle Ammo", 450, "A 60-round rifle ammo pack." ),
		new( "entities/pickup/ammo_shotgun.prefab", "Shotgun Ammo", 400, "An 18-shell shotgun ammo pack." ),
		new( "entities/pickup/ammo_rocket.prefab", "Rockets", 1800, "Two rockets for the rocket launcher.", true )
	];

	public static IReadOnlyList<AmmoShopItemDefinition> GetAll()
	{
		return Items;
	}

	public static AmmoShopItemDefinition Get( string prefabPath )
	{
		if ( string.IsNullOrWhiteSpace( prefabPath ) )
			return null;

		return Items.FirstOrDefault( x => string.Equals( x.PrefabPath, prefabPath, StringComparison.OrdinalIgnoreCase ) );
	}

	public static bool ShouldShowInShop( Player player, AmmoShopItemDefinition item )
	{
		if ( item is null )
			return false;

		return !item.GunDealerOnly || WeaponShopCatalog.IsGunDealer( player );
	}

	public static bool CanPlayerBuy( Player player, string prefabPath, out string reason )
	{
		reason = null;

		var item = Get( prefabPath );
		if ( item is null )
		{
			reason = "Unknown ammo.";
			return false;
		}

		if ( !item.GunDealerOnly )
			return true;

		if ( player is null )
		{
			reason = "Player unavailable.";
			return false;
		}

		if ( !WeaponShopCatalog.IsGunDealer( player ) )
		{
			reason = "Licensed Firearms Dealer only.";
			return false;
		}

		return true;
	}

	public static bool TryGetPickupAmmo( string prefabPath, out AmmoResource ammoType, out int ammoAmount )
	{
		ammoType = null;
		ammoAmount = 0;

		var prefab = GameObject.GetPrefab( prefabPath );
		var pickup = prefab?.GetComponent<AmmoPickup>( true );
		if ( !pickup.IsValid() || pickup.AmmoType is null || pickup.AmmoAmount <= 0 )
			return false;

		ammoType = pickup.AmmoType;
		ammoAmount = pickup.AmmoAmount;
		return true;
	}
}
