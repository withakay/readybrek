using System;

namespace Readybrek
{
    public class RedisException : Exception
    {
        public RedisException(string message) :base(message)
        {
        }
    }
}