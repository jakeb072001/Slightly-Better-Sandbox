namespace Sandbox.Tools
{
	[Library( "tool_boxgun", Title = "Prop Shooter", Description = "Shoots your last spawned prop, box by default", Group = "fun" )]
	public class PropShooter : BaseTool
	{
		TimeSince timeSinceShoot;

		private string storedPropString;

		public override void Simulate()
		{
			storedPropString = SandboxGame.UserPropCurrent;

			if ( Host.IsServer )
			{
				if ( Input.Pressed( InputButton.Attack1 ) )
				{
					ShootBox();
				}

				if ( Input.Down( InputButton.Attack2 ) && timeSinceShoot > 0.05f )
				{
					timeSinceShoot = 0;
					ShootBox();
				}
			}
		}

		void ShootBox()
		{
			var ent = new Prop
			{
				Position = Owner.EyePos + Owner.EyeRot.Forward * 50,
				Rotation = Owner.EyeRot
			};

			if ( storedPropString != null )
			{
				ent.SetModel( storedPropString );
			} else
			{
				ent.SetModel( "models/citizen_props/crate01.vmdl" );
			}
			ent.Velocity = Owner.EyeRot.Forward * 1000;

			if ( Host.IsServer )
				Undo.Add( Owner.GetClientOwner(), new EntityUndo( ent ) );
		}
	}
}
