using System;
using System.Linq;
using System.Threading.Tasks;

namespace rbcli
{
    class Program
    {
        private static Readybrek.Client _client;
        private static string _address = "127.0.0.1";
        private static ushort _port = 6379;
        
        static void Main(string[] args)
        {
            if (args.Length > 0)
                _address = args[0];

            if (args.Length > 1 && !ushort.TryParse(args[1], out _port))
            {
                Console.WriteLine($"{args[1]} is not a valid port number, exiting.");
                return;
            }
            
            Console.WriteLine("Readybrek, a simple redis client.");
            Console.WriteLine($"Using {_address}:{_port}");
            Console.WriteLine("Enter a redis command and press enter");
            
            _client = new Readybrek.Client(_address, _port);
            bool run = true;
            while (run)
            {
                string cmdLine = Console.ReadLine();
                string[] cmdArgs = cmdLine.Split(' ');
                switch (cmdArgs.Length)
                {
                    case 0:
                        // nothing to do
                        break;
                    case 1:  
                    case 2:
                    case 3:
                        RunTask(cmdArgs);
                        break;
                }
            }
        }

        static void RunTask(string[] cmdArgs)
        {
            string[] a = {null, null, null};
            cmdArgs.CopyTo(a, 0);
            try
            {
                var redisTask = Task.Run(() => _client.Send(a[0], a[1], a.Skip(2).ToArray()));
                redisTask.Wait();
                Console.WriteLine(redisTask.Result);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }
    }
}