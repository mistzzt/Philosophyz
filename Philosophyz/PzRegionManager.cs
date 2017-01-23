using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using MySql.Data.MySqlClient;
using Terraria;
using TShockAPI;
using TShockAPI.DB;

namespace Philosophyz
{
	public class PzRegion
	{
		public int Id { get; set; }

		public PlayerData PlayerData { get; set; }
	}

	internal class PzRegionManager
	{
		public List<PzRegion> PzRegions = new List<PzRegion>();

		private readonly IDbConnection _database;

		public PzRegionManager(IDbConnection db)
		{
			_database = db;

			var table = new SqlTable("PzRegions",
									 new SqlColumn("Region", MySqlDbType.Int32) { Primary = true },
									 new SqlColumn("Health", MySqlDbType.Int32),
									 new SqlColumn("MaxHealth", MySqlDbType.Int32),
									 new SqlColumn("Mana", MySqlDbType.Int32),
									 new SqlColumn("MaxMana", MySqlDbType.Int32),
									 new SqlColumn("Inventory", MySqlDbType.Text),
									 new SqlColumn("extraSlot", MySqlDbType.Int32),
									 new SqlColumn("spawnX", MySqlDbType.Int32),
									 new SqlColumn("spawnY", MySqlDbType.Int32),
									 new SqlColumn("skinVariant", MySqlDbType.Int32),
									 new SqlColumn("hair", MySqlDbType.Int32),
									 new SqlColumn("hairDye", MySqlDbType.Int32),
									 new SqlColumn("hairColor", MySqlDbType.Int32),
									 new SqlColumn("pantsColor", MySqlDbType.Int32),
									 new SqlColumn("shirtColor", MySqlDbType.Int32),
									 new SqlColumn("underShirtColor", MySqlDbType.Int32),
									 new SqlColumn("shoeColor", MySqlDbType.Int32),
									 new SqlColumn("hideVisuals", MySqlDbType.Int32),
									 new SqlColumn("skinColor", MySqlDbType.Int32),
									 new SqlColumn("eyeColor", MySqlDbType.Int32),
									 new SqlColumn("questsCompleted", MySqlDbType.Int32)
				);
			var creator = new SqlTableCreator(db,
											  db.GetSqlType() == SqlType.Sqlite
												  ? (IQueryBuilder)new SqliteQueryCreator()
												  : new MysqlQueryCreator());
			creator.EnsureTableStructure(table);
		}

		public void ReloadRegions()
		{
			PzRegions.Clear();

			using (var reader = _database.QueryReader("SELECT PzRegions.* FROM PzRegions, Regions WHERE PzRegions.Region = Regions.Id AND Regions.WorldID = @0", Main.worldID))
			{
				while (reader != null && reader.Read())
				{
					var region = new PzRegion
					{
						Id = reader.Get<int>("Region"),
						PlayerData = Read(reader)
					};

					PzRegions.Add(region);
				}
			}
		}

		private static PlayerData Read(QueryResult reader)
		{
			var inventory = reader.Get<string>("Inventory").Split('~').Select(NetItem.Parse).ToList();
			if (inventory.Count < NetItem.MaxInventory)
			{
				//TODO: unhardcode this - stop using magic numbers and use NetItem numbers
				//Set new armour slots empty
				inventory.InsertRange(67, new NetItem[2]);
				//Set new vanity slots empty
				inventory.InsertRange(77, new NetItem[2]);
				//Set new dye slots empty
				inventory.InsertRange(87, new NetItem[2]);
				//Set the rest of the new slots empty
				inventory.AddRange(new NetItem[NetItem.MaxInventory - inventory.Count]);
			}

			var playerData = new PlayerData(null)
			{
				exists = true,
				health = reader.Get<int>("Health"),
				maxHealth = reader.Get<int>("MaxHealth"),
				mana = reader.Get<int>("Mana"),
				maxMana = reader.Get<int>("MaxMana"),
				inventory = inventory.ToArray(),
				extraSlot = reader.Get<int>("extraSlot"),
				spawnX = reader.Get<int>("spawnX"),
				spawnY = reader.Get<int>("spawnY"),
				skinVariant = reader.Get<int?>("skinVariant"),
				hair = reader.Get<int?>("hair"),
				hairDye = (byte)reader.Get<int>("hairDye"),
				hairColor = TShock.Utils.DecodeColor(reader.Get<int?>("hairColor")),
				pantsColor = TShock.Utils.DecodeColor(reader.Get<int?>("pantsColor")),
				shirtColor = TShock.Utils.DecodeColor(reader.Get<int?>("shirtColor")),
				underShirtColor = TShock.Utils.DecodeColor(reader.Get<int?>("underShirtColor")),
				shoeColor = TShock.Utils.DecodeColor(reader.Get<int?>("shoeColor")),
				hideVisuals = TShock.Utils.DecodeBoolArray(reader.Get<int?>("hideVisuals")),
				skinColor = TShock.Utils.DecodeColor(reader.Get<int?>("skinColor")),
				eyeColor = TShock.Utils.DecodeColor(reader.Get<int?>("eyeColor")),
				questsCompleted = reader.Get<int>("questsCompleted")
			};

			return playerData;
		}
	}
}
