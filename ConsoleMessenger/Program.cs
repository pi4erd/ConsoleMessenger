using System.Net.Sockets;
using System.Net;
using System.Linq;
using System.Text;
using System;

namespace ConsoleMessenger
{
    internal enum LogSeverity
    {
        INFO, WARNING, ERROR, FATAL
    }
    internal interface ILogger
    {
        void Log(string message, LogSeverity severity=LogSeverity.INFO);
        void LogError(string message, Exception ex, LogSeverity severity = LogSeverity.ERROR);
    }
    internal class ServerLogger : ILogger
    {
        public void Log(string message, LogSeverity severity)
        {
            Console.WriteLine($"[{DateTime.Now}] [{severity}] {message}");
        }

        public void LogError(string message, Exception ex, LogSeverity severity)
        {
            Console.Error.WriteLine($"[{DateTime.Now}] [{severity}] {message} {ex.Message}");
        }
    }
    internal struct Message
    {
        public string author;
        public string content;
    }
    internal class MessengerServer
    {
        TcpListener listener;

        ILogger logger;

        List<Message> pendingMessages = new List<Message>();
        Dictionary<string, TcpClient> connectedUsers = new Dictionary<string, TcpClient>();
        
        public MessengerServer(ILogger logger)
        {
            listener = new TcpListener(IPAddress.Any, 2431);
            this.logger = logger;
        }

        private void WriteThread()
        {
            while(true)
            {
                List<Message> tmp = new List<Message>();

                foreach (Message msg in pendingMessages)
                {
                    tmp.Add(msg);
                }
                foreach (Message msg in tmp)
                {
                    foreach (KeyValuePair<string, TcpClient> user in connectedUsers)
                    {
                        if (msg.author == user.Key) continue;
                        try
                        {
                            user.Value.GetStream().Write(Encoding.UTF8.GetBytes($"{msg.author}: {msg.content}"));
                        }
                        catch (Exception ex)
                        {
                            logger.LogError("Error occured while sending message to users:", ex);
                            continue;
                        }
                    }
                    pendingMessages.Remove(msg);
                }
                Thread.Sleep(100);
            }
        }

        private void ClientThread(object? obj)
        {
            if (obj == null) return;

            TcpClient client = (TcpClient)obj;
            NetworkStream stream = client.GetStream();

            byte[] usrnamebuf = new byte[128];
            stream.Read(usrnamebuf);

            string username = Encoding.UTF8.GetString(usrnamebuf).Trim('\0');

            if(connectedUsers.ContainsKey(username) || username == "SERVER")
            {
                stream.Write(Encoding.UTF8.GetBytes("INUS"));
                client.Close();
                return;
            }

            connectedUsers.Add(username, client);

            logger.Log($"New user {username} connected");
            pendingMessages.Add(new() { author = "SERVER", content = $"{username} connected" });

            while (client.Connected)
            {
                try
                {
                    byte[] buffer = new byte[1024];
                    stream.Read(buffer);
                    string message = Encoding.UTF8.GetString(buffer).Trim('\0');

                    if(message == "/exit")
                    {
                        stream.Write(Encoding.UTF8.GetBytes("COK"));
                        client.Close();
                        connectedUsers.Remove(username);
                        logger.Log($"User {username} left the chat");
                        break;
                    }
                    else if(message == "/list")
                    {
                        string userList = $"Connected users ({connectedUsers.Count}):\n";
                        foreach (string un in connectedUsers.Keys) userList += $"  {un}\n";
                        stream.Write(Encoding.UTF8.GetBytes(userList));
                        continue;
                    }

                    pendingMessages.Add(new() { content = message, author = username });
                    logger.Log($"{username}: {message}");
                } catch (Exception ex)
                {
                    logger.LogError("Error occured:", ex);
                    connectedUsers.Remove(username);
                    return;
                }
            }
        }

        public void Listen()
        {
            try
            {
                listener.Start();
            } catch(SocketException ex)
            {
                logger.LogError("Error occured while starting server:", ex, LogSeverity.FATAL);
                return;
            }

            logger.Log($"Listening at {listener.LocalEndpoint}");

            Thread msgSendTask = new(new ThreadStart(WriteThread));
            msgSendTask.Start();

            while (true)
            {
                TcpClient client = listener.AcceptTcpClient();

                ThreadPool.QueueUserWorkItem(ClientThread, client);
            }

            //msgSendTask.Join();
        }
    }
    internal class ServerProgram
    {
        static void Main(string[] args)
        {
            ServerLogger logger = new();
            MessengerServer server = new(logger);

            server.Listen();
        }
    }
}