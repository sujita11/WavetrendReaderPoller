using System.Collections.Concurrent;
using System.Threading;
using System.ComponentModel;
using System.Net;
using System.IO;


namespace ReaderMonitor
{
    class Readers
    {
        private static ConcurrentBag<Reader> _readerList = new ConcurrentBag<Reader>();                         // List of Readers
        private static BlockingCollection<Reader> _readersToServiceQueue = new BlockingCollection<Reader>();    // Queue of Readers that need servicing
        private static BlockingCollection<TagFields> _tagQueue = new BlockingCollection<TagFields>();           // Queue of decoded Tag reads
        private static ConcurrentQueue<ErrorRep> _errRep = new ConcurrentQueue<ErrorRep>();                     // Queue of any errors

        private static ManualResetEvent _stopSignal = new ManualResetEvent(false);
        private static ManualResetEvent _doneSignal = new ManualResetEvent(false);
        private static BackgroundWorker _bw;                                                                    // Worker thread for Readers that need servicing

        // get the reader list
        public ConcurrentBag<Reader> ReaderList => _readerList;

        //-------------------------------------------------------------------------------------------------------------
        public Readers()
        {
            _bw = new BackgroundWorker();
            _bw.DoWork += new DoWorkEventHandler( DoWork);
            _bw.WorkerReportsProgress = false;
            _bw.RunWorkerAsync( _readersToServiceQueue);
        }
        //-------------------------------------------------------------------------------------------------------------
        public void Stop()
        {
            _stopSignal.Set();
            while (!_doneSignal.WaitOne(0));
        }
        //-------------------------------------------------------------------------------------------------------------
        private void DoWork(object sender, DoWorkEventArgs e)
        {
            Reader reader;
            BlockingCollection<Reader> readersToServiceQueue = (BlockingCollection<Reader>)e.Argument;

            try
            {
                while (!_stopSignal.WaitOne(0))
                {
                    reader = readersToServiceQueue.Take();      // Block until there's a Reader object to process

                    while ( reader.DoReaderState() == true);    // Process Reader object up to to next asynchronous socket call
                }
            }
            catch
            {
            }
            finally
            {
                _doneSignal.Set();
            }
        }
        //-------------------------------------------------------------------------------------------------------------
        public void Add( string IP, int port)
        {
            IPAddress.Parse(IP);

            Reader reader = new Reader( IP, port, _tagQueue, _errRep, _readersToServiceQueue);
            _readerList.Add(reader);

            while ( reader.DoReaderState() == true) ;       // Get Reader to the first asynchronous socket call
        }
        //-------------------------------------------------------------------------------------------------------------
        public void LoadFromFile( string fileName, int port)
        {
            string IP;

            StreamReader file = new StreamReader(fileName);
            while ((IP = file.ReadLine()) != null)
                Add(IP, port);                     
    
            file.Close();
        }
        //-------------------------------------------------------------------------------------------------------------
        public TagFields GetTag()
        {
            return _tagQueue.Take();
        }
        //-------------------------------------------------------------------------------------------------------------
    }
}