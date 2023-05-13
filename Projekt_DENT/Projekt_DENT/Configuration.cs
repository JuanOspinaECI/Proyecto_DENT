//
// Copyright (c) .NET Foundation and Contributors
// See LICENSE file in the project root for full license information.
//

namespace JsonConfigurationStore
{
    public class ConfigurationFile
    {
        public string Temp_json { get; set; }
        public string Hum_json { get; set; }

        public string Unidad_temperatura { get; set; }
        public string SSID { get; set; }
        public string PASSWORD { get; set; }
        public string UTC { get; set; }
    }
}
