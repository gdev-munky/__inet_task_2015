using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Security.Principal;
using System.Text;

namespace SNTP
{
    public class SntpPacket
    {
        public ESntpLeapIndicator LeapIndicator { get; set; }
        public ESntpMode Mode { get; set; }
        public byte Stratum { get; set; }
        public TimeSpan PollInterval { get; set; }
        public sbyte Precision { get; set; }
        public FixedPoint32 RootDelay { get; set; }
        public FixedPoint32 RootDispersion { get; set; }
        public byte[] ReferenceIdentifier { get; private set; }
        public NtpTimeStamp ReferenceTimeStamp { get; set; }
        public NtpTimeStamp OriginateTimeStamp { get; set; }
        public NtpTimeStamp ReceiveTimeStamp { get; set; }
        public NtpTimeStamp TransmitTimeStamp { get; set; }


        public SntpPacket()
        {
            LeapIndicator = ESntpLeapIndicator.NoWarning;
            Mode = ESntpMode.Server;
            Stratum = 1;
            PollInterval = new TimeSpan(0, 0, 0, 4);
            Precision = -20;
            RootDelay = (FixedPoint32)0.0;
            RootDispersion = (FixedPoint32)0.0;
            SetReferenceId(EReferenceIdentifier.CESM);
            ReferenceTimeStamp = (NtpTimeStamp) DateTime.Today;
        }
        public static SntpPacket Response (SntpPacket request, TimeSpan delta)
        {
            var p = new SntpPacket
            {
                OriginateTimeStamp = request.ReceiveTimeStamp,
                ReceiveTimeStamp = (NtpTimeStamp)(DateTime.Now + delta),
                TransmitTimeStamp = (NtpTimeStamp)(DateTime.Now + delta),
            };
            return p;
        }
        public void SetReferenceId(IPAddress ip)
        {
            if (ip.AddressFamily != AddressFamily.InterNetwork)
                throw new NotSupportedException("Only IPv4 is supported");
            ReferenceIdentifier = ip.GetAddressBytes();
        }
        public void SetReferenceId(EReferenceIdentifier id)
        {
            ReferenceIdentifier = new byte[4];
            var sId = Encoding.ASCII.GetBytes(id.ToString());
            var endi = Math.Min(4, sId.Length);
            for (var i = 0; i < endi; ++i)
            {
                ReferenceIdentifier[i] = sId[i];
            }
        }
        public byte[] GetBytes()
        {
            var bts = new List<byte>(48);

            var li = (byte)LeapIndicator;
            var mode = (byte)Mode;
            var firstByte = (byte)((li << 6) | (4 << 3) | mode);
            var poll = (byte)Math.Max(Math.Min(Math.Log(PollInterval.TotalSeconds)/Math.Log(2), 17), 4);

            bts.Add(firstByte);
            bts.Add(Stratum);
            bts.Add(poll);
            bts.Add(unchecked((byte)Precision));
            bts.AddRange((byte[])RootDelay);
            bts.AddRange((byte[])RootDispersion);
            bts.AddRange(ReferenceIdentifier);
            bts.AddRange((byte[])ReferenceTimeStamp);
            bts.AddRange((byte[])OriginateTimeStamp);
            bts.AddRange((byte[])ReceiveTimeStamp);
            bts.AddRange((byte[])TransmitTimeStamp);
            return bts.ToArray();
        }

        public static SntpPacket Read(byte[] bytes, int offset = 0, int length = -1)
        {
            if (length < 0)
                length = bytes.Length;
            if (length < 48)
                //throw new ArgumentException("Recieved packet is too short");
                return null;
            var firstByte = bytes[offset];
            var p = new SntpPacket
            {
                LeapIndicator = (ESntpLeapIndicator)((firstByte & 0xff) >> 6),
                Mode = (ESntpMode)(firstByte & 0xfff),
                ReceiveTimeStamp = new NtpTimeStamp(bytes, 40+offset),
            };
            return p;
        }
    }

    public enum ESntpLeapIndicator
    {
        NoWarning = 0,
        Min61Secs = 1,
        Min59Secs = 2,
        NoSync = 3,
    }
    public enum ESntpMode
    {
        Reserved = 0,
        SymmetricActive,
        SymmetricPassive,
        Client,
        Server,
        BroadCast,
        ReservedNTPControl,
        ReservedPrivateUse,
    }

// ReSharper disable InconsistentNaming
    public enum EReferenceIdentifier
    {
        /// <summary>
        /// Uncalibrated local clock
        /// </summary>
        LOCL = 0, 
        /// <summary>
        /// Calibrated Cesium clock
        /// </summary>
        CESM, 
        /// <summary>
        /// Calibrated Rubidium clock
        /// </summary>
        RBDM, 
        /// <summary>
        /// Calibrated quartz clock or other pulse-per-second source
        /// </summary>
        PPS, 
        /// <summary>
        /// Inter-Range Instrumentation Group
        /// </summary>
        IRIG, 
        /// <summary>
        /// NIST telephone modem service
        /// </summary>
        ACTS, 
        /// <summary>
        /// USNO telephone modem service
        /// </summary>
        USNO, 
        /// <summary>
        /// PTB (Germany) telephone modem service
        /// </summary>
        PTB, 
        /// <summary>
        /// Allouis (France) Radio 164 kHz
        /// </summary>
        TDF,
        /// <summary>
        /// Mainflingen (Germany) Radio 77.5 kHz
        /// </summary>
        DCF, 
        /// <summary>
        /// Rugby (UK) Radio 60 kHz
        /// </summary>
        MSF, 
        /// <summary>
        /// Ft. Collins (US) Radio 2.5, 5, 10, 15, 20 MHz
        /// </summary>
        WWV, 
        /// <summary>
        /// Boulder (US) Radio 60 kHz
        /// </summary>
        WWVB, 
        /// <summary>
        /// Kauai Hawaii (US) Radio 2.5, 5, 10, 15 MHz
        /// </summary>
        WWVH, 
        /// <summary>
        /// Ottawa (Canada) Radio 3330, 7335, 14670 kHz
        /// </summary>
        CHU, 
        /// <summary>
        /// LORAN-C radionavigation system
        /// </summary>
        LORC, 
        /// <summary>
        /// OMEGA radionavigation system
        /// </summary>
        OMEG, 
        /// <summary>
        /// Global Positioning Service
        /// </summary>
        GPS 
    }
}
