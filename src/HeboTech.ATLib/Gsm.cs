﻿using System;
using System.Threading;
using System.Threading.Tasks;

namespace HeboTech.ATLib
{
    public class Gsm : IGsm
    {
        private readonly IGsmStream stream;
        private readonly int writeDelayMs = 25;
        private const string OK_RESPONSE = "OK";

        public Gsm(IGsmStream stream, int writeDelayMs = 25)
        {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
            if (writeDelayMs < 0)
                throw new ArgumentOutOfRangeException($"{nameof(writeDelayMs)} must be a positive number");
            this.writeDelayMs = writeDelayMs;
        }

        public Task<bool> InitializeAsync()
        {
            return Task.Factory.StartNew(() =>
            {
                return stream.SendCheckReply("AT\r\n", OK_RESPONSE, 100);
            });
        }

        public Task<bool> SetMode(Mode mode)
        {
            return Task.Factory.StartNew(() =>
            {
                return stream.SendCheckReply($"AT+CMGF={(int)mode}\r\n", OK_RESPONSE, 5_000);
            });
        }

        public Task<bool> SendSmsAsync(string phoneNumber, string message)
        {
            return Task.Factory.StartNew(() =>
            {
                bool status = false;
                status = stream.SendCheckReply($"AT+CMGS=\"{phoneNumber}\"\r", "> ", 5_000);
                if (status)
                {
                    Thread.Sleep(writeDelayMs);
                    status = stream.SendCheckReply($"{message}\x1A\r\n", OK_RESPONSE, 180_000);
                }
                return status;
            });
        }
    }
}
