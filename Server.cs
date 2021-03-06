using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace spacecraft
{
	public partial class Server
	{
		static public Server theServ;
		static public ManualResetEvent OnExit = new ManualResetEvent(false);
		static private bool Running = true;

		private bool Initialized = false;

		private TcpListener Listener;

		public List<Player> Players { get; private set; }
		public List<Robot> Robots { get; private set; }
		
		public Map map { get; protected set; }
		public int salt { get; protected set; }
		public int port { get; protected set; }
		public int HTTPport { get; protected set; }
		public int maxplayers { get; protected set; }
		public string name { get; protected set; }
		public string motd { get; protected set; }
		public string serverhash { get; protected set; }
		public string IP { get; protected set; }

		public ConsolePlayer console { get; protected set; }
		
		public double LastHeartbeatTook { get; protected set; }
		public double LastPhysicsTickTook { get; protected set; }
		public double LastTclTickTook { get; protected set; }
		
		public GameBase Game;

		public Server()
		{
			if (theServ != null) return;
			theServ = this;

			salt = Spacecraft.random.Next(100000, 999999);
			
			IP = "";

			port = Config.GetInt("port", 25565);
			maxplayers = Config.GetInt("max-players", 16);
			name = Config.Get("server-name", "Minecraft Server");
			motd = Config.Get("motd", "Powered by " + Color.Green + "Spacecraft");
			
			Game = new GameBase();
		}

		public void Start()
		{
			// Initialize the map, using the saved one if it exists.
			if (File.Exists(Map.levelName)) {
				map = Map.Load(Map.levelName);
			} else if (File.Exists("server_level.dat")) {
				map = Map.Load("server_level.dat");
			} else if (File.Exists("level.mclevel")) {
				map = Map.Load("level.mclevel");
			}

			if (map == null) {
				map = new Map();
				map.Generate();
				map.Save(Map.levelName);
			}

			map.BlockChange += new Map.BlockChangeHandler(map_BlockChange);

			try
			{
				Players = new List<Player>();
				Robots = new List<Robot>();

				Listener = new TcpListener(new IPEndPoint(IPAddress.Any, port));
				Listener.Start();

				Spacecraft.Log("Listening on port " + port.ToString());
				Spacecraft.Log("Server name is " + Spacecraft.StripColors(name));
				Spacecraft.Log("Server MOTD is " + Spacecraft.StripColors(motd));

				Running = true;

				Thread T = new Thread(AcceptClientThread, Spacecraft.StackSize);
				T.Name = "AcceptClient Thread";
				T.Start();

				Thread T2 = new Thread(TimerThread, Spacecraft.StackSize);
				T2.Name = "Timer thread";
				T2.Start();

				HttpMonitor.Start(Config.GetInt("http-port", port + 1));
				
				console = new ConsolePlayer();
				console.Message += new Player.PlayerMsgHandler(Player_Message);
				console.Start();
				
				Game = new AirshipWars.AirshipWars();

				OnExit.WaitOne();
				Running = false;
			}
			catch (SocketException e) {
				Spacecraft.LogError("uncaught SocketException", e);
			}
			finally
			{
				// Stop listening for new clients.
				Listener.Stop();
				HttpMonitor.Stop();
			}
			
			try {
				Shutdown();
			}
			catch (Exception e) {
				Spacecraft.LogError("error while shutting down", e);
			}
		}

		private void TimerThread()
		{
			Stopwatch clock = new Stopwatch();
			clock.Start();
			double lastHeartbeat = -30;
			double lastPhysics = -0.5;
			double lastBookend = 0;
			double lastIpAttempt = -10;
			double lastFrame = 0;
			
			int ipFailures = 0;

			while(Running) {
				if (IP == "" && clock.Elapsed.TotalSeconds - lastIpAttempt >= 10 && ipFailures < 3) {
					try {
						WebClient Request = new WebClient();
						byte[] data = Request.DownloadData(@"http://whatismyip.org/");
						IP = ASCIIEncoding.ASCII.GetString(data);
						Spacecraft.Log("IP discovered: " + IP);
					}
					catch(WebException e) {
						lastIpAttempt = clock.Elapsed.TotalSeconds;
						++ipFailures;
						if(ipFailures >= 3) {
							Spacecraft.LogError("Could not discover IP address", e);
						} else {
							Spacecraft.LogError("Could not discover IP address, reattempting", e);
						}
					}
				}
				
				if (clock.Elapsed.TotalSeconds - lastHeartbeat >= 30) {
					double now = clock.Elapsed.TotalSeconds;
					Heartbeat();
					map.Save(Map.levelName);
					GC.Collect();
					lastHeartbeat = clock.Elapsed.TotalSeconds;
					LastHeartbeatTook = Math.Round(10*(clock.Elapsed.TotalSeconds - now))/10.0;
				}
				
				if (clock.Elapsed.TotalSeconds - lastPhysics >= 0.5) {
					// physics tick
					double now = clock.Elapsed.TotalSeconds;
					map.DoPhysics();
					lastPhysics = clock.Elapsed.TotalSeconds;
					LastPhysicsTickTook = Math.Round(10*(clock.Elapsed.TotalSeconds - now))/10.0;
				}

				if (clock.Elapsed.TotalSeconds - lastBookend >= 3600) {
					// To make keeping track of the logs easier, print a line each hour
					Spacecraft.Log("=======================================================");
					lastBookend = clock.Elapsed.TotalSeconds;
				}
				
				if(clock.Elapsed.TotalSeconds - lastFrame >= 0.03) {
					// For frame-wise updates: update all mobs. Just in case.
					foreach(Robot R in new List<Robot>(Robots)) {
						R.Update();
					}
				}
				
				Thread.Sleep(10);
			}
		}

		public void AcceptClientThread()
		{
			while(Running) {
				TcpClient Client = Listener.AcceptTcpClient();
				Player Player = new Player(Client);

				Player.Spawn += new Player.PlayerSpawnHandler(Player_Spawn);
				Player.Message += new Player.PlayerMsgHandler(Player_Message);
				Player.Move += new Player.PlayerMoveHandler(Player_Move);
				Player.BlockChange += new Player.PlayerBlockChangeHandler(Player_BlockChange);
				Player.Disconnect += new Player.PlayerDisconnectHandler(Player_Disconnect);

				Player.Spawn += new Player.PlayerSpawnHandler(UpdatePlayersList);
				Player.Disconnect += new Player.PlayerDisconnectHandler(UpdatePlayersList);

				Players.Add(Player);
				Player.Start();

				Thread.Sleep(10);
			}
		}

		public Robot SpawnRobot(Position pos, string name)
		{
			Robot Robot = new Robot(name);
			Robot.pos = pos;

			Robot.Spawn += new Robot.RobotSpawnHandler(Robot_Spawn);
			//Robot.Message += new Player.PlayerMsgHandler(Player_Message);
			Robot.Move += new Robot.RobotMoveHandler(Robot_Move);
			//Robot.BlockChange += new Player.PlayerBlockChangeHandler(Player_BlockChange);
			Robot.Disconnect += new Robot.RobotDisconnectHandler(Robot_Disconnect);

			Robots.Add(Robot);
			Robot.Start();
			return Robot;
		}

		public Player GetPlayer(string name)
		{
			name = name.ToLower();
			
			if(name == "[console]") {
				return console;
			}
			
			// TODO: implement abbreviations (i.e. 'Space' could become 'SpaceManiac')
			List<Player> temp = new List<Player>(Players);
			foreach(Player P in temp) {
				if(P.name.ToLower() == name) {
					return P;
				}
			}
			return null;
		}

		public Player GetPlayerNot(string name, Player not)
		{
			name = name.ToLower();
			
			if(name == "[console]") {
				return console;
			}
			
			// TODO: implement abbreviations (i.e. 'Space' could become 'SpaceManiac')
			List<Player> temp = new List<Player>(Players);
			foreach(Player P in temp) {
				if(P.name != null && P.name.ToLower() == name &&
				  P != not) {
					return P;
				}
			}
			return null;
		}

		private void Heartbeat()
		{
			try {
				if (!Config.GetBool("heartbeat", true))
				{
					return;
				}
	
				StringBuilder builder = new StringBuilder();
	
				builder.Append("port=");
				builder.Append(port.ToString());
	
				builder.Append("&users=");
				builder.Append(Players.Count);
	
				builder.Append("&max=");
				builder.Append(maxplayers);
	
				builder.Append("&name=");
				builder.Append(name);
	
				builder.Append("&public=");
				if (Config.GetBool("public", false)) {
					builder.Append("true");
				} else {
					builder.Append("false");
				}
	
				builder.Append("&version=7");
	
				builder.Append("&salt=");
				builder.Append(salt.ToString());
	
				string postcontent = builder.ToString();
				byte[] post = Encoding.ASCII.GetBytes(postcontent);
	
				HttpWebRequest req = (HttpWebRequest)WebRequest.Create("http://minecraft.net/heartbeat.jsp");
				req.ContentType = "application/x-www-form-urlencoded";
				req.Method = "POST";
				req.ContentLength = post.Length;
				Stream o = req.GetRequestStream();
				o.Write(post, 0, post.Length);
				o.Close();
	
				WebResponse resp = req.GetResponse();
				StreamReader sr = new StreamReader(resp.GetResponseStream());
				string data = sr.ReadToEnd().Trim();
				
				if (!Initialized)
				{
					if (data.Substring(0, 7) != "http://") {
						Spacecraft.LogError("Heartbeat successful, but no URL returned!", null);
					} else {
						int i = data.IndexOf('=');
						serverhash = data.Substring(i + 1);
	
						//Spacecraft.Log("Salt is " + salt);
						Spacecraft.Log("To connect directly, surf to: ");
						Spacecraft.Log(data);
						Spacecraft.Log("(This is also in externalurl.txt)");
	
						StreamWriter outfile = File.CreateText("externalurl.txt");
						outfile.Write(data);
						outfile.Close();
	
						Initialized = true;
					}
				}
			}
			catch(WebException e) {
				Spacecraft.LogError("Unable to heartbeat", e);
			}
		}
		
		public void RemovePlayer(Player p)
		{
			Players.Remove(p);
		}

		void Player_Disconnect(Player Player)
		{
			byte ID = Player.playerID;
			Players.Remove(Player);
			List<Player> temp = new List<Player>(Players);
			foreach (Player P in temp) {
				P.PlayerDisconnects(ID);
			}
			Spacecraft.Log(Player.name + " (" + Player.ipAddress + ") has disconnected");
			MessageAll(Color.Yellow + Player.name + " has left");
			
			Game.PlayerQuits(Player);
		}

		void map_BlockChange(Map map, BlockPosition pos, Block BlockType)
		{
			List<Player> temp = new List<Player>(Players);
			foreach (Player P in temp) {
				P.BlockSet(pos, BlockType);
			}
		}

		void Player_BlockChange(Player sender, BlockPosition pos, Block BlockType)
		{
			Block was = map.GetTile(pos.x, pos.y, pos.z);
			if (!Game.CanBuild(sender, pos, BlockType)) {
				sender.BlockSet(pos, was);
				sender.PrintMessage(Color.DarkGreen + "You're not allowed to do that!");
				return;
			}
			map.SetTile(pos.x, pos.y, pos.z, BlockType);
			Game.PlayerBuilds(sender, pos, BlockType, was);
		}

		void Player_Move(Player sender, Position dest, byte heading, byte pitch)
		{			
			List<Player> temp = new List<Player>(Players);
			foreach (Player P in temp) {
				if(P != sender) {
					P.PlayerMoves(sender, dest, heading, pitch);
				}
			}
		}

		void Player_Message(Player sender, string msg)
		{
			string gameResult = Game.PlayerMessage(sender, msg);
			if (gameResult == "") {
				return;
			} else if (gameResult == null) {
				MessageAll(sender.name + ": " + msg);
			} else {
				MessageAll(gameResult);
			}
		}

		void Player_Spawn(Player sender)
		{
			List<Player> temp = new List<Player>(Players);
			foreach (Player P in temp) {
				P.PlayerJoins(sender);
				sender.PlayerJoins(P);
			}
			
			foreach (Robot R in new List<Robot>(Robots)) {
				sender.PlayerJoins(R);
			}

			MovePlayer(sender, map.spawn, map.spawnHeading, 0);
			Spacecraft.Log(sender.name + " (" + sender.ipAddress + ") has connected");
			MessageAll(Color.Yellow + sender.name + " has joined!");
			
			Game.PlayerJoins(sender);
		}
		
		void Robot_Spawn(Robot sender)
		{
			List<Player> temp = new List<Player>(Players);
			foreach (Player P in temp) {
				P.PlayerJoins(sender);
			}

			Spacecraft.Log("Bot " + sender.name + " has booted");
			MessageAll(Color.Announce + "Bot " + Spacecraft.StripColors(sender.name) + " has joined!");
		}

		void Robot_Move(Robot sender, Position dest, byte heading, byte pitch)
		{
			List<Player> temp = new List<Player>(Players);
			foreach (Player P in temp) {
				P.PlayerMoves(sender, dest, heading, pitch);
			}
		}

		void Robot_Disconnect(Robot Player)
		{
			byte ID = Player.playerID;
			Robots.Remove(Player);
			List<Player> temp = new List<Player>(Players);
			foreach (Player P in temp) {
				P.PlayerDisconnects(ID);
			}
			Spacecraft.Log("Bot " + Player.name + " has shut down");
			MessageAll(Color.Announce + "Bot " + Spacecraft.StripColors(Player.name) + " has left");
		}
		
		// -----

		public void MessageAll(string message)
		{
			if (Players != null && Players.Count != 0)
			{
				List<Player> temp = new List<Player>(Players);
				foreach (Player P in temp)
				{
					P.PrintMessage(message);
				}
			}
			Spacecraft.Log("[>] " + Spacecraft.StripColors(message));
		}

		public void MovePlayer(Player player, Position dest, byte heading, byte pitch)
		{
			List<Player> temp = new List<Player>(Players);
			foreach (Player P in temp) {
				P.PlayerMoves(player, dest, heading, pitch);
			}
		}

		public void ChangeBlock(BlockPosition pos, Block blockType)
		{
			List<Player> temp = new List<Player>(Players);
			foreach (Player P in temp) {
				P.BlockSet(pos, blockType);
			}
			map.SetTile(pos.x, pos.y, pos.z, blockType);
		}

		public void Shutdown()
		{
			Spacecraft.Log("Spacecraft is shutting down...");
			map.Save(Map.levelName);
		}

		// Added an external list of players, Atkins' request.
		private static object playersfile = new object();
		
		public void UpdatePlayersList(Player player)
		{
			// refresh players.txt so that it contains a list of current players
			lock (playersfile)
			{
				StreamWriter sw = new StreamWriter("players.txt", false);
				List<Player> temp = new List<Player>(Players);
				foreach (var P in temp) {
					sw.WriteLine(P.name);
				}
				sw.Write(System.Environment.NewLine);
				sw.Close();
			}
		}
	}
}