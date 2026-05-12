using System.Collections.Generic;

/// <summary>
/// Host-side proximity damage emitter for barbed-wire-style hazards.
///
/// Place a single instance of this component anywhere in the scene. On startup
/// it walks the scene for any <see cref="ModelRenderer"/> whose model path
/// contains <see cref="ModelMatch"/>, caches their world positions, then ticks
/// damage on the host to any player within <see cref="Range"/> of any cached
/// point.
///
/// Using a single field component (instead of one per prop) keeps the scene
/// clean - the user can add or duplicate barbed wire freely without having to
/// remember to attach a damage component to every instance.
/// </summary>
public sealed class BarbedWireHazard : Component
{
	/// <summary>
	/// Substring matched (case-insensitive) against each ModelRenderer's model
	/// resource path. Defaults to "barbedwire" which catches the
	/// <c>models/rust_props/barbedwire_set/...</c> family.
	/// </summary>
	[Property]
	public string ModelMatch { get; set; } = "barbedwire";

	/// <summary>Maximum distance from a barbed-wire segment to take damage.</summary>
	[Property, Range( 1f, 200f )]
	public float Range { get; set; } = 30f;

	/// <summary>How much damage to deal per second while inside the danger zone.</summary>
	[Property, Range( 0.1f, 100f )]
	public float DamagePerSecond { get; set; } = 8f;

	/// <summary>How often to apply damage (seconds). Damage is scaled to this interval.</summary>
	[Property, Range( 0.1f, 5f )]
	public float TickInterval { get; set; } = 0.5f;

	/// <summary>Optional flesh-tear sound played at the wire when a player is cut.</summary>
	[Property]
	public SoundEvent HurtSound { get; set; }

	/// <summary>How often to re-scan the scene for new wire segments (seconds).</summary>
	[Property, Range( 1f, 60f )]
	public float RescanInterval { get; set; } = 5f;

	private readonly List<Vector3> _points = new();
	private TimeSince _sinceRefresh;
	private TimeUntil _nextTick;

	protected override void OnStart()
	{
		RefreshPoints();
		_sinceRefresh = 0f;
		_nextTick = 0f;
	}

	protected override void OnFixedUpdate()
	{
		if ( !Networking.IsHost )
			return;

		if ( _sinceRefresh > RescanInterval )
		{
			RefreshPoints();
			_sinceRefresh = 0f;
		}

		if ( _nextTick > 0f )
			return;

		_nextTick = TickInterval;
		TickDamage();
	}

	private void RefreshPoints()
	{
		_points.Clear();

		if ( string.IsNullOrWhiteSpace( ModelMatch ) )
			return;

		foreach ( var renderer in Scene.GetAllComponents<ModelRenderer>() )
		{
			if ( !renderer.IsValid() )
				continue;

			var path = renderer.Model?.ResourcePath;
			if ( string.IsNullOrEmpty( path ) )
				continue;

			if ( path.Contains( ModelMatch, System.StringComparison.OrdinalIgnoreCase ) )
				_points.Add( renderer.WorldPosition );
		}
	}

	private void TickDamage()
	{
		if ( _points.Count == 0 )
			return;

		var rangeSq = Range * Range;
		var amount = Math.Max( 1, (int)Math.Ceiling( DamagePerSecond * TickInterval ) );

		foreach ( var player in Scene.GetAllComponents<Player>() )
		{
			if ( !player.IsValid() )
				continue;
			if ( player.Health <= 0 )
				continue;

			var pos = player.WorldPosition;
			Vector3? hitPoint = null;

			foreach ( var p in _points )
			{
				if ( Vector3.DistanceBetweenSquared( pos, p ) <= rangeSq )
				{
					hitPoint = p;
					break;
				}
			}

			if ( hitPoint is null )
				continue;

			ApplyDamage( player, amount, hitPoint.Value );
		}
	}

	private void ApplyDamage( Player player, float amount, Vector3 origin )
	{
		if ( player is not IDamageable damageable )
			return;

		var dmg = new DamageInfo( amount, GameObject, null );
		damageable.OnDamage( dmg );

		PlayHurtSound( origin );
	}

	[Rpc.Broadcast]
	private void PlayHurtSound( Vector3 origin )
	{
		if ( Application.IsDedicatedServer )
			return;

		if ( HurtSound is not null )
			Sound.Play( HurtSound, origin );
	}
}
