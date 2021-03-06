using System;
using System.Collections.Generic;

namespace spacecraft
{
	public enum Block : byte
	{
		Undefined = 255, // for error checking

		Air = 0,
		Rock = 1,
		Grass = 2,
		Dirt = 3,
		Cobblestone = 4,
		Wood = 5,
		Sapling = 6,
		Adminium = 7,
		Water = 8,
		StillWater = 9,
		Lava = 10,
		StillLava = 11,
		Sand = 12,
		Gravel = 13,
		GoldOre = 14,
		IronOre = 15,
		CoalOre = 16,
		Log = 17,
		Leaves = 18,
		Sponge = 19,
		Glass = 20,

		Red = 21,
		Orange = 22,
		Yellow = 23,
		Lime = 24,
		Green = 25,
		Teal = 26,
		Aqua = 27,
		Cyan = 28,
		Blue = 29,
		Indigo = 30,
		Violet = 31,
		Magenta = 32,
		Pink = 33,
		Black = 34,
		Gray = 35,
		White = 36,

		YellowFlower = 37,
		RedFlower = 38,
		RedMushroom = 39,
		BrownMushroom = 40,

		Gold = 41,
		Iron = 42,
		DoubleStair = 43,
		Stair = 44,
		Brick = 45,
		TNT = 46,
		Books = 47,
		MossyCobblestone = 48,
		Obsidian = 49,

		/* indev only
		Torch = 50,
		Fire = 51,
		InfiniteWater = 52,
		InfiniteLava = 53,
		 */

		Unobtanium = 200,

	}

	public static class BlockInfo
	{
		public static Dictionary<string, Block> names;

		private static Dictionary<Block, Block> ClientTranslations; // To translate our custom blocks into blocks clients can understnad.
		// Private because we have .Translate. No need to get to the dictionary directly. 

		public const int SpongeRadius = 2;

		public static bool IsFluid(Block block)
		{
			return (block == Block.Water || block == Block.StillWater
					|| block == Block.Lava || block == Block.StillLava);
		}
		public static bool IsDecoration(Block block)
		{
			return (block == Block.YellowFlower || block == Block.RedFlower ||
					block == Block.BrownMushroom || block == Block.RedMushroom ||
					block == Block.Sapling);
		}

		public static bool IsTransparent(Block block)
		{
			return (IsDecoration(block) || block == Block.Glass || block == Block.Leaves || block == Block.Air);
		}
		public static bool IsOpaque(Block block)
		{
			return !IsTransparent(block);
		}

		public static bool IsSolid(Block block)
		{
			return (block != Block.Air && !IsFluid(block) && !IsDecoration(block));
		}
		
		public static bool RequiresPhysics(Block B)
		{
			return (B == Block.Water || B == Block.Lava || B == Block.Sand || B == Block.Sponge || B == Block.Gravel || B == Block.Unobtanium);
		}

		public static bool IsCustom(Block B)
		{
			return ((byte)B >= 100);
		}

		static BlockInfo()
		{
			names = new Dictionary<string, Block>();

			foreach (string block in Enum.GetNames(typeof(Block)))
			{
				names.Add(block.ToLower(), (Block)Enum.Parse(typeof(Block), block));
			}

			names["none"] = Block.Air;
			names["nothing"] = Block.Air;
			names["empty"] = Block.Air;
			names["soil"] = Block.Dirt;
			names["stone"] = Block.Rock;
			names["rocks"] = Block.Cobblestone;
			names["plant"] = Block.Sapling;
			names["admincrete"] = Block.Adminium;
			names["admin"] = Block.Adminium;
			names["ore"] = Block.IronOre;
			names["coal"] = Block.CoalOre;
			names["trunk"] = Block.Log;
			names["treetrunk"] = Block.Log;
			names["foliage"] = Block.Leaves;
			names["grey"] = Block.Gray;
			names["flower"] = Block.YellowFlower;
			names["mushroom"] = Block.BrownMushroom;
			names["steel"] = Block.Iron;
			names["metal"] = Block.Iron;
			names["silver"] = Block.Iron;
			names["stairs"] = Block.DoubleStair;
			names["bricks"] = Block.Brick;
			names["dynamite"] = Block.TNT;
			names["bookcase"] = Block.Books;
			names["shelf"] = Block.Books;
			names["shelves"] = Block.Books;
			names["book"] = Block.Books;
			names["moss"] = Block.MossyCobblestone;
			names["mossy"] = Block.MossyCobblestone;
			names["mossystone"] = Block.MossyCobblestone;
			names["mossyrocks"] = Block.MossyCobblestone;
			names["mossystones"] = Block.MossyCobblestone;
			names["dark"] = Block.Obsidian;

			ClientTranslations = new Dictionary<Block, Block>()
			{
				{Block.Unobtanium, Block.Iron},
			};
		}

		public static bool NameExists(string key)
		{
			return names.ContainsKey(key);
		}

		/// <summary>
		/// Translate custom types for clients.
		/// </summary>
		/// <param name="B"></param>
		/// <returns></returns>
		public static Block Translate(Block B)
		{
			if (ClientTranslations.ContainsKey(B))
				return ClientTranslations[B];
			else
				return B;
		}

		public static Comparison<Block> BlockSorter = new Comparison<Block>(BlockSort);
	
		static int BlockSort(Block A, Block B)
		{
			if (A == B)
			return 0;
			else if (A > B)
			return 1;
			else if (A < B)
			return -1;
			throw new ArgumentException();
		}
	}
}