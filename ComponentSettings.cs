using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;

namespace LiveSplit.UI.Components
{
    public partial class ComponentSettings : UserControl
    {

        public string COMPort { get; set; }
        public string ConfigFile { get; set; }
        public bool ResetSNES { get; set; }

        public ComponentSettings()
        {
            InitializeComponent();
            COMPort = "";
            ConfigFile = "";

            txtComPort.DataBindings.Add("Text", this, "COMPort", false, DataSourceUpdateMode.OnPropertyChanged);
            txtConfigFile.DataBindings.Add("Text", this, "ConfigFile", false, DataSourceUpdateMode.OnPropertyChanged);
            chkReset.DataBindings.Add("Checked", this, "ResetSNES", false, DataSourceUpdateMode.OnPropertyChanged);
        }
        public void SetSettings(XmlNode node)
        {
            var element = (XmlElement)node;
            COMPort = SettingsHelper.ParseString(element["COMPort"]);
            ConfigFile = SettingsHelper.ParseString(element["ConfigFile"]);
            ResetSNES = SettingsHelper.ParseBool(element["ResetSNES"]);    
        }

        public XmlNode GetSettings(XmlDocument document)
        {
            var parent = document.CreateElement("Settings");
            CreateSettingsNode(document, parent);
            return parent;
        }

        public int GetSettingsHashCode()
        {
            return CreateSettingsNode(null, null);
        }

        private int CreateSettingsNode(XmlDocument document, XmlElement parent)
        {
            return SettingsHelper.CreateSetting(document, parent, "Version", "1.2") ^
            SettingsHelper.CreateSetting(document, parent, "COMPort", COMPort) ^
            SettingsHelper.CreateSetting(document, parent, "ConfigFile", ConfigFile) ^
            SettingsHelper.CreateSetting(document, parent, "ResetSNES", ResetSNES);
        }

        private void btnBrowse_Click(object sender, EventArgs e)
        {
            var ofd = new OpenFileDialog();
            ofd.Filter = "JSON Files|*.json";
            if(ofd.ShowDialog() == DialogResult.OK)
            {
                txtConfigFile.Text = ofd.FileName;
            }
        }

        private void btnDetect_Click(object sender, EventArgs e)
        {
            var devices = usb2snes.core.core.GetDeviceList();
            if(devices.Count > 0)
            {
                txtComPort.Text = devices[0].Name;
            }
            else
            {
                MessageBox.Show("Could not auto-detect sd2snes, make sure the SNES is turned on and the USB-cable is connected");
            }
        }
    }

}
