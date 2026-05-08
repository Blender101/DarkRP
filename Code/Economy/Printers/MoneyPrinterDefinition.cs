[AssetType( Name = "Border RP Printer", Extension = "pdef", Category = "Border RP", Flags = AssetTypeFlags.NoEmbedding | AssetTypeFlags.IncludeThumbnails )]
public class MoneyPrinterDefinition : GameResource, IDefinitionResource
{
	public const int MaxOwnedPerPlayer = 5;
	public const string DefaultResourcePath = "entities/printer/bronze.pdef";

	[Property]
	public PrefabFile Prefab { get; set; }

	[Property]
	public string Title { get; set; }

	[Property]
	public string Description { get; set; }

	[Property]
	public int Price { get; set; }

	[Property]
	public int MoneyPerTick { get; set; } = 25;

	[Property]
	public float Interval { get; set; } = 20.0f;

	[Property]
	public int MaxStoredMoney { get; set; } = 8000;

	[Property]
	public Color Tint { get; set; } = Color.White;

	public static IReadOnlyList<MoneyPrinterDefinition> GetAll()
	{
		return ResourceLibrary.GetAll<MoneyPrinterDefinition>()
			.Where( x => x.Prefab is not null )
			.OrderBy( x => x.Price )
			.ToArray();
	}

	public static MoneyPrinterDefinition Get( string resourcePath )
	{
		if ( string.IsNullOrWhiteSpace( resourcePath ) )
			return null;

		return ResourceLibrary.Get<MoneyPrinterDefinition>( resourcePath );
	}

	public override Bitmap RenderThumbnail( ThumbnailOptions options )
	{
		if ( Prefab is null )
			return default;

		var bitmap = new Bitmap( options.Width, options.Height );
		bitmap.Clear( Color.Transparent );

		SceneUtility.RenderGameObjectToBitmap( Prefab.GetScene(), bitmap );
		return bitmap;
	}

	protected override Bitmap CreateAssetTypeIcon( int width, int height )
	{
		return CreateSimpleAssetTypeIcon( "$", width, height, "#35B851" );
	}
}
