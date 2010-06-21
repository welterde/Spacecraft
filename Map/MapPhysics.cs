using System;
using System.Collections;
using System.Collections.Generic;

namespace spacecraft
{
	partial class Map
	// Defining physics-related stuff, since this seems so troublesome.
	{
		/// <summary>
		/// How many physics ticks we've done. Not sure why we have this.
		/// </summary>
		private uint physicsCount;
		/// <summary>
		/// Whether we execute physics.
		/// </summary>
		public bool PhysicsOn = true;
		private object PhysicsMutex = new object();

		/// <summary>
		/// A set of all of the coodinates that contain a physics-active block and must be checked.
		/// </summary>
		private List<BlockPosition> ActiveBlocks = new List<BlockPosition>();
		/// <summary>
		/// A set of the tile changes we must perform this tick.
		/// </summary>
		private Dictionary<int, PhysicsTask> PhysicsUpdates = new Dictionary<int, PhysicsTask>();
		/// <summary>
		/// The list of items that must be removed from ActiveBlocks at the end of this tick, as they are no longer physics-active.
		/// </summary>
		private List<BlockPosition> ItemsToBeRemoved = new List<BlockPosition>();

		private int[,] Heights;

		System.Diagnostics.Stopwatch Stopwatch = new System.Diagnostics.Stopwatch();

		private void AddActiveBlock(BlockPosition pos)
		{
			ActiveBlocks.Add(pos);
		}

		/// <summary>
		/// Set up physics processing.
		/// </summary>
		private void InitPhysics()
		{
			ActiveBlocks.Clear();
			Heights = new int[xdim, zdim];
			bool CheckingHeight = true;
			
			Stopwatch.Reset();
			Stopwatch.Start();
			for (short x = 0; x < xdim; ++x) {
				for (short z = 0; z < zdim; ++z) {
					CheckingHeight = true;
					Heights[x, z] = 0;
					
					for (short y = (short)(ydim - 1); y >= 0; --y) {
						Block b = GetTile(x, y, z);
						if (CheckingHeight && BlockInfo.IsOpaque(b)) {
							Heights[x, z] = y;
							CheckingHeight = false;
						}
						if (BlockInfo.RequiresPhysics(b))
						{
							ActiveBlocks.Add(new BlockPosition(x, y, z));
						}
					}
				}
			}

			Stopwatch.Stop();
			Spacecraft.Log("Physics initialised in " + ((Stopwatch.ElapsedMilliseconds/100)/10.0).ToString() + " seconds!");
		}

		public void AlertPhysicsAround(BlockPosition pos)
		{
			//Spacecraft.Log("hi! :D");
			for (short x = -1; x <= 1; ++x)
			{
				for (short y = -1; y <= 1; ++y)
				{
					for (short z = -1; z <= 1; ++z)
					{
						short newX = (short)(pos.x + x);
						short newY = (short)(pos.y + y);
						short newZ = (short)(pos.z + z);
						if (Math.Abs(x) + Math.Abs(y) + Math.Abs(z) <= 1)
						{
							AddActiveBlock(new BlockPosition(newX, newY, newZ));
						}
					}
				}
			}
		}

		/// <summary>
		/// Process one physics step.
		/// </summary>
		public void DoPhysics()
		{
			if (!PhysicsOn)
				return;

			if (physicsCount == 0) {
				InitPhysics();
			}

			++physicsCount;

			lock (ActiveBlocks)
			{
				ItemsToBeRemoved.Clear();
				PhysicsUpdates.Clear();

				foreach (BlockPosition pos in new List<BlockPosition>(ActiveBlocks)) {
					Block Tile = GetTile(pos);

					// Check to see whether this location still needs to be on the list of physics-active blocks.
					if (!BlockInfo.RequiresPhysics(Tile)) {
						ActiveBlocks.Remove(pos);
					} else {
						HandlePhysics(pos.x, pos.y, pos.z, Tile);
					}
				}
				ActiveBlocks.Clear();
			}

			// Process physics updates. 
			foreach (PhysicsTask task in PhysicsUpdates.Values)
			{
				if(GetTile(task.x, task.y, task.z) == task.To) continue;
				AlertPhysicsAround(new BlockPosition(task.x, task.y, task.z));
				SetTile(task.x, task.y, task.z, task.To);
			}
		}

		/// <summary>
		/// Handle the physics of a specific block, checking surroundings, etc.
		/// </summary>
		/// <param name="X">The X coodinate of the block</param>
		/// <param name="Y">The Y coodinate of the block</param>
		/// <param name="Z">The Z coodinate of the block</param>
		/// <param name="block">The type of block at this position.</param>
		private void HandlePhysics(short X, short Y, short Z, Block block)
		{
			switch (block)
			{
				case Block.Water:
					//bool Sponged = false; 
					//List<PhysicsTask> spreadTiles = new List<PhysicsTask>();

					for (int x = -1; x <= 1; x++)
					{
						for (int y = -1; y <= 0; y++)
						{
							for (int z = -1; z <= 1; z++)
							{
								if (Math.Abs(x) + Math.Abs(y) + Math.Abs(z) == 1)
								{
									short newX = (short)(x + X);
									short newY = (short)(y + Y);
									short newZ = (short)(z + Z);

									if (GetTile(newX, newY, newZ) == Block.Lava)
									{
										AddPhysicsUpdate(new PhysicsTask(newX, newY, newZ, Block.Rock));
									}
									else if (GetTile(newX, newY, newZ) == Block.Water)
									{
										// Do nothing.
									}
									else if (!BlockInfo.IsSolid(GetTile(newX, newY, newZ)))
									{
										AddPhysicsUpdate(new PhysicsTask(newX, newY, newZ, Block.Water));
									}
								}
							}
						}
					}
					break;

				case Block.Lava:
					//bool Sponged = false; 
					//List<PhysicsTask> spreadTiles = new List<PhysicsTask>();

					for (int x = -1; x <= 1; x++)
					{
						for (int y = -1; y <= 0; y++)
						{
							for (int z = -1; z <= 1; z++)
							{
								if (Math.Abs(x) + Math.Abs(y) + Math.Abs(z) == 1)
								{
									short newX = (short)(x + X);
									short newY = (short)(y + Y);
									short newZ = (short)(z + Z);

									if (GetTile(newX, newY, newZ) == Block.Lava)
									{
										// Do nothing.
									}
									else if (!BlockInfo.IsSolid(GetTile(newX, newY, newZ)))
									{
										AddPhysicsUpdate(new PhysicsTask(newX, newY, newZ, Block.Lava));
									}
								}
							}
						}
					}
					break;

				case Block.Sponge:
					for (int xDiff = -BlockInfo.SpongeRadius; xDiff <= BlockInfo.SpongeRadius; xDiff++)
					{
						for (int yDiff = -BlockInfo.SpongeRadius; yDiff <= 0; yDiff++)
						{
							for (int zDiff = -BlockInfo.SpongeRadius; zDiff <= BlockInfo.SpongeRadius; zDiff++)
							{
								short newX = (short)(xDiff + X);
								short newY = (short)(yDiff + Y);
								short newZ = (short)(zDiff + Z);

								PhysicsTask task = new PhysicsTask(newX, newY, newZ, Block.Undefined); // Used to retrieve hash code, actual block type is arbitary.

								if (PhysicsUpdates.ContainsKey(task.GetHashCode()))
								{
									if (BlockInfo.IsFluid(PhysicsUpdates[task.GetHashCode()].To))
									{
										AddPhysicsUpdate(new PhysicsTask(newX, newY, newZ, Block.Air));
									}
								}

								if (BlockInfo.IsFluid(GetTile(newX, newY, newZ)))
								{
									AddPhysicsUpdate(new PhysicsTask(newX, newY, newZ, Block.Air));
								}
							}
						}
					}
					break;

				case Block.Sand:
					if (!BlockInfo.IsSolid(GetTile(X, (short)(Y - 1), Z)))
					{
						AddPhysicsUpdate(new PhysicsTask(X, (short)(Y - 1), Z, Block.Sand));
						AddPhysicsUpdate(new PhysicsTask(X, Y, Z, Block.Air));
					}

					break;

				case Block.Grass:
					if (Heights[X, Z] > Y)
					{
						AddPhysicsUpdate(new PhysicsTask(X, Y, Z, Block.Dirt));
					}
					break;
				case Block.Dirt:
					if (Heights[X, Z] == Y)
					{
						AddPhysicsUpdate(new PhysicsTask(X, Y, Z, Block.Grass));
					}
					break;

				default:
					break;
			}
		}

		void AddPhysicsUpdate(PhysicsTask task)
		{
			if (PhysicsUpdates.ContainsKey(task.GetHashCode()))
				PhysicsUpdates.Remove(task.GetHashCode());
			PhysicsUpdates.Add(task.GetHashCode(), task);
		}

		/// <summary>
		/// Recalculates the heightmap, with the given position is solid/not.
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="z"></param>
		/// <param name="solid">Whether the block is solid.</param>
		void RecalculateHeight(short x, short y, short z, bool opaque)
		{
			if (Heights == null)
			{
				Heights = new int[xdim, zdim];
			}
			
			int h = Heights[x, z];
			
			if (opaque)
			{
				Heights[x, z] = Math.Max(Heights[x, z], y);
			}
			else
			{
				if (Heights[x, z] <= y)
				{
					Heights[x, z] = 0;
					for (short Y = (short)(y-1); Y >= 0; Y--)
					{
						if (BlockInfo.IsOpaque(GetTile(x, Y, z)))
						{
							Heights[x, z] = Y;
							break;
						}
					}
				}
			}
		}
	}
}