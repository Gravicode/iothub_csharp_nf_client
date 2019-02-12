using Microsoft.Azure.Devices.Client;

using System;
using System.Threading;
using Windows.Devices.WiFi;
using System.Net;
using System.Text;

namespace NFDeviceClientHttpSample.csproj
{
    public class Program
    {
        // String containing Hostname, Device Id & Device Key in one of the following formats:
        //  "HostName=<iothub_host_name>;DeviceId=<device_id>;SharedAccessKey=<device_key>"
        //  "HostName=<iothub_host_name>;CredentialType=SharedAccessSignature;DeviceId=<device_id>;SharedAccessSignature=SharedAccessSignature sr=<iot_host>/devices/<device_id>&sig=<token>&se=<expiry_time>";
        private const string DeviceConnectionString = "HostName=FreeDeviceHub.azure-devices.net;DeviceId=ArduinoDevice;SharedAccessKey=9dqsi9BV1I5L43YsKcx7id/8b5F9ofkznTWDireYSfA=";

        private static int MESSAGE_COUNT = 5;
        // Set the SSID & Password to your local WiFi network
        const string MYSSID = "BMC123";
        const string MYPASSWORD = "123qweasd";

        public static void Main()
        {
            try
            {
                // Get the first WiFI Adapter
                WiFiAdapter wifi = WiFiAdapter.FindAllAdapters()[0];

                // Set up the AvailableNetworksChanged event to pick up when scan has completed
                wifi.AvailableNetworksChanged += Wifi_AvailableNetworksChanged;

                // Loop forever scanning every 30 seconds
                while (true)
                {
                    Console.WriteLine("starting WiFi scan");
                    wifi.ScanAsync();
                    if (IsConnect)
                    {
                        Thread th1 = new Thread(new ThreadStart(Looping));
                        th1.Start();
                    }
                    Thread.Sleep(100000);
                }

            }
            catch (Exception ex)
            {
                Console.WriteLine("message:" + ex.Message);
                Console.WriteLine("stack:" + ex.StackTrace);
            }

            Thread.Sleep(Timeout.Infinite);
        }
        static bool IsConnect = false;
        /// <summary>
        /// Event handler for when WiFi scan completes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void Wifi_AvailableNetworksChanged(WiFiAdapter sender, object e)
        {
            Console.WriteLine("Wifi_AvailableNetworksChanged - get report");

            // Get Report of all scanned WiFi networks
            WiFiNetworkReport report = sender.NetworkReport;

            // Enumerate though networks looking for our network
            foreach (WiFiAvailableNetwork net in report.AvailableNetworks)
            {
                // Show all networks found
                Console.WriteLine($"Net SSID :{net.Ssid},  BSSID : {net.Bsid},  rssi : {net.NetworkRssiInDecibelMilliwatts.ToString()},  signal : {net.SignalBars.ToString()}");

                // If its our Network then try to connect
                if (net.Ssid == MYSSID)
                {
                    // Disconnect in case we are already connected
                    sender.Disconnect();

                    // Connect to network
                    WiFiConnectionResult result = sender.Connect(net, WiFiReconnectionKind.Automatic, MYPASSWORD);

                    // Display status
                    if (result.ConnectionStatus == WiFiConnectionStatus.Success)
                    {
                        Console.WriteLine("Connected to Wifi network");
                        IsConnect = true;
                    }
                    else
                    {
                        Console.WriteLine($"Error {result.ConnectionStatus.ToString()} connecting o Wifi network");
                    }
                }
            }
        }
      static  void Looping()
        {
            try
            {
                DeviceClient deviceClient = DeviceClient.CreateFromConnectionString(DeviceConnectionString, TransportType.Http1);

                SendEvent(deviceClient);
                ReceiveCommands(deviceClient);

                Console.WriteLine("Done!\n");
            }

            catch (Exception ex) { Console.WriteLine(ex.ToString()); };
        }

        static void SendEvent(DeviceClient deviceClient)
        {
            string dataBuffer;

            Console.WriteLine("Device sending " + MESSAGE_COUNT + " messages to IoTHub...");

            for (int count = 0; count < MESSAGE_COUNT; count++)
            {
                dataBuffer = Guid.NewGuid().ToString();
                Message eventMessage = new Message(Encoding.UTF8.GetBytes(dataBuffer));
                Console.WriteLine(DateTime.UtcNow + "> Sending message: " + count + ", Data: [" + dataBuffer + "]");

                deviceClient.SendEvent(eventMessage);
            }
        }

        static void ReceiveCommands(DeviceClient deviceClient)
        {
            Console.WriteLine("Device waiting for commands from IoTHub...");
            Message receivedMessage;
            string messageData;

            while (true)
            {
                receivedMessage = deviceClient.Receive();

                if (receivedMessage != null)
                {
                    StringBuilder sb = new StringBuilder();

                    foreach (byte b in receivedMessage.GetBytes())
                    {
                        sb.Append((char)b);
                    }

                    messageData = sb.ToString();

                    // dispose string builder
                    sb = null;
                    
                    Console.WriteLine(DateTime.UtcNow + "> Received message: " + messageData);

                    deviceClient.Complete(receivedMessage);
                }

                //  Note: In this sample, the polling interval is set to 
                //  10 seconds to enable you to see messages as they are sent.
                //  To enable an IoT solution to scale, you should extend this //  interval. For example, to scale to 1 million devices, set 
                //  the polling interval to 25 minutes.
                //  For further information, see
                //  https://azure.microsoft.com/documentation/articles/iot-hub-devguide/#messaging
                Thread.Sleep(10000);
            }
        }


    }
}
