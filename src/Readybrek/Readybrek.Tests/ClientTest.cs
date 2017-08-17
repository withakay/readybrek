using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.ComTypes;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Xunit;

namespace Readybrek.Tests
{
    public class ClientTest : IDisposable
    {
        private const string Server = "127.0.0.1";
        private const int Port = 6379;

        private readonly Client _client;
        
        public ClientTest()
        {
            _client = new Client(Server, Port);
        }

        private void Connect()
        {
            if (!_client.Connected())
            {
                var redisTask = Task.Run(() => _client.Connect());
                redisTask.Wait();
            }
        }
        
        // helper method
        private string Send(params string[] commands)
        {
            return SendArray(commands);
        }
        
        private string SendArray(string[] command)
        {
            var redisTask = Task.Run(() => _client.SendArray(command));
            redisTask.Wait();
            return redisTask.Result;
        }
        
        [Fact]
        public void ShouldNotConnectToClosedPort()
        {
            // use a port expected to not be in use
            using (var c = new Client(Server, 60001))
            {
                try
                {
                    var redisTask = Task.Run(() => c.Connect());
                    redisTask.Wait();
                }
                catch (AggregateException e)
                {
                    Console.WriteLine(e);
                }
                Assert.False(c.Connected());
            }
        }
        
        [Fact]
        public void ClientShouldConnectToRunningRedisServer()
        {
            Connect();
            Assert.True(_client.Connected());
        }
       
        [Fact]
        public void CanPing()
        {
            Connect();
            var r = Send("PING");
            Assert.NotEqual(r, "XYZ");
            Assert.Equal(r, "PONG");
        }

        [Fact]
        public void CanSet()
        {
            Connect();
            var r = Send("SET", "test:mykey", "The Quick Brown Fox");
            Assert.Equal(r, "OK");
        }
        
        [Fact]
        public void CanSetAndGet()
        {
            Connect();
            var t = "The Quick Brown Fox";
            var r = Send("SET", "test:mykey", t);
            Assert.Equal(r, "OK");
            r = Send("GET", "test:mykey");
            Assert.Equal(r, t);
        }

        [Fact]
        public void CanMSet()
        {
            Connect();
            var r = Send("MSET", "test:msetkey1", "1", "test:msetkey2", "2", "test:msetkey3", "3");
            Assert.Equal(r, "OK");
            r = Send("GET", "test:msetkey3");
            Assert.Equal(r, "3");
        }
        
        [Fact]
        public void CanIncr()
        {
            Connect();
            var r = Send("SET", "test:incrkey", "10");
            Assert.Equal(r, "OK");
            r = Send("INCR", "test:incrkey");
            Assert.Equal(r, "11");
            r = Send("GET", "test:incrkey");
            Assert.Equal(r, "11");
        }
        
        [Fact]
        public void UnknownCommandShouldThrowException()
        {
            Connect();
            Exception ex = Assert.Throws<AggregateException>(() => Send("NONSENSECOMMAND"));
        }
        
        public void Dispose()
        {
            _client?.Dispose();
        }
    }
}