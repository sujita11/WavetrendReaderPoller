using System;
using System.Runtime.InteropServices;
using Com.Nexatrak.Utilities.Http;

namespace ReaderMonitor
{
    //-------------------------------------------------------------------------------------------------------------
    public class Response
    {
        public Byte command;
        public Byte len;
        public Byte[] data = new Byte[200];
    }
    //-------------------------------------------------------------------------------------------------------------

    public class ReaderFields
    {
        public string ip;
        public string macAddress;
    }

    public class TagFields : IBaseHttpRequest
    {
        public string ip;
        public string macAddress;
        public enum ReadType { Standard, Telemetry, Acceleration, Error };
        public ReadType readType;
        public Byte beaconRate;
        public UInt32 tagId;
        public UInt32 siteCode;
        public UInt32 ageCount;
        public UInt32 rssi;
        public UInt32 movementCount;
        public UInt32 tamperCount;
        public bool tamperOpen;
        public bool alarm;
    }
    //-------------------------------------------------------------------------------------------------------------
    public class ErrorRep
    {
        public string ip;
        public UInt32 errNo;
        public string errMsg;

        public ErrorRep( string a, UInt32 b, string c)
        {
            ip = a;
            errNo = b;
            errMsg = c;
        }
    }
    //-------------------------------------------------------------------------------------------------------------
    [StructLayout(LayoutKind.Explicit)]
    struct IntConv
    {
        [FieldOffset(0)]
        public byte b1;

        [FieldOffset(1)]
        public byte b2;

        [FieldOffset(2)]
        public byte b3;

        [FieldOffset(3)]
        public byte b4;

        [FieldOffset(0)]
        public UInt32 conv;
    }
    //-------------------------------------------------------------------------------------------------------------
}
