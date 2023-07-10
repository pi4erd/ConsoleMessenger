using System.Net.Sockets;
using System.Net;
using System.Text;
using System;
using System.Linq;

namespace ConsoleMessenger
{
    internal class MessengerClient : IDisposable
    {
        TcpClient client;
        NetworkStream? stream;

        string username;

        bool clientState = false;
        public MessengerClient(string username) 
        {
            client = new TcpClient();
            this.username = username;
        }

        public bool Connect(string host, int port)
        {
            try
            {
                client.Connect(host, port);
                stream = client.GetStream();
            } catch (Exception ex)
            {
                Console.Error.WriteLine("Error occured while connecting to server: {0}", ex.Message);
                return false;
            }
            clientState = true;
            Console.WriteLine($"Connected as {username}");
            Console.WriteLine("Commands: /exit /list");
            return true;
        }
        public void ReadThread()
        {
            while(client.Connected)
            {
                try
                {
                    byte[] buffer = new byte[1024];

                    if (!clientState) return;

                    stream!.Read(buffer);

                    string response = Encoding.UTF8.GetString(buffer).Trim('\0');

                    if (response == "COK")
                    {
                        Console.Write("Connection closed");
                        Close();
                    }
                    else if (response == "OK") { }
                    else if (response == "INUS")
                    {
                        Console.WriteLine("User with username {0} is already connected to remote server!", username);
                        Console.Write("Connection closed");
                        Close();
                        return;
                    }
                    else
                    {
                        Console.WriteLine('\r' + response);
                    }
                } catch (Exception ex)
                {
                    Console.Error.WriteLine("Error occured: {0}", ex.Message);
                    return;
                }
            }
        }
        public void WriteThread()
        {
            stream!.Write(Encoding.UTF8.GetBytes(username));

            while(client.Connected)
            {
                string message = Console.ReadLine()!;

                if (!clientState) return;

                try
                {
                    stream.Write(Encoding.UTF8.GetBytes(message));
                }
                catch (IOException ioex)
                {
                    Console.Error.WriteLine("Error occured while trying to send data to server: {0}", ioex.Message);
                    return;
                }
            }
        }
        public void Close()
        {
            clientState = false;
            stream?.Dispose();
            client.Close();
        }
        public void Dispose()
        {
            Close();
        }
    }
    internal class ClientProgram
    {
        static string hostname = "localhost";
        const int port = 2431;

        static string username = "";

        static void Main(string[] args)
        {
            Console.Write("Enter host name: ");
            hostname = Console.ReadLine()!;

            Console.Write("Enter your username: ");
            username = Console.ReadLine()!;

            MessengerClient client = new(username);

            if (!client.Connect(hostname, port))
            {
                Console.Write("Press any key to exit...");
                Console.ReadKey();
                return;
            }

            Thread readThread = new(new ThreadStart(client.ReadThread));
            Thread writeThread = new(new ThreadStart(client.WriteThread));

            readThread.Start();
            writeThread.Start();

            readThread.Join();
            writeThread.Join();

            client.Close();
        }
    }
}