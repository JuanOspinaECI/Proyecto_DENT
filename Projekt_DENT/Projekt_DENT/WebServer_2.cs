
using System;
using System.Collections;
using System.Device.Wifi;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.NetworkInformation;
using System.Threading;
using nanoFramework.Runtime.Native;

namespace Projekt_DENT
{

    public class WebServer_2
    {
        HttpListener _listener;
        Thread _serverThread;
        static bool WifiConnected = false;
        static string ssid = null;
        static string password = null;
        static string temp_opc;
        static string temp_op = "opc1";
        static int tp = 20;
        static int hd = 80;
        static string temp = "20";
        static string humedad = "80%";
        public void refresh()
        {
            switch (temp_op)
            {
                case "opc1":
                    tp = tp + 1;
                    temp = tp + " C";
                    break;

                case "opc2":
                    tp = tp + 10;
                    temp = tp + " K";
                    break;
                default:
                    tp = tp - 10;
                    temp = tp + " F";
                    break;
            }
            humedad = hd.ToString() + "%";
        }
        public void set_ssid(String name) 
        {
            ssid = name;
        }
        public void set_password(String pass)
        {
            password = pass;
        }
        public void Start(String red)
        {
            ssid = red;
            if (_listener == null)
            {
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
            //string ssid = null;
            //string password = null;
            bool isApSet = false;

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
                        Debug.WriteLine("URL_: " + request.RawUrl.ToString());
                        response.ContentType = "text/html";
                        refresh();
                        responseString = main_2();
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

                    if (!string.IsNullOrEmpty(ssid))
                    {
                        Debug.WriteLine($"Wireless parameters SSID:{ssid}");
                        // Guardar config en JSON y mostrar en pantalla necesidad de reinicio
                        message = "<p>COnfiguracion de red actualizada</p><p>Reiniciar el dispositivo para efectuar el cambio de red</p>";
                    }

                    if (!string.IsNullOrEmpty(password))
                    {
                        Debug.WriteLine($"Wireless parameters PASSWORD:{password}");
                        // Guardar config en JSON y mostrar en pantalla necesidad de reinicio
                        message = "<p>COnfiguracion de red actualizada</p><p>Reiniciar el dispositivo para efectuar el cambio de red</p>";

                    }
                    if (!string.IsNullOrEmpty(temp_opc))
                    {
                        Debug.WriteLine($"Wireless parameters temperature option:{temp_opc}");
                        temp_op = temp_opc;
                        // Guardar config en JSON
                        message = "<p>Configuracion de temperatura actualizada</p>";
                    }

                    
                    //responseString = CreateMainPage(message);
                    refresh();
                    responseString = ReplaceMessage(main_2(), message);
                    //responseString = ReplaceTemperature(responseString, " " + temp);
                    //responseString = ReplaceHumedad(responseString, " " + humedad);
                    OutPutResponse(response, responseString);
                    //isApSet = true;
                    break;
            }

            response.Close();

            if (isApSet && (!string.IsNullOrEmpty(ssid)) && (!string.IsNullOrEmpty(password)))
            {
                // Enable the Wireless station interface
                // Habilitar la interfaz de la estación wireless
                //Wireless80211.Configure(ssid, password);

                // Deshabilitar el acces point
                WirelessAP.Disable();
                Thread.Sleep(200);
                Debug.WriteLine("Hola a reiniciar");


                Power.RebootDevice();
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
            Debug.WriteLine("stream" + inputStream.ToString());
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
                catch
                {

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
        static string main_2()
        {
            return $"<!DOCTYPE html><html>\r\n<head>\r\n" +
                "<meta charset=\"UTF-8\">\r\n    <meta http-equiv=\"X-UA-Compatible\" content=\"IE=edge\">\r\n" +
                "<meta name=\"viewport\" content=\"width=device-width, initial-scale=1.0\">\r\n    \r\n" +
                "<title>Dispositivo </title>\r\n</head>\r\n <body>\r\n  <h1>Dispostivo de Temperatura y Humedad</h1>\r\n " +
                "<h2>Conectado actualmente a la red: " + ssid + "</h2>\r\n  <p>Hora local: " + DateTime.UtcNow.ToString() + "</p>\r\n" +
                "<p>Temperatura: " + temp + "</p>\r\n" +
                "<p>Humedad: " + humedad + "</p>\r\n  <form method='GET'>\r\n\t<input type=\"submit\" value=\"Actualizar\">\r\n </form> \r\n"
                + "<form method='POST'>\r\n    <fieldset><legend>Unidad de temperatura</legend>\r\n\t\t<input type=\"radio\" id=\"opc1\"" +
                "name=\"Option_t\" value=\"opc1\">\r\n\t\t<label for=\"opc1\">Opcion 1</label><br>\r\n\t\t<input type=\"radio\" id=\"opc2\"" +
                "name=\"Option_t\" value=\"opc2\">\r\n\t\t<label for=\"opc2\">Opcion 2</label><br>\r\n\t\t<input type=\"radio\" id=\"opc3\"" +
                "name=\"Option_t\" value=\"opc3\">\r\n\t\t<label for=\"opc3\">Opcion 3</label>\r\n\t\t<br>\r\n\t\t<input type=\"submit\"" +
                "value=\"submit\">\r\n    </fieldset>\r\n\t\r\n  </form>\r\n </body>\r\n</html>";
        }
    }
}
