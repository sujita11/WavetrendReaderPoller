using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;

namespace ReaderMonitor
{
    public class Reader
    {
        private enum ReaderState { Init, StartConnect, Disconnect, StartTX, StartRX, ProcessData};
        private ReaderState _readerState;

        private enum PacketState { Sync, Length, Net, Node, Reader, OpCode, Body, Check};
        private PacketState _packetState;

        private enum InitState { SetProtocol, SetAutopoll};
        private InitState _initState;

        private Byte[] _dataBuffer = new Byte[2048];
        private static Byte[] _setAutopoll = new Byte[] { 0xAA, 0x00, 0x00, 0x00, 0x01, 0x01, 0x00};
        private static Byte[] _setProtocol = new Byte[] { 0xAA, 0x02, 0x00, 0x00, 0x01, 0x40, 0x01, 0x00, 0x42};

        private IPAddress _readerIP;
        private Socket _receiver;
      
        private AsyncCallback _pfnReceive;
        private AsyncCallback _pfnConnect;
        private AsyncCallback _pfnTransmit;
        
        private int _count;
        private Byte _checkSum = 0;
        private int _dataCount = 0;

        private Response _response = new Response();

        private string _IP;
        private string _macAddress;
        private int _port;
        private BlockingCollection<TagFields> _tagQ;
        private ConcurrentQueue<ErrorRep> _errQ;
        private BlockingCollection<Reader> _rts;

        public string IP
        {
            get => _IP;
            set => _IP = value;
        }

        public string MacAddress       
        {
            get => _macAddress;
            set => _macAddress = value;
        }
        
        //-------------------------------------------------------------------------------------------------------------
        public Reader( string IP, int port, BlockingCollection<TagFields> tagQ, ConcurrentQueue<ErrorRep> errQ, BlockingCollection<Reader> rts)
        {
            _IP = IP;
            _port = port;
            _tagQ = tagQ;
            _errQ = errQ;
            _rts = rts;
            _readerIP = IPAddress.Parse(IP);

            _readerState = ReaderState.Init;
            _packetState = PacketState.Sync;
        }
        //-------------------------------------------------------------------------------------------------------------
        public bool DoReaderState()
        {
            bool doNextState = true;

            switch (_readerState)
            {
                case ReaderState.Init:
                    if (Initialise() == true)
                        _readerState = ReaderState.StartConnect;
                    break;

                case ReaderState.StartConnect:
                    if (StartConnect() == false)
                        _readerState = ReaderState.Disconnect;
                    else
                        doNextState = false;
                    break;

                case ReaderState.StartTX:
                    if (StartTX() == false)
                        _readerState = ReaderState.Disconnect;
                    else
                        doNextState = false;
                    break;

                case ReaderState.StartRX:
                    if (StartRX() == false)
                        _readerState = ReaderState.Disconnect;
                    else
                        doNextState = false;
                    break;

                case ReaderState.ProcessData:
                    ProcessData();
                    _readerState = ReaderState.StartRX;
                    break;

                case ReaderState.Disconnect:
                    Disconnect();
                    _readerState = ReaderState.StartConnect;
                    break;
            }

            return doNextState;
        }
        //-------------------------------------------------------------------------------------------------------------
        private Boolean Initialise()
        {
            try
            {
                if (_pfnReceive == null)
                    _pfnReceive = new AsyncCallback(OnDataReceived);

                if (_pfnConnect == null)
                    _pfnConnect = new AsyncCallback(OnConnect);

                if (_pfnTransmit == null)
                    _pfnTransmit = new AsyncCallback(OnTransmit);

                return true;
            }
            catch (Exception ex)
            {
                _errQ.Enqueue(new ErrorRep(IP, 0, ex.Message));
            }
            return false;
        }
        //-------------------------------------------------------------------------------------------------------------
        private Boolean StartConnect()
        {
            try
            {
                IPEndPoint readerEP;

                _initState = InitState.SetProtocol;

                readerEP = new IPEndPoint( _readerIP, _port);
                _receiver = new Socket( AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);              
                _receiver.BeginConnect( readerEP, _pfnConnect, null);

                return true;
            }
            catch (Exception ex)
            {
                _errQ.Enqueue(new ErrorRep(IP, 1, ex.Message));
            }
            return false;
        }
        //-------------------------------------------------------------------------------------------------------------
        private void Disconnect()
        {
            try
            {
                if (_receiver != null)
                    if (_receiver.Connected == true)
                        _receiver.Shutdown(SocketShutdown.Both);
            }
            catch (Exception ex)
            {
                _errQ.Enqueue(new ErrorRep(IP, 7, ex.Message));
            }
            finally
            {
                if (_receiver != null)
                    _receiver.Close();
            }
        }
        //-------------------------------------------------------------------------------------------------------------
        private void OnConnect(IAsyncResult asyn)
        {
            try
            {
                _receiver.EndConnect(asyn);
                _readerState = ReaderState.StartTX;

                _rts.Add( this);
            }
            catch
            {
            }
        }
        //-------------------------------------------------------------------------------------------------------------
        private void OnTransmit(IAsyncResult asyn)
        {
            try
            {
                _receiver.EndSend(asyn);

                switch (_initState)
                {
                    case InitState.SetProtocol:
                        _initState = InitState.SetAutopoll;
                        _readerState = ReaderState.StartTX;
                        break;

                    case InitState.SetAutopoll:
                        _readerState = ReaderState.StartRX;
                        break;

                    default:
                        break;
                }

                _rts.Add(this);
            }
            catch
            {
            }
        }
        //-------------------------------------------------------------------------------------------------------------
        private void OnDataReceived(IAsyncResult asyn)
        {
            try
            {
                _count = _receiver.EndReceive(asyn);
                if (_count > 0)
                    _readerState = ReaderState.ProcessData;

                _rts.Add(this);
            }
            catch
            {
            }
        }
        //-------------------------------------------------------------------------------------------------------------
        private Boolean StartTX()
        {
            try
            {
                switch( _initState)
                {
                    case InitState.SetProtocol:
                        _receiver.BeginSend( _setProtocol, 0, 9, SocketFlags.None, _pfnTransmit, null);
                        break;

                    case InitState.SetAutopoll:
                        _receiver.BeginSend( _setAutopoll, 0, 7, SocketFlags.None, _pfnTransmit, null);
                        break;

                    default:
                        break;
                }
                return true;
            }
            catch (Exception ex)
            {
                _errQ.Enqueue(new ErrorRep(IP, 3, ex.Message));
            }
            return false;
        }
        //-------------------------------------------------------------------------------------------------------------
        private Boolean StartRX()
        {
            try
            {
                _receiver.BeginReceive( _dataBuffer, 0, _dataBuffer.Length, SocketFlags.None, _pfnReceive, null);
                return true;
            }
            catch (Exception ex)
            {
                _errQ.Enqueue(new ErrorRep(IP, 3, ex.Message));
            }
            return false;
        }
        //-------------------------------------------------------------------------------------------------------------
        private void ProcessData()
        {
            Byte rx;

            for (int i = 0; i < _count; i++)
            {
                rx = _dataBuffer[i];
                switch (_packetState)
                {
                    case PacketState.Sync:
                        if (rx == 0x55)
                        {
                            _packetState = PacketState.Length;
                            _checkSum = 0;
                            _dataCount = 0;
                        }
                        break;

                    case PacketState.Length:
                        _response.len = rx;
                        _checkSum ^= rx;
                        _packetState = PacketState.Net;
                        break;

                    case PacketState.Net:
                        _checkSum ^= rx;
                        _packetState = PacketState.Node;
                        break;

                    case PacketState.Node:
                        _checkSum ^= rx;
                        _packetState = PacketState.Reader;
                        break;

                    case PacketState.Reader:
                        _checkSum ^= rx;
                        _packetState = PacketState.OpCode;
                        break;

                    case PacketState.OpCode:
                        _response.command = rx;
                        _checkSum ^= rx;
                        if (_response.len == 0)
                            _packetState = PacketState.Check;
                        else
                            _packetState = PacketState.Body;
                        break;

                    case PacketState.Body:
                        _checkSum ^= rx;
                        _response.data[_dataCount] = rx;
                        if ((++_dataCount == _response.len) || (_dataCount > 199))
                            _packetState = PacketState.Check;
                        break;

                    case PacketState.Check:
                        if (_checkSum == rx)
                            processTag();
                        _packetState = PacketState.Sync;
                        break;

                    default:
                        _packetState = PacketState.Sync;
                        break;
                }
            }
        }
        //-------------------------------------------------------------------------------------------------------------
        private void processTag()
        {
            if ((_response.len == 32) &&(_response.command == 0x06))
            {
                IntConv intConv = new IntConv();
                TagFields tagFields = new TagFields();

                intConv.b1 = _response.data[19];
                intConv.b2 = _response.data[18];
                intConv.b3 = _response.data[17];
                intConv.b4 = _response.data[16];
                tagFields.tagId = intConv.conv;

                intConv.b1 = _response.data[15];
                intConv.b2 = _response.data[14];
                intConv.b3 = _response.data[13];
                intConv.b4 = 0;
                tagFields.siteCode = intConv.conv;

                intConv.b1 = _response.data[12];
                intConv.b2 = _response.data[11];
                intConv.b3 = _response.data[10];
                intConv.b4 = _response.data[9];
                tagFields.ageCount = intConv.conv;

                switch (_response.data[9] >> 6)
                {
                    case 0:
                        tagFields.readType = TagFields.ReadType.Standard;
                        break;
                    case 1:
                        tagFields.readType = TagFields.ReadType.Acceleration;
                        break;
                    case 2:
                        tagFields.readType = TagFields.ReadType.Telemetry;
                        break;
                    case 3:
                        tagFields.readType = TagFields.ReadType.Error;
                        break;
                }

                tagFields.movementCount = (UInt32)_response.data[8];

                tagFields.tamperCount = (UInt32)(_response.data[4] & 0x7F);

                tagFields.tamperOpen = ((_response.data[4] >> 7) == 1) ? false : true;

                tagFields.beaconRate = _response.data[3];

                tagFields.rssi = (UInt32)_response.data[22];

                tagFields.alarm = (_response.data[25] == 0x51) ? true : false;

                tagFields.ip = IP;

                _tagQ.Add(tagFields);
            }
        }
        //-------------------------------------------------------------------------------------------------------------
    }
}
