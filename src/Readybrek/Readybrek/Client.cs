using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

[assembly: InternalsVisibleTo("Readybrek.Tests")]

namespace Readybrek
{
    public class Client : IDisposable
    {
        private const string Termchars = "\r\n";
        
        private TcpClient _tcpClient;
        private NetworkStream _networkStream;
        private BinaryWriter _writer;
        private BinaryReader _reader;
        
        public string Server { get; set; }
        public ushort Port { get; set; }
        private IPAddress ServerIPAddress { get; set; }
        
        public Client(string server, ushort port)
        {
            Server = server;
            Port = port;
            ResolveIpAddress();
        }

        public async Task Connect()
        {
            if (ServerIPAddress == null)
                throw new Exception("No IPv4 address for server");
            
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(ServerIPAddress, Port); // Connect
            _networkStream = _tcpClient.GetStream();
            _writer = new BinaryWriter(_networkStream, Encoding.UTF8);
            _reader = new BinaryReader(_networkStream, Encoding.UTF8);
        }

        public bool Connected()
        {
            return _tcpClient != null && _tcpClient.Connected;
        }
        
        internal async Task<string> SendRequest(byte[] requestPayload)
        {
            if (!Connected())
                await Connect();
            
            StringBuilder sb = new StringBuilder ();
            
            try
            {
                WriteBytes(requestPayload);
                int ch;
                int counter = 0;
                bool throwEx = false;
                while ((ch = ReadByte()) != -1)
                {
                    // OK response
                    if (ch == '+' && counter == 0)
                        continue;
                    // Integer response
                    if (ch == ':' && counter == 0)
                        continue;
                    // Error response
                    if (ch == '-' && counter == 0)
                        throwEx = true;
                    // Bulk string response
                    if (ch == '$' && counter == 0)
                    {
                        BulkString();
                        continue;
                    }
                    if (ch == '\r')
                        continue;
                    if (ch == '\n')
                        break;
                    sb.Append ((char) ch);
                    counter++;
                }
                if (throwEx)
                {
                    throw new RedisException(sb.ToString());
                }
                return sb.ToString();
            }
            catch (Exception ex) {
                Dispose();
                throw ex;
            }
        }

        internal void WriteBytes(byte[] data)
        {
            _writer.Write(data);
        }
        
        internal byte ReadByte()
        {
            // TODO: make this mockable for testing
            return _reader.ReadByte();
        }
        
        internal void BulkString()
        {
            int ch;
            while ((ch = ReadByte()) != -1)
            {
                if (ch == '\r')
                    continue;
                if (ch == '\n')
                    break;
            }
        }

        /// <summary>
        /// Send a single command to redis and return the response
        /// e.g. PING
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public async Task<string> Send(string cmd)
        {
            return await Send(cmd, null);
        }
        
        /// <summary>
        /// Send a command to redis with key
        /// e.g. GET mykey
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="key"></param>
        /// <returns></returns>
        public async Task<string> Send(string cmd, string key)
        {
            return await Send(cmd, key, new string[]{});
        }

        /// <summary>
        /// Send a command with a key and multiple values
        /// e.g. 
        ///     SET mykey "123"
        ///     SETEX mykey 10 "Hello"
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="key"></param>
        /// <param name="data"></param>
        /// <returns></returns>
        public async Task<string> Send(string cmd, string key, params string[] data)
        {
            // flatten input params to array
            var commands = new string[data.Length + 2];
            commands[0] = cmd;
            commands[1] = key;
            // sanitise  
            data.Where(p => p != null).ToArray().CopyTo(commands, 2);
            return await SendArray(commands);
        }
        
        /// <summary>
        /// Send an arbritary list of commands to redis
        /// </summary>
        /// <param name="commands"></param>
        /// <returns></returns>
        public async Task<string> Send(params string[] commands)
        {
            return await SendArray(commands);
        }

        internal async Task<string> SendArray(string[] commands)
        {
            byte[] respBytes = Encoding.UTF8.GetBytes(BuildRespCommand(commands));
            return await SendRequest(respBytes);
        }
        
        internal string BuildRespCommand(string[] data)
        {
            var sb = new StringBuilder("*" + data.Count(p => p != null) + Termchars);
            // count the number of inputs we have
            if (data != null && data.Length > 0)
            {
                foreach (var d in data.Where(x => x != null))
                {
                    sb.Append("$" + d.Length + Termchars + d + Termchars);
                }
            }
            return sb.ToString();
        }
        
        /// <summary>
        /// resolve this.Server which should be a FQDN or IP address string to a .net IPAddress instance 
        /// </summary>
        internal async void ResolveIpAddress()
        {
            ServerIPAddress = null;
            IPAddress ip;
            if (IPAddress.TryParse(Server, out ip))
            {
                ServerIPAddress = ip;
                return;
            }
            
            IPHostEntry ipHostInfo = await Dns.GetHostEntryAsync(Server);
            foreach (IPAddress address in ipHostInfo.AddressList)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork)
                {
                    ServerIPAddress = address;
                    break;
                }
            }
        }

        public void Dispose()
        {
            _tcpClient.Dispose();
        }
    }
}