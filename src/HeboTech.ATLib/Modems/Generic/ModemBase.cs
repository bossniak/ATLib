﻿using HeboTech.ATLib.DTOs;
using HeboTech.ATLib.Events;
using HeboTech.ATLib.Parsers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace HeboTech.ATLib.Modems.Generic
{
    public abstract class ModemBase : IDisposable
    {
        protected readonly AtChannel channel;
        private bool disposed;

        public ModemBase(AtChannel channel)
        {
            this.channel = channel;
            channel.UnsolicitedEvent += Channel_UnsolicitedEvent;
        }

        private void Channel_UnsolicitedEvent(object sender, UnsolicitedEventArgs e)
        {
            if (e.Line1 == "RING")
                IncomingCall?.Invoke(this, new IncomingCallEventArgs());
            else if (e.Line1.StartsWith("MISSED_CALL: "))
                MissedCall?.Invoke(this, MissedCallEventArgs.CreateFromResponse(e.Line1));
            else if (e.Line1.StartsWith("+CMTI: "))
            {
                var match = Regex.Match(e.Line1, @"\+CMTI:\s""(?<storage>[A-Z]+)"",(?<index>\d+)");
                if (match.Success)
                {
                    string storage = match.Groups["storage"].Value;
                    int index = int.Parse(match.Groups["index"].Value);
                    SmsReceived?.Invoke(this, new SmsReceivedEventArgs(storage, index));
                }
            }
        }

        #region _V_25TER
        public event EventHandler<IncomingCallEventArgs> IncomingCall;
        public event EventHandler<MissedCallEventArgs> MissedCall;

        public virtual async Task<CommandStatus> AnswerIncomingCallAsync()
        {
            (AtError error, _) = await channel.SendCommand("ATA");

            if (error == AtError.NO_ERROR)
                return CommandStatus.OK;
            return CommandStatus.ERROR;
        }

        public virtual async Task<CommandStatus> DisableEchoAsync()
        {
            (AtError error, _) = await channel.SendCommand("ATE0");

            if (error == AtError.NO_ERROR)
                return CommandStatus.OK;
            return CommandStatus.ERROR;
        }

        public virtual async Task<ProductIdentificationInformation> GetProductIdentificationInformationAsync()
        {
            (AtError error, AtResponse response) = await channel.SendMultilineCommand("ATI", null);

            if (error == AtError.NO_ERROR)
            {
                StringBuilder builder = new StringBuilder();
                foreach (string line in response.Intermediates)
                {
                    builder.AppendLine(line);
                }

                return new ProductIdentificationInformation(builder.ToString());
            }
            return null;
        }

        public virtual async Task<CallDetails> HangupAsync()
        {
            (AtError error, AtResponse response) = await channel.SendSingleLineCommandAsync("AT+CHUP", "VOICE CALL:");

            if (error == AtError.NO_ERROR)
            {
                string line = response.Intermediates.First();
                var match = Regex.Match(line, @"VOICE CALL: END: (?<duration>\d+)");
                if (match.Success)
                {
                    int duration = int.Parse(match.Groups["duration"].Value);
                    return new CallDetails(TimeSpan.FromSeconds(duration));
                }
            }
            return null;
        }
        #endregion

        #region _3GPP_TS_27_005
        public event EventHandler<SmsReceivedEventArgs> SmsReceived;

        public virtual async Task<CommandStatus> SetSmsMessageFormatAsync(SmsTextFormat format)
        {
            (AtError error, _) = await channel.SendCommand($"AT+CMGF={(int)format}");

            if (error == AtError.NO_ERROR)
                return CommandStatus.OK;
            return CommandStatus.ERROR;
        }

        public virtual async Task<IList<MessageStorageStatus>> GetPreferredMessageStorage()
        {
            (AtError error, AtResponse response) = await channel.SendSingleLineCommandAsync("AT+CPMS?", "+CPMS:");
            if (error == AtError.NO_ERROR)
            {
                string line = response.Intermediates.First();
                var match = Regex.Match(line, @"\+CPMS:\s""(?<mem1>[A-Z]+)"",(?<used1>\d+),(?<total1>\d+),""(?<mem2>[A-Z]+)"",(?<used2>\d+),(?<total2>\d+),""(?<mem3>[A-Z]+)"",(?<used3>\d+),(?<total3>\d+)");
                if (match.Success)
                {
                    string mem1 = match.Groups["mem1"].Value;
                    int used1 = int.Parse(match.Groups["used1"].Value);
                    int total1 = int.Parse(match.Groups["total1"].Value);

                    string mem2 = match.Groups["mem2"].Value;
                    int used2 = int.Parse(match.Groups["used2"].Value);
                    int total2 = int.Parse(match.Groups["total2"].Value);

                    string mem3 = match.Groups["mem3"].Value;
                    int used3 = int.Parse(match.Groups["used3"].Value);
                    int total3 = int.Parse(match.Groups["total3"].Value);

                    List<MessageStorageStatus> statuses = new List<MessageStorageStatus>()
                    {
                        new MessageStorageStatus(mem1, used1, total1),
                        new MessageStorageStatus(mem2, used2, total2),
                        new MessageStorageStatus(mem3, used3, total3),
                    };

                    return statuses;
                }
            }
            return null;
        }

        public virtual async Task<IList<MessageStorageStatus>> SetPreferredMessageStorage(string mem1, string mem2 = null, string mem3 = null)
        {
            if (mem1 == null)
                throw new ArgumentNullException(nameof(mem1));
            if (!(mem1 == Storages.FlashME || mem1 == Storages.FlashMT || mem1 == Storages.SIM || mem1 == Storages.StatusReport))
                throw new ArgumentOutOfRangeException(nameof(mem1));
            if (mem2 != null && !(mem2 == Storages.FlashME || mem2 == Storages.FlashMT || mem2 == Storages.SIM || mem2 == Storages.StatusReport))
                throw new ArgumentOutOfRangeException(nameof(mem2));
            if (mem3 != null && mem2 == null)
                throw new ArgumentNullException(nameof(mem2));
            if (mem3 != null && !(mem3 == Storages.FlashME || mem3 == Storages.FlashMT || mem3 == Storages.SIM || mem3 == Storages.StatusReport))
                throw new ArgumentOutOfRangeException(nameof(mem3));

            string command = $"AT+CPMS=\"{mem1}\"";
            if (mem2 != null)
                command += $",\"{mem2}\"";
            if (mem3 != null)
                command += $",\"{mem3}\"";
            (AtError error, AtResponse response) = await channel.SendSingleLineCommandAsync(command, "+CPMS:");

            if (error == AtError.NO_ERROR)
            {
                string line = response.Intermediates.First();
                var match = Regex.Match(line, @"\+CPMS:\s(?<used1>\d+),(?<total1>\d+),(?<used2>\d+),(?<total2>\d+),(?<used3>\d+),(?<total3>\d+)");
                if (match.Success)
                {
                    int used1 = int.Parse(match.Groups["used1"].Value);
                    int total1 = int.Parse(match.Groups["total1"].Value);

                    int used2 = int.Parse(match.Groups["used2"].Value);
                    int total2 = int.Parse(match.Groups["total2"].Value);

                    int used3 = int.Parse(match.Groups["used3"].Value);
                    int total3 = int.Parse(match.Groups["total3"].Value);

                    List<MessageStorageStatus> statuses = new List<MessageStorageStatus>()
                    {
                        new MessageStorageStatus("mem1", used1, total1),
                        new MessageStorageStatus("mem2", used2, total2),
                        new MessageStorageStatus("mem3", used3, total3),
                    };

                    return statuses;
                }
            }
            return null;
        }

        public virtual async Task<CommandStatus> SetNewSmsIndication(int mode, int mt, int bm, int ds, int bfr)
        {
            if (mode < 0 || mode > 2)
                throw new ArgumentOutOfRangeException(nameof(mode));
            if (mt < 0 || mt > 3)
                throw new ArgumentOutOfRangeException(nameof(mt));
            if (!(bm == 0 || bm == 2))
                throw new ArgumentOutOfRangeException(nameof(bm));
            if (ds < 0 || ds > 2)
                throw new ArgumentOutOfRangeException(nameof(ds));
            if (bfr < 0 || bfr > 1)
                throw new ArgumentOutOfRangeException(nameof(bfr));

            (AtError error, _) = await channel.SendCommand($"AT+CNMI={mode},{mt},{bm},{ds},{bfr}");

            if (error == AtError.NO_ERROR)
                return CommandStatus.OK;
            return CommandStatus.ERROR;
        }

        public virtual async Task<SmsReference> SendSmsAsync(PhoneNumber phoneNumber, string message)
        {
            string cmd1 = $"AT+CMGS=\"{phoneNumber}\"";
            string cmd2 = message;
            (AtError error, AtResponse response) = await channel.SendSmsAsync(cmd1, cmd2, "+CMGS:");

            if (error == AtError.NO_ERROR)
            {
                string line = response.Intermediates.First();
                var match = Regex.Match(line, @"\+CMGS:\s(?<mr>\d+)");
                if (match.Success)
                {
                    int mr = int.Parse(match.Groups["mr"].Value);
                    return new SmsReference(mr);
                }
            }
            return null;
        }

        public virtual async Task<Sms> ReadSmsAsync(int index, bool markAsRead)
        {
            (AtError error, AtResponse response) = await channel.SendMultilineCommand($"AT+CMGR={index},{(markAsRead ? 1 : 0)}", null);

            if (error == AtError.NO_ERROR && response.Intermediates.Count > 0)
            {
                string line = response.Intermediates.First();
                var match = Regex.Match(line, @"\+CMGR:\s""(?<status>[A-Z\s]+)"",""(?<sender>\+\d+)"",,""(?<received>(?<year>\d\d)/(?<month>\d\d)/(?<day>\d\d),(?<hour>\d\d):(?<minute>\d\d):(?<second>\d\d)(?<zone>[-+]\d\d))""");
                if (match.Success)
                {
                    SmsStatus status = SmsStatusHelpers.ToSmsStatus(match.Groups["status"].Value);
                    PhoneNumber sender = new PhoneNumber(match.Groups["sender"].Value);
                    int year = int.Parse(match.Groups["year"].Value);
                    int month = int.Parse(match.Groups["month"].Value);
                    int day = int.Parse(match.Groups["day"].Value);
                    int hour = int.Parse(match.Groups["hour"].Value);
                    int minute = int.Parse(match.Groups["minute"].Value);
                    int second = int.Parse(match.Groups["second"].Value);
                    int zone = int.Parse(match.Groups["zone"].Value);
                    DateTimeOffset received = new DateTimeOffset(2000 + year, month, day, hour, minute, second, TimeSpan.FromMinutes(15 * zone));
                    string message = response.Intermediates.Last();
                    return new Sms(status, sender, received, message);
                }
            }
            return null;
        }

        public virtual async Task<IList<SmsWithIndex>> ListSmssAsync(SmsStatus smsStatus, bool markAsRead)
        {
            (AtError error, AtResponse response) = await channel.SendMultilineCommand($"AT+CMGL=\"{SmsStatusHelpers.ToString(smsStatus)}\",{(markAsRead ? 1 : 0)}", null);

            List<SmsWithIndex> smss = new List<SmsWithIndex>();
            if (error == AtError.NO_ERROR)
            {
                for (int i = 0; i < response.Intermediates.Count; i += 2)
                {
                    string metaData = response.Intermediates[i];
                    var match = Regex.Match(metaData, @"\+CMGL:\s(?<index>\d+),""(?<status>[A-Z\s]+)"",""(?<sender>\+\d+)"",,""(?<received>(?<year>\d\d)/(?<month>\d\d)/(?<day>\d\d),(?<hour>\d\d):(?<minute>\d\d):(?<second>\d\d)(?<zone>[-+]\d\d))""");
                    if (match.Success)
                    {
                        int index = int.Parse(match.Groups["index"].Value);
                        SmsStatus status = SmsStatusHelpers.ToSmsStatus(match.Groups["status"].Value);
                        PhoneNumber sender = new PhoneNumber(match.Groups["sender"].Value);
                        int year = int.Parse(match.Groups["year"].Value);
                        int month = int.Parse(match.Groups["month"].Value);
                        int day = int.Parse(match.Groups["day"].Value);
                        int hour = int.Parse(match.Groups["hour"].Value);
                        int minute = int.Parse(match.Groups["minute"].Value);
                        int second = int.Parse(match.Groups["second"].Value);
                        int zone = int.Parse(match.Groups["zone"].Value);
                        DateTimeOffset received = new DateTimeOffset(2000 + year, month, day, hour, minute, second, TimeSpan.FromMinutes(15 * zone));
                        string message = response.Intermediates[i + 1];
                        smss.Add(new SmsWithIndex(index, status, sender, received, message));
                    }
                }
            }
            return smss;
        }

        public virtual async Task<SupportedDeleteSmsValues> TestDeleteSmsAsync()
        {
            (AtError error, AtResponse response) = await channel.SendSingleLineCommandAsync("AT+CMGD=?", "+CMGD:");
            if (error == AtError.NO_ERROR)
            {
                string line = response.Intermediates.First();
                var match = Regex.Match(line, @"\+CMGD:\s\((?<indexes>)\),\((?<delflags>\d-\d)\)");
                if (match.Success)
                {
                    IEnumerable<int> indexes;
                    if (!string.IsNullOrWhiteSpace(match.Groups["indexes"].Value))
                        indexes = match.Groups["indexes"].Value.Split(',').Select(x => int.Parse(x));
                    else
                        indexes = new List<int>();
                    IEnumerable<int> delflagsRange = match.Groups["delflags"].Value.Split('-').Select(x => int.Parse(x));
                    List<int> delFlags = new List<int>();
                    for (int i = delflagsRange.First(); i <= delflagsRange.Last(); i++)
                    {
                        delFlags.Add(i);
                    }
                    return new SupportedDeleteSmsValues(indexes, delFlags);
                }
            }
            return default;
        }

        public virtual async Task<CommandStatus> DeleteSmsAsync(int index, SmsDeleteFlags? smsDeleteFlag = null)
        {
            string command = $"AT+CMGD={index}";
            if (smsDeleteFlag != null)
                command += $",{(int)smsDeleteFlag}";
            (AtError error, _) = await channel.SendCommand(command);
            if (error == AtError.NO_ERROR)
                return CommandStatus.OK;
            return CommandStatus.ERROR;
        }
        #endregion

        #region _3GPP_TS_27_007
        public virtual async Task<SimStatus> GetSimStatusAsync()
        {
            (AtError error, AtResponse response) = await channel.SendSingleLineCommandAsync("AT+CPIN?", "+CPIN:");

            if (error != AtError.NO_ERROR)
                return SimStatus.SIM_NOT_READY;

            switch (AtErrorParsers.GetCmeError(response))
            {
                case AtErrorParsers.AtCmeError.CME_SUCCESS:
                    break;
                case AtErrorParsers.AtCmeError.CME_SIM_NOT_INSERTED:
                    return SimStatus.SIM_ABSENT;
                default:
                    return SimStatus.SIM_NOT_READY;
            }

            // CPIN? has succeeded, now look at the result
            string cpinLine = response.Intermediates.First();
            if (!AtTokenizer.TokenizeStart(cpinLine, out cpinLine))
                return SimStatus.SIM_NOT_READY;

            if (!AtTokenizer.TokenizeNextString(cpinLine, out _, out string cpinResult))
                return SimStatus.SIM_NOT_READY;

            return cpinResult switch
            {
                "SIM PIN" => SimStatus.SIM_PIN,
                "SIM PUK" => SimStatus.SIM_PUK,
                "PH-NET PIN" => SimStatus.SIM_NETWORK_PERSONALIZATION,
                "READY" => SimStatus.SIM_READY,
                _ => SimStatus.SIM_ABSENT,// Treat unsupported lock types as "sim absent"
            };
        }

        public virtual async Task<CommandStatus> EnterSimPinAsync(PersonalIdentificationNumber pin)
        {
            (AtError error, _) = await channel.SendCommand($"AT+CPIN={pin}");

            Thread.Sleep(1500); // Without it, the reader loop crashes

            if (error == AtError.NO_ERROR)
                return CommandStatus.OK;
            else return CommandStatus.ERROR;
        }

        public virtual async Task<SignalStrength> GetSignalStrengthAsync()
        {
            (AtError error, AtResponse response) = await channel.SendSingleLineCommandAsync("AT+CSQ", "+CSQ:");

            if (error == AtError.NO_ERROR)
            {
                string line = response.Intermediates.First();
                var match = Regex.Match(line, @"\+CSQ:\s(?<rssi>\d+),(?<ber>\d+)");
                if (match.Success)
                {
                    int rssi = int.Parse(match.Groups["rssi"].Value);
                    int ber = int.Parse(match.Groups["ber"].Value);
                    return new SignalStrength(rssi, ber);
                }
            }
            return null;
        }

        public virtual async Task<BatteryStatus> GetBatteryStatusAsync()
        {
            (AtError error, AtResponse response) = await channel.SendSingleLineCommandAsync("AT+CBC", "+CBC:");

            if (error == AtError.NO_ERROR)
            {
                string line = response.Intermediates.First();
                var match = Regex.Match(line, @"\+CBC:\s(?<bcs>\d+),(?<bcl>\d+)");
                if (match.Success)
                {
                    int bcs = int.Parse(match.Groups["bcs"].Value);
                    int bcl = int.Parse(match.Groups["bcl"].Value);
                    return new BatteryStatus((BatteryChargeStatus)bcs, bcl);
                }
            }
            return null;
        }

        public virtual async Task<CommandStatus> SetDateTimeAsync(DateTimeOffset value)
        {
            var sb = new StringBuilder("AT+CCLK=\"");
            int offsetQuarters = value.Offset.Hours * 4;
            sb.Append(value.ToString(@"yy/MM/dd,HH:mm:ss", CultureInfo.InvariantCulture));
            sb.Append(offsetQuarters.ToString("+00;-#", CultureInfo.InvariantCulture));
            sb.Append("\"");
            (AtError error, _) = await channel.SendCommand(sb.ToString());

            if (error == AtError.NO_ERROR)
                return CommandStatus.OK;
            return CommandStatus.ERROR;
        }

        public virtual async Task<DateTimeOffset?> GetDateTimeAsync()
        {
            (AtError error, AtResponse response) = await channel.SendSingleLineCommandAsync("AT+CCLK?", "+CCLK:");

            if (error == AtError.NO_ERROR)
            {
                string line = response.Intermediates.First();
                var match = Regex.Match(line, @"\+CCLK:\s""(?<year>\d\d)/(?<month>\d\d)/(?<day>\d\d),(?<hour>\d\d):(?<minute>\d\d):(?<second>\d\d)(?<zone>[-+]\d\d)""");
                if (match.Success)
                {
                    int year = int.Parse(match.Groups["year"].Value);
                    int month = int.Parse(match.Groups["month"].Value);
                    int day = int.Parse(match.Groups["day"].Value);
                    int hour = int.Parse(match.Groups["hour"].Value);
                    int minute = int.Parse(match.Groups["minute"].Value);
                    int second = int.Parse(match.Groups["second"].Value);
                    int zone = int.Parse(match.Groups["zone"].Value);
                    DateTimeOffset time = new DateTimeOffset(2000 + year, month, day, hour, minute, second, TimeSpan.FromMinutes(15 * zone));
                    return time;
                }
            }
            return null;
        }
        #endregion

        public void Close()
        {
            Dispose();
        }

        #region Dispose
        protected virtual void Dispose(bool disposing)
        {
            if (!disposed)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects)
                    channel.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override finalizer
                // TODO: set large fields to null
                disposed = true;
            }
        }

        // // TODO: override finalizer only if 'Dispose(bool disposing)' has code to free unmanaged resources
        // ~ModemBase()
        // {
        //     // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
        //     Dispose(disposing: false);
        // }

        public void Dispose()
        {
            // Do not change this code. Put cleanup code in 'Dispose(bool disposing)' method
            Dispose(disposing: true);
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
