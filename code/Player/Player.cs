using Sandbox;
using System;

partial class SandboxPlayer : Player
{
	private TimeSince timeSinceDropped;
	private TimeSince timeSinceJumpReleased;
	private TimeSince timeSinceLightToggled;
	private TimeSince timeBetweenTwoFall;

	private DamageInfo lastDamage;

	[Net] public PawnController VehicleController { get; set; }
	[Net] public PawnAnimator VehicleAnimator { get; set; }
	[Net, Predicted] public ICamera VehicleCamera { get; set; }
	[Net, Predicted] public Entity Vehicle { get; set; }
	[Net, Predicted] public ICamera MainCamera { get; set; }

	public bool Swimming { get; set; } = false;
	public bool Underwater { get; set; } = false;

	private bool noclipOn = false;

	private bool LightEnabled { get; set; } = false;

	private SpotLightEntity worldLight;
	private SpotLightEntity viewLight;

	protected virtual Vector3 LightOffset => Vector3.Forward * 10;
	public ICamera LastCamera { get; set; }

	public SandboxPlayer()
	{
		Inventory = new Inventory( this );
	}

	public override void Spawn()
	{
		MainCamera = new FirstPersonCamera();
		LastCamera = MainCamera;

		base.Spawn();
	}

	public override void Respawn()
	{
		SetModel( "models/citizen/citizen.vmdl" );
		// Custom Playermodel stuff
		//RenderAlpha = 0;

		//var ent = new Prop
		//{
		//	Position = 0
		//};
		//ent.SetModel( "models/playermodels/bulk/bulk.vmdl" );
		//ent.SetParent( this, true );

		Controller = new WalkController();
		Animator = new StandardPlayerAnimator();

		MainCamera = LastCamera;
		Camera = MainCamera;

		if ( DevController is NoclipController )
		{
			DevController = null;
		}

		worldLight = CreateLight();
		worldLight.SetParent( this, "hold_R", new Transform( LightOffset ) );
		worldLight.EnableHideInFirstPerson = false;
		worldLight.Enabled = LightEnabled;

		EnableAllCollisions = true;
		EnableDrawing = true;
		EnableHideInFirstPerson = true;
		EnableShadowInFirstPerson = true;

		Dress();

		Inventory.Add( new PhysGun(), true );
		Inventory.Add( new GravGun() );
		Inventory.Add( new Tool() );
		Inventory.Add( new Pistol() );
		//Inventory.Add( new Flashlight() );

		base.Respawn();
	}

	public override void OnKilled()
	{
		//PlaySound( "xnbox_death" );
		base.OnKilled();

		if ( lastDamage.Flags.HasFlag( DamageFlags.Vehicle ) )
		{
			Particles.Create( "particles/impact.flesh.bloodpuff-big.vpcf", lastDamage.Position );
			Particles.Create( "particles/impact.flesh-big.vpcf", lastDamage.Position );
			PlaySound( "kersplat" );
		}

		VehicleController = null;
		VehicleAnimator = null;
		VehicleCamera = null;
		Vehicle = null;

		BecomeRagdollOnClient( Velocity, lastDamage.Flags, lastDamage.Position, lastDamage.Force, GetHitboxBone( lastDamage.HitboxIndex ) );
		LastCamera = MainCamera;
		MainCamera = new SpectateRagdollCamera();
		Camera = MainCamera;
		Controller = null;

		EnableAllCollisions = false;
		EnableDrawing = false;

		Inventory.DropActive();
		Inventory.DeleteContents();
	}

	public override void TakeDamage( DamageInfo info )
	{
		if ( GetHitboxGroup( info.HitboxIndex ) == 1 )
		{
			info.Damage *= 10.0f;
		}

		lastDamage = info;

		TookDamage( lastDamage.Flags, lastDamage.Position, lastDamage.Force );

		base.TakeDamage( info );
	}

	[ClientRpc]
	public void TookDamage( DamageFlags damageFlags, Vector3 forcePos, Vector3 force )
	{
	}
	public bool TouchGround()
	{
		var p = Position;
		var vd = Vector3.Down;
		return Trace.Ray( p, p + vd * 20 ).Radius( 1 ).Ignore( this ).Run().Hit;
	}

	public void PlaySoundFall()
	{
		var predictDeath = this.Health - 10.0f <= 0;
		var fallSound = "xnbox_" + (predictDeath ? "death" : "fall") + "0";
		var rndFall = new Random().Next( 1, predictDeath ? 2 : 5 );
		Log.Info( fallSound + rndFall );
		Sound.FromEntity( fallSound + rndFall, this );
	}

	public void TakeFallDamage()
	{
		if ( LifeState != LifeState.Alive ) return;
		var v = Velocity;
		var d = Rotation.Down;
		var vd = (v * d).z;
		if ( timeBetweenTwoFall > 0.02f && vd >= 590 && TouchGround() )
		{
			var damage = new DamageInfo()
			{
				Position = Position,
				Damage = 10.0f
			};
			PlaySoundFall();
			TakeDamage( damage );
			timeBetweenTwoFall = 0;
		}
	}

	public override PawnController GetActiveController()
	{
		if ( VehicleController != null ) return VehicleController;
		if ( DevController != null ) return DevController;

		return base.GetActiveController();
	}

	public override PawnAnimator GetActiveAnimator()
	{
		if ( VehicleAnimator != null ) return VehicleAnimator;

		return base.GetActiveAnimator();
	}

	public ICamera GetActiveCamera()
	{
		if ( VehicleCamera != null ) return VehicleCamera;

		return MainCamera;
	}

	private SpotLightEntity CreateLight()
	{
		var light = new SpotLightEntity
		{
			Enabled = true,
			DynamicShadows = true,
			Range = 512,
			Falloff = 1.0f,
			LinearAttenuation = 0.0f,
			QuadraticAttenuation = 1.0f,
			Brightness = 2,
			Color = Color.White,
			InnerConeAngle = 20,
			OuterConeAngle = 40,
			FogStength = 1.0f,
			Owner = Owner,
			LightCookie = Texture.Load( "materials/effects/lightcookie.vtex" )
		};

		return light;
	}

	public override void Simulate( Client cl )
	{
		base.Simulate( cl );

		Swimming = this.WaterLevel.Fraction > 0.6f;
		Underwater = this.WaterLevel.Fraction > 0.6f; // change this value till its underwater, is above right now

		if ( !Underwater )
		{
			// somehow overlay an effect over the screen that makes it look like the player is underwater
		}

		if ( !noclipOn && !Swimming )
		{
			TakeFallDamage();
		}

		if ( Input.ActiveChild != null )
		{
			ActiveChild = Input.ActiveChild;
		}

		if ( LifeState != LifeState.Alive )
			return;

		if ( VehicleController != null && DevController is NoclipController )
		{
			DevController = null;
		}

		var controller = GetActiveController();
		if ( controller != null )
			EnableSolidCollisions = !controller.HasTag( "noclip" );

		TickPlayerUse();
		SimulateActiveChild( cl, ActiveChild );

		if ( Input.Pressed( InputButton.View ) )
		{
			if ( MainCamera is not FirstPersonCamera )
			{
				MainCamera = new FirstPersonCamera();
			}
			else
			{
				MainCamera = new ThirdPersonCamera();
			}
		}

		Camera = GetActiveCamera();

		if ( Input.Pressed( InputButton.Drop ) )
		{
			var dropped = Inventory.DropActive();
			if ( dropped != null )
			{
				dropped.PhysicsGroup.ApplyImpulse( Velocity + EyeRot.Forward * 500.0f + Vector3.Up * 100.0f, true );
				dropped.PhysicsGroup.ApplyAngularImpulse( Vector3.Random * 100.0f, true );

				timeSinceDropped = 0;
			}
		}

		if ( Input.Released( InputButton.Jump ) )
		{
			if ( timeSinceJumpReleased < 0.3f )
			{
				Game.Current?.DoPlayerNoclip( cl );
			}

			timeSinceJumpReleased = 0;
		}

		if ( Input.Left != 0 || Input.Forward != 0 )
		{
			timeSinceJumpReleased = 1;
		}

		if ( Input.Pressed( InputButton.Slot0 ) )
		{
			Game.NoclipCommand();
			noclipOn = !noclipOn;
		}

		if ( timeSinceLightToggled > 0.1f && Input.Pressed( InputButton.Flashlight ) )
		{
			LightEnabled = !LightEnabled;

			PlaySound( LightEnabled ? "flashlight-on" : "flashlight-off" );

			if ( worldLight.IsValid() )
			{
				worldLight.Enabled = LightEnabled;
			}

			if ( viewLight.IsValid() )
			{
				viewLight.Enabled = LightEnabled;
			}

			timeSinceLightToggled = 0;
		}
	}

	public override void StartTouch( Entity other )
	{
		if ( timeSinceDropped < 1 ) return;

		base.StartTouch( other );
	}

	[ServerCmd( "inventory_current" )]
	public static void SetInventoryCurrent( string entName )
	{
		var target = ConsoleSystem.Caller.Pawn;
		if ( target == null ) return;

		var inventory = target.Inventory;
		if ( inventory == null )
			return;

		for ( int i = 0; i < inventory.Count(); ++i )
		{
			var slot = inventory.GetSlot( i );
			if ( !slot.IsValid() )
				continue;

			if ( !slot.ClassInfo.IsNamed( entName ) )
				continue;

			inventory.SetActiveSlot( i, false );

			break;
		}
	}

	// TODO

	//public override bool HasPermission( string mode )
	//{
	//	if ( mode == "noclip" ) return true;
	//	if ( mode == "devcam" ) return true;
	//	if ( mode == "suicide" ) return true;
	//
	//	return base.HasPermission( mode );
	//	}
}
