public sealed class WeaponShopItemDefinition
{
	public WeaponShopItemDefinition( string prefabPath, string title, int price, string description, bool gunDealerOnly = false )
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

public static class WeaponShopCatalog
{
	public const string GunDealerJobDefinitionPath = "jobs/gun_dealer.jobdef";

	static readonly WeaponShopItemDefinition[] Items =
	[
		new( "weapons/crowbar/crowbar.prefab", "Crowbar", 250, "Cheap melee option for close-quarters work along the wall." ),
		new( "weapons/glock/glock.prefab", "USP", 600, "Dependable sidearm. Standard issue for civilians on either side of the line." ),
		new( "weapons/colt1911/colt1911.prefab", "1911", 750, "Heavier pistol with stronger shots and a smaller magazine. Favored by old-school border gunmen." ),
		new( "weapons/grenade/grenade.prefab", "Grenade", 900, "Thrown explosive. Restricted stock \u2014 dealer permit required.", true ),
		new( "weapons/mp5/mp5.prefab", "SMG", 1600, "Fast-firing SMG built for aggressive short-range pressure. Dealer permit required.", true ),
		new( "weapons/shotgun/shotgun.prefab", "Shotgun", 2100, "Close-quarters firepower for breaching and ambushes. Dealer permit required.", true ),
		new( "weapons/m4a1/m4a1.prefab", "M4A1", 2600, "Balanced assault rifle. Standard issue equivalent on both sides of the border. Dealer permit required.", true ),
		new( "weapons/sniper/sniper.prefab", "Sniper", 3200, "High-damage rifle made for long-range picks across the desert. Dealer permit required.", true ),
		new( "weapons/rpg/rpg.prefab", "Rocket Launcher", 10000, "Heavy launcher. Cartel artillery \u2014 only the deepest pockets carry one. Dealer permit required.", true )
	];

	public static IReadOnlyList<WeaponShopItemDefinition> GetAll()
	{
		return Items;
	}

	public static WeaponShopItemDefinition Get( string prefabPath )
	{
		if ( string.IsNullOrWhiteSpace( prefabPath ) )
			return null;

		return Items.FirstOrDefault( x => string.Equals( x.PrefabPath, prefabPath, StringComparison.OrdinalIgnoreCase ) );
	}

	public static bool ShouldShowInShop( Player player, WeaponShopItemDefinition item )
	{
		if ( item is null )
			return false;

		return !item.GunDealerOnly || IsGunDealer( player );
	}

	public static bool CanPlayerBuy( Player player, string prefabPath, out string reason )
	{
		reason = null;

		var item = Get( prefabPath );
		if ( item is null )
		{
			reason = "Unknown weapon.";
			return false;
		}

		if ( !item.GunDealerOnly )
			return true;

		if ( player is null )
		{
			reason = "Player unavailable.";
			return false;
		}

		if ( !IsGunDealer( player ) )
		{
			reason = "Licensed Firearms Dealer only.";
			return false;
		}

		return true;
	}

	public static bool IsGunDealer( Player player )
	{
		var job = player?.CurrentJobDefinition;
		if ( job is null )
			return false;

		if ( string.Equals( job.ResourcePath, GunDealerJobDefinitionPath, StringComparison.OrdinalIgnoreCase ) )
			return true;

		if ( string.Equals( job.Command, "/dealer", StringComparison.OrdinalIgnoreCase ) )
			return true;

		return string.Equals( job.Title, "Licensed Firearms Dealer", StringComparison.OrdinalIgnoreCase );
	}
}
