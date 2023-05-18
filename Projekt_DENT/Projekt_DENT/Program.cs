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
using System.Device.I2c;
using nanoFramework.Hardware.Esp32;
using Iot.Device.Ssd13xx;
using Iot.Device.DHTxx.Esp32;
using Iot.Device.Ssd13xx.Samples;
using UnitsNet;

namespace Projekt_DENT
{
    public class Program
    {
        static bool WifiConnected = false;
        static string ssid = "Zapato";
        static string password = "holaholahola";
        static string temp_op = "opc1";
        static HttpListener _listener;
        static string UTC_S = "0";
        static int UTC_I =0;
        static bool ap = true;
        // Iniciar un servidor web simple
        static WebServer server = new WebServer();
        static WebServer_2 server_2 = new WebServer_2();

        // Contador de ´cantidad de ususarios conectados
        static int connectedCount = 0;
        // GPIO pin usado para poner el dispositivo en modo configuración (reiniciar dispositivo)
        const int SETUP_PIN = 5;
        static int pinEcho = 18;
        static int pinTrigger = 19;
        static int flagButton = 0;
        static ConfigurationStore configurationStore = new ConfigurationStore();
        static Dht11 dht11;
        static Ssd1306 device;
        static GpioController gpioController;
        static GpioPin setupButton;
        static int counter = 0;
        static string temp_j;
        static string hum_j;
        public static void Main()
        {
            //Declaracion de boton
            gpioController = new GpioController();
            setupButton = gpioController.OpenPin(SETUP_PIN, PinMode.InputPullUp);
            //dht11
            dht11 = new(pinEcho, pinTrigger);
            //I2C para oled
            Configuration.SetPinFunction(21, DeviceFunction.I2C1_DATA);
            Configuration.SetPinFunction(22, DeviceFunction.I2C1_CLOCK);
            //oled
            device = new Ssd1306(I2cDevice.Create(new I2cConnectionSettings(1, Ssd1306.DefaultI2cAddress)), Ssd13xx.DisplayResolution.OLED128x64);
            
            
            //Presentacion oled
            device.ClearScreen();
            device.Font = new BasicFont();
            device.DrawHorizontalLine(1, 1, 127, true);
            device.DrawVerticalLine(1, 1, 60, true);
            device.DrawString(2, 12, "==============", 1, true);//centered text
            device.DrawString(1, 22, "Proyecto DENT", 2, true);//large size 2 font
            device.DrawString(2, 42, "==============", 1, true);//centered text
            device.DrawVerticalLine(127, 1, 60, true);
            device.DrawHorizontalLine(1, 60, 127, true);
            device.Display();
            Thread.Sleep(2000);

            Debug.WriteLine("Se iniciara el accespoint o conexión a Wifi");

            if (configurationStore.IsConfigFileExisting) {
                ap = false;
                ConfigurationFile configuration_2 = configurationStore.GetConfig();
                ssid = configuration_2.SSID;
                password = configuration_2.PASSWORD;
                temp_op = configuration_2.Unidad_temperatura;
                UTC_S = configuration_2.UTC;
                try { UTC_I = int.Parse(configuration_2.UTC); }
                catch { UTC_I = 0; }
                Debug.WriteLine($"ssid: {ssid} Pass: {password} Unidad temperatura: {temp_op} UTC:{UTC_I} y UTC STRING: {UTC_S}");
                if (ssid == string.Empty) 
                {
                    //Habilitar modo accespoint con configuracion de temperatura
                    ap = true;
                }
            }
            
            // Si el dispositivo no está conectado a Wifi iniciar acces point para permitir configuracion
            // o si el boton esta presionado
            if (setupButton.Read() == PinValue.High) //Boton oprimido
            {
                //Eliminar toda la configuracion guardada e iniciar modo acces point
                Debug.WriteLine("==========================");
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
                            device.ClearScreen();
                            device.DrawString(1, 20, "Conectando", 1, true);//centered text
                            device.DrawString(1, 30, ".   .   .", 1, true);//centered text
                            device.DrawHorizontalLine(1, 1, 127, true);
                            device.DrawVerticalLine(1, 1, 60, true);
                            device.DrawVerticalLine(127, 1, 60, true);
                            device.DrawHorizontalLine(1, 60, 127, true);
                            device.Display();
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
                        Thread.Sleep(20000);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("message:" + ex.Message);
                    Debug.WriteLine("stack:" + ex.StackTrace);
                    Power.RebootDevice();
                }
                if (counter < 4) 
                {
                    server_2.Start(ssid, dht11, device); 
                }
                else { Debug.WriteLine("No se logro conectar a red wifi, modo acces point, eliminado red wifi");
                    Debug.WriteLine("Volver a configurar en pagina de acces point");
                    Wireless80211.Disable();
                    ConfigurationFile configuration_2 = configurationStore.GetConfig();
                    configuration_2.SSID = string.Empty;
                    var writeResult = configurationStore.WriteConfig(configuration_2);
                    Debug.WriteLine($"Configuration file {(writeResult ? "" : "not ")} saved properly.");
                    Power.RebootDevice();

                }
                device.ClearScreen();
                device.DrawString(1, 15, "Conexion", 1, true);//centered text
                device.DrawString(1, 35, "Exitosa!", 1, true);//centered text
                device.DrawHorizontalLine(1, 1, 127, true);
                device.DrawVerticalLine(1, 1, 60, true);
                device.DrawVerticalLine(127, 1, 60, true);
                device.DrawHorizontalLine(1, 60, 127, true);
                device.Display();
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
                    device.ClearScreen();
                    device.DrawString(1, 15, "Direccion IP", 1, true);//centered text
                    device.DrawString(1, 25, "Conexion AP:", 1, true);//centered text
                    device.DrawString(1, 40, WirelessAP.GetIP().ToString(), 1, true);//large size 2 font
                    device.DrawHorizontalLine(1, 1, 127, true);
                    device.DrawVerticalLine(1, 1, 60, true);
                    device.DrawVerticalLine(127, 1, 60, true);
                    device.DrawHorizontalLine(1, 60, 127, true);
                    device.Display();

                    // Link up Network event to show Stations connecting/disconnecting to Access point.
                    //NetworkChange.NetworkAPStationChanged += NetworkChange_NetworkAPStationChanged;

                    // Ahora que ya tenemos la conexion de wifi desactivada, debido a que tenemos un ip estatica configurada
                    // Es posible inicial el servidor web
                    server.Start(dht11,device);
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

            //while(_listener.IsListening==false);
            Thread.Sleep(10_000);//esperar un tiempo de lectura del IP
            // Just wait for now
            // Here you would have the reset of your program using the client WiFI link
            device.ClearScreen();
            Temperature temp = dht11.Temperature;
            RelativeHumidity hum = dht11.Humidity;
            ConfigurationFile configuration_ = new ConfigurationFile();


            while (true)
            {
                if (configurationStore.IsConfigFileExisting ? true : false)
                {
                    configuration_ = configurationStore.GetConfig();
                }
                try { temp = dht11.Temperature; }
                catch { temp = UnitsNet.Temperature.Zero; }
                //temp = dht11.Temperature;
                try { hum = dht11.Humidity; }
                catch { hum = UnitsNet.RelativeHumidity.Zero; }
                //hum = dht11.Humidity;
                if ((setupButton.Read() == PinValue.High) && (flagButton == 0))
                {
                    flagButton += 1;
                }
                else if ((setupButton.Read() == PinValue.High) && (flagButton == 1))
                {
                    flagButton -= 1;
                }
                device.DrawFilledRectangle(1, 1, 126, 59, false);//limpiar la pantalla de decimales de anteriores impresiones
                if (flagButton == 0)
                {
                    try { UTC_I = int.Parse(configurationStore.GetConfig().UTC); }
                    catch { UTC_I = 0; }
                    device.DrawString(2, 5, "Fecha:", 1, false);
                    device.DrawString(2, 33, "Hora:", 1, false);
                    if (!ap)
                    {
                        device.DrawString(2, 18, DateTime.UtcNow.AddHours(UTC_I).Hour.ToString() + ":" + DateTime.UtcNow.AddHours(UTC_I).Minute.ToString() + ":" + DateTime.UtcNow.AddHours(UTC_I).Second.ToString(), 1, true);
                        device.DrawString(2, 46, DateTime.UtcNow.AddHours(UTC_I).Day.ToString() + "/" + DateTime.UtcNow.AddHours(UTC_I).Month.ToString() + "/" + DateTime.UtcNow.AddHours(UTC_I).Year.ToString(), 1, true);
                    }
                    else
                    {
                        device.DrawString(2, 18, "No Connection", 1, true);
                        device.DrawString(2, 46, "No Connection", 1, true);
                    }
                }
                else if (flagButton == 1)
                {
                    if (dht11.IsLastReadSuccessful)
                    {
                        temp_op = configurationStore.GetConfig().Unidad_temperatura;
                        switch (temp_op)
                        {
                            case "opc1":
                                device.DrawString(2, 5, "Temperatura(oC):", 1, false);
                                device.DrawString(2, 18, temp.DegreesCelsius.ToString("N2"), 1, true);
                                configuration_.Temp_json = temp.DegreesCelsius.ToString("N2");


                                break;
                            case "opc2":
                                device.DrawString(2, 5, "Temperatura(oK):", 1, false);
                                device.DrawString(2, 18, (temp.DegreesCelsius + 293).ToString("N2"), 1, true);
                                configuration_.Temp_json = (temp.DegreesCelsius + 293).ToString("N2");
                                break;
                            default:
                                device.DrawString(2, 5, "Temperatura(oF):", 1, false);
                                device.DrawString(2, 18, temp.DegreesFahrenheit.ToString("N2"), 1, true);
                                configuration_.Temp_json = temp.DegreesFahrenheit.ToString("N2");
                                break;
                        }
                        device.DrawString(2, 33, "Humedad(%):", 1, false);
                        device.DrawString(2, 46, hum.Percent.ToString(), 1, true);
                        configuration_.Hum_json = hum.Percent.ToString();
                        var writeResult = configurationStore.WriteConfig(configuration_);
                    }
                    else
                    {
                        Debug.WriteLine("Error reading DHT sensor");
                    }
                }
                device.Display();
                Thread.Sleep(1000);
            }
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
                    server.Start(dht11, device);
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
            device.ClearScreen();
            device.DrawString(2, 15, "Direccion IP", 1, true);//centered text
            device.DrawString(2, 25, "Conexion WiFi:", 1, true);//centered text
            device.DrawString(1, 40, Interfaces[0].IPv4Address.ToString(), 1, true);//Dirreción IP para el WiFi
            device.DrawHorizontalLine(1, 1, 127, true);
            device.DrawVerticalLine(1, 1, 60, true);
            device.DrawVerticalLine(127, 1, 60, true);
            device.DrawHorizontalLine(1, 60, 127, true);
            device.Display();
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
                        counter = 0;
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