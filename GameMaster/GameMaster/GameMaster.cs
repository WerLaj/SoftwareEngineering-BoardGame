using System;
using System.Xml;
using System.IO;
using System.Xml.Serialization;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Net;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Text;
using MessageProject;

namespace GM
{
    class GameMaster
    {

        public Game game;       
        int listenerPortNumber = 5000;
        public string serverIp;
        public int serverPort = 4242;
        public Socket gmSocketGlobal;

        public void launch()
        {
            serverIp = GetIP4Address();
            // set threadpool initial params
            ThreadPool.SetMinThreads(3, 3);
            ThreadPool.SetMaxThreads(10, 10);

            MakeGame("..\\..\\GameSettings\\XMLgameSettings1.xml");
            gmSocketGlobal = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            RegisterGame();

            // Start listening for Game related connections (assumes game was registered)
            Console.WriteLine("GM started listening...");
            //TcpListener listener = new TcpListener(IPAddress.Parse(GetIP4Address()), listenerPortNumber);
            //listener.Start();
           
            while(true)
            {
                //gmSocketGlobal.Listen(0);
                // Gets socket, launches thread with it as param and HandleRequest which deserializes it
                //Socket newConnectionSocket = listener.AcceptSocket();
                // Socket newConnectionSocket = gmSocketGlobal.Accept();
                byte[] bufferIn = new byte[gmSocketGlobal.SendBufferSize];
                int readbytes = gmSocketGlobal.Receive(bufferIn);
                ThreadPool.QueueUserWorkItem(HandleRequest,bufferIn);
            }
        }

        public static string GetIP4Address()
        {
            IPAddress[] ips = Dns.GetHostAddresses(Dns.GetHostName());

            foreach (IPAddress i in ips)
            {
                if (i.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) return i.ToString();
            }

            return "";
        }

        public void MakeGame(string path)
        {
            // Responsible for initializing the only game that GM can have with passed config file and ID 1


            // Only 1 game at once
            game = new Game(ReadGameInfo(path));
            Console.WriteLine(game.settings.ToString());

            int newGameID = 1;
            game.gameId = newGameID;
           
        }

        public void RegisterGame()
        {
            // Registers game to the CS
            string registrationXml = MakeGameCreationMessage();
            //Socket gmSock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
          //  gmSocketGlobal = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            byte[] buffer = new byte[gmSocketGlobal.SendBufferSize];
            buffer = Encoding.ASCII.GetBytes(registrationXml);

            IPEndPoint localIp = new IPEndPoint(IPAddress.Parse(GetIP4Address()), listenerPortNumber); // thats local port
            IPEndPoint remote = new IPEndPoint(IPAddress.Parse(serverIp), serverPort);
            //gmSock.Bind(localIp);
            //gmSock.Connect(remote);
            //gmSock.Send(buffer);
            gmSocketGlobal.Bind(localIp);
            gmSocketGlobal.Connect(remote);
            gmSocketGlobal.Send(buffer);

            while (true)
            {
                // read single response message
                //byte[] bufferIn = new byte[gmSock.SendBufferSize];
                //int readbytes = gmSock.Receive(bufferIn); 
                byte[] bufferIn = new byte[gmSocketGlobal.SendBufferSize];
                int readbytes = gmSocketGlobal.Receive(bufferIn);


                if (readbytes > 0)
                {
                    string messageString = Encoding.ASCII.GetString(bufferIn);
                    Message tempMessage = MessageProject.Message.xmlIntoMessage(messageString);
                    Type typer = tempMessage.GetType();
                    dynamic newMessage = Convert.ChangeType(tempMessage, typer);

                    if(newMessage.GetType().ToString().Equals("MessageProject.ConfirmGameRegistration"))
                    {
                        Console.WriteLine("Response message type correct! game registered with id: {0}", ((ConfirmGameRegistration)newMessage).gameID);
                    }
                    else
                    {
                        Console.WriteLine("Message received(response): {0}", messageString);
                        Console.WriteLine(newMessage.GetType().ToString());
                        Console.WriteLine("Response type incompatible!");
                    }

                    break;
                }
            }
            // The same port and IP will be reused in main listener
            //gmSock.Disconnect(false);
            //gmSock.Close();
        }

        public void HandleRequest(object bufforIn)
        {
            // Already in new thread, ThreadStart starting mathed here !!!
            // Deserialize message
            // If message P2P - handle it here, else delegate to specific game.handleMoveReq/...
            // Decide upon move/pick/drop - message type
            // formulate response, type defined by message type from input and return values
           // Socket gmSocket = (Socket)cs;
            byte[] buffer = new byte[gmSocketGlobal.SendBufferSize];
            buffer = (byte[])bufforIn;
           // int readBytes;
            try
            {
                //buffer = new byte[gmSocketGlobal.SendBufferSize];
               // readBytes = gmSocketGlobal.Receive(buffer);

               // if (readBytes > 0) // assume all is read at once
               // {
                    string messageString = Encoding.ASCII.GetString(buffer);
                    
                    // get message from socket

                    Message tempMessage = MessageProject.Message.xmlIntoMessage(messageString);
                    Type typer = tempMessage.GetType();
                    dynamic newMessage = Convert.ChangeType(tempMessage, typer);
                    Console.WriteLine("Received message of type: {0}", newMessage.GetType());
                    // Selecting action on message type

                    switch(newMessage) // c# 7.0 -> switch on type
                    {
                        case Move msg1:
                            
                            // Tuple <int, int> coordinatesMsg1 = gamesDictionary[gameID].HandleMoveRequest(msg1.playerID, (int)msg1.direction); // int by enum assigned values, internal switch
                            // string response1 = MakeMoveResponse(msg1.playerID, coordinatesMsg1);
                            // send response
                            break;

                        case JoinGame msg2:
                            Console.WriteLine("Parsing game join request!");
                            // player can join only a agame he sees, hence gameId = 1
                            Tuple<int, MessageProject.Team, MessageProject.Role> newPlayer = game.MakePlayer(msg2.preferredRole, msg2.preferredTeam, msg2.playerID);
                            string response2 = MakeJoinGameResponse(msg2.playerID, newPlayer); // assume playerID is always correct
                            Console.WriteLine("Response being sent: {0}", response2);
                            buffer = Encoding.ASCII.GetBytes(response2);
                            gmSocketGlobal.Send(buffer);

                            //After reject/ confirm was sent, verify if the game has just been started(code - 999 as player ID)
                            //if (null == newPlayer)
                            //{
                            //    // Game just started!

                            //    ThreadPool.QueueUserWorkItem(StartGame, 100); // random object passed
                            //}
                            break;

                    }


              //  }
            }

            catch
            {

            }
        }


        private string MakeMoveResponse(int playerID, Tuple<int, int> coordinates)
        {
            MoveResponse responseObj = new MoveResponse(playerID, new PlayerLocation(coordinates.Item2, coordinates.Item1)); // tupple in array format Y, X
            return MessageProject.Message.messageIntoXML(responseObj);

        }

        private string MakeGameCreationMessage()
        {
            // for main game only
            RegisterGame responseObj = new RegisterGame(game.gameId, game.settings.PlayersPerTeam, game.settings.PlayersPerTeam);
            // The same ammount of players in both teams atm!
            return MessageProject.Message.messageIntoXML(responseObj);
        }

        private string MakeJoinGameResponse(int playerID, Tuple<int, MessageProject.Team, MessageProject.Role> newPlayer)
        {
            if(null == newPlayer || newPlayer.Item1 < 0)
            {
                // or null
                // negative value as newPlayerId -> failure of player making, teams full
                RejectJoiningGame responseObj = new RejectJoiningGame(game.gameId);
                responseObj.playerID = playerID;
                return MessageProject.Message.messageIntoXML(responseObj);
            }
            else //if(newPlayer.Item1 >= 0)
            {
                ConfirmJoiningGame responseObj = new ConfirmJoiningGame(game.gameId, newPlayer.Item1, new MessageProject.Player(playerID, newPlayer.Item2, newPlayer.Item3));
                return MessageProject.Message.messageIntoXML(responseObj);
            }
            
        }

        private void StartGame(object smth)
        {
            lock(game.playersDictionary)
            {
                lock(game.board)
                {
                    Random rand = new Random();
                    List<Tuple<int, int>> coordinates = new List<Tuple<int, int>>(); // Y, X pairs

                    for(int y = game.settings.GoalLen; y<game.settings.TaskLen + game.settings.GoalLen; y++)
                    {
                        for(int x=0; x< game.settings.BoardWidth; x++)
                        {
                            Tuple<int, int> newCoord = new Tuple<int, int>(y, x);
                            coordinates.Add(newCoord);
                        }
                    }
                    // Fix players positions
                    for (int i=1; i<=game.playersDictionary.Count(); i++)
                    {
                        int indexToPop = GetRandomValue(coordinates.Count(), rand);
                        Tuple<int, int> randCoordinate = coordinates.ElementAt(indexToPop);
                        coordinates.RemoveAt(indexToPop);
                        Console.WriteLine("Coordinate picked: Y={0}, X={1}", randCoordinate.Item1, randCoordinate.Item2);
                    }
                }
                // Make all players positions, fields & goals

                // Send the same message to all players

            }
        }

        private static int GetRandomValue(int range, Random random)
        {
            return random.Next(range); // <0, range)
        }

        static public DataGame ReadGameInfo(string fileName)
        {
            // Deserializes XML with game config
            XmlDocument doc = new XmlDocument();
            
            doc.Load(fileName);

            XmlElement root = doc.DocumentElement;
            DataGame settings = new DataGame();
            settings.Name = root.SelectSingleNode("name").InnerXml;
            settings.BoardWidth = Int32.Parse(root.SelectSingleNode("board_width").InnerXml);
            settings.TaskLen = Int32.Parse(root.SelectSingleNode("task_len").InnerXml);
            settings.GoalLen = Int32.Parse(root.SelectSingleNode("goal_len").InnerXml);
            settings.InitialPieces = Int32.Parse(root.SelectSingleNode("initial_pieces").InnerXml);
            settings.PlayersPerTeam = Int32.Parse(root.SelectSingleNode("players_per_team").InnerXml);

            settings.DelayMove = Int64.Parse(root.SelectSingleNode("delay_move").InnerXml);
            settings.DelayPick = Int64.Parse(root.SelectSingleNode("delay_pick").InnerXml);
            settings.DelayDrop = Int64.Parse(root.SelectSingleNode("delay_drop").InnerXml);

            return settings;
        }

        



    }

}