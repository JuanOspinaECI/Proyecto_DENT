using Iot.Device.DhcpServer;
using JsonConfigurationStore;
using nanoFramework.Networking;
using nanoFramework.Runtime.Native;
using System;
using System.Device.Gpio;
using System.Device.Wifi;
using System.Diagnostics;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;

namespace Projekt_DENT
{
    public class Program
    {
        static bool WifiConnected = false;
        static string ssid = "Zapato";
        static string password = "holaholahola";
        static string temp_op = "opc1";
        static bool ap = true;
        // Iniciar un servidor web simple
        static WebServer server = new WebServer();
        static WebServer_2 server_2 = new WebServer_2();

        // Contador de ´cantidad de ususarios conectados
        static int connectedCount = 0;

        // GPIO pin usado para poner el dispositivo en modo configuración (reiniciar dispositivo)
        const int SETUP_PIN = 5;

        static ConfigurationStore configurationStore = new ConfigurationStore();
        public static void Main()
        {
            Debug.WriteLine("Iniciando dispositivo de Temperatura y humerdad");
            Debug.WriteLine("Se iniciara el accespoint o conexión a Wifi");

            var gpioController = new GpioController();
            GpioPin setupButton = gpioController.OpenPin(SETUP_PIN, PinMode.InputPullUp);

            
            if (configurationStore.IsConfigFileExisting) {
                ap = false;
                Configuration configuration_ = configurationStore.GetConfig();
                ssid = configuration_.SSID;
                password = configuration_.PASSWORD;
                temp_op = configuration_.Unidad_temperatura;
                Debug.WriteLine($"ssid: {ssid} Pass: {password} Unidad temperatura: {temp_op}");
                if (ssid == string.Empty) 
                {
                    //Habilitar modo accespoint con configuracion de temperatura
                    ap = true;
                }
            }
            // Si el dispositivo no está conectado a Wifi iniciar acces point para permitir configuracion
            // o si el boton esta presionado
            if (false) //Boton oprimido
            {
                //Eliminar toda la configuracion guardada e iniciar modo acces point
                Debug.WriteLine($"A configuration file does {(configurationStore.IsConfigFileExisting ? string.Empty : "not ")} esits.");
                configurationStore.ClearConfig();
                ap = true;
            }
            /*
            Configuration configuration = new Configuration()
            {
                Unidad_temperatura = "Setting 1 value",
                SSID = "Setting 2 value",
                PASSWORD = "Setting 3 value"
            };
            
            Debug.WriteLine($"A configuration file does {(configurationStore.IsConfigFileExisting ? string.Empty : "not ")} esits.");
            configurationStore.ClearConfig();
            Debug.WriteLine("The configuration file has been deleted.");
            Debug.WriteLine($"A configuration file does {(configurationStore.IsConfigFileExisting ? string.Empty : "not ")} esits.");
            Configuration configuration2 = configurationStore.GetConfig();
            Debug.WriteLine("Unidad_temperatura: " + configuration2.Unidad_temperatura);
            Debug.WriteLine("SSID: " + configuration2.SSID);
            Debug.WriteLine("PASSWORD: " + configuration2.PASSWORD);
            Debug.WriteLine("Saving configuration file");
            var writeResult = configurationStore.WriteConfig(configuration);
            Debug.WriteLine($"Configuration file {(writeResult ? "" : "not ")} saved properly.");

            var newConfig = configurationStore.GetConfig();
            Thread.Sleep(Timeout.Infinite);
            */
            if (!ap) //Realizar logica segun elementos guardados en EERPOM (true -> wifi configurado, false -> modo acces point
            {
                try { WifiNetworkHelper.Disconnect(); } catch { Debug.WriteLine("**Reiniciar dispositivo"); }// Poner en Pantalla si funciona
                Wireless80211.Enable();
                int counter = 0;
                try
                {

                    // Get the first WiFI Adapter
                    WifiAdapter wifi = WifiAdapter.FindAllAdapters()[0];
                    // Set up the AvailableNetworksChanged event to pick up when scan has completed
                    wifi.AvailableNetworksChanged += Wifi_AvailableNetworksChanged;
                    NetworkChange.NetworkAddressChanged += NetworkChange_NetworkAddressChanged;
                    NetworkChange.NetworkAvailabilityChanged += NetworkChange_NetworkAvailabilityChanged;

                    // give it some time to perform the initial "connect"
                    // trying to scan while the device is still in the connect procedure will throw an exception
                    Thread.Sleep(10_000);

                    // Loop forever scanning every 30 seconds
                    
                    while (!WifiConnected && !(counter > 3))
                    {
                        try
                        {
                            Debug.WriteLine("starting Wi-Fi scan");
                            //WirelessAP.Disable();
                            wifi.ScanAsync();

                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"Failure starting a scan operation: {ex}");
                            Debug.WriteLine("Reinicio el dispositivo nuevamente"); // Enviar a pantalla LED
                            Power.RebootDevice();
                        }
                        counter++;
                        Thread.Sleep(30000);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("message:" + ex.Message);
                    Debug.WriteLine("stack:" + ex.StackTrace);
                    Power.RebootDevice();
                }
                if (counter < 4) { server_2.Start(ssid); }
                else { Debug.WriteLine("No se logro conectar a red wifi, modo acces point, eliminado red wifi");
                    Debug.WriteLine("Volver a configurar en pagina de acces point");
                    Wireless80211.Disable();
                    Configuration configuration_ = configurationStore.GetConfig();
                    configuration_.SSID = string.Empty;
                    var writeResult = configurationStore.WriteConfig(configuration_);
                    Debug.WriteLine($"Configuration file {(writeResult ? "" : "not ")} saved properly.");
                    Power.RebootDevice();

                }
            }
            if (ap)
            {
                if (true)//(!Wireless80211.IsEnabled() || (setupButton.Read() == PinValue.High)) // Revisar si se puede quitar condicional
                {

                    Wireless80211.Disable();
                    if (WirelessAP.Setup() == false)
                    {
                        // Reiniciar el dispositvo para activar Acces Point
                        Debug.WriteLine($"Modo acces point configurada, reiniciando dispositivo");
                        Power.RebootDevice();
                    }
                    //Poner try si no sirve reiniciar dispositivo Power.RebootDevice();
                    var dhcpserver = new DhcpServer
                    {
                        CaptivePortalUrl = $"http://{WirelessAP.SoftApIP}"
                    };
                    var dhcpInitResult = dhcpserver.Start(IPAddress.Parse(WirelessAP.SoftApIP), new IPAddress(new byte[] { 255, 255, 255, 0 }));
                    if (!dhcpInitResult)
                    {
                        Debug.WriteLine($"Error iniciando servidor DHCP .");
                    }

                    Debug.WriteLine($"Acces point en curso, esperando conexión de un cliente");
                    Debug.WriteLine($"Soft AP IP address :{WirelessAP.GetIP()}");

                    // Link up Network event to show Stations connecting/disconnecting to Access point.
                    //NetworkChange.NetworkAPStationChanged += NetworkChange_NetworkAPStationChanged;

                    // Ahora que ya tenemos la conexion de wifi desactivada, debido a que tenemos un ip estatica configurada
                    // Es posible inicial el servidor web
                    server.Start();
                }
                /* else
                {
                    Debug.WriteLine($"SSID y PASSWORD configurada, funcionando en modo normal");
                    var conf = Wireless80211.GetConfiguration();

                    bool success;

                    // For devices like STM32, the password can't be read
                    if (string.IsNullOrEmpty(conf.Password))
                    {
                        // In this case, we will let the automatic connection happen
                        success = WifiNetworkHelper.Reconnect(requiresDateTime: true, token: new CancellationTokenSource(60000).Token);
                    }
                    else
                    {
                        // If we have access to the password, we will force the reconnection
                        // This is mainly for ESP32 which will connect normaly like that.
                        success = WifiNetworkHelper.ConnectDhcp(conf.Ssid, conf.Password, requiresDateTime: true, token: new CancellationTokenSource(60000).Token);
                    }

                    if (success)
                    {
                        Debug.WriteLine($"Connection is {success}");
                        Debug.WriteLine($"La fecha valida actual es: {DateTime.UtcNow}");
                    }
                    else
                    {
                        Debug.WriteLine($"Hubo algun error en la conexion,");
                    }
                }*/
            }



            // Just wait for now
            // Here you would have the reset of your program using the client WiFI link

            Thread.Sleep(Timeout.Infinite);
        }

        /// <summary>
        /// Event handler for Stations connecting or Disconnecting
        /// </summary>
        /// <param name="NetworkIndex">The index of Network Interface raising event</param>
        /// <param name="e">Event argument</param>
        private static void NetworkChange_NetworkAPStationChanged(int NetworkIndex, NetworkAPStationEventArgs e)
        {
            Debug.WriteLine($"NetworkAPStationChanged event Index:{NetworkIndex} Connected:{e.IsConnected} Station:{e.StationIndex} ");

            // if connected then get information on the connecting station 
            if (e.IsConnected)
            {
                WirelessAPConfiguration wapconf = WirelessAPConfiguration.GetAllWirelessAPConfigurations()[0];
                WirelessAPStation station = wapconf.GetConnectedStations(e.StationIndex);

                string macString = BitConverter.ToString(station.MacAddress);
                Debug.WriteLine($"Station mac {macString} Rssi:{station.Rssi} PhyMode:{station.PhyModes} ");

                connectedCount++;

                // Start web server when it connects otherwise the bind to network will fail as 
                // no connected network. Start web server when first station connects 
                if (connectedCount == 1)
                {
                    // Wait for Station to be fully connected before starting web server
                    // other you will get a Network error
                    Thread.Sleep(2000);
                    server.Start();
                }
            }
            else
            {
                // Station disconnected. When no more station connected then stop web server
                if (connectedCount > 0)
                {
                    connectedCount--;
                    if (connectedCount == 0)
                        server.Stop();
                }
            }

        }
        private static void NetworkChange_NetworkAvailabilityChanged(object sender, NetworkAvailabilityEventArgs e)
        {
            if (e.IsAvailable)
            {
                Console.WriteLine("Network Connection Ready");
            }
            else
            {
                Console.WriteLine("Network Connection Lost");
            }
        }

        private static void NetworkChange_NetworkAddressChanged(object sender, EventArgs e)
        {
            NetworkInterface[] Interfaces = NetworkInterface.GetAllNetworkInterfaces();
            foreach (NetworkInterface intf in Interfaces)
            {
                Console.WriteLine("Interface: " + intf.NetworkInterfaceType + ", IP Address: " + intf.IPv4Address.ToString());
            }
        }

        /// <summary>
        /// Event handler for when Wifi scan completes
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private static void Wifi_AvailableNetworksChanged(WifiAdapter sender, object e)
        {
            Debug.WriteLine("Wifi_AvailableNetworksChanged - get report");

            // Get Report of all scanned Wifi networks
            WifiNetworkReport report = sender.NetworkReport;

            // Enumerate though networks looking for our network
            foreach (WifiAvailableNetwork net in report.AvailableNetworks)
            {
                // Show all networks found
                Debug.WriteLine($"Net SSID :{net.Ssid},  BSSID : {net.Bsid},  rssi : {net.NetworkRssiInDecibelMilliwatts.ToString()},  signal : {net.SignalBars.ToString()}");

                // If its our Network then try to connect
                if (net.Ssid == ssid)
                {
                    // Disconnect in case we are already connected
                    sender.Disconnect();

                    // Connect to network
                    WifiConnectionResult result = sender.Connect(net, WifiReconnectionKind.Automatic, password);

                    // Display status
                    if (result.ConnectionStatus == WifiConnectionStatus.Success)
                    {
                        Debug.WriteLine("Connected to Wifi network");
                        WifiConnected = true;
                        break;
                    }
                    else
                    {
                        Debug.WriteLine($"Error {result.ConnectionStatus.ToString()} connecting o Wifi network");
                    }
                }
            }
        }
    }
}