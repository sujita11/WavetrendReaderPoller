using System;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Com.Nexatrak.Utilities.Http;
using Com.Nexatrak.Utilities.Repository;
using Com.Nexatrak.Utilities.Settings;

namespace ReaderMonitor
{
    public class ArpHelper
    {
        public static string GetMacAddress(string ipAddress)
        {
            try
            {
                // Start a new process for the arp command
                Process process = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "arp",
                        Arguments = "-a",
                        RedirectStandardOutput = true,
                        UseShellExecute = false,
                        CreateNoWindow = true
                    }
                };

                // Start the process
                process.Start();

                // Read the output of the arp command
                string output = process.StandardOutput.ReadToEnd();

                // Wait for the process to exit
                process.WaitForExit();

                // Use a regular expression to match the IP address and its corresponding MAC address
                string pattern = $@"\b{Regex.Escape(ipAddress)}\b\s+([\w-]+)";
                Match match = Regex.Match(output, pattern, RegexOptions.IgnoreCase);

                if (match.Success)
                {
                    // Return the MAC address
                    return match.Groups[1].Value.Replace('-', ':').ToUpper();
                }
                else
                {
                    return string.Empty;
                }
            }
            catch (Exception ex)
            {
                return $"Error: {ex.Message}";
            }
        }
    }

    class ReaderDetails : IBaseHttpRequest
    {
        private List<ReaderFields> _readerFieldsList = [];
        public List<ReaderFields> ReaderFieldsList => _readerFieldsList;
    }


    class Program
    {

        static void Main(string[] args)
        {
            TagFields tagFields;

            try
            {
                Console.WriteLine("Started Program");
                Readers readers = new Readers();                // Create Readers instance

                readers.LoadFromFile( "readers.txt", 35353);    // Add list of IP addresses from a simple text file. IP & Port must be configured on the RFID Reader
                

                var readerDetails = new ReaderDetails();
                foreach (var reader in readers.ReaderList)
                {
                    reader.MacAddress = ArpHelper.GetMacAddress(reader.IP);
                    readerDetails.ReaderFieldsList.Add(new ReaderFields { ip = reader.IP, macAddress = reader.MacAddress });
                }

                RepositoryHelper.PostEntity<ReaderDetails, ReaderDetails>($"URL GOES HERE", $"", readerDetails);



                while ( Console.KeyAvailable == false)           // Display Tags until keypress
                {
                    tagFields = readers.GetTag();               // Block until a Tag is available

                    RepositoryHelper.PostEntity<TagFields, TagFields>($"URL GOES HERE", $"", tagFields);
                }
                readers.Stop();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception : {ex.Message}");
            }

            Console.WriteLine("Done!");
            Console.ReadLine();
            Console.ReadLine();
        }
    } 
}
