/// <summary>
/// Which side of the border a job belongs to. Drives spawn point selection
/// and faction-aware game logic (dispatch, alerts, etc).
/// </summary>
public enum Faction
{
	Neutral = 0,
	US = 1,
	Mexico = 2,
}

[AssetType( Name = "Border RP Job", Extension = "jobdef", Category = "Border RP", Flags = AssetTypeFlags.NoEmbedding | AssetTypeFlags.IncludeThumbnails )]
public sealed class JobDefinition : GameResource, IDefinitionResource
{
	public const string DefaultResourcePath = "jobs/citizen.jobdef";

	[Property]
	public string Title { get; set; }

	[Property]
	public string Description { get; set; }

	[Property]
	public string Category { get; set; } = "Civilian";

	[Property]
	public Faction Faction { get; set; } = Faction.Neutral;

	[Property]
	public Color AccentColor { get; set; } = Color.Transparent;

	[Property]
	public int Salary { get; set; } = 45;

	[Property]
	public int MaxPlayers { get; set; }

	[Property]
	public bool RequiresVote { get; set; }

	[Property]
	public string Command { get; set; }

	[Property]
	public int Order { get; set; }

	[Property]
	public string[] StartingItems { get; set; } = [];

	[Property]
	public bool UseOwnerAvatarAppearance { get; set; }

	[Property]
	public bool PreserveOwnerAvatarAppearance { get; set; } = true;

	[Property]
	public string[] Clothing { get; set; } = [];

	[Property]
	public bool IsDefault { get; set; }

	public static IReadOnlyList<JobDefinition> GetAll()
	{
		return ResourceLibrary.GetAll<JobDefinition>()
			.OrderBy( x => x.Order )
			.ThenBy( x => x.Category )
			.ThenBy( x => x.Title )
			.ToArray();
	}

	public static JobDefinition Get( string resourcePath )
	{
		if ( string.IsNullOrWhiteSpace( resourcePath ) )
			return null;

		return ResourceLibrary.Get<JobDefinition>( resourcePath );
	}

	public static JobDefinition GetDefault()
	{
		return Get( DefaultResourcePath )
			?? GetAll().FirstOrDefault( x => x.IsDefault )
			?? GetAll().FirstOrDefault();
	}

	public Color GetDisplayColor()
	{
		if ( AccentColor.a > 0f )
			return AccentColor;

		return Category?.Trim() switch
		{
			"Government" => new Color( 0.176f, 0.337f, 0.643f ),
			"Services" => new Color( 0.388f, 0.902f, 0.745f ),
			"Commerce" => new Color( 0.988f, 0.643f, 0.286f ),
			"Cartel" => new Color( 0.486f, 0.071f, 0.071f ),
			"Criminal" => new Color( 0.906f, 0.357f, 0.357f ),
			_ => new Color( 0.847f, 0.910f, 1.000f )
		};
	}

	protected override Bitmap CreateAssetTypeIcon( int width, int height )
	{
		return CreateSimpleAssetTypeIcon( "👔", width, height, "#3b82f6" );
	}
}
