using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Web;

namespace spacecraft
{
    class Spacecraft
    {
        public static void Main()
        {
            try
            {
                Log("{0} is starting...", "Spacecraft");
                if (!File.Exists("admins.txt"))
                {
                    Log("Note: admins.txt does not exist, creating.");
                    File.Create("admins.txt");
                }

                if (!File.Exists("properties.txt"))
                {
                    Log("Error: could not find properties.txt!");
                    return;
                }
                else
                {
                    Config.Initialize();
                }

                //  Block.MakeNames();

                LoadRanks();

                MinecraftServer serv = new MinecraftServer();

                serv.Start();

                Spacecraft.Log("Bye!");
                Spacecraft.Log("");
                Environment.Exit(0);
            }
            catch (Exception e) // Something went wrong and wasn't caught.
            {
                Console.WriteLine("===FATAL ERROR===");
                Console.WriteLine(e.Message);
                Console.WriteLine(e.Source);
                Console.WriteLine();
                Console.Write(e.StackTrace);
            }
        }

        public static void LoadRanks()
        {
            Player.RankedPlayers.Clear();
            StreamReader Reader = new StreamReader("admins.txt");
            string[] Lines = Reader.ReadToEnd().Split(new string[] { System.Environment.NewLine }, StringSplitOptions.RemoveEmptyEntries);
            Reader.Close();

            foreach (var line in Lines)
            {
                string[] parts;
                string rank;
                parts = line.Split('=');

                rank = parts[0].Substring(0, 1).ToUpper() + parts[0].Substring(1, parts[0].Length - 1);

                Rank assignedRank = (Rank)Enum.Parse(typeof(Rank), rank);

                if (!Player.RankedPlayers.ContainsKey(assignedRank) || Player.RankedPlayers[assignedRank] == null)
                {
                    Player.RankedPlayers[assignedRank] = new List<string>();
                }

                string[] people = parts[1].Split(',');

                for (int i = 0; i < people.Length; i++)
                {
                    string name = people[i];
                    Player.RankedPlayers[assignedRank].Add(name);
                }


            }
        }

        private static object logfile = new object();

        public static void Log(string text)
        {
            if (!File.Exists("server.log"))
            {
                File.Create("server.log");
            }
            lock (logfile)
            {
                StreamWriter sw = new StreamWriter("server.log", true);
                if (text == "")
                {
                    sw.WriteLine();
                    Console.WriteLine();
                }
                else
                {
                    sw.WriteLine("{0}\t{1}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), text);
                    Console.WriteLine("{0}\t{1}", DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"), text);
                }
                sw.Close();
            }
        }

        private static object errorfile = new object();

        public static void LogError(string text)
        {
            lock (errorfile)
            {
                StreamWriter sw = new StreamWriter("error.log", true);
                sw.Write("==== ");
                sw.Write(DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss"));
                sw.Write(" ====");
                sw.WriteLine(text);
                sw.Close();
                Log("ERROR! Check error.log for details!");
            }
        }


        public static void Log(string format, params object[] args)
        {
            Log(String.Format(format, args));
        }

    }
}