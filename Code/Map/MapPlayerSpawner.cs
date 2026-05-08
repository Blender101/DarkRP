public sealed class MapPlayerSpawner : Component
{
	protected override void OnEnabled()
	{
		base.OnEnabled();

		if ( Components.TryGet<MapInstance>( out var mapInstance ) )
		{
			mapInstance.OnMapLoaded += RespawnPlayers;

			// already loaded
			if ( mapInstance.IsLoaded )
			{
				RespawnPlayers();
			}
		}
	}

	protected override void OnDisabled()
	{
		if ( Components.TryGet<MapInstance>( out var mapInstance ) )
		{
			mapInstance.OnMapLoaded -= RespawnPlayers;
		}

	}

	void RespawnPlayers()
	{
		var manager = GameManager.Current;
		if ( manager is null )
			return;

		foreach ( var player in Scene.GetAllComponents<Player>().ToArray() )
		{
			if ( player.IsProxy )
				continue;

			var faction = player.CurrentJobDefinition?.Faction ?? Faction.Neutral;
			var spawn = manager.FindSpawnLocation( faction );

			player.WorldPosition = spawn.Position;
			player.Controller.EyeAngles = spawn.Rotation.Angles();
		}
	}
}
