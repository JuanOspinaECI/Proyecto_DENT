//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

using System;
using System.Collections;
using System.Device.Wifi;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using JsonConfigurationStore;
using nanoFramework.Runtime.Native;
//using Iot.Device.DHTxx.Esp32;
using Iot.Device.Ssd13xx;
using Iot.Device.Ssd13xx.Samples;
using System.Device.I2c;
using System.Device.Gpio;
using nanoFramework.Hardware.Esp32;
using UnitsNet;
using Iot.Device.Ahtxx;

namespace Projekt_DENT
{

    public class WebServer
    {
        HttpListener _listener;
        Thread _serverThread;
        static bool WifiConnected = false;
        static string ssid = null;
        static string password = null;
        static string temp_opc;
        static string temp_op = "opc1";
        static double tp = 20;
        static string temp0 = "20";
        static string humedad = "80%";
        static ConfigurationStore configurationStore = new ConfigurationStore();
        static ConfigurationFile configuration_ = new ConfigurationFile();
        //static Dht11 dht11;
        static Aht10 sensor_server;
        static Ssd1306 device;
        static public void refresh()
        {
            if (configurationStore.IsConfigFileExisting)
            { temp_op = configurationStore.GetConfig().Unidad_temperatura; }
            switch (temp_op)
            {
                case "opc1":
                    temp0 = $"{sensor_server.GetTemperature().DegreesCelsius:F0} C";
                    break;

                case "opc2":
                    temp0 = $"{sensor_server.GetTemperature().Kelvins:F0} K";
                    break;
                default:
                    temp0 = $"{sensor_server.GetTemperature().DegreesFahrenheit:F0} F";
                    break;
            }
            humedad = $"{sensor_server.GetHumidity().Percent:F0}";
            humedad += " %";
        }
        public void Start(Aht10 sensor_, Ssd1306 device_)
        {
            if (_listener == null)
            {
                sensor_server = sensor_;
                device = device_;
                _listener = new HttpListener("http");
                _serverThread = new Thread(RunServer);
                _serverThread.Start();
            }
        }

        public void Stop()
        {
            if (_listener != null)
                _listener.Stop();
        }
        private void RunServer()
        {
            _listener.Start();
            while (_listener.IsListening)
            {
                var context = _listener.GetContext();
                if (context != null)
                    ProcessRequest(context);
            }
            _listener.Close();

            _listener = null;
        }

        private void ProcessRequest(HttpListenerContext context)
        {
            var request = context.Request;
            var response = context.Response;
            string responseString;
            try {
                switch (request.HttpMethod)
                {
                    case "GET":
                        string[] url = request.RawUrl.Split('?');
                        if (url[0] == "/favicon.ico")
                        {
                            response.ContentType = "image/png";
                            byte[] responseBytes = Resources.GetBytes(Resources.BinaryResources.favicon);
                            OutPutByteResponse(response, responseBytes);
                        }
                        else
                        {
                            Debug.WriteLine("URL_cero_: " + url[0]);
                            response.ContentType = "text/html";
                            responseString = ReplaceMessage(Resources.GetString(Resources.StringResources.main), "");
                            refresh();
                            responseString = ReplaceTemperature(responseString, " " + temp0);
                            responseString = ReplaceHumedad(responseString, " " + humedad);
                            OutPutResponse(response, responseString);
                        }
                        break;

                    case "POST":

                        // Tomar los parametros necesarios del stream
                        string[] url_post = request.RawUrl.Split('?');
                        Hashtable hashPars = ParseParamsFromStream(request.InputStream);

                        ssid = (string)hashPars["ssid"];
                        password = (string)hashPars["password"];
                        temp_opc = (string)hashPars["Option_t"];
                        string message = string.Empty;



                        if (configurationStore.IsConfigFileExisting ? true : false)
                        {
                            configuration_ = configurationStore.GetConfig();
                        }
                        else
                        {
                            configuration_.SSID = string.Empty;
                            configuration_.PASSWORD = string.Empty;
                            configuration_.Unidad_temperatura = string.Empty;
                        }

                        if (!string.IsNullOrEmpty(ssid))
                        {
                            configuration_.SSID = ssid;
                            Debug.WriteLine($"Wireless parameters SSID:{ssid}");
                            // Guardar config en JSON y mostrar en pantalla necesidad de reinicio
                            message = "<p>Configuracion de red actualizada</p><p>Reiniciar el dispositivo para efectuar el cambio de red</p>";
                        }

                        if (!string.IsNullOrEmpty(password))
                        {
                            configuration_.PASSWORD = password;
                            Debug.WriteLine($"Wireless parameters PASSWORD:{password}");
                            // Guardar config en JSON y mostrar en pantalla necesidad de reinicio
                            message = "<p>Configuracion de red actualizada</p><p>Reiniciar el dispositivo para efectuar el cambio de red</p>";
                            /*device.ClearScreen();
                            device.DrawString(2, 8, "Configuracion", 1, true);//centered text
                            device.DrawString(1, 18, "actualizada", 1, true);
                            device.DrawString(2, 36, "Reinicie el", 1, true);//centered text
                            device.DrawString(2, 46, "dispositivo", 1, true);//centered text
                            device.DrawHorizontalLine(1, 1, 127, true);
                            device.DrawVerticalLine(1, 1, 60, true);
                            device.DrawVerticalLine(127, 1, 60, true);
                            device.DrawHorizontalLine(1, 60, 127, true);
                            device.Display();
                            Thread.Sleep(10000);*/
                        }
                        if (!string.IsNullOrEmpty(temp_opc))
                        {
                            configuration_.Unidad_temperatura = temp_opc;
                            Debug.WriteLine($"Wireless parameters temperature option:{temp_opc}");
                            temp_op = temp_opc;
                            // Guardar config en JSON
                            message = "<p>Configuracion de temperatura actualizada</p>";
                        }
                        var writeResult = configurationStore.WriteConfig(configuration_);
                        Debug.WriteLine($"Configuration file {(writeResult ? "" : "not ")} saved properly.");

                        responseString = ReplaceMessage(Resources.GetString(Resources.StringResources.main), message);
                        refresh();
                        responseString = ReplaceTemperature(responseString, " " + temp0);
                        responseString = ReplaceHumedad(responseString, " " + humedad);
                        OutPutResponse(response, responseString);
                        break;
                }

                response.Close();
            }
            catch (Exception ex) {
                Console.WriteLine(ex.ToString());
            }
        }

        static string ReplaceMessage(string page, string message)
        {
            int index = page.IndexOf("{message}");
            if (index >= 0)
            {
                return page.Substring(0, index) + message + page.Substring(index + 9);
            }

            return page;
        }
        static string ReplaceTemperature(string page, string message)
        {
            int index = page.IndexOf("Temperatura:");
            if (index >= 0)
            {
                return page.Substring(0, index + 12) + message + page.Substring(index + 22);
            }

            return page;
        }
        static string ReplaceHumedad(string page, string message)
        {
            int index = page.IndexOf("Humedad:");
            if (index >= 0)
            {
                return page.Substring(0, index + 8) + message + page.Substring(index + 17);
            }

            return page;
        }

        static void OutPutResponse(HttpListenerResponse response, string responseString)
        {
            var responseBytes = System.Text.Encoding.UTF8.GetBytes(responseString);
            OutPutByteResponse(response, System.Text.Encoding.UTF8.GetBytes(responseString));
        }
        static void OutPutByteResponse(HttpListenerResponse response, Byte[] responseBytes)
        {
            response.ContentLength64 = responseBytes.Length;
            response.OutputStream.Write(responseBytes, 0, responseBytes.Length);

        }

        static Hashtable ParseParamsFromStream(Stream inputStream)
        {
            //Debug.WriteLine("stream" + inputStream.ToString());
            byte[] buffer = new byte[inputStream.Length];
            inputStream.Read(buffer, 0, (int)inputStream.Length);

            return ParseParams(System.Text.Encoding.UTF8.GetString(buffer, 0, buffer.Length));
        }

        static Hashtable ParseParams(string rawParams)
        {
            Hashtable hash = new Hashtable();
            Debug.WriteLine("RawParam: " + rawParams); // Revisar el string que llega
            string[] parPairs = rawParams.Split('&');
            foreach (string pair in parPairs)
            {
                try
                {
                    string[] nameValue = pair.Split('=');
                    hash.Add(nameValue[0], nameValue[1]);
                }
                catch {
                   
                }
                
            }

            return hash;
        }
        static string CreateMainPage(string message)
        {

            return $"<!DOCTYPE html><html>{GetCss()}<body>" +
                    "<h1>NanoFramework</h1>" +
                    "<form method='POST'>" +
                    "<fieldset><legend>Wireless configuration</legend>" +
                    "Ssid:</br><input type='input' name='ssid' value='' ></br>" +
                    "Password:</br><input type='password' name='password' value='' >" +
                    "<br><br>" +
                    "<input type='submit' value='Save'>" +
                    "</fieldset>" +
                    "<b>" + message + "</b>" +
                    "</form></body></html>";
        }

        static string GetCss()
        {
            return "<head><meta charset=\"UTF-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\"><style>" +
                "*{box-sizing: border-box}" +
                "h1,legend {text-align:center;}" +
                "form {max-width: 250px;margin: 10px auto 0 auto;}" +
                "fieldset {border-radius: 5px;box-shadow: 3px 3px 15px hsl(0, 0%, 90%);font-size: large;}" +
                "input {width: 100%;padding: 4px;margin-bottom: 8px;border: 1px solid hsl(0, 0%, 50%);border-radius: 3px;font-size: medium;}" +
                "input[type=submit]:hover {cursor: pointer;background-color: hsl(0, 0%, 90%);transition: 0.5s;}" +
                " @media only screen and (max-width: 768px) { form {max-width: 100%;}} " +
                "</style><title>NanoFramework</title></head>";
        }
        
    }
}
